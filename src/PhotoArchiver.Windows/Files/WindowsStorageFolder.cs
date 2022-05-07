using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;

namespace PhotoArchiver.Windows.Files
{
    using PhotoArchiver.Files;

    class WindowsStorageFolder : IDirectory
    {
        public WindowsStorageFolder(StorageFolder folder)
        {
            Folder = folder;
        }

        public string Name => Folder.Name;

        public string Path => Folder.Path;

        public StorageFolder Folder { get; }

        public async Task<IFile> GetFileAsync(string name)
        {
            try
            {
                return new WindowsStorageFile(await Folder.GetFileAsync(name));
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }

        public async Task<IFile> CreateFileAsync(string name)
        {
            return new WindowsStorageFile(await Folder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting));
        }

        public async Task<IReadOnlyList<IFile>> GetFilesAsync()
        {
            var files = await Folder.GetFilesAsync(CommonFileQuery.OrderByName);
            return files.Select(f => new WindowsStorageFile(f)).ToList();
        }
    }
}
