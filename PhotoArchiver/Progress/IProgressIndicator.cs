namespace PhotoArchiver.Progress
{
    public interface IProgressIndicator
    {
        void Initialize(long allBytes, long allItems);

        void ToIndeterminateState();

        void SetBytesProgress(long bytesProcessed);

        void SetItemProgress(long itemsProcessed);

        void ToFinishedState();

        void ToErrorState();
    }
}