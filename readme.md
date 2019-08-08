# Archive photos to Azure Storage

Archive your photos and videos to [Azure Archive Storage](https://azure.microsoft.com/en-us/services/storage/archive/) with the lowest cost of �0.00084/GB/mo or �0.8/TB/mo. See detailed [pricing](https://azure.microsoft.com/en-us/pricing/details/storage/blobs/).

This app uploads and optionally encrypts your files like `IMG_20190727_123456.jpg` or `DSC_5438.NEF`, groups them by date into directories like `2019`/`07`/`27` and sets their tiers to Archive to save cost.

## Usage

Requirements:
 - [Microsoft Azure subscription](https://azure.microsoft.com/)
   - [Azure Storage Account](https://azure.microsoft.com/en-us/services/storage/) (General Purpose v2 or Blob)
   - (optional) [Azure Key Vault](https://azure.microsoft.com/en-us/services/key-vault/) for encryption
     - An RSA 2048/3072/4096-bit, Software/HSM Key in Key Vault
     - Azure Active Directory App [read the docs](https://docs.microsoft.com/en-us/azure/active-directory/develop/howto-create-service-principal-portal)
   - (optional) [Azure Cognitive Service Computer Vision](https://azure.microsoft.com/en-us/services/cognitive-services/computer-vision/) for image tagging and captions
   - (optional) [Azure Cognitive Service Face](https://azure.microsoft.com/en-us/services/cognitive-services/face-api/) for face identification
 - [.NET Core 2.2 Runtime](https://dotnet.microsoft.com/download) installed on your machine

Download [executable from Releases](https://github.com/Peter-Juhasz/AzureArchiver/releases) or clone the source code and build it yourself.

Run on Windows:
```ps
.\PhotoArchiver.exe --Upload:Path "D:\OneDrive\Camera Roll" --Storage:ConnectionString "SECRET"
```

Run on Linux/Unix:
```sh
dotnet PhotoArchiver.dll --Upload:Path="D:\OneDrive\Camera Roll" --Storage:ConnectionString="SECRET"
```

You can also save your credentials to a configuration file. See below.

## Configuration

Configuration is based on the .NET Standard library and reads from JSON file and/or command-line arguments.

So you can set the configuration in `appsettings.json`:
 - `Storage` properties of Storage Account
   - **`ConnectionString`**: the connection string for your Azure Storage
   - `Container` (default `"photos"`): the name of the container to upload files to
   - `Archive` (default `true`): archive files after upload
   - `DirectoryFormat` (default `"{0:yyyy}/{0:MM}/{0:dd}"`): format string for blob directories that blob are organized into ([see docs](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings))
 - `Upload` upload settings
   - **`Path`**: the directory to upload the files from
   - `SearchPattern` (default `"**/*"`): glob search pattern for files to upload ([see docs](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.filesystemglobbing.matcher#remarks))
   - `Delete` (default `false`): delete files after successful upload
   - `Skip` (default `0`): skip the first given number of files (by name ascending)
   - `Take` (default `null`): upload only the first given number of files (by name ascending), can be combined with `Skip`
   - `ConflictResolution` (default `"Skip"`) possible values:
     - `"Skip"`: whenever a conflict is found, log as warning and skip
     - `"KeepBoth"`: the hash of the file to be uploaded is appended to its file name, right before its extension, and gets uploaded. The already existing blob is kept and not modified.
     - `"Overwrite"`: the existing blob gets overwritten, if it is in Archive tier, deleted and then reuploaded with the same name
     - `"SnapshotAndOverwrite"`: a snapshot is taken of the existing blob and then it gets overwritten (see `"Overwrite"` option). If the blob is in Archive tier, taking a snapshot is not possible, so it is skipped and logged as a warning.
 - `KeyVault` Azure Key Vault for encryption/decryption
   - `KeyIdentifier`: the full URL of the Azure Key Vault key to use for encryption
   - `ClientId`: the Client Id of the Active Directory App used to connect to Key Vault
   - `ClientSecret`: the Client Secret of the Active Directory App used to connect to Key Vault
   - `TenantId`: the Id of the Active Directory Tenant of the AD App
 - `ComputerVision` Azure Cognitive Services Computer Vision credentials for image tagging
   - `Endpoint`: URL of the Cognitive Service account endpoint
   - `Key`: subscription key for the service
 - `Face` Azure Cognitive Services Face credentials for face identification
   - `Endpoint`: URL of the Cognitive Service account endpoint
   - `Key`: subscription key for the service
   - `ConfidenceThreshold`: confidence threshold of identification, used to judge whether one face belong to one person
 - `Costs` set the prices based on your region and redundancy for cost estimations (see [pricing](https://azure.microsoft.com/en-us/pricing/details/storage/blobs/))
   - `Currency` (default `"$"`): currency to display costs
   - `ListOrCreateContainerPricePer10000`
   - `ReadPricePer10000`
   - `WritePricePer10000`
   - `OtherPricePer10000`
   - `DataStoragePricePerGB`
   - `GRSDataTransferPricePerGB`: leave it empty if your Storage Account is not geo-replicated
   - `KeyVaultTransactionPricePer10000`: leave it empty if you don't use Key Vault
   - `ComputerVisionDescribeTransactionPricePer1000`: leave if empty if you don't use Computer Vision

For example:
```json
{
	"Storage": {
		"ConnectionString": "SECRET"
	},
	"Upload": {
		"Delete": true
	}
}
```

Or in CLI arguments:
```ps
.\PhotoArchiver.exe --Upload:Path "D:\OneDrive\Camera Roll" --Storage:Archive false --Upload:Delete true
```

## Information

Supported file types:
 - Photos
    - Any JPEG with EXIF
	- Android
    - iOS (.JPG, .HEIF, .HEIC)
	- Office Lens
	- Windows Phone
 - Videos
	- Any video with Quick Time metadata (.MOV, .MP4)
    - Any video with RIFF IDIT metadat (.AVI)
    - Android (.MP4)
    - iOS (.MOV)
    - Windows Phone (.MP4)
 - RAW (with EXIF, matching JPEG or date in file name)
	- Canon Raw Version 2 (.CR2)
	- Digital Negative (.DNG), iOS/Windows Phone RAW
	- Nikon Electric Format (.NEF)

Supported sources to upload from:
 - Local File System
 - Synced cloud storage
   - OneDrive
   - Google Drive
 - USB, CD, DVD drives

A single file is uploaded at a time.

## Disclaimer

Use at your own risk. The creator of this software takes no responsibility in moving, storing, deleting or processing your data in any form. It is your responsibility to keep your encryption keys safe. Also, cost estimations are only for information purposes, to get exact and detailed information view Azure Storage pricing page, or check your actual consumption in Azure Portal.

## Development

Requirements:
 - Visual Studio 2019 Preview
 - .NET Core SDK 2.2

Place a file named `appsettings.json` into your project, at least as a placeholder.

## Troubleshooting

 - Make sure you have a valid key for your Storage Account. You can try it in another tool like Storage Explorer.
 - Make sure your Storage Account is accessible from your network. You can check this on the Firewall tab of your Storage Account.
 - If you want to archive blobs, make sure your Storage Account is the new kind (GPv2 or Blob), so it supports blob level tiers.
 - Make sure you (and this application) have at least Read permission to the folder you want to upload.
   - If you want to delete the files as well, make sure you have Delete permission.
   - Also, that the files are not in use anywhere else.
 - If you want to encrypt using Key Vault, make sure your AD App is defined in the Key Vault access policies.
 
## Related resources
 - [Create a Storage Account](https://docs.microsoft.com/en-us/azure/storage/common/storage-quickstart-create-account?tabs=azure-portal)
 - [Manage blobs on Azure Portal](https://docs.microsoft.com/en-us/azure/storage/blobs/storage-quickstart-blobs-portal)
 - [Manage blobs using Azure Storage Explorer](https://azure.microsoft.com/en-us/features/storage-explorer/)

## Special thanks
 - [MetadataExtractor](https://github.com/drewnoakes/metadata-extractor-dotnet) library for reading metadata from videos