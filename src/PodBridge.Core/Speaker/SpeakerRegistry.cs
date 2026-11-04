namespace PodBridge.Core.Speaker;

public enum SpeakerState
{
    /// <summary>Paired, not enabled.</summary>
    Off,

    /// <summary>Enabled: the phone can now choose this PC as a speaker.</summary>
    Ready,

    /// <summary>The audio link is open and sound is playing through the PC.</summary>
    Connected,

    Failed,
}

public sealed record SpeakerEntry(string Id, string Name, SpeakerState State, string Message);

/// <summary>The list of paired devices that can use this PC as a speaker, with their status.</summary>
public sealed class SpeakerRegistry
{
    private readonly Dictionary<string, SpeakerEntry> _entries = new(StringComparer.Ordinal);

    public int Count => _entries.Count;

    public void Upsert(string id, string name)
    {
        string shown = string.IsNullOrWhiteSpace(name) ? "Unnamed device" : name.Trim();
        _entries[id] = _entries.TryGetValue(id, out var old)
            ? old with { Name = shown }
            : new SpeakerEntry(id, shown, SpeakerState.Off, string.Empty);
    }

    public void Remove(string id) => _entries.Remove(id);

    public void SetState(string id, SpeakerState state, string message = "")
    {
        if (_entries.TryGetValue(id, out var old))
        {
            _entries[id] = old with { State = state, Message = message };
        }
    }

    public SpeakerEntry? Find(string id) => _entries.TryGetValue(id, out var e) ? e : null;

    public IReadOnlyList<SpeakerEntry> Snapshot() =>
        _entries.Values.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(e => e.Id, StringComparer.Ordinal).ToList();
}

public static class SpeakerText
{
    public static string Describe(SpeakerState state) => state switch
    {
        SpeakerState.Ready => "Enabled. Start playing on the device and choose this PC, or press Open.",
        SpeakerState.Connected => "Connected. Its sound is playing through this PC.",
        SpeakerState.Failed => "Something went wrong.",
        _ => "Not enabled.",
    };

    public static string Describe(SpeakerEntry entry) =>
        string.IsNullOrWhiteSpace(entry.Message) ? Describe(entry.State) : entry.Message;

    public static bool CanEnable(SpeakerState state) => state is SpeakerState.Off or SpeakerState.Failed;

    public static bool CanOpen(SpeakerState state) => state == SpeakerState.Ready;

    public static bool CanDisable(SpeakerState state) => state is SpeakerState.Ready or SpeakerState.Connected;
}

public static class SpeakerAutoEnable
{
    /// <summary>Devices on the auto-enable list that should be enabled now.</summary>
    public static IReadOnlyList<string> ToEnable(IReadOnlyCollection<string> autoList, IEnumerable<SpeakerEntry> entries) =>
        entries.Where(e => e.State == SpeakerState.Off && autoList.Contains(e.Id, StringComparer.Ordinal))
               .Select(e => e.Id)
               .ToList();

    /// <summary>Adds or removes an id from the list, keeping it tidy. Returns the new list.</summary>
    public static List<string> Toggle(IEnumerable<string> list, string id, bool on)
    {
        var result = list.Where(x => !string.Equals(x, id, StringComparison.Ordinal)).ToList();
        if (on)
        {
            result.Add(id);
        }
        return result;
    }
}
