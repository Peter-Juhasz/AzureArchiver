using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage.RetryPolicies;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;

using MetadataExtractor;
using MetadataExtractor.Formats.QuickTime;

namespace PhotoArchiver
{
    using ComputerVision;
    using Costs;
    using Extensions;
    using Face;
    using Formats;
    using KeyVault;
    using Storage;
    using Thumbnails;
    using Upload;

    public partial class Archiver
    {
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
        public async Task<ArchiveResult> ArchiveAsync(string path, CancellationToken cancellationToken)
        {
            // initialize
            ProgressIndicator.ToIndeterminateState();
            var directory = new DirectoryInfo(path);
            var container = Client.GetContainerReference(StorageOptions.Container);
            var lastDirectoryName = null as string;

            var results = new List<FileUploadResult>();

            if (KeyVaultOptions.IsEnabled())
            {
                _key = await KeyResolver.ResolveKeyAsync(KeyVaultOptions.KeyIdentifier!.ToString(), cancellationToken);
            }

            // set up filter
            var matcher = new Matcher().AddInclude(Options.SearchPattern);

            var query = matcher.GetResultsInFullPath(directory.FullName)
                .OrderBy(f => f)
                .Select(f => new FileInfo(f))
                .Where(f => !IgnoredFileNames.Contains(f.Name))
                .Where(f => !IgnoredExtensions.Contains(f.Extension));

            if (Options.Skip != 0)
            {
                query = query.Skip(Options.Skip);
            }

            if (Options.Take != null)
            {
                query = query.Take(Options.Take.Value);
            }

            // estimate count
            var processedCount = 0;
            var count = query.Count();
            var processedBytes = 0L;
            var allBytes = query.Sum(f => f.Length);

            // enumerate files in directory
            ProgressIndicator.Initialize();
            ProgressIndicator.SetProgress(processedBytes, allBytes);
            foreach (var file in query)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // display directory
                var currentDirectoryName = file.Directory.FullName;
                if (lastDirectoryName != currentDirectoryName)
                {
                    Logger.LogInformation($"Processing directory '{currentDirectoryName}'");
                    lastDirectoryName = currentDirectoryName;
                }

                using var item = new FileUploadItem(file);
                UploadResult result = default;

                try
                {
                    Logger.LogTrace($"Processing {file}...");

                    // read date
                    var date = await GetDateAsync(item);
                    if (date == null)
                    {
                        result = UploadResult.DateMissing;
                        results.Add(new FileUploadResult(file, result));
                        Logger.Log(UploadResultLogLevelMap[result], $"{result}\t{file.Name}");
                        processedCount++;
                        processedBytes += item.Info.Length;
                        ProgressIndicator.SetProgress(processedBytes, allBytes);
                        continue;
                    }

                    // blob
                    var blobDirectory = container.GetDirectoryReference(String.Format(CultureInfo.InvariantCulture, StorageOptions.DirectoryFormat, date));
                    item.Metadata.Add("OriginalFileName", file.FullName.RemoveDiacritics());
                    item.Metadata.Add("CreatedAt", date.Value.ToString("o", CultureInfo.InvariantCulture));
                    item.Metadata.Add("OriginalFileSize", file.Length.ToString(CultureInfo.InvariantCulture));

                    // deduplicate
                    if (Options.Deduplicate)
                    {
                        Logger.LogTrace($"Computing hash for {file}...");
                        var hash = await item.ComputeHashAsync();

                        if (await DeduplicationService.ContainsAsync(blobDirectory, hash))
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
                                var description = await ComputerVisionClient.DescribeImageInStreamAsync(thumbnail);
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
                            catch (ComputerVisionErrorException ex)
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
                                var faceResult = await FaceClient.Face.DetectWithStreamAsync(thumbnail, returnFaceId: true);
                                CostEstimator.AddFace();
                                var faceIds = faceResult.Where(f => f.FaceId != null).Select(f => f.FaceId!.Value).ToList();
                                var identifyResult = await FaceClient.Face.IdentifyAsync(faceIds, personGroupId: FaceOptions.PersonGroupId, confidenceThreshold: FaceOptions.ConfidenceThreshold);
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

                    var blob = blobDirectory.GetBlockBlobReference(file.Name);

                    if (result != UploadResult.AlreadyExists)
                    {
                        // add metadata
                        AddMetadata(blob, item.Metadata);

                        // set metadata
                        if (!KeyVaultOptions.IsEnabled() && MimeTypes.TryGetValue(file.Extension, out var mimeType))
                        {
                            blob.Properties.ContentType = mimeType;
                        }

                        // check for extistance
                        switch (await ExistsAndCompareAsync(blob, item))
                        {
                            // upload, if not exists
                            case null:
                                result = await UploadCoreAsync(blob, item);
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
                                            var hash = await item.ComputeHashAsync();
                                            var formattedHash = BitConverter.ToString(hash).Replace("-", string.Empty);

                                            blob = blobDirectory.GetBlockBlobReference(Path.ChangeExtension(file.Name, "." + formattedHash + file.Extension));

                                            // set metadata
                                            AddMetadata(blob, item.Metadata);
                                            if (!KeyVaultOptions.IsEnabled() && MimeTypes.TryGetValue(file.Extension, out mimeType))
                                            {
                                                blob.Properties.ContentType = mimeType;
                                            }

                                            // upload with new name
                                            switch (await ExistsAndCompareAsync(blob, item))
                                            {
                                                case null:
                                                    result = await UploadCoreAsync(blob, item);
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
                                        if (blob.Properties.StandardBlobTier == StandardBlobTier.Archive)
                                        {
                                            result = UploadResult.Error;
                                            break;
                                        }

                                        await blob.CreateSnapshotAsync(cancellationToken);
                                        result = await UploadCoreAsync(blob, item);
                                        break;

                                    case ConflictResolution.Overwrite:
                                        if (blob.Properties.StandardBlobTier == StandardBlobTier.Archive)
                                        {
                                            await blob.DeleteAsync(cancellationToken);
                                        }

                                        result = await UploadCoreAsync(blob, item);
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
                            if (Options.Verify && !KeyVaultOptions.IsEnabled())
                            {
                                var hash = await item.ComputeHashAsync();
                                var b64 = Convert.ToBase64String(hash);
                                if (blob.Properties.ContentMD5 != b64)
                                {
                                    throw new VerificationFailedException(item.Info, blob);
                                }
                            }

                            // archive
                            if (StorageOptions.Archive && blob.Properties.StandardBlobTier != StandardBlobTier.Archive)
                            {
                                Logger.LogTrace($"Archiving {blob}...");
                                await blob.SetStandardBlobTierAsync(StandardBlobTier.Archive);
                                CostEstimator.AddWrite();
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

                            var thumbnailContainer = Client.GetContainerReference(ThumbnailOptions.Container);
                            var thumbnailBlob = thumbnailContainer.GetBlockBlobReference(blob.Name);
                            thumbnailBlob.Properties.ContentType = "image/jpeg";
                            AddMetadata(thumbnailBlob, item.Metadata);

                            try
                            {
                                await thumbnailBlob.UploadFromStreamAsync(thumbnail);
                                CostEstimator.AddWrite(thumbnail.Length);
                            }
                            catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == 404)
                            {
                                await thumbnailContainer.CreateIfNotExistsAsync();
                                CostEstimator.AddListOrCreateContainer();
                                await thumbnailBlob.UploadFromStreamAsync(thumbnail.Rewind());
                                CostEstimator.AddWrite(thumbnail.Length);
                            }
                            catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == 409)
                            {
                                await thumbnailBlob.DeleteIfExistsAsync();
                                await thumbnailBlob.UploadFromStreamAsync(thumbnail.Rewind());
                                CostEstimator.AddWrite(thumbnail.Length);
                            }
                        }

                        // delete
                        if (Options.Delete)
                        {
                            Logger.LogTrace($"Deleting {file}...");
                            file.Delete();
                        }
                    }

                    // log
                    results.Add(new FileUploadResult(file, result));
                    Logger.Log(UploadResultLogLevelMap[result], $"{result}\t{file.Name}\t({processedCount + 1} of {count})");
                }
                catch (Exception ex)
                {
                    result = UploadResult.Error;
                    Logger.LogError(ex, $"Failed to process {file}");
                    results.Add(new FileUploadResult(file, result, ex));
                    ProgressIndicator.ToErrorState();
                }
                finally
                {
                    processedCount++;
                    processedBytes += item.Info.Length;
                    ProgressIndicator.SetProgress(processedBytes, allBytes);
                }
            }

            ProgressIndicator.ToFinishedState();

            return new ArchiveResult(results);
        }

