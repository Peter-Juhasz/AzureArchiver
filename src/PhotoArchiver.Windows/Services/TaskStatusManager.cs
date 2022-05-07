using PhotoArchiver.Windows.ViewModels;
using System.Collections.ObjectModel;

namespace PhotoArchiver.Windows.Services
{
    public class TaskStatusManager
    {
        public void Add(UploadTaskViewModel task)
        {
            Tasks.Add(task);
        }

        public ObservableCollection<UploadTaskViewModel> Tasks { get; } = new ObservableCollection<UploadTaskViewModel>();
    }
}
