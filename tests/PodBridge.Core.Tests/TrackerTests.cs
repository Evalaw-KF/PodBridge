using PodBridge.Core.State;
using static PodBridge.Core.Tests.AdvertBuilder;

namespace PodBridge.Core.Tests;

public class TrackerTests
{
    private static readonly DateTimeOffset T0 = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void First_packet_creates_a_snapshot()
    {
        var tracker = new DeviceTracker();
        var result = tracker.Update(Make(), -60, T0);

        Assert.True(result.Accepted);
        Assert.Equal(80, result.Snapshot!.LeftBattery);
        Assert.Equal(-60, result.Snapshot.Rssi);
        Assert.Same(result.Snapshot, tracker.Current);
    }

    [Fact]
    public void Weak_rssi_packets_are_ignored()
    {
        var tracker = new DeviceTracker();
        tracker.Update(Make(left: 80), -60, T0);

        var weak = tracker.Update(Make(left: 10), -95, T0.AddSeconds(1));

        Assert.False(weak.Accepted);
        Assert.Equal(80, tracker.Current!.LeftBattery);
        Assert.Equal(T0, tracker.Current.LastSeen);
    }

    [Fact]
    public void Weak_first_packet_yields_no_snapshot()
    {
        var tracker = new DeviceTracker();
        var result = tracker.Update(Make(), -100, T0);
        Assert.False(result.Accepted);
        Assert.Null(tracker.Current);
    }

    [Fact]
    public void Unknown_value_keeps_last_good_within_timeout()
    {
        var tracker = new DeviceTracker();
        tracker.Update(Make(left: 80), -60, T0);

        var r = tracker.Update(Make(left: null), -60, T0.AddSeconds(10));

        Assert.Equal(80, r.Snapshot!.LeftBattery);
    }

    [Fact]
    public void Unknown_value_becomes_unknown_after_timeout()
    {
        var tracker = new DeviceTracker(new TrackerOptions { KeepLastGood = TimeSpan.FromSeconds(30), StaleAfter = TimeSpan.FromMinutes(5) });
        tracker.Update(Make(left: 80), -60, T0);
        tracker.Update(Make(left: null), -60, T0.AddSeconds(20));

        var r = tracker.Update(Make(left: null), -60, T0.AddSeconds(31));

        Assert.Null(r.Snapshot!.LeftBattery);
    }

    [Fact]
    public void Good_value_refreshes_the_keep_timer()
    {
        var tracker = new DeviceTracker(new TrackerOptions { KeepLastGood = TimeSpan.FromSeconds(30), StaleAfter = TimeSpan.FromMinutes(5) });
        tracker.Update(Make(left: 80), -60, T0);
        tracker.Update(Make(left: 70), -60, T0.AddSeconds(25));

        var r = tracker.Update(Make(left: null), -60, T0.AddSeconds(50));

        Assert.Equal(70, r.Snapshot!.LeftBattery);
    }

    [Fact]
    public void A_single_unknown_packet_does_not_blank_the_ui()
    {
        var tracker = new DeviceTracker();
        tracker.Update(Make(left: 80, right: 70, caseLevel: 60), -60, T0);

        var r = tracker.Update(Make(left: null, right: null, caseLevel: null), -60, T0.AddSeconds(2));

        Assert.Equal(80, r.Snapshot!.LeftBattery);
        Assert.Equal(70, r.Snapshot.RightBattery);
        Assert.Equal(60, r.Snapshot.CaseBattery);
    }

    [Fact]
    public void Rssi_is_smoothed()
    {
        var tracker = new DeviceTracker(new TrackerOptions { RssiSmoothing = 0.5 });
        tracker.Update(Make(), -60, T0);
        var r = tracker.Update(Make(), -80, T0.AddSeconds(1));
        Assert.Equal(-70, r.Snapshot!.Rssi, 3);
    }

    [Fact]
    public void GetCurrent_returns_null_when_stale()
    {
        var tracker = new DeviceTracker(new TrackerOptions { StaleAfter = TimeSpan.FromSeconds(60) });
        tracker.Update(Make(), -60, T0);

        Assert.NotNull(tracker.GetCurrent(T0.AddSeconds(59)));
        Assert.Null(tracker.GetCurrent(T0.AddSeconds(61)));
    }

