namespace PhotoArchiver.Files;

public class SystemIOFile(FileInfo fileInfo) : IFile
{
	public FileInfo File { get; } = fileInfo;

	public string Name => File.Name;

	public string Path => File.FullName;


	public Task DeleteAsync(CancellationToken cancellationToken)
	{
		File.Delete();
		return Task.CompletedTask;
	}

	public Task<long> GetSizeAsync(CancellationToken cancellationToken)
	{
		return Task.FromResult(File.Length);
	}

	public Task<Stream> OpenReadAsync(CancellationToken cancellationToken)
	{
		return Task.FromResult(File.OpenRead() as Stream);
	}

	public Task<Stream> OpenWriteAsync(CancellationToken cancellationToken)
	{
		return Task.FromResult(File.OpenWrite() as Stream);
	}
}
