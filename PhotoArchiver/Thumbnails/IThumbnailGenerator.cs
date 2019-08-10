using System.IO;
using System.Threading.Tasks;

namespace PhotoArchiver.Thumbnails
{
    public interface IThumbnailGenerator
    {
        Task<Stream> GetThumbnailAsync(Stream image, int maxWidth, int maxHeight);
    }
}