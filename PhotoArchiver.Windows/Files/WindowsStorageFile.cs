using PhotoArchiver.Files;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace PhotoArchiver.Windows.Files
{
    class WindowsStorageFile : IFile
    {
        public WindowsStorageFile(IStorageFile file)
        {
            File = file;
        }

        public string Name => File.Name;

        public string Path => File.Path;

        public async Task<long> GetSizeAsync() => (long)(await File.GetBasicPropertiesAsync()).Size;

        public IStorageFile File { get; }

        public Task DeleteAsync() => File.DeleteAsync().AsTask();

        public Task<Stream> OpenReadAsync() => File.OpenStreamForReadAsync();

        public Task<Stream> OpenWriteAsync() => File.OpenStreamForWriteAsync();
    }
}
