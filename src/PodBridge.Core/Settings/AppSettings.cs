namespace PodBridge.Core.Settings;

public enum ThemePreference
{
    System,
    Light,
    Dark,
}

/// <summary>User preferences. Stored as JSON by <see cref="SettingsStore"/>.</summary>
public sealed class AppSettings
{
    public const int MinThreshold = 5;
    public const int MaxThreshold = 50;

    public int LowBatteryThreshold { get; set; } = 20;
    public bool LowBatteryNotifications { get; set; } = true;
    public bool PopupEnabled { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public ThemePreference Theme { get; set; } = ThemePreference.System;

    /// <summary>Returns a copy with out-of-range values pulled back into range.</summary>
    public AppSettings Normalized() => new()
    {
        LowBatteryThreshold = Math.Clamp(LowBatteryThreshold, MinThreshold, MaxThreshold),
        LowBatteryNotifications = LowBatteryNotifications,
        PopupEnabled = PopupEnabled,
        StartWithWindows = StartWithWindows,
        Theme = Enum.IsDefined(Theme) ? Theme : ThemePreference.System,
    };
}
