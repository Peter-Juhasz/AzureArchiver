using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace PhotoArchiver.Console.Commands;

using Upload;

public static partial class Extensions
{
	public static void AddUploadCommand(this RootCommand root, IHostBuilder hostBuilder)
	{
		var command = new Command("upload", "Uploads media from a folder.");

		var pathArgument = new Argument<string>("path", "The path to the folder to upload.");
		command.AddArgument(pathArgument);
		var searchPatternOption = new Option<string>("--search-pattern", "The search pattern to use when searching for files to upload.");
		searchPatternOption.SetDefaultValue("**/*");
		command.AddOption(searchPatternOption);

		var skipOption = new Option<int>("--skip", "The number of files to skip.");
		skipOption.SetDefaultValue(0);
		command.AddOption(skipOption);

		var takeOption = new Option<int?>("--take", "The number of files to take.");
		command.AddOption(takeOption);

		var deduplicateOption = new Option<bool>("--deduplicate", "Deduplicates files before uploading.");
		deduplicateOption.SetDefaultValue(true);
		command.AddOption(deduplicateOption);

		var conflictResolutionOption = new Option<ConflictResolution>("--conflict-resolution", "The conflict resolution strategy to use.");
		conflictResolutionOption.SetDefaultValue(ConflictResolution.Skip);
		command.AddOption(conflictResolutionOption);

		var verifyOption = new Option<bool>("--verify", "Verifies the upload after completion.");
		verifyOption.SetDefaultValue(true);
		command.AddOption(verifyOption);
		var deleteOption = new Option<bool>("--delete", "Deletes the file after upload.");
		deleteOption.SetDefaultValue(false);
		command.AddOption(deleteOption);

		var accessTierOption = new Option<AccessTier>("--access-tier", "The access tier to use for the uploaded blobs.");
		accessTierOption.SetDefaultValue(AccessTier.Cool);
		command.AddOption(accessTierOption);

		var parallelBlockCountOption = new Option<int?>("--parallel-block-count", "The number of parallel blocks to upload.");
		command.AddOption(parallelBlockCountOption);

		command.SetHandler(async (InvocationContext context) =>
		{
			var cancellationToken = context.GetCancellationToken();

			// configure
			hostBuilder.ConfigureServices((services) =>
			{
				services.Configure<UploadOptions>(options =>
				{
					options.Path = context.ParseResult.GetValueForArgument(pathArgument);
					options.SearchPattern = context.ParseResult.GetValueForOption(searchPatternOption)!;
					options.Skip = context.ParseResult.GetValueForOption(skipOption);
					options.Take = context.ParseResult.GetValueForOption(takeOption);
					options.Deduplicate = context.ParseResult.GetValueForOption(deduplicateOption);
					options.ConflictResolution = context.ParseResult.GetValueForOption(conflictResolutionOption);
					options.Verify = context.ParseResult.GetValueForOption(verifyOption);
					options.Delete = context.ParseResult.GetValueForOption(deleteOption);
					options.AccessTier = context.ParseResult.GetValueForOption(accessTierOption);
					options.ParallelBlockCount = context.ParseResult.GetValueForOption(parallelBlockCountOption);
				});
			});
			using var host = hostBuilder.Build();

			// run
			var worker = host.Services.GetRequiredService<UploadWorker>();
			await worker.StartAsync(cancellationToken);
			await worker.ExecuteTask!;
		});

		root.AddCommand(command);
	}
}
