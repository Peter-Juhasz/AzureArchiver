using System.Collections.Generic;

namespace PhotoArchiver.Download
{
    public class RetrieveResult
    {
        public RetrieveResult(IReadOnlyCollection<BlobDownloadResult> results)
        {
            Results = results;
        }

        public IReadOnlyCollection<BlobDownloadResult> Results { get; }
    }
}
