using System;

using Microsoft.Azure.Storage.Blob;

namespace PhotoArchiver.Download
{
    public class DownloadOptions
    {
        public string? Path { get; set; }

        public DateTime? Date { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public string[]? Tags { get; set; }

        public string[]? People { get; set; }

        public StandardBlobTier RehydrationTier { get; set; } = StandardBlobTier.Hot;

        public bool Verify { get; set; } = false;


        public bool Continue { get; set; } = false;



        public bool IsEnabled() => ((StartDate != null && EndDate != null) || Date != null) || Continue;
    }
}
