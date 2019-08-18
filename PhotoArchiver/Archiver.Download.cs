using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage.RetryPolicies;
using Microsoft.Extensions.Logging;

namespace PhotoArchiver
{
    using Costs;
    using Download;
    using KeyVault;
    using Storage;

    public partial class Archiver
    {
        public async Task<RetrieveResult> RetrieveAsync(DownloadOptions options, CancellationToken cancellationToken)
        {
            // initialize
            ProgressIndicator.Indeterminate();

            var container = Client.GetContainerReference(StorageOptions.Container);

            if (KeyVaultOptions.IsEnabled())
            {
                _key = await KeyResolver.ResolveKeyAsync(KeyVaultOptions.KeyIdentifier!.ToString(), cancellationToken);
            }

            var results = new List<BlobDownloadResult>();

            // collect blobs to download
            var all = await CollectBlobsForDownloadAsync(options, container);

            // download
            var processedCount = 0;
            ProgressIndicator.Initialize();
            ProgressIndicator.Set(processedCount, all.Count);
            foreach (var blob in all)
            {
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
                        ProgressIndicator.Set(processedCount, all.Count);
                        continue;
                    }

                    // download
                    var path = Path.Combine(options.Path, Path.GetFileName(blob.Name));
                    result = await DownloadCoreAsync(path, blob, cancellationToken);
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
                    ProgressIndicator.Set(processedCount, all.Count);
                }
            }

            ProgressIndicator.Finished();
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

                var directory = container.GetDirectoryReference(String.Format(StorageOptions.DirectoryFormat, date));

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

        public async Task<RetrieveResult> ContinueAsync(CancellationToken cancellationToken)
        {
            // initialize
            ProgressIndicator.Indeterminate();

            var results = new List<BlobDownloadResult>();

            ProgressIndicator.Initialize();
            foreach (var sessionFile in new DirectoryInfo("Sessions").GetFiles())
            {
                // read state
                using var stream = sessionFile.OpenRead();
                var session = await JsonSerializer.DeserializeAsync<RetrievalSession>(stream);

                var processed = 0;
                var downloaded = new List<PendingItem>();
                ProgressIndicator.Set(processed, session.PendingItems.Length);

                // process
                foreach (var item in session.PendingItems)
                {
                    var blob = new CloudBlockBlob(item.Blob, Client);
                    var path = item.Path;

                    DownloadResult result = default;

                    try
                    {
                        // download
                        result = await DownloadCoreAsync(path, blob, cancellationToken);

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
                        ProgressIndicator.Set(processed, session.PendingItems.Length);
                    }
                }

                // save state
                if (downloaded.Count == session.PendingItems.Length)
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

            ProgressIndicator.Finished();
            return new RetrieveResult(results);
        }

        private async Task<DownloadResult> DownloadCoreAsync(string path, CloudBlockBlob blob, CancellationToken cancellationToken)
        {
            // prepare
            var requestOptions = new BlobRequestOptions
            {
                LocationMode = LocationMode.PrimaryThenSecondary,
                DisableContentMD5Validation = false,
            };

            // decryption
            if (blob.Metadata.ContainsKey("encryptiondata"))
            {
                requestOptions.EncryptionPolicy = new BlobEncryptionPolicy(_key, null);
            }

            // download
            try
            {
                await blob.DownloadToFileAsync(
                    path,
                    FileMode.CreateNew,
                    AccessCondition.GenerateEmptyCondition(),
                    requestOptions,
                    null,
                    cancellationToken
                );
                CostEstimator.AddRead(blob.Properties.Length);
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

        private bool Match(CloudBlockBlob blob, DownloadOptions options)
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
