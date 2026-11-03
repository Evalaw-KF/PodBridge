using PodBridge.Core.Input;
using PodBridge.Core.Settings;

namespace PodBridge.Core.Tests;

public sealed class HotkeyTests
{
    [Fact]
    public void Defaults_are_ctrl_alt_A_and_S()
    {
        Assert.Equal("Ctrl+Alt+A", Hotkey.DefaultConnect.ToString());
        Assert.Equal("Ctrl+Alt+S", Hotkey.DefaultSwitch.ToString());
        Assert.True(Hotkey.DefaultConnect.IsValid);
    }

    [Theory]
    [InlineData("Ctrl+Alt+A", HotkeyModifiers.Control | HotkeyModifiers.Alt, 'A')]
    [InlineData("ctrl + shift + f5", HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x74)]
    [InlineData("Win+Num3", HotkeyModifiers.Win, 0x63)]
    [InlineData("Alt+PageDown", HotkeyModifiers.Alt, 0x22)]
    [InlineData("Control+7", HotkeyModifiers.Control, '7')]
    public void Parses(string text, HotkeyModifiers mods, int vk)
    {
        Assert.True(Hotkey.TryParse(text, out var h));
        Assert.Equal(mods, h.Modifiers);
        Assert.Equal(vk, h.VirtualKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("A")]
    [InlineData("Shift+A")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+A+B")]
    [InlineData("Ctrl+Banana")]
    [InlineData("Ctrl+F25")]
    [InlineData("Ctrl++A")]
    public void Rejects(string? text) => Assert.False(Hotkey.TryParse(text, out _));

    [Fact]
    public void Round_trips_through_text()
    {
        var original = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.Win, 0x7B);
        Assert.Equal("Ctrl+Alt+Shift+Win+F12", original.ToString());
        Assert.True(Hotkey.TryParse(original.ToString(), out var back));
        Assert.Equal(original, back);
    }

    [Fact]
    public void Modifier_values_match_win32()
    {
        Assert.Equal(1, (int)HotkeyModifiers.Alt);
        Assert.Equal(2, (int)HotkeyModifiers.Control);
        Assert.Equal(4, (int)HotkeyModifiers.Shift);
        Assert.Equal(8, (int)HotkeyModifiers.Win);
    }

    [Fact]
    public void Key_press_ignores_bare_modifiers()
    {
        Assert.Null(Hotkey.FromKeyPress(0x11, ctrl: true, alt: false, shift: false, win: false)); // Control
        Assert.Null(Hotkey.FromKeyPress(0xA4, ctrl: false, alt: true, shift: false, win: false)); // LMenu
        Assert.Null(Hotkey.FromKeyPress(0x5B, ctrl: false, alt: false, shift: false, win: true));  // LWin
    }

    [Fact]
    public void Key_press_needs_ctrl_alt_or_win_and_a_known_key()
    {
        Assert.Null(Hotkey.FromKeyPress('A', false, false, false, false));
        Assert.Null(Hotkey.FromKeyPress('A', false, false, true, false));
        Assert.Null(Hotkey.FromKeyPress(0xBA, true, false, false, false)); // ';' key is not supported
        var ok = Hotkey.FromKeyPress('K', true, true, false, false);
        Assert.Equal("Ctrl+Alt+K", ok.ToString());
    }

    // ---- settings integration ----

    [Fact]
    public void New_settings_have_the_documented_defaults()
    {
        var s = new AppSettings();
        Assert.True(s.AutoPauseEnabled);
        Assert.True(s.ResumeEnabled);
        Assert.False(s.OnlyUseOneBud);
        Assert.Equal("AirPods", s.HeadphoneNameMatch);
        Assert.False(s.AutoSwitchOutput);
        Assert.Equal(string.Empty, s.FallbackEndpointId);
        Assert.True(s.OutputPopupEnabled);
        Assert.True(s.KeepAliveEnabled);
        Assert.True(s.HotkeysEnabled);
        Assert.Equal("Ctrl+Alt+A", s.ConnectHotkey);
        Assert.Equal("Ctrl+Alt+S", s.SwitchHotkey);
    }

    [Fact]
    public void Old_settings_files_without_the_new_fields_still_load_with_defaults()
    {
        var dir = Path.Combine(Path.GetTempPath(), "podbridge-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            var store = new SettingsStore(dir);
            File.WriteAllText(store.FilePath, "{\"LowBatteryThreshold\": 30}");
            var s = store.Load();
            Assert.Equal(30, s.LowBatteryThreshold);
            Assert.True(s.KeepAliveEnabled);
            Assert.Equal("Ctrl+Alt+A", s.ConnectHotkey);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Normalizing_repairs_bad_values_but_keeps_an_empty_hotkey()
    {
        var s = new AppSettings
        {
            HeadphoneNameMatch = "   ",
            ConnectHotkey = "nonsense",
            SwitchHotkey = "",
        }.Normalized();
        Assert.Equal("AirPods", s.HeadphoneNameMatch);
        Assert.Equal("Ctrl+Alt+A", s.ConnectHotkey);
        Assert.Equal(string.Empty, s.SwitchHotkey);
    }

    [Fact]
    public void Normalizing_canonicalises_hotkey_text()
    {
        var s = new AppSettings { ConnectHotkey = "alt+ctrl+q", HeadphoneNameMatch = "  Pods " }.Normalized();
        Assert.Equal("Ctrl+Alt+Q", s.ConnectHotkey);
        Assert.Equal("Pods", s.HeadphoneNameMatch);
    }

    [Fact]
    public void Phase_2_settings_round_trip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "podbridge-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(dir);
            store.Save(new AppSettings
            {
                AutoPauseEnabled = false,
                ResumeEnabled = false,
                OnlyUseOneBud = true,
                HeadphoneNameMatch = "Pods",
                AutoSwitchOutput = true,
                FallbackEndpointId = "{0.0.0.00000000}.{abc}",
                OutputPopupEnabled = false,
                KeepAliveEnabled = false,
                HotkeysEnabled = false,
                ConnectHotkey = "Ctrl+Shift+F9",
                SwitchHotkey = "",
            });
            var s = store.Load();
            Assert.False(s.AutoPauseEnabled);
            Assert.False(s.ResumeEnabled);
            Assert.True(s.OnlyUseOneBud);
            Assert.Equal("Pods", s.HeadphoneNameMatch);
            Assert.True(s.AutoSwitchOutput);
            Assert.Equal("{0.0.0.00000000}.{abc}", s.FallbackEndpointId);
            Assert.False(s.OutputPopupEnabled);
            Assert.False(s.KeepAliveEnabled);
            Assert.False(s.HotkeysEnabled);
            Assert.Equal("Ctrl+Shift+F9", s.ConnectHotkey);
            Assert.Equal(string.Empty, s.SwitchHotkey);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
