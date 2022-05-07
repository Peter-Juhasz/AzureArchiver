using PhotoArchiver.Progress;
using System;
using System.ComponentModel;
using System.Threading;
using System.Windows.Input;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace PhotoArchiver.Windows.ViewModels
{
    public class UploadTaskViewModel : INotifyPropertyChanged, IProgressIndicator, IProgress<long>
    {
        public UploadTaskViewModel()
        {
            CancelCommand = new CancelCommand(this);
        }

        private CancellationTokenSource CancellationTokenSource { get; set; } = new CancellationTokenSource();

        public CancellationToken CancellationToken => CancellationTokenSource.Token;

        public void Cancel()
        {
            CancellationTokenSource.Cancel();
            IsCancelled = true;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCancelled)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInProgress)));
        }

        public ICommand CancelCommand { get; }

        public bool IsUpload => true;

        public bool IsDownload => false;


        private long _processedItemsCount;

        public long ProcessedItemsCount
        {
            get { return _processedItemsCount; }
            set
            {
                if (_processedItemsCount != value)
                {
                    _processedItemsCount = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProcessedItemsCount)));
                }
            }
        }

        private long _allItemsCount;

        public long AllItemsCount
        {
            get { return _allItemsCount; }
            set
            {
                if (_allItemsCount != value)
                {
                    _allItemsCount = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AllItemsCount)));
                }
            }
        }


        private long _value;

        public long Value
        {
            get { return _value; }
            set
            {
                if (_value != value)
                {
                    _value = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                }
            }
        }

        private long _maximum;

        public long Maximum
        {
            get { return _maximum; }
            set
            {
                if (_maximum != value)
                {
                    _maximum = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Maximum)));
                }
            }
        }

        private bool _isFinished;

        public bool IsFinished
        {
            get { return _isFinished; }
            set
            {
                if (_isFinished != value)
                {
                    _isFinished = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFinished)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInProgress)));
                }
            }
        }

        public bool IsInProgress => !IsFinished && !IsCancelled;

        public bool IsCancelled { get; set; }

        public bool HasError { get; set; } = false;

        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler Finished;

        void IProgressIndicator.Initialize(long all, long allItems)
        {
            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                Maximum = all;
                AllItemsCount = allItems;
            });
        }

        void IProgressIndicator.ToIndeterminateState()
        {
        }

        void IProgressIndicator.SetBytesProgress(long processed)
        {
            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                Value = processed;
            });
        }

        void IProgressIndicator.SetItemProgress(long itemsProcessed)
        {
            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                ProcessedItemsCount = itemsProcessed;
            });
        }

        void IProgressIndicator.ToFinishedState()
        {
            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                IsFinished = true;

                Finished?.Invoke(this, EventArgs.Empty);
            });
        }

        void IProgressIndicator.ToErrorState()
        {
            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                HasError = true;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasError)));
            });
        }

        void IProgress<long>.Report(long value)
        {
            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                Value = value;

                if (Value == Maximum)
                {
                    (this as IProgressIndicator).ToFinishedState();
                }
            });
        }

    }
}
