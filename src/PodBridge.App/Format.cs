using PodBridge.Core.Alerts;
using PodBridge.Core.Bluetooth;
using PodBridge.Core.State;

namespace PodBridge.App;

/// <summary>Text helpers shared by the tray, window and pop-up.</summary>
internal static class Format
{
    public static string Percent(int? value) => value is int v ? $"{v}%" : "–";

    public static string Tooltip(DeviceSnapshot? s)
    {
        if (s is null)
        {
            return "PodBridge: no AirPods nearby";
        }
        if (s.Kind == DeviceKind.Headphones)
        {
            return $"{s.DisplayName}\nBattery {Percent(s.LeftBattery)}";
        }
        return $"{s.DisplayName}\nL {Percent(s.LeftBattery)}  R {Percent(s.RightBattery)}  Case {Percent(s.CaseBattery)}";
    }

    /// <summary>The number shown on the tray icon: lowest earbud level, else the case, else unknown.</summary>
    public static int? TrayPercent(DeviceSnapshot? s) => s?.LowestBudBattery ?? s?.CaseBattery;

    public static bool TrayCharging(DeviceSnapshot? s) =>
        s is not null && (s.LeftCharging || s.RightCharging || s.CaseCharging);

    public static string ComponentName(DeviceSnapshot s, BatteryComponent c) => c switch
    {
        BatteryComponent.Left when s.Kind == DeviceKind.Headphones => "Headphones",
        BatteryComponent.Left => "Left earbud",
        BatteryComponent.Right => "Right earbud",
        _ => "Charging case",
    };
}
