using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using MetadataExtractor;
using MetadataExtractor.Formats.QuickTime;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace PhotoArchiver;

using ComputerVision;
using Costs;
using Extensions;
using Face;
using Files;
using Formats;

using Progress;
using Storage;
using Thumbnails;
using Upload;

public partial class Archiver
{
	public async Task<ArchiveResult> ArchiveAsync(IDirectory directory, IProgressIndicator progressIndicator, CancellationToken cancellationToken)
	{
		// set up filter
		var matcher = new Matcher().AddInclude(Options.SearchPattern);
		var files = await directory.GetFilesAsync();

		var query = files.Where(f => matcher.Match(directory.Path, f.Path).HasMatches)
			.OrderBy(f => f.Path)
			.Where(f => !IgnoredFileNames.Contains(f.Name))
			.Where(f => !IgnoredExtensions.Contains(f.GetExtension()));

		if (Options.Skip != 0)
		{
			query = query.Skip(Options.Skip);
		}

		if (Options.Take != null)
		{
			query = query.Take(Options.Take.Value);
		}

		return await ArchiveAsync(query.ToList(), progressIndicator, cancellationToken);
	}

	[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
	public async Task<ArchiveResult> ArchiveAsync(IReadOnlyList<IFile> files, IProgressIndicator progressIndicator, CancellationToken cancellationToken)
	{
		// initialize
		progressIndicator.ToIndeterminateState();
		var container = Client.GetBlobContainerClient(StorageOptions.Container);
		var lastDirectoryName = null as string;

		var results = new List<FileUploadResult>();

		// estimate count
		var processedCount = 0;
		var count = files.Count;
		var processedBytes = 0L;
		var allBytes = 0L;
		foreach (var f in files)
			allBytes += await f.GetSizeAsync();

		// enumerate files in directory
		progressIndicator.Initialize(allBytes, files.Count);
		progressIndicator.SetBytesProgress(processedBytes);
		foreach (var file in files)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// display directory
			var currentDirectoryName = Path.GetDirectoryName(file.Path);
			if (lastDirectoryName != currentDirectoryName)
			{
				Logger.LogInformation($"Processing directory '{currentDirectoryName}'");
				lastDirectoryName = currentDirectoryName;
			}

			await using var item = new FileUploadItem(file);
			UploadResult result = default;

			try
			{
				Logger.LogTrace($"Processing {file}...");

				// read date
				var date = await GetDateAsync(item, files);
				if (date == null)
				{
					result = UploadResult.DateMissing;
					results.Add(new FileUploadResult(file, result));
					Logger.Log(UploadResultLogLevelMap[result], $"{result}\t{file.Name}");
					processedCount++;
					processedBytes += await item.Info.GetSizeAsync();
					progressIndicator.SetItemProgress(processedCount);
					progressIndicator.SetBytesProgress(processedBytes);
					continue;
				}

				// blob
				var blobDirectory = String.Format(CultureInfo.InvariantCulture, StorageOptions.DirectoryFormat, date);
				item.Metadata.Add("OriginalFileName", file.Path.RemoveDiacritics());
				item.Metadata.Add("CreatedAt", date.Value.ToString("o", CultureInfo.InvariantCulture));
				item.Metadata.Add("OriginalFileSize", (await file.GetSizeAsync()).ToString(CultureInfo.InvariantCulture));

				// deduplicate
				if (Options.Deduplicate)
				{
					Logger.LogTrace($"Computing hash for {file}...");
					var hash = await item.ComputeHashAsync();

					if (await DeduplicationService.ContainsAsync(container, blobDirectory, hash))
					{
						result = UploadResult.AlreadyExists;
					}
				}

				if (file.IsJpeg())
				{
					// computer vision
					if (ComputerVisionOptions.IsEnabled())
					{
						try
						{
							// create thumbnail
							using var thumbnail = await ThumbnailGenerator.GetThumbnailAsync(await item.OpenReadAsync(), 1024, 1024);

							// describe
							Logger.LogTrace($"Describing {file}...");
							var description = await ComputerVisionClient.DescribeImageInStreamAsync(thumbnail, cancellationToken: cancellationToken);
							CostEstimator.AddDescribe();
							if (description.Captions.Any())
							{
								item.Metadata.Add("Caption", description.Captions.OrderByDescending(c => c.Confidence).First().Text);
							}
							if (description.Tags.Any())
							{
								item.Metadata.Add("Tags", String.Join(", ", description.Tags));
							}
						}
						catch (ComputerVisionErrorResponseException ex)
						{
							Logger.LogWarning(ex, ex.Message);
						}
					}

					// face
					if (FaceOptions.IsEnabled())
					{
						try
						{
							// create thumbnail
							using var thumbnail = await ThumbnailGenerator.GetThumbnailAsync(await item.OpenReadAsync(), 1024, 1024);

							// detect
							Logger.LogTrace($"Detecing faces in {file}...");
							var faceResult = await FaceClient.Face.DetectWithStreamAsync(thumbnail, returnFaceId: true, cancellationToken: cancellationToken);
							CostEstimator.AddFace();
							var faceIds = faceResult.Where(f => f.FaceId != null).Select(f => f.FaceId!.Value).ToList();
							var identifyResult = await FaceClient.Face.IdentifyAsync(faceIds, personGroupId: FaceOptions.PersonGroupId, confidenceThreshold: FaceOptions.ConfidenceThreshold, cancellationToken: cancellationToken);
							CostEstimator.AddFace();

							if (identifyResult.Any())
							{
								item.Metadata.Add("People", String.Join(", ", identifyResult.Select(r => r.Candidates.OrderByDescending(c => c.Confidence).First()).Select(c => c.PersonId)));
							}
						}
						catch (Exception ex)
						{
							Logger.LogWarning(ex, ex.Message);
						}
					}
				}

				var blobName = blobDirectory.TrimEnd('/') + "/" + file.Name;
				var blob = container.GetBlockBlobClient(blobName);
				var progress = new StorageProgressShim(progressIndicator, processedBytes);

				if (result != UploadResult.AlreadyExists)
				{
					// check for extistance
					switch (await ExistsAndCompareAsync(blob, item))
					{
						// upload, if not exists
						case null:
							result = await UploadCoreAsync(container, blob, item, progress);
							break;

						// already exists, if matches
						case true:
							result = UploadResult.AlreadyExists;
							break;

						// exists, but does not match
						case false:
							switch (Options.ConflictResolution)
							{
								case ConflictResolution.KeepBoth:
									{
										// compute hash for new file name
										var fileHash = await item.ComputeHashAsync();
										var formattedHash = Convert.ToHexString(fileHash);

										// new blob
										blobName = blobDirectory.TrimEnd('/') + "/" + Path.ChangeExtension(file.Name, "." + formattedHash + file.GetExtension());
										blob = container.GetBlockBlobClient(blobName);

										// upload with new name
										switch (await ExistsAndCompareAsync(blob, item))
										{
											case null:
												result = await UploadCoreAsync(container, blob, item, progress);
												break;

											case false:
												result = UploadResult.Error;
												break;

											case true:
												result = UploadResult.AlreadyExists;
												break;
										}
									}
									break;

								case ConflictResolution.SnapshotAndOverwrite:
									{
										var properties = (await blob.GetPropertiesAsync(cancellationToken: cancellationToken)).Value;
										if (properties.AccessTier == AccessTier.Archive)
										{
											Logger.LogInformation($"Can't snapshot, because blob is in Archive tier.");
											result = UploadResult.Error;
											break;
										}

										await blob.CreateSnapshotAsync(cancellationToken: cancellationToken);
										result = await UploadCoreAsync(container, blob, item, progress);
									}
									break;

								case ConflictResolution.Overwrite:
									{
										var properties = (await blob.GetPropertiesAsync(cancellationToken: cancellationToken)).Value;
										if (properties.AccessTier == AccessTier.Archive)
										{
											Logger.LogTrace($"Deleting '{blob}'...");
											await blob.DeleteAsync(cancellationToken: cancellationToken);
										}

										result = await UploadCoreAsync(container, blob, item, progress);
									}
									break;

								default:
								case ConflictResolution.Skip:
									result = UploadResult.Conflict;
									break;
							}
							break;
					}

					if (result.IsSuccessful())
					{
						// verify
						if (Options.Verify)
						{
							// refresh properties
							var properties = (await blob.GetPropertiesAsync(cancellationToken: cancellationToken)).Value;

							// compute file hash
							var fileHash = await item.ComputeHashAsync();
							var blobHash = properties.ContentHash;
							if (blobHash == null)
							{
								throw new VerificationFailedException(item.Info, blob.Uri);
							}

							// compare
							if (!blobHash.AsSpan().SequenceEqual(fileHash))
							{
								throw new VerificationFailedException(item.Info, blob.Uri);
							}
						}

						// deduplicate
						if (Options.Deduplicate)
						{
							var hash = await item.ComputeHashAsync();
							DeduplicationService.Add(blobDirectory, hash);
						}
					}
				}

				if (result.IsSuccessful())
				{
					// thumbnail
					if (item.Info.IsJpeg() && ThumbnailOptions.IsEnabled() && (result == UploadResult.Uploaded || ThumbnailOptions.Force))
					{
						using var thumbnail = await ThumbnailGenerator.GetThumbnailAsync(await item.OpenReadAsync(), ThumbnailOptions.MaxWidth!.Value, ThumbnailOptions.MaxHeight!.Value);

						var thumbnailContainer = Client.GetBlobContainerClient(ThumbnailOptions.Container);
						var thumbnailBlob = thumbnailContainer.GetBlockBlobClient(blob.Name);
						var headers = new BlobHttpHeaders
						{
							ContentType = "image/jpeg"
						};

						try
						{
							await thumbnailBlob.UploadAsync(thumbnail, headers, item.Metadata, cancellationToken: cancellationToken);
							CostEstimator.AddWrite(thumbnail.Length);
						}
						catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.ContainerNotFound)
						{
							await thumbnailContainer.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
							CostEstimator.AddListOrCreateContainer();
							await thumbnailBlob.UploadAsync(thumbnail.Rewind(), headers, item.Metadata, cancellationToken: cancellationToken);
							CostEstimator.AddWrite(thumbnail.Length);
						}
						catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.BlobAlreadyExists)
						{
							await thumbnailBlob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
							await thumbnailBlob.UploadAsync(thumbnail.Rewind(), headers, item.Metadata, cancellationToken: cancellationToken);
							CostEstimator.AddWrite(thumbnail.Length);
						}
						catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.BlobArchived && ThumbnailOptions.Force)
						{
							await thumbnailBlob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
							await thumbnailBlob.UploadAsync(thumbnail.Rewind(), headers, item.Metadata, cancellationToken: cancellationToken);
							CostEstimator.AddWrite(thumbnail.Length);
						}
					}

					// delete
					if (Options.Delete)
					{
						Logger.LogTrace($"Deleting {file}...");
						await file.DeleteAsync();
					}
				}

