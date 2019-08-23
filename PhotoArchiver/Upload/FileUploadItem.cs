using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace PhotoArchiver.Upload
{
    using Extensions;
    using Files;

    public sealed class FileUploadItem : IDisposable
    {
        public FileUploadItem(IFile info)
        {
            Info = info;
        }

        public IFile Info { get; }

        public IDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();

        private Stream? Buffer { get; set; } = null;

        private byte[]? Hash { get; set; } = null;

        public async Task<Stream> OpenReadAsync()
        {
            if (Buffer == null)
            {
                Buffer = new MemoryStream((int)await Info.GetSizeAsync());
                using var fileStream = await Info.OpenReadAsync();
                await fileStream.CopyToAsync(Buffer);
            }

            return Buffer.Rewind();
        }

        public async Task<byte[]> ComputeHashAsync()
        {
            if (Hash == null)
            {
                using var hashAlgorithm = MD5.Create();
                Hash = hashAlgorithm.ComputeHash(await OpenReadAsync());
                Metadata.Add(BlobMetadataKeys.OriginalMd5, Convert.ToBase64String(Hash));
            }

            return Hash;
        }

        public void Dispose()
        {
            if (Buffer != null)
            {
                Buffer.Dispose();
            }
        }
    }
}
