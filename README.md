# PodBridge

**See your AirPods and Beats battery on Windows.**

PodBridge is a small, free, open-source app that sits in your system tray and shows the battery level of your AirPods or Beats: left earbud, right earbud and charging case. When you open the case lid near your PC, a small pop-up appears in the bottom-right corner, just like on an iPhone.

> Status: **early version (Phase 1)**. It is built to work with AirPods Pro (2nd generation) and other recent AirPods and Beats, but it is new, so please tell us what works and what does not (see "Something is wrong?" below).

## What it does

- Shows left, right and case battery, plus whether each is charging.
- Tray icon whose number and colour show the lowest earbud battery. Hover for details.
- Pop-up in the corner of the screen when you open the case lid (it hides itself after about 6 seconds).
- Windows notification when a battery gets low (you choose the percentage).
- Optional: start with Windows, light / dark / system theme.

## Download and run (no installing, no coding)

1. Go to the **[Releases page](../../releases/latest)** of this project.
2. Under **Assets**, download the zip for your PC:
   - `...-win-x64.zip` for most Windows PCs (Intel or AMD).
   - `...-win-arm64.zip` for ARM PCs (for example Snapdragon laptops).
   - Not sure? Open **Settings > System > About** and look at "System type".
3. Right-click the zip, choose **Extract All...**, and pick a folder (for example Documents). Do not run it from inside the zip.
4. Open the folder and double-click **PodBridge.exe**.

PodBridge opens its window and also puts an icon in the tray (the small icons near the clock; you may need to click the little arrow `^` to see it). Right-click the tray icon for **Open**, **Settings** and **Exit**. Closing the window does not quit the app; use **Exit** in the tray menu.

**Windows may show a blue "Windows protected your PC" screen** the first time. PodBridge is not signed with a paid certificate yet. If you trust the download, click **More info**, then **Run anyway**.

### Using it

1. Make sure **Bluetooth is turned on** in Windows and your PC has Bluetooth Low Energy (almost all do).
2. Open the case lid near your PC, or put an earbud in your ear.
3. Within a few seconds PodBridge shows your battery levels.

You do **not** have to connect or pair your AirPods to the PC through PodBridge. It only listens to the short Bluetooth announcements that AirPods broadcast. Use them with your PC normally if you like.

Handy buttons in **Settings**: *Preview the pop-up* and *Send a test notification*, so you can check both without waiting for a low battery.

## Privacy

- **Works fully offline.** PodBridge never connects to the internet.
- **No data is collected.** No accounts, no analytics, no tracking, no telemetry.
- It only **listens** to Bluetooth announcements from Apple devices nearby. It never connects to them and never sends anything.
- Your settings are stored in one small file on your PC: `%LOCALAPPDATA%\PodBridge\settings.json`. If something goes wrong, a local log may be written to `%LOCALAPPDATA%\PodBridge\error.log`. Neither file ever leaves your PC.
- The source code is open, so anyone can check this.

To remove PodBridge: choose **Exit** from the tray menu, delete the folder you extracted, and optionally delete `%LOCALAPPDATA%\PodBridge`. If you turned on "Start with Windows", turn it off in Settings first.

## Something is wrong?

| Problem | Try this |
| --- | --- |
| Says "No AirPods nearby" | Open the case lid next to the PC and wait up to 10 seconds. Check that Bluetooth is on. |
| Says Bluetooth is unavailable | Turn Bluetooth on in Windows Settings; PodBridge retries by itself. |
| Battery shows a dash (-) | The earbuds do not report a level while both are in the case with the lid closed, or one bud is out of range. Put one in your ear. |
| Wrong name or "Apple / Beats device" | Your model is not in our list yet. Please open an issue and tell us which model you have. |
| Left and right look swapped | Please open an issue with your model; we want to fix this. |
| No pop-up | Check that "Show a pop-up..." is on in Settings, then use *Preview the pop-up*. |

When reporting an issue, say which Windows version and which AirPods or Beats model you have.

## Limitations

- Apple does not publish this Bluetooth format. PodBridge's decoding is based on public community research and may not match every model or firmware version. Battery levels come in steps of 10%.
- Windows only (Windows 10 version 2004 or newer, or Windows 11).
- Not affiliated with or endorsed by Apple Inc. or Beats Electronics. "AirPods" and "Beats" are trademarks of their owners.

## For developers

See [CONTRIBUTING.md](CONTRIBUTING.md). In short: `src/PodBridge.Core` holds the logic and tests (`dotnet test tests/PodBridge.Core.Tests`), and `src/PodBridge.App` is the WinUI 3 app. Every push is built by GitHub Actions; pushing a tag like `v0.1.0` publishes a release with the zips attached.

## License

[MIT](LICENSE) (c) PodBridge contributors.
