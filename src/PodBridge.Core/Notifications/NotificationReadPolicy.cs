namespace PodBridge.Core.Notifications;

public enum ReadDecision
{
    Read,
    Disabled,
    NotInEar,
    AppNotAllowed,
    OwnApp,
    Empty,
    Duplicate,
}

/// <summary>The settings and live facts a read-aloud decision depends on.</summary>
/// <param name="BudsInEar">Earbuds in ear right now; null when no AirPods have been heard recently.</param>
public sealed record ReadContext(
    bool Enabled,
    bool OnlyWhenInEar,
    int? BudsInEar,
    IReadOnlyCollection<string> AllowedAppIds);

/// <summary>Decides whether a notification should be spoken. Pure logic apart from the deduper's memory.</summary>
public sealed class NotificationReadPolicy
{
    public const string OwnAppName = "PodBridge";

    private readonly RecentTextDeduper _deduper = new();

    public ReadDecision Evaluate(NotificationInfo info, ReadContext context, DateTimeOffset now)
    {
        if (!context.Enabled)
        {
            return ReadDecision.Disabled;
        }
        if (string.Equals(info.AppName?.Trim(), OwnAppName, StringComparison.OrdinalIgnoreCase))
        {
            return ReadDecision.OwnApp; // never read our own low-battery alerts back
        }
        if (context.OnlyWhenInEar && (context.BudsInEar ?? 0) <= 0)
        {
            return ReadDecision.NotInEar;
        }
        if (!context.AllowedAppIds.Contains(info.AppId, StringComparer.OrdinalIgnoreCase))
        {
            return ReadDecision.AppNotAllowed;
        }
        if (string.IsNullOrWhiteSpace(info.Title) && string.IsNullOrWhiteSpace(info.Body))
        {
            return ReadDecision.Empty;
        }
        return _deduper.IsDuplicate(info, now) ? ReadDecision.Duplicate : ReadDecision.Read;
    }
}
