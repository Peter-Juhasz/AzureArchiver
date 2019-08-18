using System;

namespace PhotoArchiver.Thumbnails
{
    public class ThumbnailOptions
    {
        public int? MaxWidth { get; set; }

        public int? MaxHeight { get; set; }

        public double Quality { get; set; } = 0.50;

        public string Container { get; set; } = "photos-thumbnails";

        public bool Force { get; set; } = false;


        public bool IsEnabled() => MaxWidth != null && MaxHeight != null;


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
