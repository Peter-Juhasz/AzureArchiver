namespace PhotoArchiver.Progress
{
    public interface IProgressIndicator
    {
        void Initialize();

        void Set(int processed, int all);

        void Finished();

        void Error();
    }
}