        private static void AddMetadata(CloudBlockBlob blob, IDictionary<string, string> metadata)
        {
            foreach (var kv in metadata)
            {
                blob.Metadata.Add(kv.Key, kv.Value);
            }
        }

        private async Task<bool?> ExistsAndCompareAsync(CloudBlockBlob blob, FileUploadItem item)
        {
            // check for exists
            Logger.LogTrace($"Checking for {blob} exists...");
            CostEstimator.AddOther();
            if (await blob.ExistsAsync())
            {
                Logger.LogTrace($"Fetching attributes for {blob}...");
                await blob.FetchAttributesAsync();
                CostEstimator.AddOther();

                // compare file size
                if (!blob.IsEncrypted())
                {
                    if (blob.Properties.Length != item.Info.Length)
                    {
                        return false;
                    }
                }

                // compare hash
                var hash = await item.ComputeHashAsync();
                if (!blob.GetPlainMd5().AsSpan().SequenceEqual(hash.AsSpan()))
                {
                    return false;
                }

                return true;
            }

            return null;
        }

        private async Task<UploadResult> UploadCoreAsync(CloudBlockBlob blob, FileUploadItem item)
        {
            // upload
            Logger.LogTrace($"Uploading {item.Info} to {blob}...");

            var requestOptions = new BlobRequestOptions
            {
                StoreBlobContentMD5 = true,
                DisableContentMD5Validation = false,
                ParallelOperationThreadCount = Options.ParallelBlockCount,
                LocationMode = LocationMode.PrimaryOnly,
            };
            if (KeyVaultOptions.IsEnabled())
            {
                requestOptions.EncryptionPolicy = new BlobEncryptionPolicy(_key, null);
            }

            try
            {
                await blob.UploadFromStreamAsync(await item.OpenReadAsync(), AccessCondition.GenerateEmptyCondition(), requestOptions, null);
            }
            catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == 404)
            {
                await blob.Container.CreateIfNotExistsAsync();
                await blob.UploadFromStreamAsync(await item.OpenReadAsync(), AccessCondition.GenerateEmptyCondition(), requestOptions, null);
            }
            CostEstimator.AddWrite(item.Info.Length);

