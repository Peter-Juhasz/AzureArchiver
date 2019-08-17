using System;

namespace PhotoArchiver.Storage
{
    public class StorageOptions
    {
        public string? ConnectionString { get; set; }

        public string Container { get; set; } = "photos";

        public bool Archive { get; set; } = true;

        public string DirectoryFormat { get; set; } = "{0:yyyy}/{0:MM}/{0:dd}";


        public void Validate()
        {
            if (ConnectionString == null)
            {
                throw new ArgumentNullException(nameof(ConnectionString));
            }
        }
    }
}
