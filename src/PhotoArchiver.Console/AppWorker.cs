using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace PhotoArchiver.Console;

using Costs;
using Download;
using Files;
using Progress;
using Storage;
using Update;
using Upload;

public class AppWorker : BackgroundService
{
	public AppWorker(
		IUpdateService updateService,
		Archiver archiver,
		BlobServiceClient client,
		CostEstimator costEstimator,
		IProgressIndicator progressIndicator,
		IOptions<UploadOptions> options,
		IOptions<CostOptions> costOptions,
		IOptions<UpdateOptions> updateOptions,
		IOptions<DownloadOptions> downloadOptions,
		IOptions<StorageOptions> storageOptions,
		ILogger<AppWorker> logger,
		IHostApplicationLifetime lifetime
	)
	{
		UpdateService = updateService;
		Archiver = archiver;
		BlobClient = client;
		CostEstimator = costEstimator;
		ProgressIndicator = progressIndicator;
		UploadOptions = options.Value;
		CostOptions = costOptions.Value;
		UpdateOptions = updateOptions.Value;
		DownloadOptions = downloadOptions.Value;
		StorageOptions = storageOptions.Value;
		Logger = logger;
		Lifetime = lifetime;
	}

	private IUpdateService UpdateService { get; }
	private Archiver Archiver { get; }
	private BlobServiceClient BlobClient { get; }
	private CostEstimator CostEstimator { get; }
	private IProgressIndicator ProgressIndicator { get; }
	private UploadOptions UploadOptions { get; }
	private CostOptions CostOptions { get; }
	private UpdateOptions UpdateOptions { get; }
	private DownloadOptions DownloadOptions { get; }
	private StorageOptions StorageOptions { get; }
	private ILogger<AppWorker> Logger { get; }
	private IHostApplicationLifetime Lifetime { get; }

	protected override async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		try
		{
			// check for updates
			if (UpdateOptions.Enabled && await UpdateService.CheckForUpdatesAsync(cancellationToken))
			{
				Logger.LogWarning($"A new version is available. You can download it from {UpdateOptions.Home}");

				if (UpdateOptions.Stop)
				{
					return;
				}
			}

			// create container if not exists
			Logger.LogTrace("Ensure container exists...");
			await BlobClient.GetBlobContainerClient(StorageOptions.Container).CreateIfNotExistsAsync(cancellationToken: cancellationToken);

			var watch = Stopwatch.StartNew();

			// upload
			if (UploadOptions.IsEnabled())
			{
				var result = await Archiver.ArchiveAsync(new SystemIODirectory(UploadOptions.Path), ProgressIndicator, default);
				watch.Stop();

				// summarize results
				Logger.LogInformation("----------------------------------------------------------------");

				var succeeded = result.Results.Where(r => r.Result.IsSuccessful());
				var failed = result.Results.Where(r => !r.Result.IsSuccessful());

				Logger.LogInformation($"Summary: {succeeded.Count()} succeeded, {failed.Count()} failed");
				Logger.LogInformation($"Time elapsed: {watch.Elapsed}");
				if (failed.Any())
				{
					Logger.LogError(String.Join(Environment.NewLine, failed.Select(f => String.Join('\t', f.Result.ToString().PadRight(24, ' '), f.File.Path, f.Error?.Message))));
				}
			}

			// download
			else if (DownloadOptions.IsEnabled())
			{
				if (DownloadOptions.Continue)
				{
					var result = await Archiver.ContinueAsync(DownloadOptions, default);
					watch.Stop();

					// summarize results
					Logger.LogInformation("----------------------------------------------------------------");

					var succeeded = result.Results.Where(r => r.Result == DownloadResult.Succeeded);
					var pending = result.Results.Where(r => r.Result == DownloadResult.Pending);
					var failed = result.Results.Where(r => r.Result == DownloadResult.Failed);

					Logger.LogInformation($"Summary: {succeeded.Count()} succeeded, {pending.Count()} pending, {failed.Count()} failed");
					Logger.LogInformation($"Time elapsed: {watch.Elapsed}");
					if (failed.Any())
					{
						Logger.LogError(String.Join(Environment.NewLine, failed.Select(f => String.Join('\t', f.Result.ToString().PadRight(24, ' '), f.Blob.Name, f.Error?.Message))));
					}
				}
				else
				{
					var result = await Archiver.RetrieveAsync(new SystemIODirectory(DownloadOptions.Path!), DownloadOptions, ProgressIndicator, default);
					watch.Stop();

					// summarize results
					Logger.LogInformation("----------------------------------------------------------------");

					var succeeded = result.Results.Where(r => r.Result == DownloadResult.Succeeded);
					var pending = result.Results.Where(r => r.Result == DownloadResult.Pending);
					var failed = result.Results.Where(r => r.Result == DownloadResult.Failed);

					Logger.LogInformation($"Summary: {succeeded.Count()} succeeded, {pending.Count()} pending, {failed.Count()} failed");
					Logger.LogInformation($"Time elapsed: {watch.Elapsed}");
					if (failed.Any())
					{
						Logger.LogError(String.Join(Environment.NewLine, failed.Select(f => String.Join('\t', f.Result.ToString().PadRight(24, ' '), f.Blob.Name, f.Error?.Message))));
					}
				}
			}

			// usage summary
			Logger.LogInformation($"Usage summary:");
			Logger.LogInformation(String.Join(Environment.NewLine, CostEstimator.SummarizeUsage().Select(t => $"{t.item,-48}\t{t.amount:N0}")));

			var costsSummary = CostEstimator.SummarizeCosts();
			if (costsSummary.Any())
			{
				Logger.LogInformation($"Estimated costs:");
				Logger.LogInformation(string.Join(Environment.NewLine, costsSummary.Select(t => $"{t.item,-48}\t{CostOptions.Currency} {t.cost:N8}")));
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, ex.Message);
		}
		finally
		{
			Logger.LogInformation("Stopping...");
			Lifetime.StopApplication();
		}
	}
}
