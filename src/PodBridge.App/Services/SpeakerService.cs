using Microsoft.UI.Dispatching;
using PodBridge.Core.Speaker;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;

namespace PodBridge.App.Services;

/// <summary>
/// "Use this PC as a Bluetooth speaker" through AudioPlaybackConnection: lists paired devices that can send
/// audio (phones and so on), enables them so they can choose this PC, and opens the audio link on request.
/// The API needs no package identity. UI thread only.
/// </summary>
internal sealed class SpeakerService : IDisposable
{
    private readonly DispatcherQueue _dispatcher;
    private readonly Func<IReadOnlyCollection<string>> _autoList;
    private readonly Dictionary<string, AudioPlaybackConnection> _connections = new();
    private DeviceWatcher? _watcher;
    private bool _disposed;

    public SpeakerService(DispatcherQueue dispatcher, Func<IReadOnlyCollection<string>> autoList)
    {
        _dispatcher = dispatcher;
        _autoList = autoList;
    }

    public SpeakerRegistry Registry { get; } = new();

    /// <summary>Why speaker mode cannot work on this PC, or null.</summary>
    public string? UnavailableReason { get; private set; }

    public bool Scanning { get; private set; }

    public event Action? Changed;

    public void Start()
    {
        try
        {
            _watcher = DeviceInformation.CreateWatcher(AudioPlaybackConnection.GetDeviceSelector());
            _watcher.Added += (_, info) => Post(() => OnAdded(info));
            _watcher.Removed += (_, update) => Post(() => OnRemoved(update.Id));
            _watcher.Updated += (_, _) => { };
            _watcher.EnumerationCompleted += (_, _) => Post(() => { Scanning = false; Changed?.Invoke(); });
            _watcher.Stopped += (_, _) => Post(() => { Scanning = false; Changed?.Invoke(); });
            Scanning = true;
            _watcher.Start();
        }
        catch (Exception ex)
        {
            Log.Error("Speaker mode is unavailable", ex);
            UnavailableReason = "Speaker mode is not available on this PC (it needs Windows 10 version 2004 or newer and Bluetooth).";
            Scanning = false;
            _watcher = null;
        }
    }

    private void Post(Action action)
    {
        _dispatcher.TryEnqueue(() =>
        {
            if (_disposed)
            {
                return;
            }
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error("Speaker device update failed", ex);
            }
        });
    }

    private void OnAdded(DeviceInformation info)
    {
        Registry.Upsert(info.Id, info.Name);
        Changed?.Invoke();
        if (SpeakerAutoEnable.ToEnable(_autoList(), new[] { Registry.Find(info.Id)! }).Count > 0)
        {
            _ = RunSafe(EnableAsync(info.Id));
        }
    }

    private void OnRemoved(string id)
    {
        DropConnection(id);
        Registry.Remove(id);
        Changed?.Invoke();
    }

    public async Task EnableAsync(string id)
    {
        var entry = Registry.Find(id);
        if (entry is null || !SpeakerText.CanEnable(entry.State) || _connections.ContainsKey(id))
        {
            return;
        }

        try
        {
            var connection = AudioPlaybackConnection.TryCreateFromId(id);
            if (connection is null)
            {
                Fail(id, "Windows could not set this device up. Another program (such as another speaker-mode tool) may already be using it.");
                return;
            }

            connection.StateChanged += (c, _) => Post(() => OnStateChanged(id, c));
            _connections[id] = connection;
            await connection.StartAsync();
            if (_connections.ContainsKey(id))
            {
                Registry.SetState(id, SpeakerState.Ready);
                Changed?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Log.Error("Could not enable speaker mode for a device", ex);
            bool stillWanted = _connections.ContainsKey(id); // false if Disable was pressed while it was starting
            DropConnection(id);
            if (stillWanted)
            {
                Fail(id, "Could not enable this device. Make sure it is paired and Bluetooth is on.");
            }
        }
    }

    public async Task OpenAsync(string id)
    {
        var entry = Registry.Find(id);
        if (entry is null || !SpeakerText.CanOpen(entry.State) || !_connections.TryGetValue(id, out var connection))
        {
            return;
        }

        try
        {
            var result = await connection.OpenAsync();
            switch (result.Status)
            {
                case AudioPlaybackConnectionOpenResultStatus.Success:
                    Registry.SetState(id, SpeakerState.Connected);
                    break;
                case AudioPlaybackConnectionOpenResultStatus.RequestTimedOut:
                    Registry.SetState(id, SpeakerState.Ready, "The device did not answer. Check that it is nearby with Bluetooth on, start playing something on it, and try again.");
                    break;
                case AudioPlaybackConnectionOpenResultStatus.DeniedBySystem:
                    Registry.SetState(id, SpeakerState.Ready, "Windows refused the connection.");
                    break;
                default:
                    Log.Error($"Speaker connection failed: {result.ExtendedError?.Message}");
                    Registry.SetState(id, SpeakerState.Ready, "The connection failed. Try again, or turn the device off and on in Enable.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error("Could not open the speaker connection", ex);
            Registry.SetState(id, SpeakerState.Ready, "The connection failed.");
        }
        Changed?.Invoke();
    }

    public void Disable(string id)
    {
        DropConnection(id);
        Registry.SetState(id, SpeakerState.Off);
        Changed?.Invoke();
    }

    private void OnStateChanged(string id, AudioPlaybackConnection connection)
    {
        var entry = Registry.Find(id);
        if (entry is null || !_connections.ContainsKey(id))
        {
            return;
        }
        if (connection.State == AudioPlaybackConnectionState.Opened)
        {
            Registry.SetState(id, SpeakerState.Connected);
        }
        else if (entry.State == SpeakerState.Connected)
        {
            Registry.SetState(id, SpeakerState.Ready);
        }
        Changed?.Invoke();
    }

    private void Fail(string id, string message)
    {
        Registry.SetState(id, SpeakerState.Failed, message);
        Changed?.Invoke();
    }

    private void DropConnection(string id)
    {
        if (_connections.Remove(id, out var connection))
        {
            try
            {
                connection.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error("Could not close a speaker connection", ex);
            }
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
            Log.Error("Speaker action failed", ex);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        try
        {
            _watcher?.Stop();
        }
        catch (Exception ex)
        {
            Log.Error("Could not stop the speaker device watcher", ex);
        }
        foreach (var id in _connections.Keys.ToList())
        {
            DropConnection(id);
        }
    }
}
