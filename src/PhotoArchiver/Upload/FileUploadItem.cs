using System.Security.Cryptography;
using System.Diagnostics.CodeAnalysis;

namespace PhotoArchiver.Upload;

using Files;

public sealed class FileUploadItem : IDisposable, IAsyncDisposable
{
	public FileUploadItem(IFile info)
	{
		Info = info;
	}

	public IFile Info { get; }

	public IDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();

	private BinaryData? Buffer { get; set; }

	private byte[]? Hash { get; set; }

	[MemberNotNull(nameof(Buffer))]
	private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
	{
		if (Buffer == null)
		{
			using var fileStream = await Info.OpenReadAsync(cancellationToken);
			Buffer = await BinaryData.FromStreamAsync(fileStream, cancellationToken);
		}
	}

	public async Task<Stream> OpenReadAsync(CancellationToken cancellationToken)
	{
		await EnsureLoadedAsync(cancellationToken);

		return Buffer.ToStream();
	}

	public async Task<byte[]> ComputeHashAsync(CancellationToken cancellationToken)
	{
		if (Hash == null)
		{
			await EnsureLoadedAsync(cancellationToken);

			Hash = MD5.HashData(Buffer);
			Metadata.Add(BlobMetadataKeys.OriginalMd5, Convert.ToBase64String(Hash));
		}

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
