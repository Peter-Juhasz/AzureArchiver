using System.Security.Cryptography;
using System.Diagnostics.CodeAnalysis;

namespace PhotoArchiver.Upload;

using Files;

public sealed class FileUploadItem(IFile info) : IDisposable, IAsyncDisposable
{
	public IFile Info { get; } = info;

	public IDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();

	private BinaryData? Buffer { get; set; }

	private byte[]? Hash { get; set; }

	[MemberNotNull(nameof(Buffer))]
	private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
	{
		if (Buffer == null)
		{
#pragma warning disable CS8774
			await using var fileStream = await Info.OpenReadAsync(cancellationToken);
			Buffer = await BinaryData.FromStreamAsync(fileStream, cancellationToken);
#pragma warning restore CS8774
		}
	}

	public async Task<Stream> OpenReadAsync(CancellationToken cancellationToken)
	{
		await EnsureLoadedAsync(cancellationToken);

		return Buffer.ToStream();
	}

	public ValueTask<ReadOnlyMemory<byte>> ComputeHashAsync(CancellationToken cancellationToken)
	{
		if (Hash != null)
		{
			return new(Hash);
		}

		return ComputeHashCoreAsync(cancellationToken);
	}

	private async ValueTask<ReadOnlyMemory<byte>> ComputeHashCoreAsync(CancellationToken cancellationToken)
	{
		await EnsureLoadedAsync(cancellationToken);

		Hash = MD5.HashData(Buffer);
		Metadata.Add(BlobMetadataKeys.OriginalMd5, Convert.ToBase64String(Hash));

		return Hash;
	}

	public void Dispose()
	{
	}

	public ValueTask DisposeAsync()
	{
		return ValueTask.CompletedTask;
	}
}
