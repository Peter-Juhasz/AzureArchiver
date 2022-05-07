namespace PhotoArchiver.Upload;

public record class ArchiveResult(IReadOnlyList<FileUploadResult> Results);