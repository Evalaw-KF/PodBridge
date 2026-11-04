namespace PodBridge.Core.Audio;

public enum ConnectionChange
{
    None,
    Connected,
    Disconnected,
}

/// <summary>
/// Turns "is the AirPods audio endpoint active?" observations into connect / disconnect events.
/// The first observation is only the starting point (nothing is announced at app start-up), and a change must hold
/// for <see cref="Debounce"/> before it counts, so the flapping Windows does while Bluetooth profiles come and go
/// does not produce banners.
/// </summary>
public sealed class ConnectionWatcher
{
    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromSeconds(2);

    private bool? _stable;
    private bool? _pending;
    private DateTimeOffset _pendingSince;

    public ConnectionWatcher(TimeSpan? debounce = null) => Debounce = debounce ?? DefaultDebounce;

    public TimeSpan Debounce { get; }

    /// <summary>The last settled state; null until the first observation.</summary>
    public bool? Connected => _stable;

    public void Observe(bool connected, DateTimeOffset now)
    {
        if (_stable is null)
        {
            _stable = connected;
            return;
        }
        if (connected == _stable)
        {
            _pending = null; // went back before the debounce ran out
            return;
        }
        if (_pending != connected)
        {
            _pending = connected;
            _pendingSince = now;
        }
    }

    /// <summary>Call regularly. Returns the change once it has held long enough, otherwise None.</summary>
    public ConnectionChange Poll(DateTimeOffset now)
    {
        if (_pending is not bool target || now - _pendingSince < Debounce)
        {
            return ConnectionChange.None;
        }
        _stable = target;
        _pending = null;
        return target ? ConnectionChange.Connected : ConnectionChange.Disconnected;
    }
}
