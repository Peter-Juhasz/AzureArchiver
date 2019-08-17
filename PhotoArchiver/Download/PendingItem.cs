using System;

namespace PhotoArchiver.Download
{
    public class PendingItem
    {
        public Uri? Blob { get; set; }

        public string? Path { get; set; }
    }
}
