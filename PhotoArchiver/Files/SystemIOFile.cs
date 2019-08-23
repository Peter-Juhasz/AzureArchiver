using System.IO;
using System.Threading.Tasks;

namespace PhotoArchiver.Files
{
    public class SystemIOFile : IFile
    {
        public SystemIOFile(FileInfo fileInfo)
        {
            File = fileInfo;
        }

        public FileInfo File { get; }

        public string Name => File.Name;

        public string Path => File.FullName;


        public Task DeleteAsync()
        {
            File.Delete();
            return Task.CompletedTask;
        }

        public Task<long> GetSizeAsync()
        {
            return Task.FromResult(File.Length);
        }

        public Task<Stream> OpenReadAsync()
        {
            return Task.FromResult(File.OpenRead() as Stream);
        }

        public Task<Stream> OpenWriteAsync()
        {
            return Task.FromResult(File.OpenWrite() as Stream);
        }
    }
}
