
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Microsoft.Extensions.Logging;

namespace PhotoArchiver.Deduplication;

public class DeduplicationService(ILogger<DeduplicationService> logger) : IDeduplicationService
{
	protected Dictionary<string, HashSet<ReadOnlyMemory<byte>>> Store { get; } = [];

	public async ValueTask<bool> ContainsAsync(BlobContainerClient container, string directory, ReadOnlyMemory<byte> hash, CancellationToken cancellationToken)
	{
		var key = directory;
		if (!Store.TryGetValue(key, out var set))
		{
			set = await GetHashes(container, directory, cancellationToken);
			Store.Add(key, set);
		}

		return set.Contains(hash);
	}

	public void Add(string directory, ReadOnlyMemory<byte> hash)
	{
		var key = directory;
		Store[key].Add(hash);
	}

	private async Task<HashSet<ReadOnlyMemory<byte>>> GetHashes(BlobContainerClient container, string directory, CancellationToken cancellationToken)
	{
		logger.LogInformation($"Gathering hashes from '{container.Name}/{directory}/'...");

		return await container.GetBlobsAsync(
			traits: BlobTraits.None,
			prefix: directory,
			cancellationToken: cancellationToken
		)
			.Select(b => b.Properties.ContentHash)
			.Where(b => b != null)
			.Select(b => new ReadOnlyMemory<byte>(b))
			.ToHashSetAsync(ByteArrayComparer.Instance, cancellationToken);
	}


	private sealed class ByteArrayComparer : IEqualityComparer<ReadOnlyMemory<byte>>
	{
		public static readonly ByteArrayComparer Instance = new();

		public bool Equals(ReadOnlyMemory<byte> a, ReadOnlyMemory<byte> b) => a.Span.SequenceEqual(b.Span);

		public int GetHashCode(ReadOnlyMemory<byte> a)
		{
			HashCode hash = default;
			hash.AddBytes(a.Span);
			return hash.ToHashCode();
		}
	}
}
