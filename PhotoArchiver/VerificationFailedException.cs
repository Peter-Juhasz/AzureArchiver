using System;
using System.IO;

using Microsoft.Azure.Storage.Blob;

namespace PhotoArchiver
{
    public class VerificationFailedException : Exception
    {
        public VerificationFailedException(FileInfo info, CloudBlockBlob blob)
            : base($"Verification failed for file '{info}'")
        {
            Info = info;
            Blob = blob;
        }

        public FileInfo Info { get; }

        public CloudBlockBlob Blob { get; }
    }
}