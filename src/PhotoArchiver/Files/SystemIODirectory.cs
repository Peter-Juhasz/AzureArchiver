using System.Runtime.CompilerServices;

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

#pragma warning disable CS1998
	public async IAsyncEnumerable<IFile> GetFilesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		foreach (var fileInfo in Directory.GetFiles("*", SearchOption.AllDirectories))
		{
			yield return new SystemIOFile(fileInfo);
		}
	}
#pragma warning restore CS1998
}
