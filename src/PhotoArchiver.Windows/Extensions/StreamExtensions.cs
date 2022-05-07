using System;
using System.IO;
using System.Threading.Tasks;

namespace PhotoArchiver.Windows.Extensions
{
    public static class StreamExtensions
    {

        public static Stream Rewind(this Stream stream)
        {
            stream.Position = 0L;
            return stream;
        }

        public static MemoryStream ToMemoryStream(this byte[] str) => str != null ? new MemoryStream(str) : null;

        public static async Task<MemoryStream> BufferAsync(this Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (stream is MemoryStream ms)
                return ms;

            MemoryStream buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            await buffer.FlushAsync();
            return buffer.Rewind() as MemoryStream;
        }

    }
}
