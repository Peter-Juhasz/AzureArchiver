namespace PhotoArchiver.Files;

public interface IDirectory
{
	string Name { get; }

	string Path { get; }

	Task<IReadOnlyList<IFile>> GetFilesAsync();

	Task<IFile> GetFileAsync(string name);

	Task<IFile> CreateFileAsync(string name);
}
