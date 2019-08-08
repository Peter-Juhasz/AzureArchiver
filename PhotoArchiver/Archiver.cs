using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.KeyVault.Core;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

using MetadataExtractor.Formats.QuickTime;
using MetadataExtractor;

namespace PhotoArchiver
{
    using ComputerVision;
    using Costs;
    using Deduplication;
    using Extensions;
    using Face;
    using Formats;
    using KeyVault;
    using Storage;

    public class Archiver
    {
        public Archiver(
            IOptions<UploadOptions> options,
            IOptions<StorageOptions> storageOptions,
            IOptions<KeyVaultOptions> keyVaultOptions,
            IOptions<ComputerVisionOptions> computerVisionOptions,
            IOptions<FaceOptions> faceOptions,
            CloudBlobClient client,
            IKeyResolver keyResolver,
            IDeduplicationService deduplicationService,
            IComputerVisionClient computerVisionClient,
            IFaceClient faceClient,
            CostEstimator costEstimator,
            ILogger<Archiver> logger
        )
        {
            Options = options.Value;
            StorageOptions = storageOptions.Value;
            KeyVaultOptions = keyVaultOptions.Value;
            ComputerVisionOptions = computerVisionOptions;
            FaceOptions = faceOptions;
            Client = client;
            KeyResolver = keyResolver;
            DeduplicationService = deduplicationService;
            ComputerVisionClient = computerVisionClient;
            FaceClient = faceClient;
            CostEstimator = costEstimator;
            Logger = logger;
        }

        protected UploadOptions Options { get; }
        protected StorageOptions StorageOptions { get; }
        protected KeyVaultOptions KeyVaultOptions { get; }
        protected IOptions<ComputerVisionOptions> ComputerVisionOptions { get; }
        protected IOptions<FaceOptions> FaceOptions { get; }
        protected CloudBlobClient Client { get; }
        protected IKeyResolver KeyResolver { get; }
        protected IDeduplicationService DeduplicationService { get; }
        protected IComputerVisionClient ComputerVisionClient { get; }
        protected IFaceClient FaceClient { get; }
        protected CostEstimator CostEstimator { get; }
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

