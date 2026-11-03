namespace PodBridge.Core.Ear;

public enum EarAction
{
    None,
    Pause,
    Resume,
}

/// <summary>Result of feeding the policy a reading or a timer tick.</summary>
/// <param name="Action">What to do to the media player.</param>
/// <param name="StableChanged">True when the debounced number of buds in ear just changed.</param>
/// <param name="StableCount">The debounced number of buds in ear (null until the first reading).</param>
public readonly record struct EarResult(EarAction Action, bool StableChanged, int? StableCount)
{
    public static EarResult Nothing(int? stable) => new(EarAction.None, false, stable);
}

/// <summary>
/// Turns noisy in-ear readings into pause/resume actions.
/// A change must last for <see cref="Debounce"/> before it counts, to ride out Bluetooth flicker.
/// Resume only happens if this policy asked for the pause. Time is passed in, so tests need no clock.
/// </summary>
public sealed class EarDetectionPolicy
{
    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(1500);

    private int? _stable;
    private int? _pending;
    private DateTimeOffset _pendingSince;
    private bool _pausedByUs;

    public TimeSpan Debounce { get; set; } = DefaultDebounce;
    public bool AutoPause { get; set; } = true;
    public bool Resume { get; set; } = true;

    /// <summary>For people who wear only one bud: playback is paused when none is in, and resumes with one.</summary>
    public bool OnlyUseOneBud { get; set; }

    public int? StableCount => _stable;
    public bool PausedByUs => _pausedByUs;
    public bool HasPendingChange => _pending is not null;

    /// <summary>Feeds a new reading (number of buds in ear, 0 to 2).</summary>
    public EarResult Update(int budsInEar, DateTimeOffset now)
    {
        budsInEar = Math.Clamp(budsInEar, 0, 2);

        if (_stable is null)
        {
            // The first reading is only the starting point: it triggers no action and is not reported as a change.
            _stable = budsInEar;
            _pending = null;
            return EarResult.Nothing(budsInEar);
        }

        if (budsInEar == _stable)
        {
            _pending = null;
        }
        else if (_pending != budsInEar)
        {
            _pending = budsInEar;
            _pendingSince = now;
        }
        return Tick(now);
    }

    /// <summary>Call regularly (for example twice a second) so a settled change is noticed even without new readings.</summary>
    public EarResult Tick(DateTimeOffset now)
    {
        if (_pending is not int next || now - _pendingSince < Debounce)
        {
            return EarResult.Nothing(_stable);
        }

        int previous = _stable ?? next;
        _stable = next;
        _pending = null;
        return new EarResult(Decide(previous, next), true, next);
    }

    private EarAction Decide(int previous, int current)
    {
        int needed = OnlyUseOneBud ? 1 : 2;

        if (previous >= needed && current < needed)
        {
            if (AutoPause)
            {
                _pausedByUs = true; // optimistic; call PauseDidNothing if nothing was playing
                return EarAction.Pause;
            }
            return EarAction.None;
        }

        if (current >= needed && previous < needed && _pausedByUs)
        {
            _pausedByUs = false;
            return Resume ? EarAction.Resume : EarAction.None;
        }

        return EarAction.None;
    }

    /// <summary>The caller found nothing playing, so there is nothing to resume later.</summary>
    public void PauseDidNothing() => _pausedByUs = false;

    /// <summary>Forget everything, for example when the device disappears.</summary>
    public void Reset()
    {
        _stable = null;
        _pending = null;
        _pausedByUs = false;
    }
}
