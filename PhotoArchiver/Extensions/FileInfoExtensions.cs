using System;
using System.IO;

namespace PhotoArchiver.Extensions
{
    internal static class FileInfoExtensions
    {
        public static bool IsCognitiveServiceCompatible(this FileInfo file) => file.Length <= 4 * 1024 * 1024 && file.Extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase);
    }
}
