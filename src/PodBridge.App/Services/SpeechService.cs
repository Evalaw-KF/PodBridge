using PodBridge.Core.Notifications;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace PodBridge.App.Services;

internal sealed record VoiceChoice(string Id, string Name);

/// <summary>
/// Speaks text with the voices installed on this PC (SpeechSynthesizer.AllVoices lists local voices only,
/// so nothing is sent to the internet). Utterances play one after another through a short queue.
/// </summary>
internal sealed class SpeechService : IDisposable
{
    private readonly SpeechQueue _queue = new(5);
    private bool _pumping;
    private bool _disposed;
    private MediaPlayer? _player;

    public string VoiceId { get; set; } = string.Empty;
    public int RatePercent { get; set; } = 100;
    public int VolumePercent { get; set; } = 80;

    /// <summary>Installed voices, or an empty list if Windows cannot list them.</summary>
    public static IReadOnlyList<VoiceChoice> ListVoices()
    {
        try
        {
            return SpeechSynthesizer.AllVoices
                .Select(v => new VoiceChoice(v.Id, $"{v.DisplayName} ({v.Language})"))
                .OrderBy(v => v.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            Log.Error("Could not list voices", ex);
            return Array.Empty<VoiceChoice>();
        }
    }

    /// <summary>Queues text. Call on the UI thread.</summary>
    public void Speak(string text)
    {
        if (_disposed)
        {
            return;
        }
        _queue.Enqueue(text);
        if (!_pumping)
        {
            _pumping = true;
            _ = PumpAsync();
        }
    }

    public void Stop()
    {
        _queue.Clear();
        try
        {
            _player?.Pause();
        }
        catch (Exception ex)
        {
            Log.Error("Could not stop speech", ex);
        }
    }

    private async Task PumpAsync()
    {
        try
        {
            while (!_disposed && _queue.TryDequeue(out var text))
            {
                try
                {
                    await SpeakOneAsync(text);
                }
                catch (Exception ex)
                {
                    Log.Error("Speech failed", ex); // carry on with the next one
                }
            }
        }
        finally
        {
            _pumping = false;
        }
    }

    private async Task SpeakOneAsync(string text)
    {
        using var synth = new SpeechSynthesizer();
        if (!string.IsNullOrEmpty(VoiceId))
        {
            var voice = SpeechSynthesizer.AllVoices.FirstOrDefault(v => v.Id == VoiceId);
            if (voice is not null)
            {
                synth.Voice = voice;
            }
        }
        synth.Options.SpeakingRate = Math.Clamp(RatePercent, 50, 200) / 100.0;

        using var stream = await synth.SynthesizeTextToStreamAsync(text);
        var done = new TaskCompletionSource();
        using var player = new MediaPlayer { Volume = Math.Clamp(VolumePercent, 0, 100) / 100.0 };
        using var source = MediaSource.CreateFromStream(stream, stream.ContentType);
        player.MediaEnded += (_, _) => done.TrySetResult();
        player.MediaFailed += (_, e) =>
        {
            Log.Error($"Speech playback failed: {e.ErrorMessage}");
            done.TrySetResult();
        };
        player.Source = source;
        _player = player;
        try
        {
            player.Play();
            await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromSeconds(60)));
        }
        finally
        {
            _player = null;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        Stop();
    }
}
