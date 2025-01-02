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
using Update;

public class DownloadWorker(
	IUpdateService updateService,
	Archiver archiver,
	CostEstimator costEstimator,
	IProgressIndicator progressIndicator,
	IOptions<CostOptions> costOptions,
	IOptions<UpdateOptions> updateOptions,
	IOptions<DownloadOptions> downloadOptions,
	ILogger<DownloadWorker> logger,
	IHostApplicationLifetime lifetime
) : BackgroundService
{
	private CostOptions CostOptions { get; } = costOptions.Value;
	private UpdateOptions UpdateOptions { get; } = updateOptions.Value;
	private DownloadOptions DownloadOptions { get; } = downloadOptions.Value;

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

			var watch = Stopwatch.StartNew();

			// download
			var result = await archiver.RetrieveAsync(new SystemIODirectory(DownloadOptions.Path!), DownloadOptions, progressIndicator, default);
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
