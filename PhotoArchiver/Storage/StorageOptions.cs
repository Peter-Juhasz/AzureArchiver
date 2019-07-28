namespace PhotoArchiver
{
    public class StorageOptions
    {
        public string ConnectionString { get; set; }

        public string Container { get; set; } = "photos";

        public bool Archive { get; set; } = true;
    }
}
