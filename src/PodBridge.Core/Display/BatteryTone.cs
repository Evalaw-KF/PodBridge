namespace PodBridge.Core.Display;

public enum BatteryTone
{
    Unknown,
    Normal,
    Low,
    Critical,
}

public static class BatteryToneRules
{
    public const int LowAt = 20;
    public const int CriticalAt = 10;

    /// <summary>Green normally, amber at 20% or below, red at 10% or below. Charging batteries stay green.</summary>
    public static BatteryTone For(int? percent, bool charging = false)
    {
        if (percent is not int p)
        {
            return BatteryTone.Unknown;
        }
        if (charging)
        {
            return BatteryTone.Normal;
        }
        if (p <= CriticalAt)
        {
            return BatteryTone.Critical;
        }
        return p <= LowAt ? BatteryTone.Low : BatteryTone.Normal;
    }
}
