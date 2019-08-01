using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PhotoArchiver.Formats
{
    internal static class AviDateReader
    {
        private static readonly byte[] IDIT = new byte[] { (byte)'I', (byte)'D', (byte)'I', (byte)'T' };
        private static readonly byte[] End = new byte[] { 0x0A, 0x00 };

        public static async Task<DateTime?> ReadAsync(Stream stream)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(1024);
            await stream.ReadAsync(buffer);

            var idx = buffer.AsSpan().IndexOf(IDIT);
            if (idx == -1)
                return null;

            var end = buffer.AsSpan(idx + IDIT.Length + 4).IndexOf(End);
            if (end == -1)
                return null;

            var str = Encoding.ASCII.GetString(buffer.AsSpan(idx + IDIT.Length + 4, end));

            return DateTime.ParseExact(str, "ddd MMM dd HH:mm:ss yyyy", new CultureInfo("en-us"));
        }
    }
}
