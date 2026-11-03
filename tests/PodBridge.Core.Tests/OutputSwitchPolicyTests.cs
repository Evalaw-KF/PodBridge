using PodBridge.Core.Audio;

namespace PodBridge.Core.Tests;

public sealed class OutputSwitchPolicyTests
{
    private const string Pods = "{pods}", PodsCall = "{pods-call}", Speakers = "{speakers}", Monitor = "{monitor}";

    private static EndpointInfo Ep(string id, string name, EndpointState s = EndpointState.Active) => new(id, name, s);

    private static OutputContext Ctx(string? defaultId, EndpointState podsState = EndpointState.Active, bool monitor = true)
    {
        var list = new List<EndpointInfo>
        {
            Ep(Speakers, "Speakers (Realtek)"),
            Ep(Pods, "Headphones (Sam's AirPods Pro)", podsState),
            Ep(PodsCall, "Headset (Sam's AirPods Pro)", podsState),
        };
        if (monitor)
        {
            list.Add(Ep(Monitor, "DELL U2720Q (NVIDIA)"));
        }
        return new OutputContext(list, defaultId, "AirPods");
    }

    private static OutputSwitchPolicy Policy(bool enabled = true) => new() { Enabled = enabled };

    // ---- matching ----

    [Theory]
    [InlineData("Headphones (AirPods Pro)", "airpods", true)]
    [InlineData("Headphones (Sam’s AirPods Pro)", "Sam's", true)]
    [InlineData("Speakers (Realtek)", "AirPods", false)]
    [InlineData("Headphones (AirPods Pro)", "", false)]
    [InlineData("Headphones (AirPods Pro)", "   ", false)]
    [InlineData(null, "AirPods", false)]
    public void Matches_names(string? name, string pattern, bool expected) =>
        Assert.Equal(expected, EndpointMatcher.IsMatch(name, pattern));

    [Fact]
    public void Pick_prefers_active_music_profile()
    {
        var list = new[]
        {
            Ep("a", "Headset (AirPods Pro)"),
            Ep("b", "Headphones (AirPods Pro)", EndpointState.Unplugged),
            Ep("c", "Headphones (AirPods Pro)"),
        };
        Assert.Equal("c", EndpointMatcher.Pick(list, "AirPods")!.Id);
    }

    [Fact]
    public void Pick_finds_an_unplugged_endpoint_when_nothing_is_active()
    {
        var list = new[] { Ep("b", "Headphones (AirPods Pro)", EndpointState.Unplugged), Ep("s", "Speakers") };
        var pick = EndpointMatcher.Pick(list, "AirPods");
        Assert.Equal("b", pick!.Id);
        Assert.True(pick.IsUnplugged);
    }

    [Fact]
    public void Pick_ignores_disabled_and_missing_endpoints()
    {
        var list = new[]
        {
            Ep("d", "Headphones (AirPods)", EndpointState.Disabled),
            Ep("n", "Headphones (AirPods)", EndpointState.NotPresent),
        };
        Assert.Null(EndpointMatcher.Pick(list, "AirPods"));
    }

    [Fact]
    public void Context_treats_either_profile_as_the_AirPods_default()
    {
        Assert.True(Ctx(Pods).DefaultIsAirPods);
        Assert.True(Ctx(PodsCall).DefaultIsAirPods);
        Assert.False(Ctx(Speakers).DefaultIsAirPods);
        Assert.False(Ctx(null).DefaultIsAirPods);
        Assert.Equal(Pods, Ctx(Speakers).AirPods!.Id);
    }

    // ---- fallback choice ----

    [Fact]
    public void Remembers_the_output_used_before_the_AirPods()
    {
        var p = Policy();
        p.ObserveDefault(Ctx(Speakers));
        p.ObserveDefault(Ctx(Pods));
        Assert.Equal(Speakers, p.LearnedFallbackId);
        Assert.Equal(Speakers, p.ResolveFallback(Ctx(Pods)));
    }

    [Fact]
    public void Configured_fallback_wins_over_the_remembered_one()
    {
        var p = Policy();
        p.ObserveDefault(Ctx(Speakers));
        p.ConfiguredFallbackId = Monitor;
        Assert.Equal(Monitor, p.ResolveFallback(Ctx(Pods)));
    }

    [Fact]
    public void Missing_configured_fallback_falls_back_to_the_remembered_one()
    {
        var p = Policy();
        p.ObserveDefault(Ctx(Speakers));
        p.ConfiguredFallbackId = Monitor;
        Assert.Equal(Speakers, p.ResolveFallback(Ctx(Pods, monitor: false)));
    }

    [Fact]
    public void No_fallback_when_nothing_is_known()
    {
        var p = Policy();
        Assert.Null(p.ResolveFallback(Ctx(Pods)));
    }

    [Fact]
    public void A_configured_fallback_that_is_the_AirPods_is_not_used()
    {
        var p = Policy();
        p.ConfiguredFallbackId = Pods;
        Assert.Null(p.ResolveFallback(Ctx(Pods)));
    }

    // ---- switching away / back ----

    [Fact]
    public void Switches_to_the_fallback_when_both_buds_are_out()
    {
        var p = Policy();
        p.ObserveDefault(Ctx(Speakers));
        var d = p.OnBudsChanged(0, Ctx(Pods));
        Assert.Equal(new SwitchDecision(SwitchKind.ToFallback, Speakers), d);
        Assert.True(p.IsAway);
    }

    [Fact]
    public void Does_nothing_when_disabled()
    {
        var p = Policy(enabled: false);
        p.ObserveDefault(Ctx(Speakers));
        Assert.Equal(SwitchKind.None, p.OnBudsChanged(0, Ctx(Pods)).Kind);
        Assert.False(p.IsAway);
    }

