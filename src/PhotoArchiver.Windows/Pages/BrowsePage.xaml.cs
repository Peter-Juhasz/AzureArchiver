using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PhotoArchiver.Download;
using PhotoArchiver.Storage;
using PhotoArchiver.Thumbnails;
using PhotoArchiver.Windows.Extensions;
using PhotoArchiver.Windows.Files;
using PhotoArchiver.Windows.Services;
using PhotoArchiver.Windows.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;

namespace PhotoArchiver.Windows
{
    public sealed partial class BrowsePage : Page
    {
        public BrowsePage()
        {
            Client = App.ServiceProvider.GetRequiredService<BlobServiceClient>();
            ThumbnailOptions = App.ServiceProvider.GetRequiredService<IOptions<ThumbnailOptions>>().Value;
            StorageOptions = App.ServiceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;
            TaskStatusManager = App.ServiceProvider.GetRequiredService<TaskStatusManager>();

            ViewModel = new MainViewModel()
            {
                SelectedDate = DateTimeOffset.Now,
            };

            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("Browse.Date", out var date))
            {
                ViewModel.SelectedDate = (DateTimeOffset)date;
            }

            this.InitializeComponent();

            DataTransferManager.GetForCurrentView().DataRequested += BrowsePage_DataRequested;

            Unloaded += BrowsePage_Unloaded;
        }

        private void BrowsePage_Unloaded(object sender, RoutedEventArgs e)
        {
            DataTransferManager.GetForCurrentView().DataRequested -= BrowsePage_DataRequested;

            ApplicationData.Current.LocalSettings.Values["Browse.Date"] = ViewModel.SelectedDate;

            if (ViewModel.SelectedItem != null)
            {
                ApplicationData.Current.LocalSettings.Values["Browse.SelectedItem.Blob.Name"] = ViewModel.SelectedItem.Blob.Name.ToString();
            }
        }

