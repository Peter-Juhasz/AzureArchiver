namespace PhotoArchiver.Files;

public interface IFile
{
	string Name { get; }

	string Path { get; }

	Task<long> GetSizeAsync(CancellationToken cancellationToken);

	Task<Stream> OpenReadAsync(CancellationToken cancellationToken);

	Task DeleteAsync(CancellationToken cancellationToken);

	Task<Stream> OpenWriteAsync(CancellationToken cancellationToken);
}

public static class FileExtensions
{
	public static string GetExtension(this IFile file) => Path.GetExtension(file.Name);

	public static bool IsJpeg(this IFile file) => file.GetExtension().Equals(".jpg", StringComparison.OrdinalIgnoreCase);
}
