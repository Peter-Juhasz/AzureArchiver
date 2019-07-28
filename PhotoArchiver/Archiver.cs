using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MetadataExtractor.Formats.QuickTime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.MetaData.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using MetadataExtractor;
using Microsoft.Extensions.FileProviders;

namespace PhotoArchiver
{
    public class Archiver
    {
        public Archiver(
            IOptions<Options> options,
            CloudBlobClient client,
            ILogger<Archiver> logger
        )
        {
            Options = options.Value;
            Client = client;
            Logger = logger;
        }

        protected Options Options { get; }
        protected CloudBlobClient Client { get; }
        protected ILogger<Archiver> Logger { get; }

        private static readonly IReadOnlyDictionary<string, string> MimeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".jpg", "image/jpeg" },
            { ".mp4", "video/mp4" },
            { ".nef", "image/nef" },
            { ".dng", "image/dng" },
        };

        private static readonly IReadOnlyDictionary<UploadResult, LogLevel> UploadResultLogLevelMap = new Dictionary<UploadResult, LogLevel>()
        {
            { UploadResult.AlreadyExists, LogLevel.Information },
            { UploadResult.DateMissing, LogLevel.Warning },
            { UploadResult.Error, LogLevel.Error },
            { UploadResult.FileHashMismatch, LogLevel.Warning },
            { UploadResult.FileSizeMismatch, LogLevel.Warning },
            { UploadResult.Uploaded, LogLevel.Information },
        };

        public async Task<ArchiveResult> ArchiveAsync(string path)
        {
            var directory = new DirectoryInfo(path);
            var container = Client.GetContainerReference(Options.Container);

            var results = new List<FileUploadResult>();

            // enumerate files in directory
            foreach (var file in directory.GetFiles("*", Options.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Where(f => f.Name != "Thumbs.db")
            )
            {
                try
                {
                    Logger.LogTrace($"Processing {file}...");

                    // read date
                    var date = await GetDateAsync(file);
                    if (date == null)
                    {
                        results.Add(new FileUploadResult(file, UploadResult.DateMissing));
                        Logger.Log(UploadResultLogLevelMap[UploadResult.DateMissing], $"{UploadResult.DateMissing}\t{file.Name}");
                        continue;
                    }

                    var blobDirectory = container
                        .GetDirectoryReference(date.Value.Year.ToString())
                        .GetDirectoryReference(date.Value.Month.ToString().PadLeft(2, '0'))
                        .GetDirectoryReference(date.Value.Day.ToString().PadLeft(2, '0'));
                    var blob = blobDirectory.GetBlockBlobReference(file.Name);

                    // set metadata
                    if (MimeTypes.TryGetValue(file.Extension, out var mimeType))
                        blob.Properties.ContentType = mimeType;

                    // upload
                    var result = await UploadCoreAsync(blob, file);
                    results.Add(new FileUploadResult(file, result));

                    // log
                    Logger.Log(UploadResultLogLevelMap[result], $"{result}\t{file.Name}");
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"Failed to process {file}");
                    results.Add(new FileUploadResult(file, UploadResult.Error, ex));
                }
            }

            return new ArchiveResult(results);
        }

        private async Task<UploadResult> UploadCoreAsync(CloudBlockBlob blob, FileInfo file)
        {
            // check for exists
            Logger.LogTrace($"Checking for {file} exists...");
            if (await blob.ExistsAsync())
            {
                Logger.LogTrace($"Fetching attributes for {blob}...");
                await blob.FetchAttributesAsync();

                // compare file size
                if (blob.Properties.Length != file.Length)
                {
                    return UploadResult.FileSizeMismatch;
                }

                // compare hash
                Logger.LogTrace($"Computing MD5 hash for {file}...");
                var reference = Convert.FromBase64String(blob.Properties.ContentMD5);
                using var alg = MD5.Create();
                using var stream = file.OpenRead();
                var hash = alg.ComputeHash(stream);

                if (!reference.AsSpan().SequenceEqual(hash.AsSpan()))
                {
                    return UploadResult.FileHashMismatch;
                }

                // archive, if not archived yet
                if (Options.Archive && blob.Properties.StandardBlobTier != StandardBlobTier.Archive)
                {
                    Logger.LogTrace($"Archiving {blob}...");
                    await blob.SetStandardBlobTierAsync(StandardBlobTier.Archive);
                }

                if (Options.Delete)
                {
                    Logger.LogTrace($"Deleting {file}...");
                    file.Delete();
                }

                return UploadResult.AlreadyExists;
            }

            // upload
            Logger.LogTrace($"Uploading {file}...");
            await blob.UploadFromFileAsync(file.FullName);

            // archive
            if (Options.Archive)
            {
                Logger.LogTrace($"Archiving {blob}...");
                await blob.SetStandardBlobTierAsync(StandardBlobTier.Archive);
            }

            // delete
            if (Options.Delete)
            {
                Logger.LogTrace($"Deleting {file}...");
                file.Delete();
            }

            return UploadResult.Uploaded;
        }

        private async Task<DateTime?> GetDateAsync(FileInfo file)
        {
            Logger.LogTrace($"Reading date for {file}...");

            switch (file.Extension.ToLower())
            {
                case ".jpg":
                case ".jpeg":
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
                            return DateTime.ParseExact(tag.Description, "ddd MMM dd HH:mm:ss yyyy", new CultureInfo("en-us"));
                        }
                    }
                    break;

                case ".mpg":
                    {
                        if (TryParseDate(file.Name, out var date))
                            return date;
                    }
                    break;

                case ".thm":
                    var mpg = new FileInfo(Path.ChangeExtension(file.FullName, ".mpg"));
                    if (mpg.Exists)
                        return await GetDateAsync(mpg);
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
