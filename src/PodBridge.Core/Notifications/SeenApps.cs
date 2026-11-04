namespace PodBridge.Core.Notifications;

/// <summary>Keeps the list of apps seen so far, for the allow-list picker in Settings.</summary>
public static class SeenApps
{
    public const int MaxApps = 100;

    /// <summary>Adds the app, or updates its name. Returns true if the list changed.</summary>
    public static bool Record(List<SeenApp> list, string appId, string appName)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return false;
        }
        appId = appId.Trim();
        string name = string.IsNullOrWhiteSpace(appName) ? appId : appName.Trim();

        var existing = list.FirstOrDefault(a => string.Equals(a.Id, appId, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            if (existing.Name == name)
            {
                return false;
            }
            existing.Name = name;
            return true;
        }

        if (list.Count >= MaxApps)
        {
            list.RemoveAt(0); // forget the oldest
        }
        list.Add(new SeenApp { Id = appId, Name = name });
        return true;
    }

    /// <summary>Alphabetical by name, as shown to people.</summary>
    public static IReadOnlyList<SeenApp> Sorted(IEnumerable<SeenApp> apps) =>
        apps.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
}
