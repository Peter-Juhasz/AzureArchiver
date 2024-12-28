using System.Buffers;
using System.Globalization;
using System.Text;

namespace PhotoArchiver.Formats;

internal static class AviDateReader
{
	private static readonly byte[] IDIT = "IDIT"u8.ToArray();
	private static readonly byte[] End = [0x0A, 0x00];

	public static async Task<DateTime?> ReadAsync(Stream stream, CancellationToken cancellationToken)
	{
		var length = Math.Min(4096, (int)stream.Length);
		var pool = ArrayPool<byte>.Shared;
		var buffer = pool.Rent(length);
		try
		{
			await stream.ReadExactlyAsync(buffer, cancellationToken);

			var idx = buffer.AsSpan().IndexOf(IDIT);
			if (idx == -1)
				return null;

			var end = buffer.AsSpan(idx + IDIT.Length + 4).IndexOf(End);
			if (end == -1)
				return null;

			var value = Encoding.ASCII.GetString(buffer.AsSpan(idx + IDIT.Length + 4, end));

			if (DateTime.TryParseExact(value, WellKnownDateTimeFormats.Riff, CultureInfo.CurrentCulture, default, out var result))
				return result;

			if (DateTime.TryParseExact(value, WellKnownDateTimeFormats.Riff, WellKnownCultures.EnglishUnitedStates, default, out result))
				return result;

			return null;
		}
		finally
		{
			pool.Return(buffer);
		}
	}
}