    [Fact]
    public void Does_nothing_when_the_default_is_not_the_AirPods()
    {
        var p = Policy();
        p.ObserveDefault(Ctx(Speakers));
        Assert.Equal(SwitchKind.None, p.OnBudsChanged(0, Ctx(Monitor)).Kind);
    }

    [Fact]
    public void Does_nothing_when_there_is_no_fallback()
    {
        var p = Policy();
        Assert.Equal(SwitchKind.None, p.OnBudsChanged(0, Ctx(Pods)).Kind);
        Assert.False(p.IsAway);
    }

    [Fact]
    public void One_bud_in_ear_never_switches_away()
    {
        var p = Policy();
        p.ObserveDefault(Ctx(Speakers));
        Assert.Equal(SwitchKind.None, p.OnBudsChanged(1, Ctx(Pods)).Kind);
    }

    [Fact]
    public void Switches_back_when_a_bud_returns()
    {
        var p = Policy();
        p.ObserveDefault(Ctx(Speakers));
        p.OnBudsChanged(0, Ctx(Pods));
        var d = p.OnBudsChanged(1, Ctx(Speakers));
        Assert.Equal(new SwitchDecision(SwitchKind.ToAirPods, Pods), d);
        Assert.False(p.IsAway);
    }

    [Fact]
    public void Waits_for_the_AirPods_endpoint_to_appear_then_switches_back()
    {
        var p = Policy();
        p.ObserveDefault(Ctx(Speakers));
        p.OnBudsChanged(0, Ctx(Pods));

        var gone = Ctx(Speakers, EndpointState.Unplugged);
        Assert.Equal(SwitchKind.None, p.OnBudsChanged(2, gone).Kind);
        Assert.True(p.IsAway);

        var d = p.OnEnvironmentChanged(2, Ctx(Speakers));
        Assert.Equal(new SwitchDecision(SwitchKind.ToAirPods, Pods), d);
    }

    [Fact]
    public void Does_not_switch_back_if_we_never_switched_away()
    {
        var p = Policy();
        p.ObserveDefault(Ctx(Speakers));
        Assert.Equal(SwitchKind.None, p.OnBudsChanged(2, Ctx(Speakers)).Kind);
    }

    [Fact]
    public void Leaves_a_manual_choice_alone()
    {
        var p = Policy();
        p.ObserveDefault(Ctx(Speakers));
        p.OnBudsChanged(0, Ctx(Pods));
        // The user moved to the monitor meanwhile.
        Assert.Equal(SwitchKind.None, p.OnBudsChanged(2, Ctx(Monitor)).Kind);
        Assert.False(p.IsAway);
    }

    [Fact]
    public void Environment_change_with_buds_out_never_switches()
    {
        var p = Policy();
        p.ObserveDefault(Ctx(Speakers));
        p.OnBudsChanged(0, Ctx(Pods));
        Assert.Equal(SwitchKind.None, p.OnEnvironmentChanged(0, Ctx(Speakers)).Kind);
        Assert.True(p.IsAway);
    }

    [Fact]
    public void User_choosing_the_AirPods_with_buds_out_is_not_overridden_by_environment_events()
    {
        var p = Policy();
        p.ObserveDefault(Ctx(Speakers));
        Assert.Equal(SwitchKind.None, p.OnEnvironmentChanged(0, Ctx(Pods)).Kind);
    }

    [Fact]
    public void Disabling_forgets_a_pending_switch_back()
    {
        var p = Policy();
        p.ObserveDefault(Ctx(Speakers));
        p.OnBudsChanged(0, Ctx(Pods));
        p.Enabled = false;
        Assert.Equal(SwitchKind.None, p.OnBudsChanged(2, Ctx(Speakers)).Kind);
        Assert.False(p.IsAway);
    }

    // ---- manual toggle and keep-alive ----

    [Fact]
    public void Toggle_goes_from_AirPods_to_the_fallback_and_back()
    {
        var p = Policy(enabled: false);
        p.ObserveDefault(Ctx(Speakers));
        Assert.Equal(new SwitchDecision(SwitchKind.ToFallback, Speakers), p.ToggleTarget(Ctx(Pods)));
        Assert.Equal(new SwitchDecision(SwitchKind.ToAirPods, Pods), p.ToggleTarget(Ctx(Speakers)));
    }

    [Fact]
    public void Toggle_does_nothing_without_an_active_AirPods_endpoint()
    {
        var p = Policy();
        p.ObserveDefault(Ctx(Speakers));
        Assert.Equal(SwitchKind.None, p.ToggleTarget(Ctx(Speakers, EndpointState.Unplugged)).Kind);
    }

    [Fact]
    public void Toggle_does_nothing_without_a_fallback()
    {
        Assert.Equal(SwitchKind.None, Policy().ToggleTarget(Ctx(Pods)).Kind);
    }

    [Fact]
    public void Keep_alive_runs_only_when_enabled_and_the_AirPods_are_the_active_default()
    {
        Assert.True(OutputSwitchPolicy.ShouldKeepAlive(true, Ctx(Pods)));
        Assert.False(OutputSwitchPolicy.ShouldKeepAlive(false, Ctx(Pods)));
        Assert.False(OutputSwitchPolicy.ShouldKeepAlive(true, Ctx(Speakers)));
        Assert.False(OutputSwitchPolicy.ShouldKeepAlive(true, Ctx(null)));
        Assert.False(OutputSwitchPolicy.ShouldKeepAlive(true, Ctx(Pods, EndpointState.Unplugged)));
    }
}
