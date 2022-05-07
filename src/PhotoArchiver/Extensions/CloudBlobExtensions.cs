using Azure.Storage.Blobs.Models;

namespace PhotoArchiver.Extensions;

public static class CloudBlobExtensions
{
	public static bool IsObsoleteEncrypted(this BlobItem blob) => blob.Metadata.ContainsKey("encryptiondata");
}
