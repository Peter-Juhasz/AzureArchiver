using Azure.Storage.Blobs.Models;
using System;

namespace PhotoArchiver.Download
{
    public class BlobDownloadResult
    {
        public BlobDownloadResult(BlobItem blob, DownloadResult result, Exception? error)
        {
            Blob = blob;
            Result = result;
            Error = error;
        }
        public BlobDownloadResult(BlobItem blob, DownloadResult result)
            : this(blob, result, null)
        { }

        public BlobItem Blob { get; }
        public DownloadResult Result { get; }
        public Exception? Error { get; }
    }
}
