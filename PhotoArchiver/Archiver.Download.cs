using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace PhotoArchiver;

using Costs;
using Download;
using Extensions;
using Files;
using Progress;
using Storage;

public partial class Archiver
{
	public async Task<RetrieveResult> RetrieveAsync(IDirectory directory, DownloadOptions options, IProgressIndicator progressIndicator, CancellationToken cancellationToken)
	{
		// initialize
		progressIndicator.ToIndeterminateState();

		var container = Client.GetBlobContainerClient(StorageOptions.Container);

		// collect blobs to download
		var all = await CollectBlobsForDownloadAsync(options, container);

		return await RetrieveAsync(container, all, directory, options, progressIndicator, cancellationToken);
	}

	public async Task<RetrieveResult> RetrieveAsync(BlobContainerClient container, IReadOnlyList<BlobItem> blobs, IDirectory directory, DownloadOptions options, IProgressIndicator progressIndicator, CancellationToken cancellationToken)
	{
		var results = new List<BlobDownloadResult>();

		// download
		var processedCount = 0;
		var processedBytes = 0L;
		var allBytes = blobs.Sum(b => b.Properties.ContentLength ?? 0);
		var count = blobs.Count;
		progressIndicator.Initialize(allBytes, count);
		progressIndicator.SetBytesProgress(processedBytes);
		foreach (var blob in blobs)
		{
			cancellationToken.ThrowIfCancellationRequested();

			DownloadResult result = default;

			try
			{
				var blobClient = container.GetBlockBlobClient(blob.Name);

				// rehydrate archived blob
				if (blob.Properties.AccessTier == AccessTier.Archive)
				{
					Logger.LogInformation($"Rehydrate '{blob}'...");
					await blobClient.SetAccessTierAsync(options.RehydrationTier, cancellationToken: cancellationToken);
					CostEstimator.AddRead();
					CostEstimator.AddWrite();

					processedCount++;
					progressIndicator.SetItemProgress(processedCount);
					progressIndicator.SetBytesProgress(processedBytes);
					continue;
				}

				// download
				var targetFile = await directory.CreateFileAsync(Path.GetFileName(blob.Name));
				result = await DownloadCoreAsync(targetFile, blob, blobClient, Options.Verify, cancellationToken);
			}
			catch (RequestFailedException ex)
			{
				Logger.LogError(ex, ex.Message);
				result = DownloadResult.Failed;
			}
			finally
			{
				results.Add(new BlobDownloadResult(blob, result));
				processedCount++;
				processedBytes += blob.Properties.ContentLength ?? 0;
				progressIndicator.SetItemProgress(processedCount);
				progressIndicator.SetBytesProgress(processedBytes);
			}
		}

		progressIndicator.ToFinishedState();
		return new RetrieveResult(results);
	}

	private async Task<IReadOnlyList<BlobItem>> CollectBlobsForDownloadAsync(DownloadOptions options, BlobContainerClient container)
	{
		if (options.Date != null && options.StartDate == null && options.EndDate == null)
		{
			options.StartDate = options.Date;
			options.EndDate = options.Date;
		}

		var all = new List<BlobItem>();
		for (var date = options.StartDate!.Value; date <= options.EndDate!.Value; date = date.AddDays(1))
		{
			Logger.LogTrace($"Listing blobs by date '{date}'...");
			CostEstimator.AddListOrCreateContainer();

			var directory = String.Format(CultureInfo.InvariantCulture, StorageOptions.DirectoryFormat, date);

			var page = await container.GetBlobsAsync(BlobTraits.Metadata, BlobStates.None, directory)
				.Where(b => Match(b, options))
				.ToListAsync();
			all.AddRange(page);
		}
		return all;
	}

