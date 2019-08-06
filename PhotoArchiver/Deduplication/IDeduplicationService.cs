using System;
using System.Threading.Tasks;
using Microsoft.Azure.Storage.Blob;

namespace PhotoArchiver.Deduplication
{
    public interface IDeduplicationService
    {
        void Add(CloudBlobDirectory directory, ReadOnlySpan<byte> hash);
        Task<bool> ContainsAsync(CloudBlobDirectory directory, byte[] hash);
    }
}