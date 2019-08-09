namespace PhotoArchiver.Progress
{
    public interface IProgressIndicator
    {
        void Initialize();

        void Indeterminate();

        void Set(int processed, int all);

        void Finished();

        void Error();
    }
}