        public async Task<ArchiveResult> ArchiveAsync(string path, CancellationToken cancellationToken)
        {
            // initialize
            var directory = new DirectoryInfo(path);
            var container = Client.GetContainerReference(StorageOptions.Container);
            var lastDirectoryName = null as string;

            var results = new List<FileUploadResult>();

            if (KeyVaultOptions.IsEnabled())
            {
                _key = await KeyResolver.ResolveKeyAsync(KeyVaultOptions.KeyIdentifier.ToString(), cancellationToken);
            }

            // set up filter
            var matcher = new Matcher().AddInclude(Options.SearchPattern);

            var query = matcher.GetResultsInFullPath(directory.FullName)
                .OrderBy(f => f)
                .Select(f => new FileInfo(f))
                .Where(f => !IgnoredFileNames.Contains(f.Name))
                .Where(f => !IgnoredExtensions.Contains(f.Extension));

            if (Options.Skip != 0)
            {
                query = query.Skip(Options.Skip);
            }

            if (Options.Take != null)
            {
                query = query.Take(Options.Take.Value);
            }

            // estimate count
            var processedCount = 0;
            var count = query.Count();

            // enumerate files in directory
            foreach (var file in query)
            {
                // display directory
                var currentDirectoryName = file.Directory.FullName;
                if (lastDirectoryName != currentDirectoryName)
                {
                    Logger.LogInformation($"Processing directory '{currentDirectoryName}'");
                    lastDirectoryName = currentDirectoryName;
                }

                UploadResult result = default;
                IDictionary<string, string> metadata = new Dictionary<string, string>();
                byte[]? hash = null;

                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    Logger.LogTrace($"Processing {file}...");

                    // read date
                    var date = await GetDateAsync(file);
                    if (date == null)
                    {
                        result = UploadResult.DateMissing;
                        results.Add(new FileUploadResult(file, result));
                        Logger.Log(UploadResultLogLevelMap[result], $"{result}\t{file.Name}");
                        processedCount++;
                        continue;
                    }

                    // blob
                    var blobDirectory = container.GetDirectoryReference(String.Format(StorageOptions.DirectoryFormat, date));
                    metadata.Add("OriginalFileName", file.FullName.RemoveDiacritics());
                    metadata.Add("CreatedAt", date.Value.ToString("o"));
                    metadata.Add("OriginalFileSize", file.Length.ToString());

                    // deduplicate
                    if (Options.Deduplicate)
                    {
                        using var hashAlgorithm = MD5.Create();
                        using var stream = file.OpenRead();
                        hash = hashAlgorithm.ComputeHash(stream);
                        metadata.Add("OriginalMD5", Convert.ToBase64String(hash));

                        if (await DeduplicationService.ContainsAsync(blobDirectory, hash))
                        {
                            result = UploadResult.AlreadyExists;
                        }
                    }

                    if (file.IsCognitiveServiceCompatible())
                    {
                        // computer vision
                        if (ComputerVisionOptions.Value.IsEnabled())
                        {
                            using var stream = file.OpenRead();
                            try
                            {
                                var description = await ComputerVisionClient.DescribeImageInStreamAsync(stream);
                                CostEstimator.AddDescribe();
                                if (description.Captions.Any())
                                {
                                    metadata.Add("Caption", description.Captions.OrderByDescending(c => c.Confidence).First().Text);
                                }
                                if (description.Tags.Any())
                                {
                                    metadata.Add("Tags", String.Join(", ", description.Tags));
                                }
                            }
                            catch (ComputerVisionErrorException ex)
                            {
                                Logger.LogWarning(ex, ex.Message);
                            }
                        }

                        // face
                        if (FaceOptions.Value.IsEnabled())
                        {
                            using var stream = file.OpenRead();
                            try
                            {
                                var faceResult = await FaceClient.Face.DetectWithStreamAsync(stream, returnFaceId: true);
                                CostEstimator.AddFace();
                                var faceIds = faceResult.Where(f => f.FaceId != null).Select(f => f.FaceId.Value).ToList();
                                var identifyResult = await FaceClient.Face.IdentifyAsync(faceIds, personGroupId: FaceOptions.Value.PersonGroupId, confidenceThreshold: FaceOptions.Value.ConfidenceThreshold);
                                CostEstimator.AddFace();

                                if (identifyResult.Any())
                                {
                                    metadata.Add("People", String.Join(", ", identifyResult.Select(r => r.Candidates.OrderByDescending(c => c.Confidence).First()).Select(c => c.PersonId)));
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogWarning(ex, ex.Message);
                            }
                        }
                    }

                    if (result != UploadResult.AlreadyExists)
                    {
                        var blob = blobDirectory.GetBlockBlobReference(file.Name);

                        // add metadata
                        AddMetadata(blob, metadata);

                        // set metadata
                        if (!KeyVaultOptions.IsEnabled() && MimeTypes.TryGetValue(file.Extension, out var mimeType))
                        {
                            blob.Properties.ContentType = mimeType;
                        }

                        // check for extistance
                        switch (await ExistsAndCompareAsync(blob, file))
                        {
                            // upload, if not exists
                            case null:
                                result = await UploadCoreAsync(blob, file);
                                break;

                            // already exists, if matches
                            case true:
                                result = UploadResult.AlreadyExists;
                                break;

                            // exists, but does not match
                            case false:
                                switch (Options.ConflictResolution)
                                {
                                    case ConflictResolution.KeepBoth:
                                        {
                                            // compute hash for new file name
                                            if (hash == null)
                                            {
                                                using var hashAlgorithm = MD5.Create();
                                                using var stream = file.OpenRead();
                                                hash = hashAlgorithm.ComputeHash(stream);
                                                metadata.TryAdd("OriginalMD5", Convert.ToBase64String(hash));
                                            }
                                            var formattedHash = BitConverter.ToString(hash).Replace("-", String.Empty);

                                            blob = blobDirectory.GetBlockBlobReference(Path.ChangeExtension(file.Name, "." + formattedHash + file.Extension));
                                            AddMetadata(blob, metadata);

                                            // upload with new name
                                            switch (await ExistsAndCompareAsync(blob, file))
                                            {
                                                case null:
                                                    result = await UploadCoreAsync(blob, file);
                                                    break;

                                                case false:
                                                    result = UploadResult.Error;
                                                    break;

                                                case true:
                                                    result = UploadResult.AlreadyExists;
                                                    break;
                                            }
                                        }
                                        break;

                                    case ConflictResolution.SnapshotAndOverwrite:
                                        if (blob.Properties.StandardBlobTier == StandardBlobTier.Archive)
                                        {
                                            result = UploadResult.Error;
                                            break;
                                        }

                                        await blob.CreateSnapshotAsync(cancellationToken);
                                        result = await UploadCoreAsync(blob, file);
                                        break;

                                    case ConflictResolution.Overwrite:
                                        if (blob.Properties.StandardBlobTier == StandardBlobTier.Archive)
                                        {
                                            await blob.DeleteAsync(cancellationToken);
                                        }

                                        result = await UploadCoreAsync(blob, file);
                                        break;

                                    default:
                                    case ConflictResolution.Skip:
                                        result = UploadResult.Conflict;
                                        break;
                                }
                                break;
                        }

                        if (result.IsSuccessful())
                        {
                            // archive
                            if (StorageOptions.Archive && blob.Properties.StandardBlobTier != StandardBlobTier.Archive)
                            {
                                Logger.LogTrace($"Archiving {blob}...");
                                await blob.SetStandardBlobTierAsync(StandardBlobTier.Archive);
                                CostEstimator.AddWrite();
                            }

                            // deduplicate
                            if (Options.Deduplicate)
                            {
                                DeduplicationService.Add(blobDirectory, hash);
                            }
                        }
                    }

                    // delete
                    if (result.IsSuccessful())
                    {
                        if (Options.Delete)
                        {
                            Logger.LogTrace($"Deleting {file}...");
                            file.Delete();
                        }
                    }

                    // log
                    results.Add(new FileUploadResult(file, result));
                    Logger.Log(UploadResultLogLevelMap[result], $"{result}\t{file.Name}\t({processedCount + 1} of {count})");
                }
                catch (Exception ex)
                {
                    result = UploadResult.Error;
                    Logger.LogError(ex, $"Failed to process {file}");
                    results.Add(new FileUploadResult(file, result, ex));
                }
                finally
                {
                    processedCount++;
                }
            }

            return new ArchiveResult(results);
        }

