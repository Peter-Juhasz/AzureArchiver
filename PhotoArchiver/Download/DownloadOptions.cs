using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Azure.Storage.Blob;

namespace PhotoArchiver.Download
{
    public class DownloadOptions
    {
        public string? Path { get; set; }

        public DateTime? Date { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public IReadOnlyCollection<string>? Tags { get; set; }

        public IReadOnlyCollection<string>? People { get; set; }

        public StandardBlobTier RehydrationTier { get; set; } = StandardBlobTier.Hot;

        public bool Verify { get; set; } = false;


        public bool Continue { get; set; } = false;

        public bool IsEnabled() => ((StartDate != null && EndDate != null) || Date != null) || Continue;
    }
}
