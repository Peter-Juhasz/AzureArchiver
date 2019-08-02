namespace PhotoArchiver
{
    public enum UploadResult
    {
        Uploaded,
        AlreadyExists,
        Conflict,
        DateMissing,
        Error,
    }

    public static partial class UploadResultExtensions
    {
        public static bool IsSuccessful(this UploadResult result) => result == UploadResult.Uploaded || result == UploadResult.AlreadyExists;
    }
}