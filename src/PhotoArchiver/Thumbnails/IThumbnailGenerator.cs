namespace PhotoArchiver.Thumbnails;

public interface IThumbnailGenerator
{
	Task<Stream> GetThumbnailAsync(Stream image, int maxWidth, int maxHeight, CancellationToken cancellationToken);
}
