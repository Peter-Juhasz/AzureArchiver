using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace PhotoArchiver.Windows.ViewModels
{
    public class ItemViewModel : INotifyPropertyChanged
    {
        public BlobContainerClient BlobContainer { get; set; }

        public BlobItem Blob { get; set; }

        public BlockBlobClient BlobClient => BlobContainer.GetBlockBlobClient(Blob.Name);

        public BlobProperties BlobProperties { get; set; }

        public string Name { get; set; }

        public DateTime Date { get; set; }

        public ImageSource ThumbnailSource { get; set; }

        public BlobContainerClient ThumbnailBlobContainer { get; set; }

        public BlobItem ThumbnailBlob { get; set; }

        public BlockBlobClient ThumbnailBlobClient => ThumbnailBlobContainer.GetBlockBlobClient(ThumbnailBlob.Name);

        public event PropertyChangedEventHandler PropertyChanged;

        public bool HasThumbnail => ThumbnailBlob != null;

        public Symbol Symbol
        {
            get
            {
                switch (Path.GetExtension(Name).ToUpperInvariant())
                {
                    case ".MP4":
                    case ".MOV":
                    case ".AVI":
                    case ".MPG":
                        return Symbol.Video;

                    case ".JPG":
                    case ".JPEG":
                    case ".JFIF":
                    case ".HEIF":
                    case ".HEIC":
                    case ".CR2":
                    case ".NEF":
                    case ".DNG":
                        return Symbol.Camera;

                    default:
                        return Symbol.Document;
                }
            }
        }

        public void RaiseChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Blob)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StateText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAvailable)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRehydrating)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanRehydrate)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanQueue)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StartedRehydrateHoursAgo)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RehydrationRemainingHours)));
        }

        private bool TryGetMetadata(string key, out string value)
        {
            if (BlobProperties != null && BlobProperties.Metadata.TryGetValue(key, out value))
            {
                return true;
            }

            if (Blob.Metadata.TryGetValue(key, out value))
            {
                return true;
            }
            
            if (ThumbnailBlob?.Metadata.TryGetValue(key, out value) ?? false)
            {
                return true;
            }

            value = null;
            return false;
        }


        public long Size
        {
            get
            {
                if (TryGetMetadata("OriginalFileSize", out var fileSize))
                {
                    return Int64.Parse(fileSize, CultureInfo.InvariantCulture);
                }

                return Blob.Properties.ContentLength ?? 0;
            }
        }

        public string SizeText
        {
            get
            {
                if (TryGetMetadata("OriginalFileSize", out var fileSize))
                {
                    return BytesToString(Int64.Parse(fileSize, CultureInfo.InvariantCulture));
                }

                return BytesToString(Blob.Properties.ContentLength ?? 0);
            }
        }

        public string Tags
        {
            get
            {
                if (TryGetMetadata("Tags", out var fileSize))
                {
                    return fileSize;
                }

                return null;
            }
        }

        public string Caption
        {
            get
            {
                if (TryGetMetadata("Caption", out var fileSize))
                {
                    return fileSize;
                }

                return null;
            }
        }

        public string OriginalFileName
        {
            get
            {
                if (TryGetMetadata("OriginalFileName", out var fileSize))
                {
                    return fileSize;
                }

                return null;
            }
        }

        public DateTime? CreatedAt
        {
            get
            {
                if (TryGetMetadata("CreatedAt", out var fileSize))
                {
                    return DateTimeOffset.Parse(fileSize).LocalDateTime;
                }

                return null;
            }
        }

        public string Md5 => Blob.Properties.ContentHash is byte[] hash ? ToHexString(hash) : String.Empty;


        public bool IsRehydrating => (BlobProperties?.ArchiveStatus ?? Blob.Properties.ArchiveStatus?.ToString()) != null;

        public bool IsAvailable => (BlobProperties?.AccessTier ?? Blob.Properties.AccessTier?.ToString()) != AccessTier.Archive.ToString();

        public bool CanRehydrate => !IsAvailable && !IsRehydrating;

        public bool CanQueue => !IsAvailable;

        public int? StartedRehydrateHoursAgo
        {
            get
            {
                if (!IsRehydrating)
                {
                    return null;
                }

                return (int)Math.Floor((DateTimeOffset.Now - (BlobProperties?.AccessTierChangedOn ?? Blob.Properties.AccessTierChangedOn.Value)).TotalHours);
            }
        }

        public int? RehydrationRemainingHours
        {
            get
            {
                if (!IsRehydrating)
                {
                    return null;
                }

                return (int)Math.Ceiling((TimeSpan.FromHours(15) - (DateTimeOffset.Now - (BlobProperties?.AccessTierChangedOn ?? Blob.Properties.AccessTierChangedOn.Value))).TotalHours);
            }
        }
        
        public string StateText
        {
            get
            {
                if (IsRehydrating) return "Rehydrating";
                if (IsAvailable) return "Downloadable";
                return "Archived";
            }
        }


        public static String BytesToString(long byteCount)
        {
            string[] suf = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB" };
            if (byteCount == 0)
                return "0 " + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = System.Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + " " + suf[place];
        }

        public static string ToHexString(byte[] source) =>
            BitConverter.ToString(source).Replace("-", "");
    }
}
