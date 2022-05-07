namespace PhotoArchiver.Download;

public class RetrievalSession
{
	public Guid Id { get; set; }

	public string? Path { get; set; }

	public DateTimeOffset? Started { get; set; }

	public IReadOnlyCollection<PendingItem> PendingItems { get; set; } = Array.Empty<PendingItem>();
}
