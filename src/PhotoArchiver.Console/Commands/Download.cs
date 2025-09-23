using Microsoft.Extensions.DependencyInjection;
using System;
using System.CommandLine;

namespace PhotoArchiver.Console.Commands;

using Download;
using PhotoArchiver.Storage;

public static partial class Extensions
{
	public static void AddDownloadCommand(this RootCommand root)
	{
		var command = new Command("download", "Downloads media from cloud storage to a folder.");

		var dateArgument = new Argument<DateTime>("date") { Description = "The date to download media for." };
		command.Add(dateArgument);
		var pathArgument = new Argument<string>("path") { Description = "The path of the destination folder." };
		command.Add(pathArgument);

		var containerOption = new Option<string>("--container") { Description = "The container to download files from." };
		containerOption.DefaultValueFactory = _ => "photos";
		command.Add(containerOption);

		var verifyOption = new Option<bool>("--verify") { Description = "Verifies the upload after completion." };
		verifyOption.DefaultValueFactory = _ => true;
		command.Add(verifyOption);
		var archiveOption = new Option<bool>("--archive") { Description = "Archives the file after download." };
		archiveOption.DefaultValueFactory = _ => false;
		command.Add(archiveOption);

		command.SetAction(async (result, cancellationToken) =>
		{
			// configure
			var host = result.GetHost();
			host.Services.Configure<StorageOptions>(options =>
			{
				result.Bind(containerOption, s => options.Container = s);
			});
			host.Services.Configure<DownloadOptions>(options =>
			{
				options.Date = result.GetValue(dateArgument);
				options.Path = result.GetValue(pathArgument);

				result.Bind(verifyOption, s => options.Verify = s);
				result.Bind(archiveOption, s => options.Archive = s);
			});

			// run
			var worker = host.Services.GetRequiredService<DownloadWorker>();
			await worker.StartAsync(cancellationToken);
			await worker.ExecuteTask!;
		});

		root.Add(command);
	}
}
