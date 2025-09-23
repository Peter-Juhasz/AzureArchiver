using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotoArchiver;
using PhotoArchiver.ComputerVision;
using PhotoArchiver.Console;
using PhotoArchiver.Console.Commands;
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
using System;
using System.CommandLine;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("PhotoArchiver.Tests")]

var builder = Host.CreateDefaultBuilder(args);
builder = builder
	.ConfigureAppConfiguration(builder =>
		builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
	)
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

		// workers
		services.AddScoped<UploadWorker>();
		services.AddScoped<DownloadWorker>();

		// commands
		var rootCommand = new RootCommand();
		rootCommand.AddUploadCommand();
		rootCommand.AddDownloadCommand();
		services.AddSingleton(rootCommand);
		services.AddSingleton<IHost, CliHost>();
	})
	.UseConsoleLifetime();

using var host = builder.Build();
await host.RunAsync();


class CliHost(IServiceProvider serviceProvider, RootCommand rootCommand, IHostApplicationLifetime lifetime) : IHost
{
	internal static IHost Instance = null!;

	public IServiceProvider Services => serviceProvider;

	public async Task StartAsync(CancellationToken cancellationToken = default)
	{
		Instance = this;
		Environment.ExitCode = await rootCommand.Parse(Environment.CommandLine).InvokeAsync(cancellationToken: cancellationToken);
		lifetime.StopApplication();
	}

	public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

	public void Dispose() { }
}

internal static partial class Shim
{
	public static IHost GetHost(this ParseResult _) => CliHost.Instance;

	public static void Configure<T>(this IServiceProvider services, Action<T> configure) where T : class
	{
		var options = services.GetRequiredService<IOptions<T>>();
		configure(options.Value);
	}

	public static void Bind<T>(this ParseResult parseResult, Option<T> option, Action<T> bind)
	{
		if (parseResult.GetResult(option) is { Implicit: false } result)
		{
			bind(result.GetValueOrDefault<T>());
		}
	}
}
