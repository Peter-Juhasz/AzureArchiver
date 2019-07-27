namespace PhotoArchiver
{
    public enum UploadResult
    {
        FileSizeMismatch,
        FileHashMismatch,
        AlreadyExists,
        Uploaded,
        DateMissing,
        Error,
    }

    public static partial class Extensions
    {
        public static bool IsSuccessful(this UploadResult result) => result == UploadResult.Uploaded || result == UploadResult.AlreadyExists;
    }
}