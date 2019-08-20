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

        public void Initialize()
        {
        }

        public void SetProgress(long processed, long all)
        {
        }
    }
}
