using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace PhotoArchiver.Console.Commands;

using Download;

public static partial class Extensions
{
	public static void AddDownloadCommand(this RootCommand root, IHostBuilder hostBuilder)
	{
		var command = new Command("download", "Downloads media from cloud storage to a folder.");

		var dateArgument = new Argument<DateTime>("date", "The date to download media for.");
		command.AddArgument(dateArgument);
		var pathArgument = new Argument<string>("path", "The path of the destination folder.");
		command.AddArgument(pathArgument);

		var verifyOption = new Option<bool>("--verify", "Verifies the upload after completion.");
		verifyOption.SetDefaultValue(true);
		command.AddOption(verifyOption);
		var archiveOption = new Option<bool>("--archive", "Archives the file after download.");
		archiveOption.SetDefaultValue(false);
		command.AddOption(archiveOption);

		command.SetHandler(async (InvocationContext context) =>
		{
			var cancellationToken = context.GetCancellationToken();

			// configure
			hostBuilder.ConfigureServices((services) =>
			{
				services.Configure<DownloadOptions>(options =>
				{
					options.Date = context.ParseResult.GetValueForArgument(dateArgument);
					options.Path = context.ParseResult.GetValueForArgument(pathArgument);
					options.Verify = context.ParseResult.GetValueForOption(verifyOption);
					options.Archive = context.ParseResult.GetValueForOption(archiveOption);
				});
			});
			using var host = hostBuilder.Build();

			// run
			var worker = host.Services.GetRequiredService<DownloadWorker>();
			await worker.StartAsync(cancellationToken);
			await worker.ExecuteTask!;
		});

		root.AddCommand(command);
	}
}
