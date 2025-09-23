using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

namespace PhotoArchiver.Console.Commands;

using PhotoArchiver.Storage;
using PhotoArchiver.Thumbnails;
using Upload;

public static partial class Extensions
{
	public static void AddUploadCommand(this RootCommand root)
	{
		var command = new Command("upload", "Uploads media from a folder.");

		// upload options
		var pathArgument = new Argument<string>("path") { Description = "The path to the folder to upload." };
		command.Add(pathArgument);
		var searchPatternOption = new Option<string>("--search-pattern") { Description = "The search pattern to use when searching for files to upload." };
		searchPatternOption.DefaultValueFactory = _ => "**/*";
		command.Add(searchPatternOption);

		var containerOption = new Option<string>("--container") { Description = "The container to upload files to." };
		containerOption.DefaultValueFactory = _ => "photos";
		command.Add(containerOption);

		var skipOption = new Option<int>("--skip") { Description = "The number of files to skip." };
		skipOption.DefaultValueFactory = _ => 0;
		command.Add(skipOption);

		var takeOption = new Option<int?>("--take") { Description = "The number of files to take." };
		command.Add(takeOption);

		var deduplicateOption = new Option<bool>("--deduplicate") { Description = "Deduplicates files before uploading." };
		deduplicateOption.DefaultValueFactory = _ => true;
		command.Add(deduplicateOption);

		var conflictResolutionOption = new Option<ConflictResolution>("--conflict-resolution") { Description = "The conflict resolution strategy to use." };
		conflictResolutionOption.DefaultValueFactory = _ => ConflictResolution.Skip;
		command.Add(conflictResolutionOption);

		var verifyOption = new Option<bool>("--verify") { Description = "Verifies the upload after completion." };
		verifyOption.DefaultValueFactory = _ => true;
		command.Add(verifyOption);
		var deleteOption = new Option<bool>("--delete") { Description = "Deletes the file after upload." };
		deleteOption.DefaultValueFactory = _ => false;
		command.Add(deleteOption);

		var accessTierOption = new Option<AccessTier>("--access-tier") { Description = "The access tier to use for the uploaded blobs." };
		accessTierOption.DefaultValueFactory = _ => AccessTier.Cool;
		command.Add(accessTierOption);

		var parallelBlockCountOption = new Option<int?>("--parallel-block-count") { Description = "The number of parallel blocks to upload." };
		command.Add(parallelBlockCountOption);

		// thumbnail options
		var maxThumbnailWidth = new Option<int>("--max-thumbnail-width") { Description = "Maximum width of thumbnails generated." };
		maxThumbnailWidth.DefaultValueFactory = _ => 256;
		command.Add(maxThumbnailWidth);

		var maxThumbnailHeight = new Option<int>("--max-thumbnail-height") { Description = "Maximum height of thumbnails generated." };
		maxThumbnailHeight.DefaultValueFactory = _ => 256;
		command.Add(maxThumbnailHeight);

		var thumbnailQuality = new Option<double>("--thumbnail-quality") { Description = "Quality of thumbnails generated." };
		thumbnailQuality.DefaultValueFactory = _ => 0.50;
		command.Add(thumbnailQuality);

		var forceThumbnails = new Option<bool>("--force-thumbnails") { Description = "Forces thumbnail generation even if already uploaded." };
		forceThumbnails.DefaultValueFactory = _ => false;
		command.Add(forceThumbnails);

		var thumbnailContainer = new Option<string>("--thumbnail-container") { Description = "The container to store thumbnails in." };
		thumbnailContainer.DefaultValueFactory = _ => "photos-thumbnails";
		command.Add(thumbnailContainer);

		command.SetAction(async (result, cancellationToken) =>
		{
			// configure
			var host = result.GetHost();
			host.Services.Configure<StorageOptions>(options =>
			{
				result.Bind(containerOption, s => options.Container = s);
			});
			host.Services.Configure<UploadOptions>(options =>
			{
				options.Path = result.GetValue(pathArgument);

				result.Bind(searchPatternOption, s => options.SearchPattern = s);
				result.Bind(skipOption, i => options.Skip = i);
				result.Bind(takeOption, i => options.Take = i);
				result.Bind(deduplicateOption, b => options.Deduplicate = b);
				result.Bind(conflictResolutionOption, cr => options.ConflictResolution = cr);
				result.Bind(verifyOption, b => options.Verify = b);
				result.Bind(deleteOption, b => options.Delete = b);
				result.Bind(accessTierOption, at => options.AccessTier = at);
				result.Bind(parallelBlockCountOption, i => options.ParallelBlockCount = i);
			});
			host.Services.Configure<ThumbnailOptions>(options =>
			{
				result.Bind(maxThumbnailWidth, i => options.MaxWidth = i);
				result.Bind(maxThumbnailHeight, i => options.MaxHeight = i);
				result.Bind(thumbnailQuality, d => options.Quality = d);
				result.Bind(forceThumbnails, b => options.Force = b);
				result.Bind(thumbnailContainer, s => options.Container = s);
			});

			// run
			var worker = host.Services.GetRequiredService<UploadWorker>();
			await worker.StartAsync(cancellationToken);
			await worker.ExecuteTask!;
		});

		root.Add(command);
	}
}
