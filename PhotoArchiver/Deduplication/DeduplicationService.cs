using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Azure.Storage.Blob;

namespace PhotoArchiver.Deduplication
{
    using Costs;
    using Extensions;

    public class DeduplicationService : IDeduplicationService
    {
        public DeduplicationService(CostEstimator costEstimator)
        {
            CostEstimator = costEstimator;
        }

        protected CostEstimator CostEstimator { get; }

        protected IDictionary<string, HashSet<string>> Store { get; } = new Dictionary<string, HashSet<string>>();

        public async Task<bool> ContainsAsync(CloudBlobDirectory directory, byte[] hash)
        {
            var key = directory.Uri.ToString();
            if (!Store.TryGetValue(key, out var set))
            {
                set = await GetHashes(directory);
                Store.Add(key, set);
            }

            var encoded = Convert.ToBase64String(hash);
            return set.Contains(encoded);
        }

        public void Add(CloudBlobDirectory directory, ReadOnlySpan<byte> hash)
        {
            var key = directory.Uri.ToString();
            var encoded = Convert.ToBase64String(hash.ToArray());
            Store[key].Add(encoded);
        }


        private async Task<HashSet<string>> GetHashes(CloudBlobDirectory directory)
        {
            var set = new HashSet<string>();

            BlobContinuationToken? continuationToken = null;

            do
            {
                var page = await directory.ListBlobsSegmentedAsync(
                    useFlatBlobListing: true,
                    BlobListingDetails.Metadata,
                    maxResults: null,
                    currentToken: continuationToken,
                    options: null,
                    operationContext: null
                );
                CostEstimator.AddListOrCreateContainer();

                foreach (var item in page.Results.OfType<CloudBlockBlob>())
                {
                    var hash = Convert.ToBase64String(item.GetPlainMd5());
                        
                    set.Add(hash);
                }

                continuationToken = page.ContinuationToken;
            }
            while (continuationToken != null);

            return set;
        }
    }
}