        private static void AddMetadata(CloudBlockBlob blob, IDictionary<string, string> metadata)
        {
            foreach (var kv in metadata)
            {
                blob.Metadata.Add(kv.Key, kv.Value);
            }
        }

        private async Task<bool?> ExistsAndCompareAsync(CloudBlockBlob blob, FileInfo file)
        {
            // check for exists
            Logger.LogTrace($"Checking for {blob} exists...");
            CostEstimator.AddOther();
            if (await blob.ExistsAsync())
            {
                Logger.LogTrace($"Fetching attributes for {blob}...");
                await blob.FetchAttributesAsync();
                CostEstimator.AddOther();

                // compare file size
                if (blob.Properties.Length != file.Length)
                {
                    return false;
                }

                // compare hash
                Logger.LogTrace($"Computing MD5 hash for {file}...");
                var reference = Convert.FromBase64String(blob.Properties.ContentMD5);
                using var alg = MD5.Create();
                using var stream = file.OpenRead();
                var hash = alg.ComputeHash(stream);

                if (!reference.AsSpan().SequenceEqual(hash.AsSpan()))
                {
                    return false;
                }

                return true;
            }

            return null;
        }

        private async Task<UploadResult> UploadCoreAsync(CloudBlockBlob blob, FileInfo file)
        {
            // upload
            Logger.LogTrace($"Uploading {file} to {blob}...");

            var requestOptions = new BlobRequestOptions
            {
                StoreBlobContentMD5 = true,
            };
            if (KeyVaultOptions.IsEnabled())
            {
                requestOptions.EncryptionPolicy = new BlobEncryptionPolicy(_key, null);
            }

            await blob.UploadFromFileAsync(file.FullName, AccessCondition.GenerateEmptyCondition(), requestOptions, null);
            CostEstimator.AddWrite(file.Length);

            return UploadResult.Uploaded;
        }

