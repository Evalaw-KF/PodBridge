using PodBridge.Core.Bluetooth;
using PodBridge.Core.State;

namespace PodBridge.Core.Alerts;

public enum BatteryComponent
{
    /// <summary>Left earbud, or the single battery of a headset.</summary>
    Left,
    Right,
    Case,
}

public readonly record struct LowBatteryAlert(BatteryComponent Component, int Level);

/// <summary>
/// Reports a low-battery alert once each time a battery drops to or below the threshold.
/// It re-arms only after the level climbs back above threshold + hysteresis.
/// Charging components never alert.
/// </summary>
public sealed class LowBatteryMonitor
{
    private readonly bool[] _fired = new bool[3];
    private int _threshold;

    public LowBatteryMonitor(int threshold, int hysteresis = 5)
    {
        _threshold = threshold;
        Hysteresis = hysteresis;
    }

    public int Hysteresis { get; }

    /// <summary>Alert level in percent. Zero or less disables alerts. Changing it re-arms everything.</summary>
    public int Threshold
    {
        get => _threshold;
        set
        {
            if (value == _threshold)
            {
                return;
            }
            _threshold = value;
            Reset();
        }
    }

    public void Reset() => Array.Clear(_fired);

    public IReadOnlyList<LowBatteryAlert> Evaluate(DeviceSnapshot snapshot)
    {
        var alerts = new List<LowBatteryAlert>();
        Check(BatteryComponent.Left, snapshot.LeftBattery, snapshot.LeftCharging, alerts);

        if (snapshot.Kind != DeviceKind.Headphones)
        {
            Check(BatteryComponent.Right, snapshot.RightBattery, snapshot.RightCharging, alerts);
            Check(BatteryComponent.Case, snapshot.CaseBattery, snapshot.CaseCharging, alerts);
        }
        return alerts;
    }

    private void Check(BatteryComponent component, int? level, bool charging, List<LowBatteryAlert> alerts)
    {
        if (level is not int value || _threshold <= 0)
        {
            return;
        }

        int index = (int)component;
        if (value > _threshold + Hysteresis)
        {
            _fired[index] = false;
        }
        else if (value <= _threshold && !_fired[index] && !charging)
        {
            _fired[index] = true;
            alerts.Add(new LowBatteryAlert(component, value));
        }
    }
}
