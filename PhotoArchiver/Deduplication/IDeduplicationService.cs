
using Azure.Storage.Blobs;

namespace PhotoArchiver.Deduplication;

public interface IDeduplicationService
{
	void Add(string directory, byte[] hash);

	ValueTask<bool> ContainsAsync(BlobContainerClient container, string directory, byte[] hash);
}
