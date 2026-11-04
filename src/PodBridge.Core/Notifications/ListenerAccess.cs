namespace PodBridge.Core.Notifications;

/// <summary>Where the "read notifications" permission stands.</summary>
public enum ListenerAccess
{
    /// <summary>Not checked yet.</summary>
    Unknown,

    /// <summary>Windows would not let this app use the notification listener at all (it needs a packaged app identity).</summary>
    Unavailable,

    /// <summary>Windows has not asked the user yet; the Allow button asks.</summary>
    NeedsRequest,

    Allowed,

    /// <summary>The user said no in Windows Settings.</summary>
    Denied,
}

public static class ListenerAccessText
{
    public static string Describe(ListenerAccess access, bool hasPackageIdentity) => access switch
    {
        ListenerAccess.Allowed => "Windows has allowed PodBridge to read your notifications.",
        ListenerAccess.NeedsRequest => "Windows has not been asked yet. Press \"Allow access\" and answer the Windows prompt.",
        ListenerAccess.Denied => "Access was refused. Open Windows notification settings and allow PodBridge, then come back.",
        ListenerAccess.Unavailable => hasPackageIdentity
            ? "Windows would not let PodBridge read notifications on this PC."
            : "Not available in this version. Windows only lets apps that are installed as a \"packaged\" app read other apps' notifications, " +
              "and this download is a plain unzip-and-run app. Everything else in PodBridge works as normal.",
        _ => "Checking…",
    };

    public static bool CanRequest(ListenerAccess access) => access is ListenerAccess.NeedsRequest;

    public static bool ShowOpenSettings(ListenerAccess access) => access is ListenerAccess.Denied;

    /// <summary>True if the read-aloud controls can be used at all.</summary>
    public static bool FeatureUsable(ListenerAccess access) => access != ListenerAccess.Unavailable;
}
