using System;
using System.IO;
using System.Threading.Tasks;

namespace PhotoArchiver.Files
{
    public interface IFile
    {
        string Name { get; }

        string Path { get; }

        Task<long> GetSizeAsync();
        
        Task<Stream> OpenReadAsync();

        Task DeleteAsync();
        Task<Stream> OpenWriteAsync();
    }

    public static class FileExtensions
    {
        public static string GetExtension(this IFile file) => Path.GetExtension(file.Name);

        public static bool IsJpeg(this IFile file) => file.GetExtension().Equals(".jpg", StringComparison.OrdinalIgnoreCase);
    }
}