    [Fact]
    public void Stale_state_does_not_leak_old_batteries_into_a_new_session()
    {
        var tracker = new DeviceTracker(new TrackerOptions { KeepLastGood = TimeSpan.FromMinutes(10), StaleAfter = TimeSpan.FromSeconds(60) });
        tracker.Update(Make(left: 80), -60, T0);

        var r = tracker.Update(Make(left: null), -60, T0.AddMinutes(5));

        Assert.Null(r.Snapshot!.LeftBattery);
    }

    [Fact]
    public void Lid_open_fires_once_on_transition()
    {
        var tracker = new DeviceTracker();
        Assert.False(tracker.Update(Make(lidOpen: false), -60, T0).LidJustOpened);
        Assert.True(tracker.Update(Make(lidOpen: true), -60, T0.AddSeconds(1)).LidJustOpened);
        Assert.False(tracker.Update(Make(lidOpen: true), -60, T0.AddSeconds(2)).LidJustOpened);
    }

    [Fact]
    public void Lid_open_on_first_sighting_counts()
    {
        var tracker = new DeviceTracker();
        Assert.True(tracker.Update(Make(lidOpen: true), -60, T0).LidJustOpened);
    }

    [Fact]
    public void Lid_flicker_is_rate_limited()
    {
        var tracker = new DeviceTracker(new TrackerOptions { LidEventCooldown = TimeSpan.FromSeconds(10) });
        Assert.True(tracker.Update(Make(lidOpen: true), -60, T0).LidJustOpened);
        tracker.Update(Make(lidOpen: false), -60, T0.AddSeconds(1));
        Assert.False(tracker.Update(Make(lidOpen: true), -60, T0.AddSeconds(2)).LidJustOpened);

        tracker.Update(Make(lidOpen: false), -60, T0.AddSeconds(11));
        Assert.True(tracker.Update(Make(lidOpen: true), -60, T0.AddSeconds(12)).LidJustOpened);
    }

    [Fact]
    public void Weaker_different_device_is_ignored_while_current_is_fresh()
    {
        var tracker = new DeviceTracker();
        tracker.Update(Make(model: 0x1420, left: 80), -50, T0);

        var other = tracker.Update(Make(model: 0x0F20, left: 10), -80, T0.AddSeconds(1));

        Assert.False(other.Accepted);
        Assert.Equal(0x1420, tracker.Current!.RawModelId);
    }

    [Fact]
    public void Stronger_different_device_takes_over_and_resets_values()
    {
        var tracker = new DeviceTracker();
        tracker.Update(Make(model: 0x1420, left: 80, caseLevel: 60), -80, T0);

        var other = tracker.Update(Make(model: 0x0F20, left: 30, caseLevel: null), -40, T0.AddSeconds(1));

        Assert.True(other.Accepted);
        Assert.Equal(0x0F20, other.Snapshot!.RawModelId);
        Assert.Equal(30, other.Snapshot.LeftBattery);
        Assert.Null(other.Snapshot.CaseBattery);
    }

    [Fact]
    public void Headphones_report_a_single_battery_as_left()
    {
        var tracker = new DeviceTracker();
        var r = tracker.Update(Make(model: 0x0A20, left: null, right: 60, caseLevel: 40), -60, T0);

        Assert.Equal(60, r.Snapshot!.LeftBattery);
        Assert.Null(r.Snapshot.RightBattery);
        Assert.Null(r.Snapshot.CaseBattery);
    }

    [Fact]
    public void LowestBudBattery_ignores_unknowns()
    {
        var tracker = new DeviceTracker();
        var r = tracker.Update(Make(left: null, right: 40), -60, T0);
        Assert.Equal(40, r.Snapshot!.LowestBudBattery);

        var none = tracker.Update(Make(left: null, right: null), -60, T0.AddSeconds(100));
        Assert.Null(none.Snapshot!.LowestBudBattery);
    }

    [Fact]
    public void Reset_clears_everything()
    {
        var tracker = new DeviceTracker();
        tracker.Update(Make(), -60, T0);
        tracker.Reset();
        Assert.Null(tracker.Current);
    }
}
