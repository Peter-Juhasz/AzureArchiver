using System;
using System.Threading.Tasks;
using Azure.Storage.Blobs;

namespace PhotoArchiver.Deduplication
{
    public interface IDeduplicationService
    {
        void Add(string directory, ReadOnlySpan<byte> hash);

        Task<bool> ContainsAsync(BlobContainerClient container, string directory, byte[] hash);
    }
}