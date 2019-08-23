using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage.Core.Util;
using Microsoft.Azure.Storage.RetryPolicies;
using Microsoft.Extensions.Logging;

namespace PhotoArchiver
{
    using Costs;
    using Download;
    using Extensions;
    using Files;
    using KeyVault;
    using Progress;
    using Storage;

    public partial class Archiver
    {
        public async Task<RetrieveResult> RetrieveAsync(IDirectory directory, DownloadOptions options, IProgressIndicator progressIndicator, CancellationToken cancellationToken)
        {
            // initialize
            progressIndicator.ToIndeterminateState();

            var container = Client.GetContainerReference(StorageOptions.Container);

            if (KeyVaultOptions.IsEnabled())
            {
                _key = await KeyResolver.ResolveKeyAsync(KeyVaultOptions.KeyIdentifier!.ToString(), cancellationToken);
            }

            // collect blobs to download
            var all = await CollectBlobsForDownloadAsync(options, container);

            return await RetrieveAsync(all, directory, options, progressIndicator, cancellationToken);
        }

        public async Task<RetrieveResult> RetrieveAsync(IReadOnlyList<CloudBlockBlob> blobs, IDirectory directory, DownloadOptions options, IProgressIndicator progressIndicator, CancellationToken cancellationToken)
        {
            var results = new List<BlobDownloadResult>();

            // download
            var processedCount = 0;
            var processedBytes = 0L;
            var allBytes = blobs.Sum(b => b.Properties.Length);
            progressIndicator.Initialize(allBytes, blobs.Count);
            progressIndicator.SetBytesProgress(processedBytes);
            foreach (var blob in blobs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DownloadResult result = default;

                try
                {
                    // rehydrate archived blob
                    if (blob.Properties.StandardBlobTier == StandardBlobTier.Archive)
                    {
                        Logger.LogInformation($"Rehydrate '{blob}'...");
                        await blob.SetStandardBlobTierAsync(options.RehydrationTier);
                        CostEstimator.AddRead();
                        CostEstimator.AddWrite();

                        processedCount++;
                        progressIndicator.SetItemProgress(processedCount);
                        progressIndicator.SetBytesProgress(processedBytes);
                        continue;
                    }

                    // download
                    var targetFile = await directory.CreateFileAsync(Path.GetFileName(blob.Name));
                    var progress = new StorageProgressShim(progressIndicator, processedBytes);
                    result = await DownloadCoreAsync(targetFile, blob, Options.Verify, progress, cancellationToken);
                }
                catch (StorageException ex)
                {
                    Logger.LogError(ex, ex.Message);
                    result = DownloadResult.Failed;
                }
                finally
                {
                    results.Add(new BlobDownloadResult(blob, result));
                    processedCount++;
                    processedBytes += blob.Properties.Length;
                    progressIndicator.SetItemProgress(processedCount);
                    progressIndicator.SetBytesProgress(processedBytes);
                }
            }

            progressIndicator.ToFinishedState();
            return new RetrieveResult(results);
        }

