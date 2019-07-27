# Archive photos to Azure Storage

Archive your RAW photos and videos to [Azure Archive Storage](https://azure.microsoft.com/en-us/services/storage/archive/) with the lowest cost of €0.00084/GB/mo or €0.8/TB/mo. See detailed [pricing](https://azure.microsoft.com/en-us/pricing/details/storage/blobs/).

This app uploads your files like `IMG_20190727_123456.jpg` to a container `photos` and groups them into directories like `2019`/`07`/`27`.

## Usage

Requirements:
 - [Microsoft Azure subscription](https://azure.microsoft.com/)
   - [Azure Storage Account](https://azure.microsoft.com/en-us/services/storage/) (General Purpose v2 or Blob)
 - [.NET Core 2.2 Runtime](https://dotnet.microsoft.com/download) installed on your machine

Download [executable from Releases](https://github.com/Peter-Juhasz/AzureArchiver/releases) or clone the source code and build it yourself.

Run on Windows:
```ps
.\PhotoArchiver.exe --Path "D:\OneDrive\Camera Roll" --ConnectionString "SECRET"
```

Run on Linux:
```sh
dotnet PhotoArchiver.dll --Path "D:\OneDrive\Camera Roll" --ConnectionString "SECRET"
```

You can also save your connection string to a configuration file. See below.

## Configuration

Configuration is based on the .NET Standard library and reads from JSON file and/or command-line arguments.

So you can set the configuration in `appsettings.json`:
 - `ConnectionString`: the connection string for your Azure Storage
 - `Archive` (default `true`): archive files after upload
 - `Container` (default `"photos"`): the name of the container to upload files to
 - `Delete` (default `false`): delete files after successful upload

For example:
```json
{
	"ConnectionString": "SECRET"
}
```

Or in CLI arguments:
```ps
.\PhotoArchiver.exe --Path "D:\OneDrive\Camera Roll" --Archive false --Delete true
```

## Information

Supported file types:
 - Photos (with EXIF or date in file name)
	- Android
	- Windows Phone
	- Office Lens
 - Videos (with date in file name)
    - Android (.MP4)
    - Windows Phone (.MP4)
 - RAW (with matching JPEG or date in file name)
	- Digital Negative (.DNG), Windows Phone RAW
	- Nikon Electric Format (.NEF)
	- Canon Raw Version 2 (.CR2)

A single file is uploaded at a time.

## Development

Requirements:
 - Visual Studio 2019 Preview
 - .NET Core SDK 2.2

## Troubleshooting

 - Make sure you have a valid key for your Storage Account. You can try it in another tool like Storage Explorer.
 - Make sure your Storage Account is accessible from your network. You can check this on the Firewall tab of your Storage Account.
 - If you want to archive blobs, make sure your Storage Account is the new kind (GPv2 or Blob), so it supports blob level tiers.
 - Make sure you (and this application) have at least Read permission to the folder you want to upload.
   - If you want to delete the files as well, make sure you have Delete permission.
   - Also, that the files are not in use anywhere else.

## Special thanks
 - [SixLabors ImageSharp](https://github.com/SixLabors/ImageSharp) library for reading EXIF data