using System;

using Microsoft.Azure.Storage.Blob;

namespace PhotoArchiver.Download
{
    public class DownloadOptions
    {
        public string? Path { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public string[]? Tags { get; set; }

        public string[]? People { get; set; }

        public StandardBlobTier RehydrationTier { get; set; } = StandardBlobTier.Hot;


        public bool IsEnabled() => Path != null && StartDate != null && EndDate != null;
    }
}