        private async Task<List<CloudBlockBlob>> CollectBlobsForDownloadAsync(DownloadOptions options, CloudBlobContainer container)
        {
            if (options.Date != null && options.StartDate == null && options.EndDate == null)
            {
                options.StartDate = options.Date;
                options.EndDate = options.Date;
            }

            var all = new List<CloudBlockBlob>();
            for (var date = options.StartDate!.Value; date <= options.EndDate!.Value; date = date.AddDays(1))
            {
                Logger.LogTrace($"Listing blobs by date '{date}'...");
                CostEstimator.AddListOrCreateContainer();

                var directory = container.GetDirectoryReference(String.Format(CultureInfo.InvariantCulture, StorageOptions.DirectoryFormat, date));

                BlobContinuationToken? continuationToken = null;
                do
                {
                    var page = await directory.ListBlobsSegmentedAsync(
                        useFlatBlobListing: true,
                        BlobListingDetails.Metadata,
                        maxResults: null,
                        continuationToken,
                        options: null,
                        operationContext: null
                    );

                    var matching = page.Results.OfType<CloudBlockBlob>().Where(b => Match(b, options));
                    all.AddRange(matching);

                    continuationToken = page.ContinuationToken;
                } while (continuationToken != null);
            }

            return all;
        }

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
        public async Task<RetrieveResult> ContinueAsync(CancellationToken cancellationToken)
        {
            // initialize
            ProgressIndicator.ToIndeterminateState();

            var results = new List<BlobDownloadResult>();

            foreach (var sessionFile in new DirectoryInfo("Sessions").GetFiles())
            {
                // read state
                using var stream = sessionFile.OpenRead();
                var session = await JsonSerializer.DeserializeAsync<RetrievalSession>(stream);

                var processed = 0;
                var downloaded = new List<PendingItem>();
                ProgressIndicator.Initialize(0L, session.PendingItems.Count); // TODO: fix
                ProgressIndicator.SetBytesProgress(processed);

                // process
                foreach (var item in session.PendingItems)
                {
                    var blob = new CloudBlockBlob(item.Blob, Client);
                    var path = item.Path;

                    DownloadResult result = default;

                    try
                    {
                        // download
                        IFile file = null; // TODO: fix
                        var progress = new StorageProgressShim(null, 0L); // TODO: fix
                        result = await DownloadCoreAsync(file, blob, Options.Verify, progress, cancellationToken);

                        // archive
                        if (StorageOptions.Archive)
                        {
                            await blob.SetStandardBlobTierAsync(StandardBlobTier.Archive);
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
                        results.Add(new BlobDownloadResult(blob, result));
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
                    await JsonSerializer.SerializeAsync(writeStream, session);
                }
            }

            ProgressIndicator.ToFinishedState();
            return new RetrieveResult(results);
        }

        private async Task<DownloadResult> DownloadCoreAsync(IFile file, CloudBlockBlob blob, bool verify, IProgress<StorageProgress> progress, CancellationToken cancellationToken)
        {
            // prepare
            var requestOptions = new BlobRequestOptions
            {
                LocationMode = LocationMode.PrimaryThenSecondary,
                DisableContentMD5Validation = false,
            };

            // decryption
            if (blob.IsEncrypted())
            {
                requestOptions.EncryptionPolicy = new BlobEncryptionPolicy(_key, null);
            }

            // download
            try
            {
                using var stream = await file.OpenWriteAsync();
                await blob.DownloadToStreamAsync(
                    stream,
                    AccessCondition.GenerateEmptyCondition(),
                    requestOptions,
                    null,
                    progress,
                    cancellationToken
                );
                CostEstimator.AddRead(blob.Properties.Length);

                // verify
                if (verify)
                {
                    if (blob.Properties.ContentMD5 == null)
                    {
                        await blob.FetchAttributesAsync();
                    }

                    using var verifyStream = await file.OpenReadAsync();
                    using var hashAlgorithm = MD5.Create();
                    var hash = hashAlgorithm.ComputeHash(verifyStream);
                    
                    if (!blob.GetPlainMd5().AsSpan().SequenceEqual(hash))
                    {
                        //throw new VerificationFailedException(file, blob);
                    }
                }

                return DownloadResult.Succeeded;
            }

            // not rehydrated yet
            catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == 400)
            {
                await blob.FetchAttributesAsync();
                CostEstimator.AddOther();

                if (blob.Properties.RehydrationStatus == RehydrationStatus.PendingToCool ||
                    blob.Properties.RehydrationStatus == RehydrationStatus.PendingToHot)
                {
                    return DownloadResult.Pending;
                }
                else
                {
                    return DownloadResult.Failed;
                }
            }

            // already exists
            catch (IOException ex) when (ex.HResult == -2147024816)
            {
                return DownloadResult.Conflict;
            }
        }

        private static bool Match(CloudBlockBlob blob, DownloadOptions options)
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
}
