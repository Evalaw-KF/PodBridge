using PodBridge.Core.Display;
using PodBridge.Core.Input;
using PodBridge.Core.Notifications;

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

    /// <summary>Show a small banner when the AirPods audio connects or disconnects.</summary>
    public bool ShowConnectionBanner { get; set; } = true;
    public bool KeepAliveEnabled { get; set; } = true;

    // Phase 2: hotkeys (text such as "Ctrl+Alt+A"; empty means not assigned)
    public bool HotkeysEnabled { get; set; } = true;
    public string ConnectHotkey { get; set; } = Hotkey.DefaultConnect.ToString();
    public string SwitchHotkey { get; set; } = Hotkey.DefaultSwitch.ToString();

    // Phase 4: read notifications aloud
    public const int MinSpeechRate = 50;
    public const int MaxSpeechRate = 200;
    public const int MinSpeechVolume = 10;
    public const int MaxSpeechVolume = 100;

    public bool ReadNotificationsAloud { get; set; }
    public bool ReadOnlyWhenInEar { get; set; } = true;

    /// <summary>Id of the chosen voice. Empty means the Windows default voice.</summary>
    public string SpeechVoiceId { get; set; } = string.Empty;
    public int SpeechRatePercent { get; set; } = 100;
    public int SpeechVolumePercent { get; set; } = 80;

    /// <summary>Notification apps (by id) that may be read aloud. Empty means none.</summary>
    public List<string> NotificationAllowedApps { get; set; } = new();

    /// <summary>False: say the app name and title only. True: say the whole text.</summary>
    public bool NotificationReadFullText { get; set; }

    /// <summary>Apps that have sent a notification, so they can be picked in Settings.</summary>
    public List<SeenApp> NotificationSeenApps { get; set; } = new();

    // Phase 4: speaker mode
    public List<string> AutoEnableSpeakerDevices { get; set; } = new();

    // Phase 4: looks
    public PopupSize PopupSize { get; set; } = PopupSize.Normal;
    public PopupPosition PopupPosition { get; set; } = PopupPosition.BottomRight;

    /// <summary>File name (inside the PodBridge data folder) of the custom headphone picture; empty means the built-in one.</summary>
    public string CustomPictureFile { get; set; } = string.Empty;

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
        ShowConnectionBanner = ShowConnectionBanner,
        KeepAliveEnabled = KeepAliveEnabled,
        HotkeysEnabled = HotkeysEnabled,
        ConnectHotkey = NormalizeHotkey(ConnectHotkey, Hotkey.DefaultConnect),
        SwitchHotkey = NormalizeHotkey(SwitchHotkey, Hotkey.DefaultSwitch),
        ReadNotificationsAloud = ReadNotificationsAloud,
        ReadOnlyWhenInEar = ReadOnlyWhenInEar,
        SpeechVoiceId = SpeechVoiceId ?? string.Empty,
        SpeechRatePercent = Math.Clamp(SpeechRatePercent, MinSpeechRate, MaxSpeechRate),
        SpeechVolumePercent = Math.Clamp(SpeechVolumePercent, MinSpeechVolume, MaxSpeechVolume),
        NotificationAllowedApps = CleanIds(NotificationAllowedApps),
        NotificationReadFullText = NotificationReadFullText,
        NotificationSeenApps = (NotificationSeenApps ?? new())
            .Where(a => a is not null && !string.IsNullOrWhiteSpace(a.Id))
            .GroupBy(a => a.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => new SeenApp { Id = g.Key, Name = g.First().Name ?? g.Key })
            .Take(SeenApps.MaxApps)
            .ToList(),
        AutoEnableSpeakerDevices = CleanIds(AutoEnableSpeakerDevices),
        PopupSize = Enum.IsDefined(PopupSize) ? PopupSize : PopupSize.Normal,
        PopupPosition = Enum.IsDefined(PopupPosition) ? PopupPosition : PopupPosition.BottomRight,
        CustomPictureFile = CustomPicture.IsStoredName(CustomPictureFile) ? CustomPictureFile : string.Empty,
    };

    private static List<string> CleanIds(List<string>? ids) =>
        (ids ?? new()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).Take(500).ToList();

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
