using Azure.Storage.Blobs;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

[assembly: InternalsVisibleTo("PhotoArchiver.Tests")]

using var host = Host.CreateDefaultBuilder(args)
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
		.AddSingleton<IComputerVisionClient>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<ComputerVisionOptions>>().Value;
			var client = new ComputerVisionClient(new Microsoft.Azure.CognitiveServices.Vision.ComputerVision.ApiKeyServiceClientCredentials(options.Key));
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
			var client = new FaceClient(new Microsoft.Azure.CognitiveServices.Vision.Face.ApiKeyServiceClientCredentials(options.Key));
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
	.UseConsoleLifetime()
	.Build();

await host.RunAsync();