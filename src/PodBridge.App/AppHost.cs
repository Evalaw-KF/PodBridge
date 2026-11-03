using Microsoft.UI.Dispatching;
using PodBridge.App.Services;
using PodBridge.App.Tray;
using PodBridge.App.Views;
using PodBridge.Core.Alerts;
using PodBridge.Core.Bluetooth;
using PodBridge.Core.Settings;
using PodBridge.Core.State;

namespace PodBridge.App;

/// <summary>
/// Wires everything together: scanner -> tracker -> tray / window / pop-up / toast.
/// All members must be used on the UI thread.
/// </summary>
internal sealed class AppHost
{
    private readonly DispatcherQueue _dispatcher;
    private readonly DispatcherQueueTimer _refreshTimer;
    private readonly SettingsStore _store = new(SettingsStore.DefaultDirectory());
    private readonly DeviceTracker _tracker = new();
    private readonly BleScanner _scanner = new();
    private readonly AdvertLog _advertLog = new();
    private readonly NotificationService _notifications = new();
    private readonly LowBatteryMonitor _lowBattery;

    private TrayIcon? _tray;
    private MainWindow? _mainWindow;
    private PopupWindow? _popupWindow;

    public AppHost(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        Settings = _store.Load();
        _lowBattery = new LowBatteryMonitor(Settings.LowBatteryThreshold);
        _refreshTimer = dispatcher.CreateTimer();
    }

    public AppSettings Settings { get; private set; }
    public bool IsExiting { get; private set; }
    public string ScannerStatus { get; private set; } = "Starting…";

    /// <summary>Recent distinct raw adverts, for the Diagnostics section in Settings.</summary>
    public AdvertLog AdvertLog => _advertLog;

    /// <summary>The newest device state, or null if nothing has been heard recently.</summary>
    public DeviceSnapshot? Latest => _tracker.GetCurrent(DateTimeOffset.UtcNow);

    /// <summary>Raised whenever the device state or scanner status changes.</summary>
    public event Action? StateChanged;

    /// <summary>Raised after settings are saved (for theme changes).</summary>
    public event Action? SettingsChanged;

    public void Start(bool background)
    {
        _notifications.Initialize();

        _mainWindow = new MainWindow();
        _popupWindow = new PopupWindow();
        ApplyTheme();
        StartupRegistration.Apply(Settings.StartWithWindows);

        _tray = new TrayIcon();
        _tray.OpenRequested += ShowMainWindow;
        _tray.SettingsRequested += ShowSettings;
        _tray.ExitRequested += Exit;
        RefreshTray();

        _scanner.AdvertReceived += (advert, rssi) =>
            _dispatcher.TryEnqueue(() => OnAdvert(advert, rssi));
        _scanner.StatusChanged += status =>
            _dispatcher.TryEnqueue(() =>
            {
                ScannerStatus = status;
                StateChanged?.Invoke();
            });
        _scanner.Start();

        _refreshTimer.Interval = TimeSpan.FromSeconds(5);
        _refreshTimer.Tick += (_, _) => { RefreshTray(); StateChanged?.Invoke(); };
        _refreshTimer.Start();

        if (!background)
        {
            ShowMainWindow();
        }
    }

    private void OnAdvert(ProximityAdvert advert, int rssi)
    {
        if (IsExiting)
        {
            return;
        }

        // Record before the tracker, which may reject the advert (for example a weak signal).
        _advertLog.Record(advert, rssi);

        var update = _tracker.Update(advert, rssi, DateTimeOffset.UtcNow);
        if (!update.Accepted || update.Snapshot is null)
        {
            return;
        }

        var snapshot = update.Snapshot;
        RefreshTray();
        StateChanged?.Invoke();
        _popupWindow?.UpdateIfVisible(snapshot);

        if (update.LidJustOpened && Settings.PopupEnabled)
        {
            _popupWindow?.ShowFor(snapshot);
        }

        if (Settings.LowBatteryNotifications)
        {
            foreach (var alert in _lowBattery.Evaluate(snapshot))
            {
                _notifications.Show(
                    $"{snapshot.DisplayName}: battery low",
                    $"{Format.ComponentName(snapshot, alert.Component)} is at {alert.Level}%.");
            }
        }
    }

    private void RefreshTray()
    {
        var snapshot = Latest;
        var icon = TrayIconRenderer.CreateIcon(Format.TrayPercent(snapshot), Format.TrayCharging(snapshot));
        _tray?.Update(icon, Format.Tooltip(snapshot));
    }

    public void UpdateSettings(AppSettings newSettings)
    {
        Settings = newSettings.Normalized();
        try
        {
            _store.Save(Settings);
        }
        catch (Exception ex)
        {
            Log.Error("Could not save settings", ex);
        }

        _lowBattery.Threshold = Settings.LowBatteryThreshold;
        StartupRegistration.Apply(Settings.StartWithWindows);
        ApplyTheme();
        SettingsChanged?.Invoke();
    }

    private void ApplyTheme()
    {
        _mainWindow?.ApplyTheme(Settings.Theme);
        _popupWindow?.ApplyTheme(Settings.Theme);
    }

    public void ShowMainWindow() => _mainWindow?.Present(showSettings: false);

    public void ShowSettings() => _mainWindow?.Present(showSettings: true);

    /// <summary>Lets the user preview the pop-up without opening a case.</summary>
    public void ShowPopupPreview()
    {
        var snapshot = Latest ?? new DeviceSnapshot
        {
            Model = AppleDeviceModel.AirPodsPro2UsbC,
            RawModelId = 0x2420,
            LeftBattery = 80,
            RightBattery = 70,
            CaseBattery = 50,
            LidOpen = true,
            LastSeen = DateTimeOffset.UtcNow,
        };
        _popupWindow?.ShowFor(snapshot);
    }

    public void ShowTestNotification() =>
        _notifications.Show("PodBridge test", "If you can read this, low-battery alerts will work.");

    public void Exit()
    {
        if (IsExiting)
        {
            return;
        }
        IsExiting = true;

        _refreshTimer.Stop();
        _scanner.Dispose();
        _tray?.Dispose();
        _notifications.Dispose();
        _popupWindow?.Close();
        _mainWindow?.Close();
        Microsoft.UI.Xaml.Application.Current.Exit();
    }
}
