using System.IO;

namespace PhotoArchiver
{
    public class FileUploadResult
    {
        public FileUploadResult(FileInfo file, UploadResult result)
        {
            File = file;
            Result = result;
        }

        public FileInfo File { get; }
        public UploadResult Result { get; }
    }
}