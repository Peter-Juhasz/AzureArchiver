using System.Collections.Generic;

namespace PhotoArchiver
{
    public class ArchiveResult
    {
        public ArchiveResult(List<FileUploadResult> results)
        {
            Results = results;
        }

        public IReadOnlyList<FileUploadResult> Results { get; private set; }
    }
}