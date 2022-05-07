using System.Diagnostics.CodeAnalysis;

namespace PhotoArchiver.Storage;

public class StorageOptions
{
	public string? ConnectionString { get; set; }

	public string Container { get; set; } = "photos";

	public string DirectoryFormat { get; set; } = "{0:yyyy}/{0:MM}/{0:dd}";


	[SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "<Pending>")]
	public void Validate()
	{
		if (ConnectionString == null)
		{
			throw new ArgumentNullException(nameof(ConnectionString));
		}
	}
}
