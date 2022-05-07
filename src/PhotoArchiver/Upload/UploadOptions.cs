using Azure.Storage.Blobs.Models;

using System.Diagnostics.CodeAnalysis;

namespace PhotoArchiver.Upload;

[SuppressMessage("Performance", "CA1805:Do not initialize unnecessarily", Justification = "<Pending>")]
public class UploadOptions
{
	public string? Path { get; set; }

	public bool Verify { get; set; } = true;

	public bool Delete { get; set; } = false;

	public string SearchPattern { get; set; } = "**/*";

	public int Skip { get; set; } = 0;

	public int? Take { get; set; } = null;

	public ConflictResolution ConflictResolution { get; set; } = ConflictResolution.Skip;

	public bool Deduplicate { get; set; } = false;

	public int? ParallelBlockCount { get; set; }

	public AccessTier AccessTier { get; set; } = AccessTier.Cool;


	[MemberNotNullWhen(true, nameof(Path))]
	public bool IsEnabled() => Path != null;


	[SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "<Pending>")]
	public void Validate()
	{
		if (Skip < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(Skip));
		}

		if (Take < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(Take));
		}

		if (ParallelBlockCount < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(ParallelBlockCount));
		}
	}
}
