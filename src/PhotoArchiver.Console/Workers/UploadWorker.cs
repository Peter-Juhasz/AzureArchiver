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
using Files;
using Progress;
using Storage;
using Update;
using Upload;

public class UploadWorker(
	IUpdateService updateService,
	Archiver archiver,
	BlobServiceClient client,
	CostEstimator costEstimator,
	IProgressIndicator progressIndicator,
	IOptions<UploadOptions> options,
	IOptions<CostOptions> costOptions,
	IOptions<UpdateOptions> updateOptions,
	IOptions<StorageOptions> storageOptions,
	ILogger<UploadWorker> logger,
	IHostApplicationLifetime lifetime
) : BackgroundService
{
	private UploadOptions UploadOptions { get; } = options.Value;
	private CostOptions CostOptions { get; } = costOptions.Value;
	private UpdateOptions UpdateOptions { get; } = updateOptions.Value;
	private StorageOptions StorageOptions { get; } = storageOptions.Value;

	protected override async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		try
		{
			// check for updates
			if (UpdateOptions.Enabled && await updateService.CheckForUpdatesAsync(cancellationToken))
			{
				logger.LogWarning($"A new version is available. You can download it from {UpdateOptions.Home}");

				if (UpdateOptions.Stop)
				{
					return;
				}
			}

			// create container if not exists
			logger.LogTrace("Ensure container exists...");
			await client.GetBlobContainerClient(StorageOptions.Container).CreateIfNotExistsAsync(cancellationToken: cancellationToken);

			var watch = Stopwatch.StartNew();

			// upload
			var result = await archiver.ArchiveAsync(new SystemIODirectory(UploadOptions.Path), progressIndicator, default);
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

			// usage summary
			logger.LogInformation($"Usage summary:");
			logger.LogInformation(String.Join(Environment.NewLine, costEstimator.SummarizeUsage().Select(t => $"{t.item,-48}\t{t.amount:N0}")));

			var costsSummary = costEstimator.SummarizeCosts();
			if (costsSummary.Any())
			{
				logger.LogInformation($"Estimated costs:");
				logger.LogInformation(string.Join(Environment.NewLine, costsSummary.Select(t => $"{t.item,-48}\t{CostOptions.Currency} {t.cost:N8}")));
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, ex.Message);
		}
		finally
		{
			logger.LogInformation("Stopping...");
			lifetime.StopApplication();
		}
	}
}
