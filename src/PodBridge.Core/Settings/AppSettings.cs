using PodBridge.Core.Input;

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

    // Phase 2: ear detection
    public bool AutoPauseEnabled { get; set; } = true;
    public bool ResumeEnabled { get; set; } = true;
    public bool OnlyUseOneBud { get; set; }

    // Phase 2: audio output
    public const string DefaultHeadphoneNameMatch = "AirPods";

    /// <summary>Text the AirPods audio endpoint's name must contain (case-insensitive).</summary>
    public string HeadphoneNameMatch { get; set; } = DefaultHeadphoneNameMatch;
    public bool AutoSwitchOutput { get; set; }

    /// <summary>Endpoint id of the speaker to fall back to. Empty means "the output used before the AirPods".</summary>
    public string FallbackEndpointId { get; set; } = string.Empty;
    public bool OutputPopupEnabled { get; set; } = true;
    public bool KeepAliveEnabled { get; set; } = true;

    // Phase 2: hotkeys (text such as "Ctrl+Alt+A"; empty means not assigned)
    public bool HotkeysEnabled { get; set; } = true;
    public string ConnectHotkey { get; set; } = Hotkey.DefaultConnect.ToString();
    public string SwitchHotkey { get; set; } = Hotkey.DefaultSwitch.ToString();

    /// <summary>Returns a copy with out-of-range values pulled back into range.</summary>
    public AppSettings Normalized() => new()
    {
        LowBatteryThreshold = Math.Clamp(LowBatteryThreshold, MinThreshold, MaxThreshold),
        LowBatteryNotifications = LowBatteryNotifications,
        PopupEnabled = PopupEnabled,
        StartWithWindows = StartWithWindows,
        Theme = Enum.IsDefined(Theme) ? Theme : ThemePreference.System,
        AutoPauseEnabled = AutoPauseEnabled,
        ResumeEnabled = ResumeEnabled,
        OnlyUseOneBud = OnlyUseOneBud,
        HeadphoneNameMatch = string.IsNullOrWhiteSpace(HeadphoneNameMatch) ? DefaultHeadphoneNameMatch : HeadphoneNameMatch.Trim(),
        AutoSwitchOutput = AutoSwitchOutput,
        FallbackEndpointId = FallbackEndpointId ?? string.Empty,
        OutputPopupEnabled = OutputPopupEnabled,
        KeepAliveEnabled = KeepAliveEnabled,
        HotkeysEnabled = HotkeysEnabled,
        ConnectHotkey = NormalizeHotkey(ConnectHotkey, Hotkey.DefaultConnect),
        SwitchHotkey = NormalizeHotkey(SwitchHotkey, Hotkey.DefaultSwitch),
    };

    /// <summary>Null (missing in the file) gives the default; empty means "none"; unparsable text gives the default.</summary>
    private static string NormalizeHotkey(string? text, Hotkey fallback)
    {
        if (text is null)
        {
            return fallback.ToString();
        }
        if (text.Trim().Length == 0)
        {
            return string.Empty;
        }
        return Hotkey.TryParse(text, out var parsed) ? parsed.ToString() : fallback.ToString();
    }
}
