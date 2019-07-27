# Archive photos to Azure Storage

Archive your RAW photos and videos to [Azure Archive Storage](https://azure.microsoft.com/en-us/services/storage/archive/) with the lowest cost of €0.00084/GB/mo or €0.8/TB/mo. See detailed [pricing](https://azure.microsoft.com/en-us/pricing/details/storage/blobs/).

This app uploads your files like `IMG_20190727_123456.jpg` to a container `photos` and groups them into directories like `2019`/`07`/`27`.

## Usage

Requirements:
 - [.NET Core 2.2](https://dotnet.microsoft.com/download)

```ps
.\PhotoArchiver.exe --Path "D:\OneDrive\Camera Roll" --ConnectionString "SECRET"
```

On Linux:

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
 - RAW (with matching JPEG)
	- Digital Negative (.DNG), Windows Phone RAW
	- Nikon Electric Format (.NEF)
	- Canon Raw Version 2 (.CR2)

## Development

Requirements:
 - Visual Studio 2019 Preview
 - .NET Core SDK 2.2