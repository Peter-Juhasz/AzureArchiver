namespace PhotoArchiver.Download;

public record class RetrieveResult(IReadOnlyCollection<BlobDownloadResult> Results);