namespace PhotoArchiver.Progress
{
    public class NullProgressIndicator : IProgressIndicator
    {
        public void Error()
        {
        }

        public void Finished()
        {
        }

        public void Indeterminate()
        {
        }

        public void Initialize()
        {
        }

        public void Set(long processed, long all)
        {
        }
    }
}
