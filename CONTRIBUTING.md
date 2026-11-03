# Contributing to PodBridge

Thanks for helping! PodBridge is MIT-licensed and everything here is written from scratch. Please do not paste in code from closed-source or differently licensed projects.

## Layout

| Folder | What it is |
| --- | --- |
| `src/PodBridge.Core` | Plain .NET 8 library with no Windows dependencies: advert parser, device smoothing, low-battery logic, settings file. |
| `tests/PodBridge.Core.Tests` | xUnit tests. They run on Linux, macOS and Windows. |
| `src/PodBridge.App` | The WinUI 3 app (Windows only): Bluetooth watcher, tray icon, windows, notifications. |

Rule of thumb: logic goes in Core with tests; the App project stays thin and only talks to Windows.

## Running the tests

Install the .NET 8 SDK, then:

```
dotnet test tests/PodBridge.Core.Tests
```

## Building the app (Windows only)

```
dotnet publish src/PodBridge.App/PodBridge.App.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -o publish/win-x64
```

Use `-r win-arm64 -p:Platform=ARM64` for ARM. Visual Studio 2022 with the ".NET desktop development" workload works too: open `src/PodBridge.App/PodBridge.App.csproj` directly.

## Adding a device model

Model IDs live in `AppleDeviceCatalog` (`src/PodBridge.Core/Bluetooth/AppleDeviceModel.cs`). Add the enum value, the ID, a display name, and a test in `ParserTests`. If you can, include a captured advert from real hardware in the pull request description (hex bytes only, no addresses).

## Pull requests

- Keep changes focused and add tests for Core changes.
- Make sure `dotnet test` passes.
- Describe what you tried on real hardware, if anything, and which device.

## Releases

Maintainers create a release by pushing a tag such as `v0.1.0`. The GitHub Actions workflow builds x64 and ARM64 zips and attaches them to the release.

## Roadmap note

Phase 1 ships as an unpackaged, self-contained app (unzip and run). A later phase that needs Windows notification-listener access requires package identity, so the app will move to MSIX then.
