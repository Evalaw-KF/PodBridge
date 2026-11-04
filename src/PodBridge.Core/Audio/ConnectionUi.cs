namespace PodBridge.Core.Audio;

/// <summary>Plain-words status line for the Audio card.</summary>
public static class AudioStatusText
{
    /// <summary>
    /// Shortens a Windows endpoint name such as "Headphones (AirPods Pro - Find My)" to "AirPods Pro".
    /// Falls back to the original text (or "AirPods") when nothing is left.
    /// </summary>
    public static string FriendlyName(string? endpointName)
    {
        string text = (endpointName ?? string.Empty).Trim();
        int open = text.IndexOf('(');
        int close = text.LastIndexOf(')');
        if (open >= 0 && close > open)
        {
            text = text[(open + 1)..close].Trim();
        }
        foreach (var suffix in new[] { " - Find My", " Hands-Free", " Stereo", " Hands-Free AG Audio", " Hands-Free HF Audio" })
        {
            if (text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                text = text[..^suffix.Length].Trim();
            }
        }
        return text.Length == 0 ? "AirPods" : text;
    }

    public static string Build(bool found, bool connected, string? podsName, string? outputName, bool outputIsPods,
        string nameMatch)
    {
        if (!found)
        {
            return $"No audio device with \"{nameMatch}\" in its name was found. Pair your AirPods in Windows Bluetooth settings first.";
        }
        string pods = FriendlyName(podsName);
        if (!connected)
        {
            return "Not connected";
        }
        if (outputIsPods)
        {
            return $"Playing through {pods}";
        }
        return string.IsNullOrWhiteSpace(outputName)
            ? "Connected, but no sound output is selected"
            : $"Connected, but sound is going to {FriendlyName(outputName)}";
    }
}

/// <summary>
/// Remembers that a connect or disconnect was just asked for, so the UI can say "Connecting…" until the state really
/// changes, but never for longer than <see cref="Timeout"/>.
/// </summary>
public sealed class PendingConnection
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private bool? _target;
    private DateTimeOffset _since;

    public PendingConnection(TimeSpan? timeout = null) => Timeout = timeout ?? DefaultTimeout;

    public TimeSpan Timeout { get; }

    public void Begin(bool connect, DateTimeOffset now)
    {
        _target = connect;
        _since = now;
    }

    public void Clear() => _target = null;

    /// <summary>
    /// The requested state (true = connecting, false = disconnecting) while it is still pending, else null.
    /// It ends when the real state reaches the target or the timeout passes.
    /// </summary>
    public bool? Current(bool connected, DateTimeOffset now)
    {
        if (_target is not bool target)
        {
            return null;
        }
        if (connected == target || now - _since >= Timeout)
        {
            _target = null;
            return null;
        }
        return target;
    }
}

public readonly record struct ToggleView(string Label, bool Accent, bool Enabled)
{
    /// <summary>"Connect" (accent) while not connected, "Disconnect" while connected, a disabled "…ing…" while pending.</summary>
    public static ToggleView For(bool connected, bool? pendingTarget, bool canChange)
    {
        if (pendingTarget is bool connecting)
        {
            return new ToggleView(connecting ? "Connecting…" : "Disconnecting…", Accent: false, Enabled: false);
        }
        return new ToggleView(connected ? "Disconnect" : "Connect", Accent: !connected, Enabled: canChange);
    }
}
