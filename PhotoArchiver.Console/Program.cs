using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Azure.Storage.Blobs;
using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

[assembly: InternalsVisibleTo("PhotoArchiver.Tests")]

namespace PhotoArchiver.Console
{
    using ComputerVision;
    using Costs;
    using Deduplication;
    using Download;
    using Face;
    using Files;
    using Logging;
    using Progress;
    using Storage;
    using Thumbnails;
    using Update;
    using Upload;

    class Program
    {
        static async Task Main(string[] args)
        {
            // configure
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddCommandLine(args)
                .Build();

            // set up services
            var services = new ServiceCollection()
                // logging
                .AddLogging(builder => builder
                    .AddConsole(options =>
                    {
                        options.IncludeScopes = false;
                    })
                    .AddProvider(new FileLoggerProvider())
                    .SetMinimumLevel(LogLevel.Information)
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
            ;

            // add platform dependant services
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                services.AddScoped<IProgressIndicator, WindowsTaskbarProgressIndicator>();
            }
            else
            {
                services.AddSingleton<IProgressIndicator, NullProgressIndicator>();
            }

            // build
            using var serviceProvider = services.BuildServiceProvider();

            // initialize
            using var scope = serviceProvider.CreateScope();
            var provider = scope.ServiceProvider;

            var archiver = provider.GetRequiredService<Archiver>();
            var options = provider.GetRequiredService<IOptions<UploadOptions>>().Value;
            var storageOptions = provider.GetRequiredService<IOptions<StorageOptions>>().Value;
            var costOptions = provider.GetRequiredService<IOptions<CostOptions>>().Value;
            var uploadOptions = provider.GetRequiredService<IOptions<UploadOptions>>().Value;
            var downloadOptions = provider.GetRequiredService<IOptions<DownloadOptions>>().Value;
            var client = provider.GetRequiredService<BlobServiceClient>();
            var costEstimator = provider.GetRequiredService<CostEstimator>();
            var logger = provider.GetRequiredService<ILogger<Program>>();

            // check for updates
            var updateService = provider.GetRequiredService<IUpdateService>();
            var updateOptions = provider.GetRequiredService<IOptions<UpdateOptions>>().Value;

            if (updateOptions.Enabled && await updateService.CheckForUpdatesAsync())
            {
                logger.LogWarning($"A new version is available. You can download it from {updateOptions.Home}");
            }

            // create container if not exists
            logger.LogTrace("Ensure container exists...");
            await client.GetBlobContainerClient(storageOptions.Container).CreateIfNotExistsAsync();

            var watch = Stopwatch.StartNew();
            var progressIndicator = provider.GetRequiredService<IProgressIndicator>();

            // upload
            if (uploadOptions.IsEnabled())
            {
                var result = await archiver.ArchiveAsync(new SystemIODirectory(options.Path!), progressIndicator, default);
                watch.Stop();

                // summarize results
                logger.LogInformation("----------------------------------------------------------------");

                var succeeded = result.Results.Where(r => r.Result.IsSuccessful());
                var failed = result.Results.Where(r => !r.Result.IsSuccessful());

                logger.LogInformation($"Summary: {succeeded.Count()} succeeded, {failed.Count()} failed");
                logger.LogInformation($"Time elapsed: {watch.Elapsed}");
                if (failed.Any())
                {
                    logger.LogError(String.Join(Environment.NewLine, failed.Select(f => String.Join('\t', f.Result.ToString().PadRight(24, ' '), f.File.Path, f.Error?.Message))));
                }
            }

            // download
            else if (downloadOptions.IsEnabled())
            {
                if (downloadOptions.Continue)
                {
                    var result = await archiver.ContinueAsync(downloadOptions, default);
                    watch.Stop();

                    // summarize results
                    logger.LogInformation("----------------------------------------------------------------");

                    var succeeded = result.Results.Where(r => r.Result == DownloadResult.Succeeded);
                    var pending = result.Results.Where(r => r.Result == DownloadResult.Pending);
                    var failed = result.Results.Where(r => r.Result == DownloadResult.Failed);

                    logger.LogInformation($"Summary: {succeeded.Count()} succeeded, {pending.Count()} pending, {failed.Count()} failed");
                    logger.LogInformation($"Time elapsed: {watch.Elapsed}");
                    if (failed.Any())
                    {
                        logger.LogError(String.Join(Environment.NewLine, failed.Select(f => String.Join('\t', f.Result.ToString().PadRight(24, ' '), f.Blob.Name, f.Error?.Message))));
                    }
                }
                else
                {
                    var result = await archiver.RetrieveAsync(new SystemIODirectory(downloadOptions.Path!), downloadOptions, progressIndicator, default);
                    watch.Stop();

                    // summarize results
                    logger.LogInformation("----------------------------------------------------------------");

                    var succeeded = result.Results.Where(r => r.Result == DownloadResult.Succeeded);
                    var pending = result.Results.Where(r => r.Result == DownloadResult.Pending);
                    var failed = result.Results.Where(r => r.Result == DownloadResult.Failed);

                    logger.LogInformation($"Summary: {succeeded.Count()} succeeded, {pending.Count()} pending, {failed.Count()} failed");
                    logger.LogInformation($"Time elapsed: {watch.Elapsed}");
                    if (failed.Any())
                    {
                        logger.LogError(String.Join(Environment.NewLine, failed.Select(f => String.Join('\t', f.Result.ToString().PadRight(24, ' '), f.Blob.Name, f.Error?.Message))));
                    }
                }
            }

            // usage summary
            logger.LogInformation($"Usage summary:");
            logger.LogInformation(String.Join(Environment.NewLine, costEstimator.SummarizeUsage().Select(t => $"{t.item.PadRight(48, ' ')}\t{t.amount:N0}")));

            var costsSummary = costEstimator.SummarizeCosts();
            if (costsSummary.Any())
            {
                logger.LogInformation($"Estimated costs:");
                logger.LogInformation(string.Join(Environment.NewLine, costsSummary.Select(t => $"{t.item.PadRight(48, ' ')}\t{costOptions.Currency} {t.cost:N8}")));
            }
        }
    }
}
