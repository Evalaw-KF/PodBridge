using Microsoft.UI.Dispatching;
using PodBridge.Core.Notifications;
using PodBridge.Core.Settings;

namespace PodBridge.App.Services;

/// <summary>
/// Polls for new notifications and speaks the ones the settings allow. Polling (every 2 seconds) is used rather
/// than the listener's change event because it is one simple code path that fails soft. UI thread only.
/// </summary>
internal sealed class NotificationReader : IDisposable
{
    private readonly DispatcherQueueTimer _timer;
    private readonly Func<AppSettings> _settings;
    private readonly Func<int?> _budsInEar;
    private readonly Action<NotificationInfo> _onSeen;
    private readonly NotificationTracker _tracker = new();
    private readonly NotificationReadPolicy _policy = new();
    private bool _busy;
    private bool _wasActive;

    public NotificationReader(DispatcherQueue dispatcher, Func<AppSettings> settings, Func<int?> budsInEar, Action<NotificationInfo> onSeen)
    {
        _settings = settings;
        _budsInEar = budsInEar;
        _onSeen = onSeen;
        _timer = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(2);
        _timer.Tick += async (_, _) => await TickAsync();
    }

    public NotificationListenerService Listener { get; } = new();
    public SpeechService Speech { get; } = new();

    /// <summary>Call at start-up and whenever settings change.</summary>
    public void Apply()
    {
        var s = _settings();
        Speech.VoiceId = s.SpeechVoiceId;
        Speech.RatePercent = s.SpeechRatePercent;
        Speech.VolumePercent = s.SpeechVolumePercent;

        if (Listener.Access == ListenerAccess.Unknown)
        {
            Listener.Probe();
        }

        bool active = s.ReadNotificationsAloud && Listener.Access == ListenerAccess.Allowed;
        if (active && !_wasActive)
        {
            _tracker.Reset(); // start from "what is on screen now": never read old notifications
            _timer.Start();
        }
        else if (!active && _wasActive)
        {
            _timer.Stop();
            Speech.Stop();
        }
        _wasActive = active;
    }

    private async Task TickAsync()
    {
        if (_busy)
        {
            return;
        }
        _busy = true;
        try
        {
            var settings = _settings();
            if (!settings.ReadNotificationsAloud)
            {
                return;
            }

            var current = await Listener.GetCurrentAsync();
            if (current is null)
            {
                Apply(); // access was lost: stop polling
                return;
            }

            var now = DateTimeOffset.UtcNow;
            foreach (var info in _tracker.Poll(current))
            {
                _onSeen(info);
                var context = new ReadContext(true, settings.ReadOnlyWhenInEar, _budsInEar(), settings.NotificationAllowedApps);
                if (_policy.Evaluate(info, context, now) == ReadDecision.Read)
                {
                    Speech.Speak(SpeechFormatter.Format(info, settings.NotificationReadFullText));
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("Reading notifications failed", ex);
        }
        finally
        {
            _busy = false;
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        Speech.Dispose();
    }
}
