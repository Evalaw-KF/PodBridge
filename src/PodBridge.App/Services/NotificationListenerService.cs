using PodBridge.Core.Notifications;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace PodBridge.App.Services;

/// <summary>
/// Thin wrapper over UserNotificationListener. Windows only lets apps with a package identity use it, and this
/// app is an unpackaged zip, so every call is guarded: on any failure the feature reports "unavailable"
/// (with an explanation in Settings) instead of throwing.
/// </summary>
internal sealed class NotificationListenerService
{
    public ListenerAccess Access { get; private set; } = ListenerAccess.Unknown;

    public bool HasPackageIdentity { get; } = DetectIdentity();

    private static bool DetectIdentity()
    {
        try
        {
            _ = Windows.ApplicationModel.Package.Current.Id; // throws when the app has no package identity
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Reads the current permission state without asking the user anything.</summary>
    public ListenerAccess Probe()
    {
        try
        {
            var status = UserNotificationListener.Current.GetAccessStatus();
            Access = ListenerAccessText.FromStatus((int)status, HasPackageIdentity);
        }
        catch (Exception ex)
        {
            Log.Error("Notification listener is unavailable", ex);
            Access = ListenerAccess.Unavailable;
        }
        return Access;
    }

    /// <summary>Shows Windows' permission prompt. Must be called on the UI thread.</summary>
    public async Task<ListenerAccess> RequestAccessAsync()
    {
        if (!ListenerAccessText.CanRequest(Access) && Probe() != ListenerAccess.NeedsRequest)
        {
            return Access;
        }

        try
        {
            var status = await UserNotificationListener.Current.RequestAccessAsync();
            Access = ListenerAccessText.FromStatus((int)status, HasPackageIdentity);
        }
        catch (Exception ex)
        {
            Log.Error("Could not ask for notification access", ex);
            Access = ListenerAccess.Unavailable;
        }
        return Access;
    }

    /// <summary>The toast notifications currently in the action centre, or null if they cannot be read.</summary>
    public async Task<IReadOnlyList<NotificationInfo>?> GetCurrentAsync()
    {
        try
        {
            var items = await UserNotificationListener.Current.GetNotificationsAsync(NotificationKinds.Toast);
            var result = new List<NotificationInfo>(items.Count);
            foreach (var item in items)
            {
                try
                {
                    result.Add(Convert(item));
                }
                catch (Exception ex)
                {
                    Log.Error("Could not read one notification", ex); // skip just this one
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            Log.Error("Could not read notifications", ex);
            Access = ListenerAccess.Unavailable;
            return null;
        }
    }

    private static NotificationInfo Convert(UserNotification item)
    {
        string appId = string.Empty;
        string appName = string.Empty;
        try
        {
            appId = item.AppInfo?.AppUserModelId ?? string.Empty;
            appName = item.AppInfo?.DisplayInfo?.DisplayName ?? string.Empty;
        }
        catch (Exception ex)
        {
            Log.Error("Could not read a notification's app", ex);
        }

        string title = string.Empty;
        string body = string.Empty;
        var binding = item.Notification?.Visual?.GetBinding(KnownNotificationBindings.ToastGeneric);
        if (binding is not null)
        {
            var texts = binding.GetTextElements().Select(t => t.Text).ToList();
            title = texts.Count > 0 ? texts[0] : string.Empty;
            body = string.Join(" ", texts.Skip(1));
        }
        return new NotificationInfo(item.Id.ToString(), appId, appName, title, body);
    }
}
