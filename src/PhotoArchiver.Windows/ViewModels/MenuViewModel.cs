using PhotoArchiver.Windows.Services;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace PhotoArchiver.Windows.ViewModels
{
    public class MenuViewModel : INotifyPropertyChanged
    {
        public MenuViewModel(TaskStatusManager taskStatusManager)
        {
            TaskStatusManager = taskStatusManager;
            taskStatusManager.Tasks.CollectionChanged += Tasks_CollectionChanged;
        }

        private void Tasks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Recalculate();
        }

        public void Recalculate()
        {
            InProgressCount = TaskStatusManager.Tasks.Count(t => t.IsInProgress);
        }


        private long _inProgressCount;

        public long InProgressCount
        {
            get { return _inProgressCount; }
            set
            {
                if (_inProgressCount != value)
                {
                    _inProgressCount = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InProgressCount)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowCount)));
                }
            }
        }

        public bool ShowCount => InProgressCount > 0;

        public TaskStatusManager TaskStatusManager { get; }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
