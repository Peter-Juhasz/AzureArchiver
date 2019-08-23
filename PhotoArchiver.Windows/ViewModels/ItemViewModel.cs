using Microsoft.Azure.Storage.Blob;
using PhotoArchiver.Extensions;
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
        public CloudBlockBlob Blob { get; set; }

        public string Name { get; set; }

        public DateTime Date { get; set; }

        public ImageSource ThumbnailSource { get; set; }

        public CloudBlockBlob ThumbnailBlob { get; set; }

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

                return Blob.Properties.Length;
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

                return BytesToString(Blob.Properties.Length);
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

        public bool IsEncrypted => Blob.IsEncrypted();

        public string Md5 => ToHexString(Blob.GetPlainMd5());


        public bool IsRehydrating => Blob.Properties.RehydrationStatus == RehydrationStatus.PendingToHot || Blob.Properties.RehydrationStatus == RehydrationStatus.PendingToCool;

        public bool IsAvailable => Blob.Properties.StandardBlobTier != StandardBlobTier.Archive;

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

                return (int)Math.Floor((DateTimeOffset.Now - Blob.Properties.BlobTierLastModifiedTime.Value).TotalHours);
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

                return (int)Math.Ceiling((TimeSpan.FromHours(15) - (DateTimeOffset.Now - Blob.Properties.BlobTierLastModifiedTime.Value)).TotalHours);
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
