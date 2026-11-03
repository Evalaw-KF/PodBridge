namespace PodBridge.Core.State;

public sealed class TrackerOptions
{
    /// <summary>Adverts with an RSSI below this (dBm) are ignored as unreliable. Default -105 (real frames arrive at -95 to -101 dBm).</summary>
    public int MinRssi { get; init; } = -105;

    /// <summary>How long a previously good value is kept when a later packet reports "unknown".</summary>
    public TimeSpan KeepLastGood { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>After this long without an accepted advert the device is considered gone.</summary>
    public TimeSpan StaleAfter { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Minimum time between two "lid just opened" events, so a flickering bit cannot spam pop-ups.</summary>
    public TimeSpan LidEventCooldown { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Weight (0..1) of a new RSSI reading in the running average.</summary>
    public double RssiSmoothing { get; init; } = 0.3;
}
