namespace PhotoArchiver.Files;

public interface IDirectory
{
	string Name { get; }

	string Path { get; }

	Task<IReadOnlyList<IFile>> GetFilesAsync(CancellationToken cancellationToken);

	Task<IFile> GetFileAsync(string name, CancellationToken cancellationToken);

	Task<IFile> CreateFileAsync(string name, CancellationToken cancellationToken);
}