	[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
	public async Task<RetrieveResult> ContinueAsync(DownloadOptions options, CancellationToken cancellationToken)
	{
		// initialize
		ProgressIndicator.ToIndeterminateState();

		var results = new List<BlobDownloadResult>();

		foreach (var sessionFile in new DirectoryInfo("Sessions").GetFiles())
		{
			// read state
			using var stream = sessionFile.OpenRead();
			var session = await JsonSerializer.DeserializeAsync<RetrievalSession>(stream, cancellationToken: cancellationToken);

			var processed = 0;
			var downloaded = new List<PendingItem>();
			ProgressIndicator.Initialize(0L, session!.PendingItems.Count); // TODO: fix
			ProgressIndicator.SetBytesProgress(processed);

			// process
			foreach (var item in session.PendingItems)
			{
				BlobItem? blob = null; // TODO: fix
				var client = new BlockBlobClient(item.Blob, GetStorageSharedKeyCredential());
				var path = item.Path;

				DownloadResult result = default;

				try
				{
					// download
					IFile? file = null; // TODO: fix
					var progress = new StorageProgressShim(null!, 0L); // TODO: fix
					result = await DownloadCoreAsync(file!, blob!, client, Options.Verify, cancellationToken);

					// archive
					if (options.Archive)
					{
						await client.SetAccessTierAsync(AccessTier.Archive, cancellationToken: cancellationToken);
						CostEstimator.AddRead();
						CostEstimator.AddWrite();
					}
				}
				catch (Exception ex)
				{
					Logger.LogError(ex, ex.Message);
					result = DownloadResult.Failed;
				}
				finally
				{
					results.Add(new BlobDownloadResult(blob!, result));
					processed++;
					ProgressIndicator.SetBytesProgress(processed);
				}
			}

			// save state
			if (downloaded.Count == session.PendingItems.Count)
			{
				sessionFile.Delete();
			}
			else if (downloaded.Any())
			{
				session.PendingItems = session.PendingItems.Except(downloaded).ToArray();
				using var writeStream = sessionFile.OpenWrite();
				await JsonSerializer.SerializeAsync(writeStream, session, cancellationToken: cancellationToken);
			}
		}

		ProgressIndicator.ToFinishedState();
		return new RetrieveResult(results);
	}

	private async Task<DownloadResult> DownloadCoreAsync(IFile file, BlobItem blob, BlockBlobClient client, bool verify, CancellationToken cancellationToken)
	{
		// decryption
		if (blob.IsObsoleteEncrypted())
		{
			throw new NotSupportedException("Encryption is obsolete and not supported by the new Azure storage library.");
		}

		// download
		try
		{
			using var stream = await file.OpenWriteAsync();
			await client.DownloadToAsync(stream, cancellationToken);
			CostEstimator.AddRead(blob.Properties.ContentLength ?? 0);

			// verify
			if (verify)
			{
				// acquire file hash
				var blobHash = blob.Properties.ContentHash ?? (await client.GetPropertiesAsync(cancellationToken: cancellationToken)).Value.ContentHash;
				if (blobHash == null)
				{
					throw new VerificationFailedException(file, client.Uri);
				}

				// compute downloaded file hash
				using var verifyStream = await file.OpenReadAsync();
				using var hashAlgorithm = MD5.Create();
				var hash = hashAlgorithm.ComputeHash(verifyStream);

				// compare
				if (!blobHash.AsSpan().SequenceEqual(hash))
				{
					throw new VerificationFailedException(file, client.Uri);
				}
			}

			return DownloadResult.Succeeded;
		}

		// not rehydrated yet
		catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.BlobBeingRehydrated)
		{
			return DownloadResult.Pending;
		}

		// already exists
		catch (IOException ex) when (ex.HResult == -2147024816)
		{
			return DownloadResult.Conflict;
		}
	}

	private static bool Match(BlobItem blob, DownloadOptions options)
	{
		if (options.Tags?.Any() ?? false)
		{
			if (blob.Metadata.TryGetValue("Tags", out var tags))
			{
				return options.Tags.Any(t => tags.Split(',').Select(t2 => t2.Trim()).Contains(t));
			}
			else
			{
				return false;
			}
		}

		if (options.People?.Any() ?? false)
		{
			if (blob.Metadata.TryGetValue("People", out var people))
			{
				return options.People.Any(t => people.Split(',').Select(t2 => t2.Trim()).Contains(t));
			}
			else
			{
				return false;
			}
		}

		return true;
	}
}
