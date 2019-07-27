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

            // start
            var result = await archiver.ArchiveAsync(options.Value.Path);

            // summarize results
            var succeeded = result.Results.Where(r => r.Result.IsSuccessful());
            var failed = result.Results.Where(r => !r.Result.IsSuccessful());

            Console.WriteLine($"{succeeded.Count()} succeeded, {failed.Count()} failed");

            foreach (var f in failed)
            {
                Console.WriteLine(String.Join('\t', f.Result, f.File.Name));
            }
        }
    }
}
