namespace PhotoArchiver.Progress
{
    public interface IProgressIndicator
    {
        void Initialize();

        void Indeterminate();

        void Set(long processed, long all);

        void Finished();

        void Error();
    }
}