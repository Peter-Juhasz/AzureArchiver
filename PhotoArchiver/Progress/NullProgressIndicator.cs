namespace PhotoArchiver.Progress
{
    internal class NullProgressIndicator : IProgressIndicator
    {
        public void Error()
        {
        }

        public void Finished()
        {
        }

        public void Initialize()
        {
        }

        public void Set(int processed, int all)
        {
        }
    }
}
