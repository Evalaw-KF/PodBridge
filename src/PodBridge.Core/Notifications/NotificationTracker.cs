namespace PodBridge.Core.Notifications;

/// <summary>
/// Turns repeated "list everything currently shown" polls into "what is new". The first poll only
/// records what is already there, so old notifications are never read aloud when listening starts.
/// </summary>
public sealed class NotificationTracker
{
    private readonly HashSet<string> _known = new(StringComparer.Ordinal);
    private bool _baselineDone;

    /// <summary>Returns the notifications not seen in an earlier poll, oldest first as given.</summary>
    public IReadOnlyList<NotificationInfo> Poll(IReadOnlyList<NotificationInfo> current)
    {
        var fresh = new List<NotificationInfo>();
        var now = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in current)
        {
            if (string.IsNullOrEmpty(item.Id) || !now.Add(item.Id))
            {
                continue;
            }
            if (_baselineDone && !_known.Contains(item.Id))
            {
                fresh.Add(item);
            }
        }

        // Forget notifications that have gone, so memory stays small and reused ids still count as new.
        _known.Clear();
        _known.UnionWith(now);
        _baselineDone = true;
        return fresh;
    }

    /// <summary>Starts over: the next poll is a baseline again (used when reading is switched off and on).</summary>
    public void Reset()
    {
        _known.Clear();
        _baselineDone = false;
    }
}

/// <summary>Drops the same message (same app, title and text) arriving again within a short window.</summary>
public sealed class RecentTextDeduper
{
    private readonly Dictionary<string, DateTimeOffset> _seen = new(StringComparer.Ordinal);
    private readonly TimeSpan _window;

    public RecentTextDeduper(TimeSpan? window = null) => _window = window ?? TimeSpan.FromSeconds(20);

    /// <summary>True if the same message was accepted within the window. Otherwise records it and returns false.</summary>
    public bool IsDuplicate(NotificationInfo info, DateTimeOffset now)
    {
        foreach (var stale in _seen.Where(p => now - p.Value > _window).Select(p => p.Key).ToList())
        {
            _seen.Remove(stale);
        }

        string key = $"{info.AppId}\u001f{info.Title.Trim()}\u001f{info.Body.Trim()}";
        if (_seen.ContainsKey(key))
        {
            return true;
        }
        _seen[key] = now;
        return false;
    }
}
