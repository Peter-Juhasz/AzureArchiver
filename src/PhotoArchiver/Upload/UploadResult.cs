namespace PhotoArchiver.Upload;

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
	public static bool IsSuccessful(this UploadResult result) => result is UploadResult.Uploaded or UploadResult.AlreadyExists;
}
