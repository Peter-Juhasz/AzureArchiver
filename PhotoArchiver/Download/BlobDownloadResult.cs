using Microsoft.Azure.Storage.Blob;
using System;

namespace PhotoArchiver.Download
{
    public class BlobDownloadResult
    {
        public BlobDownloadResult(CloudBlockBlob blob, DownloadResult result, Exception? error)
        {
            Blob = blob;
            Result = result;
            Error = error;
        }
        public BlobDownloadResult(CloudBlockBlob blob, DownloadResult result)
            : this(blob, result, null)
        { }

        public CloudBlockBlob Blob { get; }
        public DownloadResult Result { get; }
        public Exception? Error { get; }
    }
}
