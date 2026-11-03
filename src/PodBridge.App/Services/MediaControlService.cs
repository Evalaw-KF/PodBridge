using Windows.Media.Control;
using PodBridge.App.Native;

namespace PodBridge.App.Services;

/// <summary>
/// Pauses and resumes whatever is playing, through the Windows media transport controls (the same system
/// that drives the volume flyout's media card). Only sessions that were actually playing are paused,
/// and only those are resumed. If there is no media session at all, falls back to the Play/Pause media key.
/// Use from the UI thread.
/// </summary>
internal sealed class MediaControlService
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private readonly List<GlobalSystemMediaTransportControlsSession> _pausedByUs = new();
    private bool _pausedWithKey;

    /// <summary>Pauses what is playing. Returns true if something was paused (or the key was sent).</summary>
    public async Task<bool> PauseAsync()
    {
        _pausedByUs.Clear();
        _pausedWithKey = false;

        var manager = await GetManagerAsync();
        if (manager is null)
        {
            return false;
        }

        var sessions = manager.GetSessions();
        if (sessions.Count == 0)
        {
            // Nothing registered with Windows: we cannot tell if audio is playing. Pressing the
            // Play/Pause key here could START playback when nothing was playing, so do nothing.
            return false;
        }

        foreach (var session in sessions)
        {
            try
            {
                if (session.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
                    && await session.TryPauseAsync())
                {
                    _pausedByUs.Add(session);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not pause a media session", ex);
            }
        }
        return _pausedByUs.Count > 0;
    }

    /// <summary>Resumes what <see cref="PauseAsync"/> paused. Sessions the user has since changed are left alone.</summary>
    public async Task ResumeAsync()
    {
        if (_pausedWithKey)
        {
            _pausedWithKey = false;
            SendPlayPauseKey();
            return;
        }

        var sessions = _pausedByUs.ToList();
        _pausedByUs.Clear();
        foreach (var session in sessions)
        {
            try
            {
                if (session.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused)
                {
                    await session.TryPlayAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not resume a media session", ex);
            }
        }
    }

    private async Task<GlobalSystemMediaTransportControlsSessionManager?> GetManagerAsync()
    {
        try
        {
            return _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        }
        catch (Exception ex)
        {
            Log.Error("Media controls are not available", ex);
            return null;
        }
    }

    private static void SendPlayPauseKey()
    {
        NativeMethods.keybd_event(NativeMethods.VK_MEDIA_PLAY_PAUSE, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VK_MEDIA_PLAY_PAUSE, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
    }
}
