using Azure.Storage;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("PhotoArchiver.Tests")]

namespace PhotoArchiver;

using ComputerVision;
using Costs;
using Deduplication;
using Face;
using Progress;
using Storage;
using System.Collections.Frozen;
using Thumbnails;
using Upload;

public partial class Archiver
{
	public Archiver(
		IOptions<UploadOptions> options,
		IOptions<StorageOptions> storageOptions,
		IOptions<ThumbnailOptions> thumbnailOptions,
		IOptions<ComputerVisionOptions> computerVisionOptions,
		IOptions<FaceOptions> faceOptions,
		BlobServiceClient client,
		IThumbnailGenerator thumbnailGenerator,
		IDeduplicationService deduplicationService,
		CostEstimator costEstimator,
		IProgressIndicator progressIndicator,
		ILogger<Archiver> logger
	)
	{
		Options = options.Value;
		StorageOptions = storageOptions.Value;
		ThumbnailOptions = thumbnailOptions.Value;
		ComputerVisionOptions = computerVisionOptions.Value;
		FaceOptions = faceOptions.Value;
		Client = client;
		ThumbnailGenerator = thumbnailGenerator;
		DeduplicationService = deduplicationService;
		CostEstimator = costEstimator;
		ProgressIndicator = progressIndicator;
		Logger = logger;
	}

	protected UploadOptions Options { get; }
	protected StorageOptions StorageOptions { get; }
	protected ThumbnailOptions ThumbnailOptions { get; }
	protected ComputerVisionOptions ComputerVisionOptions { get; }
	protected FaceOptions FaceOptions { get; }
	protected BlobServiceClient Client { get; }
	protected IThumbnailGenerator ThumbnailGenerator { get; }
	protected IDeduplicationService DeduplicationService { get; }
	protected CostEstimator CostEstimator { get; }
	protected IProgressIndicator ProgressIndicator { get; }
	protected ILogger<Archiver> Logger { get; }

	protected StorageSharedKeyCredential GetStorageSharedKeyCredential()
	{
		var props = StorageOptions.ConnectionString!.Split(';').ToDictionary(p => p.Split('=')[0].Trim(), p => p.Split('=')[1].Trim());
		return new StorageSharedKeyCredential(props["AccountName"], props["AccountKey"]);
	}

	private static readonly IReadOnlyDictionary<string, string> MimeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		{ ".jpg", "image/jpeg" },
		{ ".mp4", "video/mp4" },
		{ ".nef", "image/nef" },
		{ ".dng", "image/dng" },
		{ ".mov", "video/quicktime" },
		{ ".avi", "video/x-msvideo" },
		{ ".mpg", "video/mpeg" },
	}.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

	private static readonly IReadOnlyDictionary<UploadResult, LogLevel> UploadResultLogLevelMap = new Dictionary<UploadResult, LogLevel>()
	{
		{ UploadResult.Uploaded, LogLevel.Information },
		{ UploadResult.AlreadyExists, LogLevel.Information },
		{ UploadResult.Conflict, LogLevel.Warning },
		{ UploadResult.DateMissing, LogLevel.Warning },
		{ UploadResult.Error, LogLevel.Error },
	}.ToFrozenDictionary();

	private static readonly IReadOnlySet<string> IgnoredFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"Thumbs.db", // Windows Explorer
        "desktop.ini", // Windows Explorer
        "ZbThumbnail.info", // Canon PowerShot
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

	private static readonly IReadOnlySet<string> IgnoredExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		".thumb",
		".thm", // Camera thumbnail
        ".tmp",
	}.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