        private async Task<DateTime?> GetDateAsync(FileInfo file)
        {
            Logger.LogTrace($"Reading date for {file}...");

            switch (file.Extension.ToLower())
            {
                case ".jpg":
                case ".jpeg":
                case ".jfif":
                case ".heif":
                case ".heic":
                    {
                        using var stream = file.OpenRead();
                        var metadata = ImageMetadataReader.ReadMetadata(stream);
                        var tag = metadata.SelectMany(d => d.Tags).FirstOrDefault(t => t.Name == "Date/Time Original" || t.Name == "Date/Time");
                        if (tag != null)
                        {
                            return ParseExifDateTime(tag.Description);
                        }
                    }
                    break;

                case ".cr2":
                case ".nef":
                case ".dng":
                    {
                        using var stream = file.OpenRead();
                        var metadata = ImageMetadataReader.ReadMetadata(stream);
                        var tag = metadata.SelectMany(d => d.Tags).FirstOrDefault(t => t.Name == "Date/Time Original" || t.Name == "Date/Time");
                        if (tag != null)
                        {
                            return ParseExifDateTime(tag.Description);
                        }

                        // fallback to JPEG
                        var jpeg = new FileInfo(Path.ChangeExtension(file.FullName, ".jpg"));
                        if (jpeg.Exists)
                            return await GetDateAsync(jpeg);

                        if (file.Extension.Equals(".dng", StringComparison.OrdinalIgnoreCase))
                        {
                            jpeg = new FileInfo(Path.ChangeExtension(file.FullName, ".jpg").Replace("__highres", ""));
                            if (jpeg.Exists)
                                return await GetDateAsync(jpeg);
                        }
                    }
                    break;

                case ".mp4":
                case ".mov":
                    {
                        using var stream = file.OpenRead();
                        var metadata = QuickTimeMetadataReader.ReadMetadata(stream);
                        var tag = metadata.SelectMany(d => d.Tags).FirstOrDefault(t => t.Name == "Created");
                        if (tag != null)
                        {
                            if (DateTime.TryParseExact(tag.Description, WellKnownDateTimeFormats.QuickTime, CultureInfo.CurrentCulture, default, out var result))
                                return result;

                            return DateTime.ParseExact(tag.Description, WellKnownDateTimeFormats.QuickTime, WellKnownCultures.EnglishUnitedStates);
                        }
                    }
                    break;

                case ".avi":
                    {
                        using var stream = file.OpenRead();
                        var date = await AviDateReader.ReadAsync(stream);
                        if (date != null)
                        {
                            return date;
                        }
                    }
                    break;

                case ".mpg":
                    {
                        if (TryParseDate(file.Name, out var date))
                            return date;
                    }
                    break;
            }

            if (TryParseDate(file.Name, out var dt))
                return dt;

            return null;
        }

        internal static bool TryParseDate(string fileName, out DateTime result)
        {
            // IMG_20190525_120904
            // VID_20181226_163237
            if (fileName.Length >= 19 + 4 && (fileName.StartsWith("IMG_2") || fileName.StartsWith("VID_2")) && fileName[12] == '_')
            {
                result = new DateTime(Int32.Parse(fileName.Substring(4, 4)), Int32.Parse(fileName.Substring(8, 2)), Int32.Parse(fileName.Substring(10, 2)));
                return true;
            }

            // WP_20140711_15_25_11_0_Pro
            if (fileName.Length >= 11 + 4 && fileName.StartsWith("WP_") && fileName[11] == '_')
            {
                result = new DateTime(Int32.Parse(fileName.Substring(3, 4)), Int32.Parse(fileName.Substring(7, 2)), Int32.Parse(fileName.Substring(9, 2)));
                return true;
            }

            // 2018_07_01 18_41 Office Lens
            if (fileName.Length >= 25 + 4 && fileName.EndsWith(" Office Lens.jpg") && fileName.Take(4).All(Char.IsDigit) && fileName.StartsWith('2'))
            {
                result = new DateTime(Int32.Parse(fileName.Substring(0, 4)), Int32.Parse(fileName.Substring(5, 2)), Int32.Parse(fileName.Substring(8, 2)));
                return true;
            }

            // 5_25_18 11_39 Office Lens
            if (fileName.Length >= 25 + 4 && fileName.EndsWith(" Office Lens.jpg") && !fileName.Take(4).All(Char.IsDigit))
            {
                var split = fileName.Split('_', ' ');
                result = new DateTime(Int32.Parse(split[2]) + 2000, Int32.Parse(split[0]), Int32.Parse(split[1]));
                return true;
            }

            // Office Lens_20140919_110252
            if (fileName.Length >= 25 + 4 && fileName.StartsWith("Office Lens_2"))
            {
                result = new DateTime(Int32.Parse(fileName.Substring(12, 4)), Int32.Parse(fileName.Substring(16, 2)), Int32.Parse(fileName.Substring(18, 2)));
                return true;
            }

            result = default;
            return false;
        }

        internal static DateTime ParseExifDateTime(string exifValue) =>
            DateTime.Parse(
                exifValue
                    .Remove(4, 1).Insert(4, ".")
                    .Remove(7, 1).Insert(7, ".")
                    .Insert(10, ".")
            );
    }
}
