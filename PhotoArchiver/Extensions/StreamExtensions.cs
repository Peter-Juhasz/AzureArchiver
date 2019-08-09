using System.IO;

namespace PhotoArchiver.Extensions
{
    internal static class StreamExtensions
    {
        public static Stream Rewind(this Stream stream)
        {
            stream.Seek(0L, SeekOrigin.Begin);
            return stream;
        }
    }
}