				// log
				results.Add(new FileUploadResult(file, result));
				Logger.Log(UploadResultLogLevelMap[result], $"{result}\t{file.Name}\t({processedCount + 1} of {count})");
			}
			catch (Exception ex)
			{
				result = UploadResult.Error;
				Logger.LogError(ex, $"Failed to process {file.Name}");
				results.Add(new FileUploadResult(file, result, ex));
				progressIndicator.ToErrorState();
			}
			finally
			{
				processedCount++;
				processedBytes += await item.Info.GetSizeAsync();
				progressIndicator.SetItemProgress(processedCount);
				progressIndicator.SetBytesProgress(processedBytes);
			}
		}

		progressIndicator.ToFinishedState();

		return new ArchiveResult(results);
	}

	private async Task<bool?> ExistsAndCompareAsync(BlockBlobClient blob, FileUploadItem item)
	{
		// check for exists
		Logger.LogTrace($"Checking for {blob} exists...");
		CostEstimator.AddOther();
		if (await blob.ExistsAsync())
		{
			Logger.LogTrace($"Fetching attributes for {blob}...");
			var properties = (await blob.GetPropertiesAsync()).Value;
			CostEstimator.AddOther();

			// compare file size
			if (properties.ContentLength != await item.Info.GetSizeAsync())
			{
				return false;
			}

			// compare hash
			var fileHash = await item.ComputeHashAsync();
			var blobHash = properties.ContentHash;
			if (blobHash == null)
			{
				Logger.LogWarning($"Blob has no hash stored.");
				return false;
			}

			if (!blobHash.AsSpan().SequenceEqual(fileHash.AsSpan()))
			{
				return false;
			}

			return true;
		}

		return null;
	}

	private async Task<UploadResult> UploadCoreAsync(BlobContainerClient container, BlockBlobClient blob, FileUploadItem item, IProgress<long> progress)
	{
		// upload
		Logger.LogTrace($"Uploading {item.Info} to {blob}...");

		var headers = new BlobHttpHeaders
		{
			ContentType = MimeTypes.TryGetValue(item.Info.GetExtension(), out var contentType) ? contentType : "application/octet-stream"
		};

		try
		{
			await blob.UploadAsync(await item.OpenReadAsync(), headers, item.Metadata, progressHandler: progress);
		}
		catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.ContainerNotFound)
		{
			await container.CreateIfNotExistsAsync();
			await blob.UploadAsync(await item.OpenReadAsync(), headers, item.Metadata, progressHandler: progress);
		}
		CostEstimator.AddWrite(await item.Info.GetSizeAsync());

		return UploadResult.Uploaded;
	}

	private async ValueTask<DateTime?> GetDateAsync(FileUploadItem item, IEnumerable<IFile> peers)
	{
		Logger.LogTrace($"Reading date for {item.Info}...");

		switch (item.Info.GetExtension().ToUpperInvariant())
		{
			case ".JPG":
			case ".JPEG":
			case ".JFIF":
			case ".HEIF":
			case ".HEIC":
				{
					var metadata = ImageMetadataReader.ReadMetadata(await item.OpenReadAsync());
					var tag = metadata.SelectMany(d => d.Tags).FirstOrDefault(t => t.Name == "Date/Time Original" || t.Name == "Date/Time");
					if (tag != null)
					{
						return ParseExifDateTime(tag.Description!);
					}
				}
				break;

			case ".CR2":
			case ".NEF":
			case ".DNG":
			case ".GPR":
				{
					var metadata = ImageMetadataReader.ReadMetadata(await item.OpenReadAsync());
					var tag = metadata.SelectMany(d => d.Tags).FirstOrDefault(t => t.Name == "Date/Time Original" || t.Name == "Date/Time");
					if (tag != null)
					{
						return ParseExifDateTime(tag.Description!);
					}

					// fallback to JPEG
					var jpeg = peers.FirstOrDefault(p => p.Path.Equals(Path.ChangeExtension(item.Info.Path, ".jpg"), StringComparison.CurrentCultureIgnoreCase));
					if (jpeg != null)
					{
						await using var item2 = new FileUploadItem(jpeg);
						return await GetDateAsync(item2, peers);
					}

					if (item.Info.GetExtension().Equals(".dng", StringComparison.OrdinalIgnoreCase))
					{
						jpeg = peers.FirstOrDefault(p => p.Path.Equals(Path.ChangeExtension(item.Info.Path, ".jpg").Replace("__highres", ""), StringComparison.CurrentCultureIgnoreCase));
						if (jpeg != null)
						{
							await using var item2 = new FileUploadItem(jpeg);
							return await GetDateAsync(item2, peers);
						}
					}
				}
				break;

			case ".MP4":
			case ".MOV":
				{
					var metadata = QuickTimeMetadataReader.ReadMetadata(await item.OpenReadAsync());
					var tag = metadata.SelectMany(d => d.Tags).FirstOrDefault(t => t.Name == "Created");
					if (tag != null)
					{
						if (DateTime.TryParseExact(tag.Description, WellKnownDateTimeFormats.QuickTime, CultureInfo.CurrentCulture, default, out var result))
							return result;

						if (DateTime.TryParseExact(tag.Description, WellKnownDateTimeFormats.QuickTime, WellKnownCultures.EnglishUnitedStates, default, out result))
							return result;
					}
				}
				break;

			case ".AVI":
				{
					var date = await AviDateReader.ReadAsync(await item.OpenReadAsync());
					if (date != null)
					{
						return date;
					}
				}
				break;

			case ".MPG":
				{
					if (TryParseDate(item.Info.Name, out var date))
						return date;
				}
				break;

			case ".WAV":
				{
					// fallback to MP4
					var mp4 = peers.FirstOrDefault(p => p.Path.Equals(Path.ChangeExtension(item.Info.Path, ".mp4"), StringComparison.CurrentCultureIgnoreCase));
					if (mp4 != null)
					{
						using var item2 = new FileUploadItem(mp4);
						return await GetDateAsync(item2, peers);
					}
				}
				break;

		}

		if (TryParseDate(item.Info.Name, out var dt))
			return dt;

		return null;
	}

	[SuppressMessage("Globalization", "CA1307:Specify StringComparison", Justification = "<Pending>")]
	[SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "<Pending>")]
	internal static bool TryParseDate(string fileName, out DateTime result)
	{
		// IMG_20190525_120904
		// VID_20181226_163237
		// PXL_20181226_163237
		if (fileName.Length >= 19 + 4 && (fileName.StartsWith("IMG_2") || fileName.StartsWith("VID_2") || fileName.StartsWith("PXL_2")) && fileName[12] == '_')
		{
			result = new DateTime(Int32.Parse(fileName.Substring(4, 4)), Int32.Parse(fileName.Substring(8, 2)), Int32.Parse(fileName.Substring(10, 2)));
			return true;
		}

		// WP_20140711_15_25_11_0_Pro
		if (fileName.Length >= 11 + 4 && fileName.StartsWith("WP_") && fileName[11] == '_')
		{
			result = new DateTime(Int32.Parse(fileName.Substring(3, 4)), Int32.Parse(fileName.Substring(7, 2)), Int32.Parse(fileName.Substring(9, 2)));
			return true;
		}

		// 2018_07_01 18_41 Office Lens
		if (fileName.Length >= 25 + 4 && fileName.EndsWith(" Office Lens.jpg") && fileName.Take(4).All(Char.IsDigit) && fileName.StartsWith("2"))
		{
			result = new DateTime(Int32.Parse(fileName.Substring(0, 4)), Int32.Parse(fileName.Substring(5, 2)), Int32.Parse(fileName.Substring(8, 2)));
			return true;
		}

		// 5_25_18 11_39 Office Lens
		if (fileName.Length >= 25 + 4 && fileName.EndsWith(" Office Lens.jpg") && !fileName.Take(4).All(Char.IsDigit))
		{
			var split = fileName.Split('_', ' ');
			result = new DateTime(Int32.Parse(split[2]) + 2000, Int32.Parse(split[0]), Int32.Parse(split[1]));
			return true;
		}

		// Office Lens_20140919_110252
		if (fileName.Length >= 25 + 4 && fileName.StartsWith("Office Lens_2"))
		{
			result = new DateTime(Int32.Parse(fileName.Substring(12, 4)), Int32.Parse(fileName.Substring(16, 2)), Int32.Parse(fileName.Substring(18, 2)));
			return true;
		}

		result = default;
		return false;
	}

	[SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "<Pending>")]
	internal static DateTime ParseExifDateTime(string exifValue) =>
		DateTime.Parse(
			exifValue
				.Remove(4, 1).Insert(4, ".")
				.Remove(7, 1).Insert(7, ".")
				.Insert(10, ".")
		);
}
