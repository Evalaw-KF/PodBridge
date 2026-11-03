using Microsoft.UI.Dispatching;
using PodBridge.App.Services;
using PodBridge.App.Tray;
using PodBridge.App.Views;
using PodBridge.Core.Alerts;
using PodBridge.Core.Audio;
using PodBridge.Core.Bluetooth;
using PodBridge.Core.Ear;
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

    // Phase 2
    private readonly AudioService _audio = new();
    private readonly BluetoothAudioControl _bluetooth;
    private readonly KeepAliveService _keepAlive;
    private readonly MediaControlService _media = new();
    private readonly EarDetectionPolicy _ear = new();
    private readonly OutputSwitchPolicy _switch = new();
    private readonly SemaphoreSlim _earActions = new(1, 1);
    private readonly DispatcherQueueTimer _earTimer;
    private readonly DispatcherQueueTimer _endpointsTimer;
    private string? _lastDefaultId;
    private bool _connectionBusy;

    private TrayIcon? _tray;
    private MainWindow? _mainWindow;
    private PopupWindow? _popupWindow;
    private OutputPopupWindow? _outputPopup;
    private HotkeyService? _hotkeys;

    public AppHost(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        Settings = _store.Load();
        _lowBattery = new LowBatteryMonitor(Settings.LowBatteryThreshold);
        _refreshTimer = dispatcher.CreateTimer();
        _earTimer = dispatcher.CreateTimer();
        _endpointsTimer = dispatcher.CreateTimer();
        _bluetooth = new BluetoothAudioControl(_audio);
        _keepAlive = new KeepAliveService(_audio);
        ApplyPolicySettings();
    }

    public AppSettings Settings { get; private set; }
    public bool IsExiting { get; private set; }
    public string ScannerStatus { get; private set; } = "Starting…";

    /// <summary>Recent distinct raw adverts, for the Diagnostics section in Settings.</summary>
    public AdvertLog AdvertLog => _advertLog;

    public AudioService Audio => _audio;
    public HotkeyService? Hotkeys => _hotkeys;

    /// <summary>One-line description of the AirPods audio link and the current output, for the main page.</summary>
    public string AudioStatus { get; private set; } = string.Empty;

    /// <summary>Result of the last connect / disconnect / switch request.</summary>
    public string AudioMessage { get; private set; } = string.Empty;

    /// <summary>True if an AirPods audio endpoint exists (connected or not) and no request is running.</summary>
    public bool CanChangeConnection { get; private set; }

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
        try
        {
            _outputPopup = new OutputPopupWindow();
        }
        catch (Exception ex)
        {
            Log.Error("Output pop-up is unavailable", ex); // the rest of the app works without it
        }
        ApplyTheme();
        StartupRegistration.Apply(Settings.StartWithWindows);

        _tray = new TrayIcon();
        _tray.OpenRequested += ShowMainWindow;
        _tray.SettingsRequested += ShowSettings;
        _tray.ExitRequested += Exit;
        _tray.ConnectRequested += RequestConnect;
        _tray.DisconnectRequested += RequestDisconnect;
        _tray.SwitchOutputRequested += RequestSwitchOutput;
        RefreshTray();

        _hotkeys = new HotkeyService(_tray.Handle);
        _hotkeys.Pressed += OnHotkey;
        _tray.HotkeyPressed += _hotkeys.HandleHotkeyMessage;
        _hotkeys.Apply(Settings);

        Guard("Audio start-up", StartAudio);

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
        _refreshTimer.Tick += (_, _) =>
        {
            RefreshTray();
            Guard("Audio refresh", RefreshAudioState);
            StateChanged?.Invoke();
        };
        _refreshTimer.Start();

        _earTimer.Interval = TimeSpan.FromMilliseconds(500);
        _earTimer.Tick += (_, _) => Guard("Ear timer", () => HandleEar(_ear.Tick(DateTimeOffset.UtcNow)));
        _earTimer.Start();

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

        if (snapshot.Kind == DeviceKind.Earbuds)
        {
            int budsInEar = (snapshot.LeftInEar ? 1 : 0) + (snapshot.RightInEar ? 1 : 0);
            HandleEar(_ear.Update(budsInEar, DateTimeOffset.UtcNow));
        }

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
        ApplyPolicySettings();
        _hotkeys?.Apply(Settings);
        Guard("Audio refresh", RefreshAudioState);
        SettingsChanged?.Invoke();
    }

    // ------------------------------------------------------------------
    // Phase 2: audio, ear detection, hotkeys
    // ------------------------------------------------------------------

    private void ApplyPolicySettings()
    {
        _ear.AutoPause = Settings.AutoPauseEnabled;
        _ear.Resume = Settings.ResumeEnabled;
        _ear.OnlyUseOneBud = Settings.OnlyUseOneBud;
        _switch.Enabled = Settings.AutoSwitchOutput;
        _switch.ConfiguredFallbackId = Settings.FallbackEndpointId;
    }

    private void StartAudio()
    {
        // Windows calls these on its own (MTA) threads. Only hop to the UI thread here: no MMDevice call may be made
        // inside the callback itself, and exceptions in dispatcher callbacks must never reach WinUI.
        _audio.DefaultRenderChanged += _ => _dispatcher.TryEnqueue(() => Guard("Default output change", OnDefaultOutputChanged));
        _audio.EndpointsChanged += () => _dispatcher.TryEnqueue(() => Guard("Endpoint change", ScheduleEndpointsRefresh));

        _endpointsTimer.IsRepeating = false;
        _endpointsTimer.Interval = TimeSpan.FromMilliseconds(400);
        _endpointsTimer.Tick += (_, _) => Guard("Endpoint refresh", OnEndpointsChanged);

        _audio.StartListening();

        var ctx = BuildContext();
        _lastDefaultId = ctx.DefaultId;
        _switch.ObserveDefault(ctx);
        ApplyAudioState(ctx);
    }

    private OutputContext BuildContext() =>
        new(_audio.ListRenderEndpoints(includeUnplugged: true), _audio.GetDefaultEndpointId(), Settings.HeadphoneNameMatch);

    /// <summary>Re-reads the audio state and updates keep-alive and the status text.</summary>
    private void RefreshAudioState()
    {
        if (!IsExiting)
        {
            ApplyAudioState(BuildContext());
        }
    }

    private void ApplyAudioState(OutputContext ctx)
    {
        if (OutputSwitchPolicy.ShouldKeepAlive(Settings.KeepAliveEnabled, ctx))
        {
            _keepAlive.EnsureRunning(ctx.DefaultId!);
        }
        else
        {
            _keepAlive.Stop();
        }

        var pods = ctx.AirPods;
        var current = ctx.Find(ctx.DefaultId)?.Name;
        if (pods is null)
        {
            AudioStatus = $"No audio device with \"{Settings.HeadphoneNameMatch}\" in its name was found. Pair your AirPods in Windows Bluetooth settings first.";
        }
        else if (pods.IsActive)
        {
            AudioStatus = $"Audio connected: {pods.Name}. Current output: {current ?? "none"}.";
        }
        else
        {
            AudioStatus = $"Audio not connected: {pods.Name}. Current output: {current ?? "none"}.";
        }
        CanChangeConnection = pods is not null && !_connectionBusy;
    }

    private void ScheduleEndpointsRefresh()
    {
        // Windows sends several notifications for one change; handle them together.
        _endpointsTimer.Stop();
        _endpointsTimer.Start();
    }

    private void OnEndpointsChanged()
    {
        if (IsExiting)
        {
            return;
        }
        var ctx = BuildContext();
        _switch.ObserveDefault(ctx);
        ApplyAudioState(ctx);
        StateChanged?.Invoke();

        // The AirPods endpoint may just have come back after a bud was put in.
        if (_ear.StableCount is int buds)
        {
            _ = RunSafe(ApplySwitchAsync(_switch.OnEnvironmentChanged(buds, ctx)));
        }
    }

    private void OnDefaultOutputChanged()
    {
        if (IsExiting)
        {
            return;
        }
        var ctx = BuildContext();
        _switch.ObserveDefault(ctx);
        ApplyAudioState(ctx);
        StateChanged?.Invoke();

        if (ctx.DefaultId is not null && ctx.DefaultId != _lastDefaultId && Settings.OutputPopupEnabled)
        {
            _outputPopup?.ShowFor(ctx.Find(ctx.DefaultId)?.Name ?? "Audio output");
        }
        _lastDefaultId = ctx.DefaultId;

        if (_ear.StableCount is int buds)
        {
            _ = RunSafe(ApplySwitchAsync(_switch.OnEnvironmentChanged(buds, ctx)));
        }
    }

    private void HandleEar(EarResult result)
    {
        if (result.Action != EarAction.None || result.StableChanged)
        {
            _ = RunSafe(HandleEarAsync(result));
        }
    }

    private async Task HandleEarAsync(EarResult result)
    {
        // One at a time, so a quick pause/resume pair cannot overlap.
        await _earActions.WaitAsync();
        try
        {
            if (result.Action == EarAction.Pause)
            {
                // Pause first, so nothing blasts out of the speakers when the output moves.
                if (!await _media.PauseAsync())
                {
                    _ear.PauseDidNothing();
                }
            }

            bool switched = false;
            if (result.StableChanged && result.StableCount is int buds)
            {
                switched = await ApplySwitchAsync(_switch.OnBudsChanged(buds, BuildContext()));
            }

            if (result.Action == EarAction.Resume)
            {
                if (switched)
                {
                    await Task.Delay(600); // let the AirPods output settle before music starts
                }
                await _media.ResumeAsync();
            }
        }
        finally
        {
            _earActions.Release();
        }
    }

    /// <summary>Carries out a switching decision. Returns true if the default output was changed.</summary>
    private async Task<bool> ApplySwitchAsync(SwitchDecision decision)
    {
        if (decision.Kind == SwitchKind.None || decision.TargetId is null)
        {
            return false;
        }

        string target = decision.TargetId;
        string? error = await Task.Run(() => _audio.SetDefaultEndpoint(target));
        if (error is not null)
        {
            SetAudioMessage(error);
            return false;
        }
        return true;
    }

    // Entry points for the tray menu and the buttons (errors are logged, never thrown).
    public void RequestConnect() => _ = RunSafe(ConnectAsync(notify: false));

    public void RequestDisconnect() => _ = RunSafe(DisconnectAsync(notify: false));

    public void RequestSwitchOutput() => _ = RunSafe(SwitchOutputAsync(notify: false));

    /// <summary>Hotkey / tray / button action: toggles the output between the AirPods and the fallback speaker.</summary>
    private async Task SwitchOutputAsync(bool notify)
    {
        var ctx = BuildContext();
        _switch.ObserveDefault(ctx);
        var decision = _switch.ToggleTarget(ctx);
        string message;
        if (decision.Kind == SwitchKind.None)
        {
            message = ctx.DefaultIsAirPods
                ? "No speaker to switch to. Pick one under Audio output in Settings."
                : "The AirPods audio is not connected, so there is nothing to switch to.";
        }
        else
        {
            _switch.ClearAway(); // the user is in charge now: no automatic switch back
            bool ok = await ApplySwitchAsync(decision);
            var name = ctx.Find(decision.TargetId)?.Name ?? "the selected device";
            message = ok ? $"Output switched to {name}." : AudioMessage;
        }
        SetAudioMessage(message, notify);
    }

    /// <summary>Connects the AirPods' audio link (see BluetoothAudioControl for how, and its caveats).</summary>
    private async Task ConnectAsync(bool notify)
    {
        await ChangeConnectionAsync(connect: true, notify);
    }

    /// <summary>Disconnects the AirPods' audio link.</summary>
    private async Task DisconnectAsync(bool notify)
    {
        await ChangeConnectionAsync(connect: false, notify);
    }

    private async Task ChangeConnectionAsync(bool connect, bool notify)
    {
        if (_connectionBusy)
        {
            return;
        }
        var pods = BuildContext().AirPods;
        if (pods is null)
        {
            SetAudioMessage($"No audio device with \"{Settings.HeadphoneNameMatch}\" in its name was found.", notify);
            return;
        }

        _connectionBusy = true;
        CanChangeConnection = false;
        SetAudioMessage(connect ? "Connecting…" : "Disconnecting…");
        try
        {
            string id = pods.Id;
            string? error = await Task.Run(() => connect ? _bluetooth.Connect(id) : _bluetooth.Disconnect(id));
            SetAudioMessage(error ?? (connect
                ? "Connection requested. It can take a few seconds."
                : "Disconnected."), notify);
        }
        finally
        {
            _connectionBusy = false;
            RefreshAudioState();
            StateChanged?.Invoke();
        }
    }

    private void OnHotkey(HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.ToggleConnection:
                bool connected = BuildContext().AirPods is { IsActive: true };
                _ = RunSafe(connected ? DisconnectAsync(notify: true) : ConnectAsync(notify: true));
                break;
            case HotkeyAction.SwitchOutput:
                _ = RunSafe(SwitchOutputAsync(notify: true));
                break;
        }
    }

    private void SetAudioMessage(string message, bool notify = false)
    {
        AudioMessage = message;
        StateChanged?.Invoke();
        if (notify)
        {
            _notifications.Show("PodBridge", message);
        }
    }

    /// <summary>Runs a UI-thread handler; logs instead of letting an exception escape into WinUI.</summary>
    private static void Guard(string what, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Log.Error($"{what} failed", ex);
        }
    }

    private static async Task RunSafe(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            Log.Error("Background action failed", ex);
        }
    }

    private void ApplyTheme()
    {
        _mainWindow?.ApplyTheme(Settings.Theme);
        _popupWindow?.ApplyTheme(Settings.Theme);
        _outputPopup?.ApplyTheme(Settings.Theme);
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
        _earTimer.Stop();
        _endpointsTimer.Stop();
        _hotkeys?.Dispose();
        _keepAlive.Dispose();
        _audio.Dispose();
        _scanner.Dispose();
        _tray?.Dispose();
        _notifications.Dispose();
        _popupWindow?.Close();
        _outputPopup?.Close();
        _mainWindow?.Close();
        Microsoft.UI.Xaml.Application.Current.Exit();
    }
}
