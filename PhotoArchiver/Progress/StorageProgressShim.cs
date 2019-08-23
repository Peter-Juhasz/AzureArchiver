using Microsoft.Azure.Storage.Core.Util;
using System;

namespace PhotoArchiver.Progress
{
    public class StorageProgressShim : IProgress<StorageProgress>
    {
        public StorageProgressShim(IProgressIndicator progressIndicator, long snaphotBytes)
        {
            ProgressIndicator = progressIndicator;
            SnaphotBytes = snaphotBytes;
        }

        public IProgressIndicator ProgressIndicator { get; }
        public long SnaphotBytes { get; }

        public void Report(StorageProgress value)
        {
            ProgressIndicator.SetBytesProgress(SnaphotBytes + value.BytesTransferred);
        }
    }
}
