namespace PhotoArchiver.Files;

public class SystemIODirectory : IDirectory
{
	public SystemIODirectory(DirectoryInfo directory)
	{
		Directory = directory;
	}
	public SystemIODirectory(string path)
		: this(new DirectoryInfo(path))
	{

	}

	public DirectoryInfo Directory { get; }

	public string Name => Directory.Name;

	public string Path => Directory.FullName;


	public async Task<IFile> CreateFileAsync(string name, CancellationToken cancellationToken)
	{
		var file = new FileInfo(System.IO.Path.Combine(Directory.FullName, name));
		await using var _ = file.Create();
		return new SystemIOFile(file);
	}

	public Task<IFile> GetFileAsync(string name, CancellationToken cancellationToken)
	{
		return Task.FromResult(new SystemIOFile(new FileInfo(System.IO.Path.Combine(Directory.FullName, name))) as IFile);
	}

	public Task<IReadOnlyList<IFile>> GetFilesAsync(CancellationToken cancellationToken)
	{
		return Task.FromResult(Directory.GetFiles("*", SearchOption.AllDirectories).Select(f => new SystemIOFile(f)).ToList() as IReadOnlyList<IFile>);
	}
}
