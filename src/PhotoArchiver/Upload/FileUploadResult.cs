namespace PhotoArchiver.Upload;

using Files;

public record class FileUploadResult(IFile File, UploadResult Result, Exception? Error = null);