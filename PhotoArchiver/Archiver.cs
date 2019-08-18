using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.KeyVault.Core;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

[assembly: InternalsVisibleTo("PhotoArchiver.Tests")]

namespace PhotoArchiver
{
    using ComputerVision;
    using Costs;
    using Deduplication;
    using Face;
    using KeyVault;
    using Progress;
    using Storage;
    using Thumbnails;
    using Upload;

    public partial class Archiver
    {
        public Archiver(
            IOptions<UploadOptions> options,
            IOptions<StorageOptions> storageOptions,
            IOptions<ThumbnailOptions> thumbnailOptions,
            IOptions<KeyVaultOptions> keyVaultOptions,
            IOptions<ComputerVisionOptions> computerVisionOptions,
            IOptions<FaceOptions> faceOptions,
            CloudBlobClient client,
            IThumbnailGenerator thumbnailGenerator,
            IKeyResolver keyResolver,
            IDeduplicationService deduplicationService,
            IComputerVisionClient computerVisionClient,
            IFaceClient faceClient,
            CostEstimator costEstimator,
            IProgressIndicator progressIndicator,
            ILogger<Archiver> logger
        )
        {
            Options = options.Value;
            StorageOptions = storageOptions.Value;
            KeyVaultOptions = keyVaultOptions.Value;
            ThumbnailOptions = thumbnailOptions.Value;
            ComputerVisionOptions = computerVisionOptions.Value;
            FaceOptions = faceOptions.Value;
            Client = client;
            ThumbnailGenerator = thumbnailGenerator;
            KeyResolver = keyResolver;
            DeduplicationService = deduplicationService;
            ComputerVisionClient = computerVisionClient;
            FaceClient = faceClient;
            CostEstimator = costEstimator;
            ProgressIndicator = progressIndicator;
            Logger = logger;
        }

        protected UploadOptions Options { get; }
        protected StorageOptions StorageOptions { get; }
        protected KeyVaultOptions KeyVaultOptions { get; }
        protected ThumbnailOptions ThumbnailOptions { get; }
        protected ComputerVisionOptions ComputerVisionOptions { get; }
        protected FaceOptions FaceOptions { get; }
        protected CloudBlobClient Client { get; }
        protected IThumbnailGenerator ThumbnailGenerator { get; }
        protected IKeyResolver KeyResolver { get; }
        protected IDeduplicationService DeduplicationService { get; }
        protected IComputerVisionClient ComputerVisionClient { get; }
        protected IFaceClient FaceClient { get; }
        protected CostEstimator CostEstimator { get; }
        protected IProgressIndicator ProgressIndicator { get; }
        protected ILogger<Archiver> Logger { get; }

        private static readonly IReadOnlyDictionary<string, string> MimeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".jpg", "image/jpeg" },
            { ".mp4", "video/mp4" },
            { ".nef", "image/nef" },
            { ".dng", "image/dng" },
            { ".mov", "video/quicktime" },
            { ".avi", "video/x-msvideo" },
            { ".mpg", "video/mpeg" },
        };

        private static readonly IReadOnlyDictionary<UploadResult, LogLevel> UploadResultLogLevelMap = new Dictionary<UploadResult, LogLevel>()
        {
            { UploadResult.Uploaded, LogLevel.Information },
            { UploadResult.AlreadyExists, LogLevel.Information },
            { UploadResult.Conflict, LogLevel.Warning },
            { UploadResult.DateMissing, LogLevel.Warning },
            { UploadResult.Error, LogLevel.Error },
        };

        private static readonly IReadOnlyCollection<string> IgnoredFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Thumbs.db", // Windows Explorer
            "desktop.ini", // Windows Explorer
            "ZbThumbnail.info", // Canon PowerShot
        };

        private static readonly IReadOnlyCollection<string> IgnoredExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".thumb",
            ".thm", // Camera thumbnail
            ".tmp",
        };

        private IKey? _key = null;
    }
}
