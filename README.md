# MiddleClickToClose

A tiny Windows tray utility that closes a window when you middle-click its taskbar button.

## Usage

1. Download the zip from [Releases](https://github.com/ponpok0/MiddleClickToClose/releases) and extract it.
2. Run `MiddleClickToClose.exe`. An icon appears in the system tray.
3. Middle-click a taskbar button to close that window (the app sends `WM_CLOSE` to it).

To quit, right-click the tray icon and choose **Exit**.

The UI follows the Windows display language (English and Japanese are supported; other languages fall back to English).

To start it automatically at sign-in, put a shortcut to the exe in `shell:startup`.

## Requirements

- Windows 10 / 11 (x64)
- The release build is self-contained, so no .NET runtime installation is needed.

## Build

Requires the .NET 8 SDK.

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## License

[MIT](LICENSE)

The icon is a modified version of the Google Material Icons `mouse` icon, licensed under the Apache License 2.0. See [Resources/README.md](Resources/README.md) for details.
