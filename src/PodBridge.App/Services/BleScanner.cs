using PodBridge.Core.Bluetooth;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace PodBridge.App.Services;

/// <summary>
/// Listens passively for Apple Bluetooth LE advertisements and hands decoded proximity-pairing
/// adverts to subscribers. Events are raised on a background thread.
/// PodBridge never connects to anything and never transmits.
/// </summary>
internal sealed class BleScanner : IDisposable
{
    private readonly object _gate = new();
    private BluetoothLEAdvertisementWatcher? _watcher;
    private Timer? _retryTimer;
    private bool _disposed;

    public event Action<ProximityAdvert, int>? AdvertReceived;
    public event Action<string>? StatusChanged;

    public void Start()
    {
        lock (_gate)
        {
            if (_disposed || _watcher is not null)
            {
                return;
            }

            try
            {
                var watcher = new BluetoothLEAdvertisementWatcher
                {
                    ScanningMode = BluetoothLEScanningMode.Passive,
                };

                // No OS-level advertisement filter on purpose: a manufacturer-data filter entry with an
                // unset Data buffer is not documented to mean "any data", and a wrong filter would
                // silently hide every advert. Apple's data is picked out in OnReceived instead.

                watcher.Received += OnReceived;
                watcher.Stopped += OnStopped;
                watcher.Start();
                _watcher = watcher;
                StatusChanged?.Invoke("Scanning");
            }
            catch (Exception ex)
            {
                Log.Error("Could not start Bluetooth scanning", ex);
                _watcher = null;
                StatusChanged?.Invoke("Bluetooth is unavailable. Make sure Bluetooth is turned on.");
                ScheduleRetry();
            }
        }
    }

    private void OnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        try
        {
            foreach (var section in args.Advertisement.GetManufacturerDataByCompanyId(ProximityPairingParser.AppleCompanyId))
            {
                var buffer = section.Data;
                var bytes = new byte[buffer.Length];
                using (var reader = DataReader.FromBuffer(buffer))
                {
                    reader.ReadBytes(bytes);
                }

                if (ProximityPairingParser.TryParse(bytes, out var advert))
                {
                    AdvertReceived?.Invoke(advert, args.RawSignalStrengthInDBm);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("Failed to process an advertisement", ex);
        }
    }

    private void OnStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            if (_watcher is not null)
            {
                _watcher.Received -= OnReceived;
                _watcher.Stopped -= OnStopped;
                _watcher = null;
            }
        }

        StatusChanged?.Invoke(args.Error == BluetoothError.Success
            ? "Scanning stopped"
            : "Bluetooth is turned off or unavailable. Turn it on and PodBridge will reconnect.");
        ScheduleRetry();
    }

    private void ScheduleRetry()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _retryTimer?.Dispose();
            _retryTimer = new Timer(_ => Start(), null, TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _retryTimer?.Dispose();
            _retryTimer = null;
            if (_watcher is not null)
            {
                _watcher.Received -= OnReceived;
                _watcher.Stopped -= OnStopped;
                try { _watcher.Stop(); } catch { /* shutting down */ }
                _watcher = null;
            }
        }
    }
}
