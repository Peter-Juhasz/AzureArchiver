using System;
using System.Windows.Input;

namespace PhotoArchiver.Windows.ViewModels
{
    internal class CancelCommand : ICommand
    {
        public CancelCommand(UploadTaskViewModel viewModel)
        {
            ViewModel = viewModel;
        }

        public UploadTaskViewModel ViewModel { get; }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => ViewModel.IsInProgress;

        public void Execute(object parameter) => ViewModel.Cancel();
    }
}
