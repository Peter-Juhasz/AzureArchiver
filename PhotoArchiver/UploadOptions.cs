namespace PhotoArchiver
{
    public class UploadOptions
    {
        public string Path { get; set; }

        public bool IncludeSubdirectories { get; set; } = true;

        public bool Delete { get; set; } = false;

        public string SearchPattern { get; set; } = "*";

        public int Skip { get; set; } = 0;

        public int? Take { get; set; } = null;
    }
}
