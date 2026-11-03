using PodBridge.Core.Bluetooth;

namespace PodBridge.Core.State;

/// <summary>Result of feeding one advert into the tracker.</summary>
public readonly record struct TrackerUpdate(bool Accepted, DeviceSnapshot? Snapshot, bool LidJustOpened);

/// <summary>
/// Turns a noisy stream of adverts into a stable <see cref="DeviceSnapshot"/>:
/// weak packets are dropped, "unknown" values keep the last good one for a while,
/// and a second, weaker device nearby does not hijack the display.
/// Not thread-safe: call from a single thread.
/// </summary>
public sealed class DeviceTracker
{
    private sealed class Held<T> where T : struct
    {
        public T? Value;
        public DateTimeOffset At;

        public T? Merge(T? incoming, DateTimeOffset now, TimeSpan keep)
        {
            if (incoming.HasValue)
            {
                Value = incoming;
                At = now;
            }
            else if (Value.HasValue && now - At > keep)
            {
                Value = null;
            }
            return Value;
        }

        public void Clear() { Value = null; At = default; }
    }

    private readonly TrackerOptions _options;
    private readonly Held<int> _left = new();
    private readonly Held<int> _right = new();
    private readonly Held<int> _case = new();
    private DeviceSnapshot? _current;
    private DateTimeOffset _lastLidEvent = DateTimeOffset.MinValue;

    public DeviceTracker(TrackerOptions? options = null)
    {
        _options = options ?? new TrackerOptions();
    }

    /// <summary>The latest snapshot, even if it is old. Null before any accepted advert.</summary>
    public DeviceSnapshot? Current => _current;

    /// <summary>The latest snapshot, or null when none has been accepted within <see cref="TrackerOptions.StaleAfter"/>.</summary>
    public DeviceSnapshot? GetCurrent(DateTimeOffset now) =>
        _current is not null && now - _current.LastSeen <= _options.StaleAfter ? _current : null;

    public void Reset()
    {
        _current = null;
        _left.Clear();
        _right.Clear();
        _case.Clear();
        _lastLidEvent = DateTimeOffset.MinValue;
    }

    public TrackerUpdate Update(ProximityAdvert advert, int rssi, DateTimeOffset now)
    {
        if (rssi < _options.MinRssi)
        {
            return new TrackerUpdate(false, _current, false);
        }

        var previous = _current;
        bool previousFresh = previous is not null && now - previous.LastSeen <= _options.StaleAfter;
        bool previousVeryFresh = previous is not null && now - previous.LastSeen <= _options.KeepLastGood;

        // A different, weaker device while the current one is still being heard: ignore it.
        if (previous is not null && previousVeryFresh &&
            advert.RawModelId != previous.RawModelId &&
            advert.Model != AppleDeviceModel.Unknown &&
            rssi <= previous.Rssi)
        {
            return new TrackerUpdate(false, _current, false);
        }

        // A different device that is stronger (or the old one is gone): start fresh.
        bool differentDevice = previous is not null &&
            (advert.RawModelId != previous.RawModelId) &&
            advert.Model != AppleDeviceModel.Unknown;
        if (differentDevice || !previousFresh)
        {
            _left.Clear();
            _right.Clear();
            _case.Clear();
            previous = differentDevice ? null : previousFresh ? previous : null;
            previousFresh = previous is not null;
        }

        var kind = AppleDeviceCatalog.GetKind(advert.Model);
        int? incomingLeft = advert.LeftBattery;
        int? incomingRight = advert.RightBattery;
        int? incomingCase = advert.CaseBattery;
        bool leftCharging = advert.LeftCharging;
        bool rightCharging = advert.RightCharging;
        bool leftInEar = advert.LeftInEar;
        bool rightInEar = advert.RightInEar;
        if (kind == DeviceKind.Headphones)
        {
            // Single battery: expose it as "left" only.
            incomingLeft ??= incomingRight;
            incomingRight = null;
            incomingCase = null;
            leftCharging |= rightCharging;
            rightCharging = false;
            leftInEar |= rightInEar;
            rightInEar = false;
        }

        double rssiAvg = previous is null
            ? rssi
            : previous.Rssi + _options.RssiSmoothing * (rssi - previous.Rssi);

        var snapshot = new DeviceSnapshot
        {
            Model = advert.Model,
            RawModelId = advert.RawModelId,
            LeftBattery = _left.Merge(incomingLeft, now, _options.KeepLastGood),
            RightBattery = _right.Merge(incomingRight, now, _options.KeepLastGood),
            CaseBattery = _case.Merge(incomingCase, now, _options.KeepLastGood),
            LeftCharging = leftCharging,
            RightCharging = rightCharging,
            CaseCharging = advert.CaseCharging,
            LeftInEar = leftInEar,
            RightInEar = rightInEar,
            LidOpen = advert.LidOpen,
            Rssi = rssiAvg,
            LastSeen = now,
        };

        bool wasOpen = previousFresh && previous!.LidOpen && !(previous.LeftInEar && previous.RightInEar);
        bool lidJustOpened = false;
        // With both buds in ears they are not in the case, so the lid bit says nothing useful
        // and must not trigger the "case opened" pop-up.
        bool bothInEar = snapshot.LeftInEar && snapshot.RightInEar;
        if (snapshot.LidOpen && !wasOpen && !bothInEar && now - _lastLidEvent >= _options.LidEventCooldown)
        {
            lidJustOpened = true;
            _lastLidEvent = now;
        }

        _current = snapshot;
        return new TrackerUpdate(true, snapshot, lidJustOpened);
    }
}