            return UploadResult.Uploaded;
        }

        private async Task<DateTime?> GetDateAsync(FileUploadItem item)
        {
            Logger.LogTrace($"Reading date for {item.Info}...");

            switch (item.Info.Extension.ToUpperInvariant())
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
                            return ParseExifDateTime(tag.Description);
                        }
                    }
                    break;

                case ".CR2":
                case ".NEF":
                case ".DNG":
                    {
                        var metadata = ImageMetadataReader.ReadMetadata(await item.OpenReadAsync());
                        var tag = metadata.SelectMany(d => d.Tags).FirstOrDefault(t => t.Name == "Date/Time Original" || t.Name == "Date/Time");
                        if (tag != null)
                        {
                            return ParseExifDateTime(tag.Description);
                        }

                        // fallback to JPEG
                        var jpeg = new FileInfo(Path.ChangeExtension(item.Info.FullName, ".jpg"));
                        if (jpeg.Exists)
                        {
                            using var item2 = new FileUploadItem(jpeg);
                            return await GetDateAsync(item2);
                        }

                        if (item.Info.Extension.Equals(".dng", StringComparison.OrdinalIgnoreCase))
                        {
                            jpeg = new FileInfo(Path.ChangeExtension(item.Info.FullName, ".jpg").Replace("__highres", ""));
                            if (jpeg.Exists)
                            {
                                using var item2 = new FileUploadItem(jpeg);
                                return await GetDateAsync(item2);
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

                            return DateTime.ParseExact(tag.Description, WellKnownDateTimeFormats.QuickTime, WellKnownCultures.EnglishUnitedStates);
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
            if (fileName.Length >= 19 + 4 && (fileName.StartsWith("IMG_2") || fileName.StartsWith("VID_2")) && fileName[12] == '_')
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

        [SuppressMessage("Globalization", "CA1307:Specify StringComparison", Justification = "<Pending>")]
        [SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "<Pending>")]
        internal static DateTime ParseExifDateTime(string exifValue) =>
            DateTime.Parse(
                exifValue
                    .Remove(4, 1).Insert(4, ".")
                    .Remove(7, 1).Insert(7, ".")
                    .Insert(10, ".")
            );
    }
}
