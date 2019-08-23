namespace PhotoArchiver.Progress
{
    public class NullProgressIndicator : IProgressIndicator
    {
        public void ToErrorState()
        {
        }

        public void ToFinishedState()
        {
        }

        public void ToIndeterminateState()
        {
        }

        public void Initialize(long all, long allItems)
        {
        }

        public void SetBytesProgress(long processed)
        {
        }

        public void SetItemProgress(long itemsProcessed)
        {
        }
    }
}
