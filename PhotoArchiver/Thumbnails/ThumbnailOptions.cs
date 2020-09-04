using System;
using System.Diagnostics.CodeAnalysis;

namespace PhotoArchiver.Thumbnails
{
    [SuppressMessage("Performance", "CA1805:Do not initialize unnecessarily", Justification = "<Pending>")]
    public class ThumbnailOptions
    {
        public int? MaxWidth { get; set; }

        public int? MaxHeight { get; set; }

        public double Quality { get; set; } = 0.50;

        public string Container { get; set; } = "photos-thumbnails";

        public bool Force { get; set; } = false;


        public bool IsEnabled() => MaxWidth != null && MaxHeight != null;


        [SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "<Pending>")]
        public void Validate()
        {
            if (MaxWidth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxWidth));
            }

            if (MaxHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxHeight));
            }

            if (Quality < 0 || Quality > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(Quality));
            }
        }
    }
}
