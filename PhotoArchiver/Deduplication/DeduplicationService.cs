using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace PhotoArchiver.Deduplication
{
    using Costs;

    public class DeduplicationService : IDeduplicationService
    {
        public DeduplicationService(CostEstimator costEstimator)
        {
            CostEstimator = costEstimator;
        }

        protected CostEstimator CostEstimator { get; }

        protected IDictionary<string, HashSet<byte[]>> Store { get; } = new Dictionary<string, HashSet<byte[]>>();

        public async Task<bool> ContainsAsync(BlobContainerClient container, string directory, byte[] hash)
        {
            var key = directory;
            if (!Store.TryGetValue(key, out var set))
            {
                set = await GetHashes(container, directory);
                Store.Add(key, set);
            }

            return set.Contains(hash);
        }

        public void Add(string directory, ReadOnlySpan<byte> hash)
        {
            var key = directory;
            Store[key].Add(hash.ToArray());
        }

        private static async Task<HashSet<byte[]>> GetHashes(BlobContainerClient container, string directory)
        {
            return await container.GetBlobsAsync(
                traits: BlobTraits.None,
                prefix: directory
            )
                .Select(b => b.Properties.ContentHash)
                .Where(b => b != null)
                .ToHashSetAsync(new ByteArrayComparer());
        }


        class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[] a, byte[] b)
            {
                if (a.Length != b.Length) return false;
                for (int i = 0; i < a.Length; i++)
                    if (a[i] != b[i]) return false;
                return true;
            }

            public int GetHashCode(byte[] a)
            {
                uint b = 0;
                for (int i = 0; i < a.Length; i++)
                    b = ((b << 23) | (b >> 9)) ^ a[i];
                return unchecked((int)b);
            }
        }
    }
}
