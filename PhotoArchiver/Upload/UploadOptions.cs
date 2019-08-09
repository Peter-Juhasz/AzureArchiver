namespace PhotoArchiver.Upload
{
    public class UploadOptions
    {
        public string? Path { get; set; }

        public bool Delete { get; set; } = false;

        public string SearchPattern { get; set; } = "**/*";

        public int Skip { get; set; } = 0;

        public int? Take { get; set; } = null;

        public ConflictResolution ConflictResolution { get; set; } = ConflictResolution.Skip;

        public bool Deduplicate { get; set; } = false;

        public int? ParallelBlockCount { get; set; }
    }
}
