namespace PodBridge.Core.Audio;

public enum SwitchKind
{
    None,
    ToFallback,
    ToAirPods,
}

public readonly record struct SwitchDecision(SwitchKind Kind, string? TargetId)
{
    public static readonly SwitchDecision None = new(SwitchKind.None, null);
}

/// <summary>
/// Decides when to move the default output away from the AirPods (both buds out) and back again.
/// Holds only a little memory; all Windows calls are made by the caller.
/// </summary>
public sealed class OutputSwitchPolicy
{
    private string? _lastNonAirPodsDefault;
    private string? _awayTo;

    /// <summary>Master switch for automatic switching.</summary>
    public bool Enabled { get; set; }

    /// <summary>The speaker the user picked in Settings, or null/empty for "the output used before the AirPods".</summary>
    public string? ConfiguredFallbackId { get; set; }

    /// <summary>The last default output that was not the AirPods. Used as the automatic fallback.</summary>
    public string? LearnedFallbackId => _lastNonAirPodsDefault;

    /// <summary>True while we are holding the output away from the AirPods and plan to switch back.</summary>
    public bool IsAway => _awayTo is not null;

    /// <summary>Call whenever the default output changes (and once at start-up) so the previous output is remembered.</summary>
    public void ObserveDefault(OutputContext ctx)
    {
        if (ctx.DefaultId is not null && !ctx.DefaultIsAirPods)
        {
            _lastNonAirPodsDefault = ctx.DefaultId;
        }
    }

    /// <summary>The speaker to use when leaving the AirPods: the configured one if usable, else the remembered one, else null.</summary>
    public string? ResolveFallback(OutputContext ctx)
    {
        if (!string.IsNullOrEmpty(ConfiguredFallbackId) && ctx.IsUsableSpeaker(ConfiguredFallbackId))
        {
            return ConfiguredFallbackId;
        }
        return ctx.IsUsableSpeaker(_lastNonAirPodsDefault) ? _lastNonAirPodsDefault : null;
    }

    /// <summary>
    /// Call when the debounced number of buds in ear changes.
    /// 0 buds while the AirPods are the default: switch to the fallback.
    /// 1 or more buds after we switched away: switch back, if the AirPods endpoint is available.
    /// </summary>
    public SwitchDecision OnBudsChanged(int budsInEar, OutputContext ctx)
    {
        if (!Enabled)
        {
            _awayTo = null;
            return SwitchDecision.None;
        }

        if (budsInEar <= 0)
        {
            if (ctx.DefaultIsAirPods && ResolveFallback(ctx) is { } fallback)
            {
                _awayTo = fallback;
                return new SwitchDecision(SwitchKind.ToFallback, fallback);
            }
            return SwitchDecision.None;
        }

        return TrySwitchBack(ctx);
    }

    /// <summary>
    /// Call when the endpoints or the default output change while buds are in the ears
    /// (for example the AirPods endpoint has just become active). Only ever switches back.
    /// </summary>
    public SwitchDecision OnEnvironmentChanged(int budsInEar, OutputContext ctx)
    {
        if (!Enabled || budsInEar <= 0)
        {
            return SwitchDecision.None;
        }
        return TrySwitchBack(ctx);
    }

    private SwitchDecision TrySwitchBack(OutputContext ctx)
    {
        if (_awayTo is null)
        {
            return SwitchDecision.None;
        }
        if (ctx.DefaultId != _awayTo)
        {
            // The user (or another app) chose something else meanwhile: leave their choice alone.
            _awayTo = null;
            return SwitchDecision.None;
        }
        if (ctx.AirPods is { IsActive: true } airPods)
        {
            _awayTo = null;
            return new SwitchDecision(SwitchKind.ToAirPods, airPods.Id);
        }
        // AirPods endpoint not available yet: keep waiting.
        return SwitchDecision.None;
    }

    /// <summary>Forget any pending switch-back, for example after the user switches by hand.</summary>
    public void ClearAway() => _awayTo = null;

    /// <summary>The target of the manual "switch output" action: AirPods to fallback, anything else to AirPods.</summary>
    public SwitchDecision ToggleTarget(OutputContext ctx)
    {
        if (ctx.DefaultIsAirPods)
        {
            return ResolveFallback(ctx) is { } fb ? new SwitchDecision(SwitchKind.ToFallback, fb) : SwitchDecision.None;
        }
        return ctx.AirPods is { IsActive: true } airPods
            ? new SwitchDecision(SwitchKind.ToAirPods, airPods.Id)
            : SwitchDecision.None;
    }

    /// <summary>True when the keep-alive silence should be playing: wanted, and the AirPods are the default output.</summary>
    public static bool ShouldKeepAlive(bool enabled, OutputContext ctx) =>
        enabled && ctx.DefaultIsAirPods && ctx.Find(ctx.DefaultId) is { IsActive: true };
}
