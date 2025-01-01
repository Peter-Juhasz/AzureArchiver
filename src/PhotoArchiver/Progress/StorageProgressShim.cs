namespace PhotoArchiver.Progress;

public sealed record StorageProgressShim(IProgressIndicator ProgressIndicator, long SnapshotBytes) : IProgress<long>
{
	public void Report(long value)
	{
		ProgressIndicator.SetBytesProgress(SnapshotBytes + value);
	}
}
