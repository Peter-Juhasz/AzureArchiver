using System;

namespace PhotoArchiver.Progress
{
    public class StorageProgressShim : IProgress<long>
    {
        public StorageProgressShim(IProgressIndicator progressIndicator, long snaphotBytes)
        {
            ProgressIndicator = progressIndicator;
            SnaphotBytes = snaphotBytes;
        }

        public IProgressIndicator ProgressIndicator { get; }
        public long SnaphotBytes { get; }

        public void Report(long value)
        {
            ProgressIndicator.SetBytesProgress(SnaphotBytes + value);
        }
    }
}
