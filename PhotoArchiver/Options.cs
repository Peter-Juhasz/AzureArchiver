namespace PhotoArchiver
{
    public class Options
    {
        public string Path { get; set; }

        public bool IncludeSubdirectories { get; set; } = true;


        public string Container { get; set; } = "photos";

        public bool Archive { get; set; } = true;

        public bool Delete { get; set; } = false;
    }
}
