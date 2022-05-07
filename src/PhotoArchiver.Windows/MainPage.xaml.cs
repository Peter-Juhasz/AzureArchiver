using Microsoft.Extensions.DependencyInjection;
using PhotoArchiver.Windows.Services;
using PhotoArchiver.Windows.ViewModels;
using System.Linq;
using Windows.ApplicationModel.Core;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace PhotoArchiver.Windows
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;
            ApplicationView.GetForCurrentView().TitleBar.ButtonBackgroundColor = Colors.Transparent;
            ApplicationView.GetForCurrentView().TitleBar.ButtonForegroundColor = Colors.Black;

            TaskStatusManager = App.ServiceProvider.GetRequiredService<TaskStatusManager>();

            ViewModel = new MenuViewModel(TaskStatusManager);
            ViewModel.Recalculate();

            this.InitializeComponent();
        }

        public MenuViewModel ViewModel
        {
            get => DataContext as MenuViewModel;
            set => DataContext = value;
        }

        public TaskStatusManager TaskStatusManager { get; }

        private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                ContentFrame.Navigate(typeof(SettingsPage), null, new SlideNavigationTransitionInfo());
                return;
            }

            switch (args.InvokedItemContainer.Tag)
            {
                case "upload":
                    ContentFrame.Navigate(typeof(UploadPage), null, new SlideNavigationTransitionInfo());
                    break;

                case "browse":
                    ContentFrame.Navigate(typeof(BrowsePage), null, new SlideNavigationTransitionInfo());
                    break;

                case "rehydrating":
                    ContentFrame.Navigate(typeof(RehydratingPage), null, new SlideNavigationTransitionInfo());
                    break;
            }
        }

        private void NavigationView_Loaded(object sender, RoutedEventArgs e)
        {
            NavigationView.SelectedItem = NavigationView.MenuItems[1];
            ContentFrame.Navigate(typeof(BrowsePage), null, new EntranceNavigationTransitionInfo());
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            if (e.SourcePageType.Equals(typeof(SettingsPage)))
                NavigationView.SelectedItem = null;

            if (e.SourcePageType.Equals(typeof(UploadPage)))
                NavigationView.SelectedItem = NavigationView.MenuItems[0];

            if (e.SourcePageType.Equals(typeof(BrowsePage)))
                NavigationView.SelectedItem = NavigationView.MenuItems[1];

            if (e.SourcePageType.Equals(typeof(RehydratingPage)))
            {
                NavigationView.SelectedItem = NavigationView.MenuItems[2];
                ViewModel.Recalculate();
            }
        }
    }
}
