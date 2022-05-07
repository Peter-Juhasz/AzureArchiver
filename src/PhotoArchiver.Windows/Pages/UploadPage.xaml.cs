using Microsoft.Extensions.DependencyInjection;
using PhotoArchiver.Windows.Files;
using PhotoArchiver.Windows.Services;
using PhotoArchiver.Windows.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace PhotoArchiver.Windows
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class UploadPage : Page
    {
        public UploadPage()
        {
            this.InitializeComponent();

            TaskStatusManager = App.ServiceProvider.GetRequiredService<TaskStatusManager>();
        }

        public TaskStatusManager TaskStatusManager { get; }

        private void Grid_DragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
            }
        }

        private async void UploadFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add("*");
            var folder = await picker.PickSingleFolderAsync();
            if (folder == null)
            {
                return;
            }

            var path = folder.Path;
            using (var scope = App.ServiceProvider.CreateScope())
            {
                var archiver = scope.ServiceProvider.GetRequiredService<Archiver>();
                var progressIndicator = new UploadTaskViewModel();
                TaskStatusManager.Add(progressIndicator);

                Task.Run(() => archiver.ArchiveAsync(new WindowsStorageFolder(folder), progressIndicator, progressIndicator.CancellationToken));
                Frame.Navigate(typeof(RehydratingPage));
            }
        }

        private async void UploadFilesButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add("*");
            var selected = await picker.PickMultipleFilesAsync();
            if (!selected.Any())
            {
                return;
            }

            using (var scope = App.ServiceProvider.CreateScope())
            {
                var archiver = scope.ServiceProvider.GetRequiredService<Archiver>();
                var progressIndicator = new UploadTaskViewModel();
                TaskStatusManager.Add(progressIndicator);

                var files = selected.Select(f => new WindowsStorageFile(f)).ToList();

                Task.Run(() => archiver.ArchiveAsync(files, progressIndicator, progressIndicator.CancellationToken));
                Frame.Navigate(typeof(RehydratingPage));
            }
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            var items = await e.DataView.GetStorageItemsAsync();
            foreach (var folder in items.OfType<StorageFolder>().Select(f => new WindowsStorageFolder(f)))
            {
                using (var scope = App.ServiceProvider.CreateScope())
                {
                    var archiver = scope.ServiceProvider.GetRequiredService<Archiver>();
                    var progressIndicator = new UploadTaskViewModel();
                    TaskStatusManager.Add(progressIndicator);

                    Task.Run(() => archiver.ArchiveAsync(folder, progressIndicator, progressIndicator.CancellationToken));
                }
            }

            var files = items.OfType<IStorageFile>().Select(f => new WindowsStorageFile(f)).ToList();
            if (files.Any())
            {
                using (var scope = App.ServiceProvider.CreateScope())
                {
                    var archiver = scope.ServiceProvider.GetRequiredService<Archiver>();
                    var progressIndicator = new UploadTaskViewModel();
                    TaskStatusManager.Add(progressIndicator);

                    Task.Run(() => archiver.ArchiveAsync(files, progressIndicator, progressIndicator.CancellationToken));
                }
            }

            Frame.Navigate(typeof(RehydratingPage));
        }
    }
}
