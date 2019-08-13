namespace PhotoArchiver.Thumbnails
{
    public class ThumbnailOptions
    {
        public int? MaxWidth { get; set; }

        public int? MaxHeight { get; set; }

        public double Quality { get; set; } = 0.50;

        public string Container { get; set; } = "photos-thumbnails";


        public bool IsEnabled() => MaxWidth != null && MaxHeight != null;
    }
}
