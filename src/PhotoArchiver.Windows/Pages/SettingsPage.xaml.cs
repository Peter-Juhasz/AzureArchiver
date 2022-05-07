using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PhotoArchiver.Storage;
using PhotoArchiver.Thumbnails;
using Windows.UI.Xaml.Controls;

namespace PhotoArchiver.Windows
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            this.InitializeComponent();


            StorageOptions = App.ServiceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;
            ThumbnailOptions = App.ServiceProvider.GetRequiredService<IOptions<ThumbnailOptions>>().Value;
        }

        public StorageOptions StorageOptions { get; }
        public ThumbnailOptions ThumbnailOptions { get; }
    }
}
