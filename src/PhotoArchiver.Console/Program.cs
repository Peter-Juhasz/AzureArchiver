using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.CommandLine;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using PhotoArchiver;
using PhotoArchiver.ComputerVision;
using PhotoArchiver.Costs;
using PhotoArchiver.Deduplication;
using PhotoArchiver.Download;
using PhotoArchiver.Face;
using PhotoArchiver.Logging;
using PhotoArchiver.Progress;
using PhotoArchiver.Storage;
using PhotoArchiver.Thumbnails;
using PhotoArchiver.Update;
using PhotoArchiver.Upload;
using PhotoArchiver.Console;
using PhotoArchiver.Console.Commands;

[assembly: InternalsVisibleTo("PhotoArchiver.Tests")]

var hostBuilder = Host.CreateDefaultBuilder(args)
	.ConfigureLogging(builder => builder
		.AddProvider(new FileLoggerProvider())
	)
	.ConfigureServices((hostContext, services) =>
	{
		var configuration = hostContext.Configuration;

		services

		// application insights
		.AddApplicationInsightsTelemetryWorkerService(options =>
		{
			options.EnableAdaptiveSampling = false;
		})

		// storage
		.Configure<StorageOptions>(configuration.GetSection("Storage"))
		.AddSingleton(sp => new BlobServiceClient(sp.GetRequiredService<IOptions<StorageOptions>>().Value.ConnectionString))

		// vision
		.Configure<ComputerVisionOptions>(configuration.GetSection("ComputerVision"))

		// face
		.Configure<FaceOptions>(configuration.GetSection("Face"))

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
		})

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

		// worker
		services.AddHostedService<AppWorker>();
	})
	.UseConsoleLifetime();

var rootCommand = new RootCommand();
rootCommand.AddUploadCommand(hostBuilder);
rootCommand.AddDownloadCommand(hostBuilder);

await rootCommand.InvokeAsync(args);