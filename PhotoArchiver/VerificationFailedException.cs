using System;

using Microsoft.Azure.Storage.Blob;

namespace PhotoArchiver
{
    using Files;

    public class VerificationFailedException : Exception
    {
        public VerificationFailedException(IFile info, CloudBlockBlob blob)
            : base($"Verification failed for file '{info}'")
        {
            Info = info;
            Blob = blob;
        }

        public IFile Info { get; }

        public CloudBlockBlob Blob { get; }
    }
}