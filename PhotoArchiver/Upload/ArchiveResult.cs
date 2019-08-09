using System.Collections.Generic;

namespace PhotoArchiver.Upload
{
    public class ArchiveResult
    {
        public ArchiveResult(IReadOnlyList<FileUploadResult> results)
        {
            Results = results;
        }

        public IReadOnlyList<FileUploadResult> Results { get; private set; }
    }
}