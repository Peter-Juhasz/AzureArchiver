﻿using PhotoArchiver.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace PhotoArchiver
{
    public class FileUploadItem : IDisposable
    {
        public FileUploadItem(FileInfo info)
        {
            Info = info;
        }

        public FileInfo Info { get; }

        public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

        private Stream? Buffer { get; set; } = null;

        private byte[]? Hash { get; set; } = null;

        public async Task<Stream> OpenReadAsync()
        {
            if (Buffer == null)
            {
                Buffer = new MemoryStream((int)Info.Length);
                using var fileStream = Info.OpenRead();
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
                Metadata.Add("OriginalMD5", Convert.ToBase64String(Hash));
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
