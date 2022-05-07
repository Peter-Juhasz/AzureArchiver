namespace PhotoArchiver.Extensions;

internal static class FileInfoExtensions
{
	public static bool IsJpeg(this FileInfo file) => file.Extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase);

	public static bool SupportsThumbnail(this FileInfo file) => file.IsJpeg();
}
