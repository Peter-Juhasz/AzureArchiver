using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace PhotoArchiver.Windows.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public MainViewModel()
        {
            SelectedItems = new ObservableCollection<ItemViewModel>();
            SelectedItems.CollectionChanged += SelectedItems_CollectionChanged;
        }

        private void SelectedItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AreMultipleSelected)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownloadSelectedText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QueueSelectedText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowDownloadAll)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowQueueAll)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowDownloadSelected)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowQueueSelected)));
        }

        private DateTimeOffset selectedDate;
        private IReadOnlyList<ItemViewModel> thumbnails;
        private IReadOnlyList<ItemViewModel> items = new List<ItemViewModel>();
        private ItemViewModel selectedItem;
        private bool showThumbnails;

        public DateTimeOffset SelectedDate
        {
            get => selectedDate;
            set
            {
                if (selectedDate != value)
                {
                    selectedDate = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedDate)));
                }
            }
        }

        public IReadOnlyList<ItemViewModel> Thumbnails => thumbnails;

        public IReadOnlyList<ItemViewModel> Items
        {
            get => items;
            set
            {
                items = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Items)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SummaryText)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowDownloadAll)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowQueueAll)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowDownloadSelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowQueueSelected)));

                thumbnails = items.Where(t => t.ThumbnailBlob != null).ToList();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnails)));

                foreach (var selected in SelectedItems.ToList())
                {
                    if (!items.Contains(selected))
                        SelectedItems.Remove(selected);
                }

                if (SelectedItem != null)
                {
                    if (!Items.Contains(SelectedItem))
                    {
                        SelectedItem = null;
                    }
                }
            }
        }

        public ItemViewModel SelectedItem
        {
            get => selectedItem;
            set
            {
                if (selectedItem != value)
                {
                    selectedItem = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedItem)));
                }
            }
        }

        public ObservableCollection<ItemViewModel> SelectedItems { get; }

        public bool AreMultipleSelected => SelectedItems.Count > 1;

        public bool ShowThumbnails
        {
            get => showThumbnails; 
            set
            {
                if (showThumbnails != value)
                {
                    showThumbnails = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowThumbnails)));

                    if (SelectedItem != null && SelectedItem.ThumbnailBlob == null)
                    {
                        SelectedItem = null;
                    }

                    if (!SelectedItems.All(i => i.ThumbnailBlob != null))
                    {
                        SelectedItems.Clear();
                    }
                }
            }
        }

        public string SummaryText
        {
            get
            {
                if (items == null)
                {
                    return null;
                }

                return $"{Count} file(s), {ItemViewModel.BytesToString(items.Sum(i => i.Size))}";
            }
        }

        public string DownloadSelectedText => $"Download selected ({SelectedItems.Count})";
        public string QueueSelectedText => $"Queue download selected ({SelectedItems.Count})";

        public bool ShowDownloadAll => Items.All(b => b.IsAvailable) && !AreMultipleSelected;
        public bool ShowQueueAll => Items.Any(b => !b.IsAvailable) && !AreMultipleSelected;
        public bool ShowDownloadSelected => SelectedItems.All(b => b.IsAvailable) && AreMultipleSelected;
        public bool ShowQueueSelected => SelectedItems.Any(b => !b.IsAvailable) && AreMultipleSelected;

        public int? Count => Items?.Count;


        public event PropertyChangedEventHandler PropertyChanged;
    }
}
