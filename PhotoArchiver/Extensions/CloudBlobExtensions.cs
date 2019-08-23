using System;

using Microsoft.Azure.Storage.Blob;

namespace PhotoArchiver.Extensions
{
    public static class CloudBlobExtensions
    {
        public static bool IsEncrypted(this CloudBlob blob) => blob.Metadata.ContainsKey("encryptiondata");

        public static bool IsArchived(this CloudBlob blob) => blob.Properties.StandardBlobTier == StandardBlobTier.Archive;

        public static byte[] GetPlainMd5(this CloudBlob item)
        {
            string? hash = null;

            if (item.IsEncrypted() && item.Metadata.TryGetValue(BlobMetadataKeys.OriginalMd5, out var originalHash))
            {
                hash = originalHash;
            }
            else
            {
                hash = item.Properties.ContentMD5;
            }

            if (hash == null)
            {
                throw new InvalidOperationException("Blob doesn't contain MD5 hash.");
            }

            return Convert.FromBase64String(hash);
        }
    }
}
