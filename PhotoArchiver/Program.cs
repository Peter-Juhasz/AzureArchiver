using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

[assembly: InternalsVisibleTo("PhotoArchiver.Tests")]

namespace PhotoArchiver
{
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
                .AddLogging(builder => builder
                    .AddConsole(options =>
                    {
                        options.IncludeScopes = false;
                    })
                    .SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information)
                    .AddConfiguration(configuration)
                )
                .AddSingleton(sp => CloudStorageAccount.Parse(sp.GetService<IOptions<StorageOptions>>().Value.ConnectionString))
                .AddSingleton(sp => sp.GetService<CloudStorageAccount>().CreateCloudBlobClient())
                .Configure<Options>(configuration)
                .Configure<StorageOptions>(configuration.GetSection("Storage"))
                .AddSingleton<Archiver>()
                .BuildServiceProvider();

            // initialize
            var archiver = serviceProvider.GetRequiredService<Archiver>();
            var options = serviceProvider.GetRequiredService<IOptions<Options>>();
            var client = serviceProvider.GetRequiredService<CloudBlobClient>();
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            logger.LogTrace("Ensure container exists...");
            if (await client.GetContainerReference(options.Value.Container).CreateIfNotExistsAsync())
            {
                logger.LogInformation("Container created.");
            }

            // start
            var result = await archiver.ArchiveAsync(options.Value.Path);

            // summarize results
            var succeeded = result.Results.Where(r => r.Result.IsSuccessful());
            var failed = result.Results.Where(r => !r.Result.IsSuccessful());

            logger.LogInformation($"{succeeded.Count()} succeeded, {failed.Count()} failed");
            logger.LogError(String.Join(Environment.NewLine, failed.Select(f => String.Join('\t', f.Result, f.File.FullName))));
        }
    }
}
