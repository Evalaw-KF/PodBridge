using PodBridge.Core.Alerts;
using PodBridge.Core.State;
using static PodBridge.Core.Tests.AdvertBuilder;

namespace PodBridge.Core.Tests;

public class LowBatteryTests
{
    private static readonly DateTimeOffset T0 = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static DeviceSnapshot Snap(int? left = 80, int? right = 80, int? caseLevel = 80,
        bool lc = false, bool rc = false, bool cc = false, ushort model = 0x1420)
    {
        var tracker = new DeviceTracker();
        return tracker.Update(Make(model: model, left: left, right: right, caseLevel: caseLevel,
            leftCharging: lc, rightCharging: rc, caseCharging: cc), -50, T0).Snapshot!;
    }

    [Fact]
    public void No_alert_above_threshold()
    {
        var m = new LowBatteryMonitor(20);
        Assert.Empty(m.Evaluate(Snap(left: 30, right: 50, caseLevel: 90)));
    }

    [Fact]
    public void Alert_at_threshold_exactly()
    {
        var m = new LowBatteryMonitor(20);
        var alerts = m.Evaluate(Snap(left: 20));
        Assert.Single(alerts);
        Assert.Equal(BatteryComponent.Left, alerts[0].Component);
        Assert.Equal(20, alerts[0].Level);
    }

    [Fact]
    public void Fires_once_not_repeatedly()
    {
        var m = new LowBatteryMonitor(20);
        Assert.Single(m.Evaluate(Snap(left: 20)));
        Assert.Empty(m.Evaluate(Snap(left: 20)));
        Assert.Empty(m.Evaluate(Snap(left: 10)));
        Assert.Empty(m.Evaluate(Snap(left: 0)));
    }

    [Fact]
    public void Rearms_after_rising_above_threshold_plus_hysteresis()
    {
        var m = new LowBatteryMonitor(20, hysteresis: 5);
        Assert.Single(m.Evaluate(Snap(left: 20)));
        Assert.Empty(m.Evaluate(Snap(left: 20)));
        m.Evaluate(Snap(left: 30));                       // above 25 -> re-armed
        Assert.Single(m.Evaluate(Snap(left: 10)));
    }

    [Fact]
    public void Wobbling_just_above_threshold_does_not_rearm()
    {
        var m = new LowBatteryMonitor(20, hysteresis: 5);
        Assert.Single(m.Evaluate(Snap(left: 20)));
        m.Evaluate(Snap(left: 25));                       // 25 is not > 25
        Assert.Empty(m.Evaluate(Snap(left: 20)));
    }

    [Fact]
    public void Components_are_independent()
    {
        var m = new LowBatteryMonitor(20);
        var first = m.Evaluate(Snap(left: 10, right: 80, caseLevel: 80));
        Assert.Equal(BatteryComponent.Left, Assert.Single(first).Component);

        var second = m.Evaluate(Snap(left: 10, right: 10, caseLevel: 5));
        Assert.Equal(new[] { BatteryComponent.Right, BatteryComponent.Case }, second.Select(a => a.Component).ToArray());
    }

    [Fact]
    public void Charging_components_do_not_alert_until_unplugged()
    {
        var m = new LowBatteryMonitor(20);
        Assert.Empty(m.Evaluate(Snap(left: 10, lc: true)));
        Assert.Single(m.Evaluate(Snap(left: 10, lc: false)));
    }

    [Fact]
    public void Unknown_levels_never_alert_and_do_not_rearm()
    {
        var m = new LowBatteryMonitor(20);
        Assert.Single(m.Evaluate(Snap(left: 10)));
        Assert.Empty(m.Evaluate(Snap(left: null)));
        Assert.Empty(m.Evaluate(Snap(left: 10)));
    }

    [Fact]
    public void Zero_threshold_disables_alerts()
    {
        var m = new LowBatteryMonitor(0);
        Assert.Empty(m.Evaluate(Snap(left: 0, right: 0, caseLevel: 0)));
    }

    [Fact]
    public void Changing_threshold_rearms()
    {
        var m = new LowBatteryMonitor(20);
        Assert.Single(m.Evaluate(Snap(left: 10)));
        m.Threshold = 30;
        Assert.Single(m.Evaluate(Snap(left: 10)));
    }

    [Fact]
    public void Headphones_only_alert_for_their_single_battery()
    {
        var m = new LowBatteryMonitor(20);
        var alerts = m.Evaluate(Snap(model: 0x0A20, left: 10, right: 10, caseLevel: 10));
        Assert.Equal(BatteryComponent.Left, Assert.Single(alerts).Component);
    }
}
