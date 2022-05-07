
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Microsoft.Extensions.Logging;

namespace PhotoArchiver.Deduplication;

using Costs;

public class DeduplicationService : IDeduplicationService
{
	public DeduplicationService(CostEstimator costEstimator, ILogger<DeduplicationService> logger)
	{
		CostEstimator = costEstimator;
		Logger = logger;
	}

	protected CostEstimator CostEstimator { get; }
	public ILogger<DeduplicationService> Logger { get; }
	protected IDictionary<string, HashSet<byte[]>> Store { get; } = new Dictionary<string, HashSet<byte[]>>();

	public async ValueTask<bool> ContainsAsync(BlobContainerClient container, string directory, byte[] hash)
	{
		var key = directory;
		if (!Store.TryGetValue(key, out var set))
		{
			set = await GetHashes(container, directory);
			Store.Add(key, set);
		}

		return set.Contains(hash);
	}

	public void Add(string directory, byte[] hash)
	{
		var key = directory;
		Store[key].Add(hash);
	}

	private async Task<HashSet<byte[]>> GetHashes(BlobContainerClient container, string directory)
	{
		Logger.LogInformation($"Gathering hashes from '{container.Name}/{directory}/'...");

		return await container.GetBlobsAsync(
			traits: BlobTraits.None,
			prefix: directory
		)
			.Select(b => b.Properties.ContentHash)
			.Where(b => b != null)
			.ToHashSetAsync(ByteArrayComparer.Instance);
	}


	private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
	{
		public static readonly ByteArrayComparer Instance = new();

		public bool Equals(byte[]? a, byte[]? b)
		{
			if (a == b)
			{
				return true;
			}

			if (a == null || b == null)
			{
				return false;
			}

			if (a.Length != b.Length)
			{
				return false;
			}

			return a.AsSpan().SequenceEqual(b.AsSpan());
		}

		public int GetHashCode(byte[] a)
		{
			HashCode hash = default;
			hash.AddBytes(a);
			return hash.ToHashCode();
		}
	}
}
