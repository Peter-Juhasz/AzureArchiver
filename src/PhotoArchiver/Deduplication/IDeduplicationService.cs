
using Azure.Storage.Blobs;

namespace PhotoArchiver.Deduplication;

public interface IDeduplicationService
{
	void Add(string directory, ReadOnlyMemory<byte> hash);

	ValueTask<bool> ContainsAsync(BlobContainerClient container, string directory, ReadOnlyMemory<byte> hash, CancellationToken cancellationToken);
}
