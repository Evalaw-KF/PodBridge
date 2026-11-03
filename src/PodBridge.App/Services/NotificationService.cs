using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace PodBridge.App.Services;

/// <summary>Windows toast notifications through the Windows App SDK.</summary>
internal sealed class NotificationService : IDisposable
{
    private bool _registered;

    public void Initialize()
    {
        try
        {
            AppNotificationManager.Default.NotificationInvoked += (_, _) => { /* nothing to do on click */ };
            AppNotificationManager.Default.Register();
            _registered = true;
        }
        catch (Exception ex)
        {
            Log.Error("Could not register for notifications", ex);
        }
    }

    public void Show(string title, string message)
    {
        if (!_registered)
        {
            return;
        }

        try
        {
            var notification = new AppNotificationBuilder()
                .AddText(title)
                .AddText(message)
                .BuildNotification();
            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            Log.Error("Could not show a notification", ex);
        }
    }

    public void Dispose()
    {
        if (_registered)
        {
            try { AppNotificationManager.Default.Unregister(); } catch { /* shutting down */ }
            _registered = false;
        }
    }
}
