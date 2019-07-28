namespace PhotoArchiver
{
    public class Options
    {
        public string Path { get; set; }

        public bool IncludeSubdirectories { get; set; } = true;

        public bool Delete { get; set; } = false;

        public string SearchPattern { get; set; } = "*";
    }
}
