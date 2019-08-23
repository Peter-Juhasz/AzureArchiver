using System;

namespace PhotoArchiver.Upload
{
    using Files;

    public class FileUploadResult
    {
        public FileUploadResult(IFile file, UploadResult result, Exception? error)
        {
            File = file;
            Result = result;
            Error = error;
        }
        public FileUploadResult(IFile file, UploadResult result)
            : this(file, result, null)
        { }

        public IFile File { get; }

        public UploadResult Result { get; }

        public Exception? Error { get; }
    }
}