using Azure.Storage.Blobs.Models;

namespace PhotoArchiver.Download;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1805:Do not initialize unnecessarily", Justification = "<Pending>")]
public class DownloadOptions
{
	public string? Path { get; set; }

	public DateTime? Date { get; set; }

	public DateTime? StartDate { get; set; }

	public DateTime? EndDate { get; set; }

	public IReadOnlyCollection<string>? Tags { get; set; }

	public IReadOnlyCollection<string>? People { get; set; }

	public AccessTier RehydrationTier { get; set; } = AccessTier.Hot;

	public bool Verify { get; set; } = false;

	public bool Archive { get; set; } = false;


	public bool Continue { get; set; } = false;

	public bool IsEnabled() => (StartDate != null && EndDate != null) || Date != null || Continue;
}
