namespace PhotoArchiver.Thumbnails;

public interface IThumbnailGenerator
{
	Task<BinaryData> GetThumbnailAsync(Stream image, int maxWidth, int maxHeight, CancellationToken cancellationToken);
}
