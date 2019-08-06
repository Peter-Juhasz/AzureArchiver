using System;

namespace PhotoArchiver.Update
{
    public class UpdateOptions
    {
        public bool Enabled { get; set; } = true;

        public Uri Home { get; set; } = new Uri("https://github.com/Peter-Juhasz/AzureArchiver");

        public Uri Feed { get; set; } = new Uri("https://api.github.com/repos/Peter-Juhasz/AzureArchiver/releases");
    }
}
