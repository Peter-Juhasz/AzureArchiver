using System;
using System.IO;

namespace PhotoArchiver.Upload
{
    public class FileUploadResult
    {
        public FileUploadResult(FileInfo file, UploadResult result, Exception? error)
        {
            File = file;
            Result = result;
            Error = error;
        }
        public FileUploadResult(FileInfo file, UploadResult result)
            : this(file, result, null)
        { }

        public FileInfo File { get; }

        public UploadResult Result { get; }

        public Exception? Error { get; }
    }
}