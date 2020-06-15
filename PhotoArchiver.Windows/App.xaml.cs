using Azure.Storage.Blobs;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using PhotoArchiver.ComputerVision;
using PhotoArchiver.Costs;
using PhotoArchiver.Deduplication;
using PhotoArchiver.Download;
using PhotoArchiver.Face;
using PhotoArchiver.Progress;
using PhotoArchiver.Storage;
using PhotoArchiver.Thumbnails;
using PhotoArchiver.Update;
using PhotoArchiver.Upload;
using PhotoArchiver.Windows.Services;
using System;
using System.Net.Http;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace PhotoArchiver.Windows
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;

            RegisterServices();
        }

        internal static IServiceProvider ServiceProvider { get; private set; }

        private void RegisterServices()
        {
            // configure
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();

            // set up services
            var services = new ServiceCollection()
                // logging
                .AddLogging(builder => builder
                    .SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information)
                    .AddConfiguration(configuration.GetSection("Logging"))
                )

                // storage
                .Configure<StorageOptions>(configuration.GetSection("Storage"))
                .AddSingleton(sp => new BlobServiceClient(sp.GetService<IOptions<StorageOptions>>().Value.ConnectionString))

                // vision
                .Configure<ComputerVisionOptions>(configuration.GetSection("ComputerVision"))
                .AddSingleton<IComputerVisionClient>(sp =>
                {
                    var options = sp.GetRequiredService<IOptions<ComputerVisionOptions>>().Value;
                    var client = new ComputerVisionClient(new Microsoft.Azure.CognitiveServices.Vision.ComputerVision.ApiKeyServiceClientCredentials(options.Key), Array.Empty<DelegatingHandler>());
                    if (options.Endpoint != null)
                    {
                        client.Endpoint = options.Endpoint.ToString();
                    }
                    return client;
                })

                // face
                .Configure<FaceOptions>(configuration.GetSection("Face"))
                .AddSingleton<IFaceClient>(sp =>
                {
                    var options = sp.GetRequiredService<IOptions<FaceOptions>>().Value;
                    var client = new FaceClient(new Microsoft.Azure.CognitiveServices.Vision.Face.ApiKeyServiceClientCredentials(options.Key), Array.Empty<DelegatingHandler>());
                    if (options.Endpoint != null)
                    {
                        client.Endpoint = options.Endpoint.ToString();
                    }
                    return client;
                })

                // costs
                .Configure<CostOptions>(configuration.GetSection("Costs"))
                .AddScoped<CostEstimator>()

                // thumbnails
                .Configure<ThumbnailOptions>(configuration.GetSection("Thumbnails"))
                .AddSingleton<IThumbnailGenerator, SixLaborsThumbnailGenerator>()

                // upload
                .Configure<UploadOptions>(configuration.GetSection("Upload"))
                .AddScoped<Archiver>()
                .AddScoped<IDeduplicationService, DeduplicationService>()

                // download
                .Configure<DownloadOptions>(configuration.GetSection("Download"))

                // update
                .Configure<UpdateOptions>(configuration.GetSection("Update"))
                .AddSingleton<IUpdateService, GitHubUpdateService>()
                    .AddHttpClient<IUpdateService, GitHubUpdateService>(client =>
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "AzureArchiver");
                    }).Services

                // tasks
                .AddSingleton<TaskStatusManager>()

                .AddScoped<IProgressIndicator, NullProgressIndicator>()
            ;

            ServiceProvider = services.BuildServiceProvider();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                // Ensure the current window is active
                Window.Current.Activate();
            }
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }
    }
}
