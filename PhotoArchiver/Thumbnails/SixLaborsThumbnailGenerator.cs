using System;
using System.IO;
using System.Threading.Tasks;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace PhotoArchiver.Thumbnails
{
    using Extensions;

    public class SixLaborsThumbnailGenerator : IThumbnailGenerator
    {
        public async Task<Stream> GetThumbnailAsync(Stream image, int maxWidth, int maxHeight)
        {
            if (maxWidth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxWidth));
            }

            if (maxHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHeight));
            }

            using var img = Image.Load(image);
            if (img.Width > maxWidth || img.Height > maxHeight)
            {
                int width = maxWidth, height = maxHeight;
                if (img.Width < img.Height)
                {
                    height = maxHeight;
                    width = (int)(img.Width * (double)maxHeight / img.Height);
                }
                else if (img.Height < img.Width)
                {
                    width = maxHeight;
                    height = (int)(img.Height * (double)maxWidth / img.Width);
                }

                img.Mutate(x => x.Resize(width, height));
            }
            var buffer = new MemoryStream();
            img.SaveAsJpeg(buffer);
            return buffer.Rewind();
        }
    }
}
