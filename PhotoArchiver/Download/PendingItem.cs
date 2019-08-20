using System;

namespace PhotoArchiver.Download
{
    public class PendingItem
    {
        public PendingItem(Uri blob, string path)
        {
            Blob = blob;
            Path = path;
        }

        public Uri Blob { get; set; }

        public string Path { get; set; }
    }
}
