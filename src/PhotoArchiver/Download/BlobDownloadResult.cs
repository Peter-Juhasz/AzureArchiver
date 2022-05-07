using Azure.Storage.Blobs.Models;

namespace PhotoArchiver.Download;

public record class BlobDownloadResult(BlobItem Blob, DownloadResult Result, Exception? Error = null);