        private async void BrowsePage_DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            if (ViewModel.SelectedItem != null)
            {
                var deferral = args.Request.GetDeferral();

                args.Request.Data.Properties.Title = ViewModel.SelectedItem.Name;
                
                if (ViewModel.SelectedItem.ThumbnailBlob != null)
                {
                    var thumbnailBuffer = new MemoryStream();
                    await ViewModel.SelectedItem.ThumbnailBlobClient.DownloadToAsync(thumbnailBuffer);
                    args.Request.Data.Properties.Thumbnail = RandomAccessStreamReference.CreateFromStream(thumbnailBuffer.Rewind().AsRandomAccessStream());
                }

                var buffer = new MemoryStream();
                await ViewModel.SelectedItem.BlobClient.DownloadToAsync(buffer);
                args.Request.Data.SetBitmap(RandomAccessStreamReference.CreateFromStream(buffer.Rewind().AsRandomAccessStream()));

                deferral.Complete();
            }
        }

        public BlobServiceClient Client { get; }
        public ThumbnailOptions ThumbnailOptions { get; }
        public StorageOptions StorageOptions { get; }
        public TaskStatusManager TaskStatusManager { get; }
        public MainViewModel ViewModel
        {
            get => DataContext as MainViewModel;
            set => DataContext = value;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadThumbnailsAsync();
        }

        private StorageSharedKeyCredential GetStorageSharedKeyCredential()
        {
            var props = StorageOptions.ConnectionString.Split(';').ToDictionary(p => p.Substring(0, p.IndexOf('=')).Trim(), p => p.Substring(p.IndexOf('=') + 1).Trim());
            return new StorageSharedKeyCredential(props["AccountName"], props["AccountKey"]);
        }

        private async Task LoadThumbnailsAsync()
        {
            var thumbnailsContainer = Client.GetBlobContainerClient(ThumbnailOptions.Container);

            var builder = new BlobSasBuilder()
            {
                BlobContainerName = thumbnailsContainer.Name,
                ExpiresOn = DateTimeOffset.Now.Date.AddDays(2),
            };
            builder.SetPermissions(BlobAccountSasPermissions.Read);
            var thumbnailsSignature = builder.ToSasQueryParameters(GetStorageSharedKeyCredential()).ToString();

            var directory = String.Format(StorageOptions.DirectoryFormat, ViewModel.SelectedDate.LocalDateTime);
            var thumbnails = await thumbnailsContainer
                .GetBlobsAsync(BlobTraits.Metadata, prefix: directory)
                .ToListAsync();

            var container = Client.GetBlobContainerClient(StorageOptions.Container);
            var blobs = await container
                .GetBlobsAsync(BlobTraits.Metadata, prefix: directory)
                .ToListAsync();

            var items = blobs.Select(b => new ItemViewModel
            {
                BlobContainer = container,
                Blob = b,
                Name = Path.GetFileName(b.Name),
            }).ToList();

            foreach (var item in items)
            {
                var thumbnail = thumbnails.SingleOrDefault(b => b.Name == item.Blob.Name);
                if (thumbnail != null)
                {
                    item.ThumbnailBlobContainer = thumbnailsContainer;
                    item.ThumbnailBlob = thumbnail;
                    item.ThumbnailSource = new BitmapImage(new Uri(item.ThumbnailBlobClient.Uri + "?" + thumbnailsSignature));
                }
            }

            ViewModel.Items = items;
            ViewModel.SelectedItem = null;

            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("Browse.SelectedItem.Blob.Name", out var uri))
            {
                var any = items.FirstOrDefault(i => i.Blob.Name == uri.ToString());
                if (any != null)
                {
                    ViewModel.SelectedItem = any;
                }
            }
        }

        private async void CalendarDatePicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs e)
        {
            await LoadThumbnailsAsync();
        }
        
        private async void DateBackButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectedDate = ViewModel.SelectedDate.AddDays(-1);
            await LoadThumbnailsAsync();
        }

        private async void DateForwardButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectedDate = ViewModel.SelectedDate.AddDays(+1);
            await LoadThumbnailsAsync();
        }

        private async void DownloadSelectedItemsButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add("*");
            var folder = await picker.PickSingleFolderAsync();
            if (folder == null)
            {
                return;
            }

            var args = new DownloadOptions
            {
                Date = ViewModel.SelectedDate.LocalDateTime,
            };
            using (var scope = App.ServiceProvider.CreateScope())
            {
                var archiver = scope.ServiceProvider.GetRequiredService<Archiver>();
                var progressIndicator = new UploadTaskViewModel();
                TaskStatusManager.Add(progressIndicator);

                var container = Client.GetBlobContainerClient(StorageOptions.Container);
                var blobs = ViewModel.SelectedItems.Select(i => i.Blob).ToList();
                Task.Run(() => archiver.RetrieveAsync(container, blobs, new WindowsStorageFolder(folder), args, progressIndicator, progressIndicator.CancellationToken));
                Frame.Navigate(typeof(RehydratingPage));
            }
        }

        private async void DownloadAllButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add("*");
            var folder = await picker.PickSingleFolderAsync();
            if (folder == null)
            {
                return;
            }

            var args = new DownloadOptions
            {
                Date = ViewModel.SelectedDate.LocalDateTime,
            };
            using (var scope = App.ServiceProvider.CreateScope())
            {
                var archiver = scope.ServiceProvider.GetRequiredService<Archiver>();
                var progressIndicator = new UploadTaskViewModel();
                TaskStatusManager.Add(progressIndicator);

                Task.Run(() => archiver.RetrieveAsync(new WindowsStorageFolder(folder), args, progressIndicator, progressIndicator.CancellationToken));
                Frame.Navigate(typeof(RehydratingPage));
            }
        }

        private void SelectionModeToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            switch (SelectionModeToggleButton.IsChecked)
            {
                case true:
                    ThumbnailsGridView.SelectionMode = ListViewSelectionMode.Multiple;
                    break;

                case false:
                    ThumbnailsGridView.SelectionMode = ListViewSelectionMode.Single;
                    break;
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadThumbnailsAsync();
        }

        private async void RehydrateSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var item = ViewModel.SelectedItem;
            await item.BlobClient.SetAccessTierAsync(AccessTier.Hot);
            item.BlobProperties = await item.BlobClient.GetPropertiesAsync();
            item.RaiseChanged();
        }

        private async void ArchiveSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var item = ViewModel.SelectedItem;
            await item.BlobClient.SetAccessTierAsync(AccessTier.Archive);
            item.BlobProperties = await item.BlobClient.GetPropertiesAsync();
            item.RaiseChanged();
        }

        private async void RefreshSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var item = ViewModel.SelectedItem;
            item.BlobProperties = await item.BlobClient.GetPropertiesAsync();
            item.RaiseChanged();
        }

        private async void DeleteSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            ItemViewModel item = ViewModel.SelectedItem;

            ContentDialog dialog = new ContentDialog();
            dialog.Title = $"Delete permanently?";
            dialog.Content = $"Are you sure, you want to delete file '{item.Name}' permanently? You may not be able to recover it.";
            dialog.PrimaryButtonText = "Yes";
            dialog.CloseButtonText = "Cancel";
            var result = await dialog.ShowAsync(ContentDialogPlacement.Popup);
            if (result == ContentDialogResult.Primary)
            {
                if (item.ThumbnailBlob != null)
                {
                    await item.ThumbnailBlobClient.DeleteAsync();
                }

                await item.BlobClient.DeleteAsync();

                ViewModel.Items = ViewModel.Items.Where(i => i.Blob.Name != item.Blob.Name).ToList();
            }
        }

        private void ZoomSliderContainer_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            (sender as Border).Background = new SolidColorBrush(Color.FromArgb(192, 255, 255, 255));
        }

        private void ZoomSliderContainer_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            (sender as Border).Background = null;
        }

        private async void DownloadSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            FileSavePicker picker = new FileSavePicker();
            picker.SuggestedFileName = ViewModel.SelectedItem.Name;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeChoices.Add("Image", new[] { Path.GetExtension(ViewModel.SelectedItem.Name) });
            var file = await picker.PickSaveFileAsync();
            if (file == null)
            {
                return;
            }

            var progress = new UploadTaskViewModel();
            progress.Maximum = ViewModel.SelectedItem.Blob.Properties.ContentLength ?? 0;
            TaskStatusManager.Add(progress);

            var blob = ViewModel.SelectedItem.BlobClient;
            Task.Run(async () =>
            {
                using (var stream = await file.OpenStreamForWriteAsync())
                {
                    await blob.DownloadToAsync(stream, cancellationToken: progress.CancellationToken);
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        progress.Value = progress.Maximum;
                        progress.ProcessedItemsCount = 1;
                        progress.IsFinished = true;
                    });
                }
            });
        }

        private async void ShareSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            DataTransferManager.ShowShareUI();
        }

        private void AllBlobsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel.ShowThumbnails)
            {
                return;
            }

            foreach (ItemViewModel item in e.AddedItems)
                ViewModel.SelectedItems.Add(item);

            foreach (ItemViewModel item in e.RemovedItems)
                ViewModel.SelectedItems.Remove(item);
        }

        private void ThumbnailsGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ViewModel.ShowThumbnails)
            {
                return;
            }

            foreach (ItemViewModel item in e.AddedItems)
                ViewModel.SelectedItems.Add(item);

            foreach (ItemViewModel item in e.RemovedItems)
                ViewModel.SelectedItems.Remove(item);
        }

        private void Img_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            var storyboard = new Storyboard() { Duration = TimeSpan.FromSeconds(0.5), AutoReverse = false };
            var image = sender as Image;
            if (image.RenderTransform == null || image.RenderTransform is MatrixTransform)
            {
                image.RenderTransform = new ScaleTransform();
            }

            var geometry = new RectangleGeometry();
            geometry.Rect = new Rect(0, 0, ZoomSlider.Value, ZoomSlider.Value);
            (image.Parent as UIElement).Clip = geometry;

            var animationX = new DoubleAnimation() { To = 1.25 };
            Storyboard.SetTarget(animationX, image.RenderTransform);
            Storyboard.SetTargetProperty(animationX, "ScaleX");
            storyboard.Children.Add(animationX);

            var animationY = new DoubleAnimation() { To = 1.25 };
            Storyboard.SetTarget(animationY, image.RenderTransform);
            Storyboard.SetTargetProperty(animationY, "ScaleY");
            storyboard.Children.Add(animationY);

            storyboard.Begin();
        }

        private void Img_PointerLeft(object sender, PointerRoutedEventArgs e)
        {
            var storyboard = new Storyboard() { Duration = TimeSpan.FromSeconds(0.5), AutoReverse = false };
            var image = sender as Image;
            if (image.RenderTransform == null || image.RenderTransform is MatrixTransform)
            {
                image.RenderTransform = new ScaleTransform();
            }

            var animationX = new DoubleAnimation() { To = 1 };
            Storyboard.SetTarget(animationX, image.RenderTransform);
            Storyboard.SetTargetProperty(animationX, "ScaleX");
            storyboard.Children.Add(animationX);

            var animationY = new DoubleAnimation() { To = 1 };
            Storyboard.SetTarget(animationY, image.RenderTransform);
            Storyboard.SetTargetProperty(animationY, "ScaleY");
            storyboard.Children.Add(animationY);

            storyboard.Begin();
        }

        private async void RehydrateSelectedItemsButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in ViewModel.SelectedItems)
            {
                await item.BlobClient.SetAccessTierAsync(AccessTier.Hot);
                item.BlobProperties = await item.BlobClient.GetPropertiesAsync();
                item.RaiseChanged();
            }
        }
    }
}
