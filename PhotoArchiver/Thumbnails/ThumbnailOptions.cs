namespace PhotoArchiver.Upload
{
    public class ThumbnailOptions
    {
        public int? MaxWidth { get; set; }

        public int? MaxHeight { get; set; }

        public string Container { get; set; } = "photos-thumbnails";


        public bool IsEnabled() => MaxWidth != null && MaxHeight != null;
    }
}
