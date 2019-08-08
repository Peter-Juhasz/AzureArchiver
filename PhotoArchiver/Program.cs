using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Core;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

[assembly: InternalsVisibleTo("PhotoArchiver.Tests")]

namespace PhotoArchiver
{
    using ComputerVision;
    using Costs;
    using Deduplication;
    using Face;
    using KeyVault;
    using Logging;
    using Update;
    using Storage;

    class Program
    {
        static async Task Main(string[] args)
        {
            // configure
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddCommandLine(args)
                .Build();

            // set up services
            using var serviceProvider = new ServiceCollection()
                // logging
                .AddLogging(builder => builder
                    .AddConsole(options =>
                    {
                        options.IncludeScopes = false;
                    })
                    .AddProvider(new FileLoggerProvider())
                    .SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information)
                    .AddConfiguration(configuration.GetSection("Logging"))
                )

                // storage
                .Configure<StorageOptions>(configuration.GetSection("Storage"))
                .AddSingleton(sp => CloudStorageAccount.Parse(sp.GetService<IOptions<StorageOptions>>().Value.ConnectionString))
                .AddSingleton(sp => sp.GetService<CloudStorageAccount>().CreateCloudBlobClient())

                // key vault
                .Configure<KeyVaultOptions>(configuration.GetSection("KeyVault"))
                .AddSingleton<TokenCache>()
                .AddSingleton<IActiveDirectoryAccessTokenProvider, ActiveDirectoryAccessTokenProvider>()
                .AddSingleton<IKeyResolver>(sp => new KeyVaultKeyResolver((a, r, s) => sp.GetRequiredService<IActiveDirectoryAccessTokenProvider>().GetAccessTokenAsync(r)))

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

                // upload
                .Configure<UploadOptions>(configuration.GetSection("Upload"))
                .AddScoped<Archiver>()
                .AddScoped<IDeduplicationService, DeduplicationService>()

                // update
                .Configure<UpdateOptions>(configuration.GetSection("Update"))
                .AddSingleton<IUpdateService, GitHubUpdateService>()
                    .AddHttpClient<IUpdateService, GitHubUpdateService>(client =>
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "AzureArchiver");
                    }).Services

                .BuildServiceProvider();

            // initialize
            using var scope = serviceProvider.CreateScope();
            var provider = scope.ServiceProvider;

            var archiver = provider.GetRequiredService<Archiver>();
            var options = provider.GetRequiredService<IOptions<UploadOptions>>().Value;
            var storageOptions = provider.GetRequiredService<IOptions<StorageOptions>>().Value;
            var costOptions = provider.GetRequiredService<IOptions<CostOptions>>().Value;
            var client = provider.GetRequiredService<CloudBlobClient>();
            var costEstimator = provider.GetRequiredService<CostEstimator>();
            var logger = provider.GetRequiredService<ILogger<Program>>();

            // check for updates
            var updateService = provider.GetRequiredService<IUpdateService>();
            var updateOptions = provider.GetRequiredService<IOptions<UpdateOptions>>();

            if (updateOptions.Value.Enabled && await updateService.CheckForUpdatesAsync())
            {
                logger.LogWarning($"A new version is available. You can download it from {updateOptions.Value.Home}");
            }

            // create container if not exists
            logger.LogTrace("Ensure container exists...");
            if (await client.GetContainerReference(storageOptions.Container).CreateIfNotExistsAsync())
            {
                logger.LogInformation("Container created.");
                costEstimator.AddListOrCreateContainer();
            }

            // start
            var watch = Stopwatch.StartNew();
            var result = await archiver.ArchiveAsync(options.Path!, default);
            watch.Stop();

            // summarize results
            logger.LogInformation("----------------------------------------------------------------");

            var succeeded = result.Results.Where(r => r.Result.IsSuccessful());
            var failed = result.Results.Where(r => !r.Result.IsSuccessful());

            logger.LogInformation($"Summary: {succeeded.Count()} succeeded, {failed.Count()} failed");
            logger.LogInformation($"Time elapsed: {watch.Elapsed}");
            if (failed.Any())
            {
                logger.LogError(String.Join(Environment.NewLine, failed.Select(f => String.Join('\t', f.Result.ToString().PadRight(24, ' '), f.File.FullName, f.Error?.Message))));
            }

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
