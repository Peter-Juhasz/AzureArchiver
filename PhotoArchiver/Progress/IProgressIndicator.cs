namespace PhotoArchiver.Progress
{
    public interface IProgressIndicator
    {
        void Initialize();

        void ToIndeterminateState();

        void SetProgress(long processed, long all);

        void ToFinishedState();

        void ToErrorState();
    }
}