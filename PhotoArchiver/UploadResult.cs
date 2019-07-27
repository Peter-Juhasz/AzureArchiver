namespace PhotoArchiver
{
    public enum UploadResult
    {
        FileSizeMismatch,
        FileHashMismatch,
        AlreadyExists,
        Uploaded,
        DateMissing
    }
}