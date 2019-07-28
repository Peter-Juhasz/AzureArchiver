﻿using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

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
    using Costs;
    using KeyVault;

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
                    .SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information)
                    .AddConfiguration(configuration)
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

                // costs
                .AddScoped<CostEstimator>()

                // app
                .Configure<UploadOptions>(configuration.GetSection("Upload"))
                .AddScoped<Archiver>()

                .BuildServiceProvider();

            // initialize
            using var scope = serviceProvider.CreateScope();
            var provider = scope.ServiceProvider;

            var archiver = provider.GetRequiredService<Archiver>();
            var options = provider.GetRequiredService<IOptions<UploadOptions>>();
            var storageOptions = provider.GetRequiredService<IOptions<StorageOptions>>();
            var client = provider.GetRequiredService<CloudBlobClient>();
            var costEstimator = provider.GetRequiredService<CostEstimator>();
            var logger = provider.GetRequiredService<ILogger<Program>>();

            logger.LogTrace("Ensure container exists...");
            if (await client.GetContainerReference(storageOptions.Value.Container).CreateIfNotExistsAsync())
            {
                logger.LogInformation("Container created.");
                costEstimator.AddListOrCreateContainer();
            }

            // start
            var result = await archiver.ArchiveAsync(options.Value.Path, default);

            // summarize results
            var succeeded = result.Results.Where(r => r.Result.IsSuccessful());
            var failed = result.Results.Where(r => !r.Result.IsSuccessful());

            logger.LogInformation($"{succeeded.Count()} succeeded, {failed.Count()} failed");
            logger.LogError(String.Join(Environment.NewLine, failed.Select(f => String.Join('\t', f.Result, f.File.FullName, f.Error?.Message))));

            logger.LogInformation($"Estimated costs:");
            logger.LogInformation(String.Join(Environment.NewLine, costEstimator.Summarize().Select(t => String.Join('\t', t.Item1, t.Item2.ToString("€{0}")))));
        }
    }
}
