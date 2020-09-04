using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PhotoArchiver.Files
{
    public class SystemIODirectory : IDirectory
    {
        public SystemIODirectory(DirectoryInfo directory)
        {
            Directory = directory;
        }
        public SystemIODirectory(string path)
            : this(new DirectoryInfo(path))
        {

        }

        public DirectoryInfo Directory { get; }

        public string Name => Directory.Name;

        public string Path => Directory.FullName;


        public Task<IFile> CreateFileAsync(string name)
        {
            var file = new FileInfo(System.IO.Path.Combine(Directory.FullName, name));
            file.Create().Dispose();
            return Task.FromResult(new SystemIOFile(file) as IFile);
        }

        public Task<IFile> GetFileAsync(string name)
        {
            return Task.FromResult(new SystemIOFile(new FileInfo(System.IO.Path.Combine(Directory.FullName, name))) as IFile);
        }

        public Task<IReadOnlyList<IFile>> GetFilesAsync()
        {
            return Task.FromResult(Directory.GetFiles("*", SearchOption.AllDirectories).Select(f => new SystemIOFile(f)).ToList() as IReadOnlyList<IFile>);
        }
    }
}
