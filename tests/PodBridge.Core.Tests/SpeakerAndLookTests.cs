using PodBridge.Core.Display;
using PodBridge.Core.Notifications;
using PodBridge.Core.Settings;
using PodBridge.Core.Speaker;

namespace PodBridge.Core.Tests;

public sealed class SpeakerAndLookTests
{
    // ---- speaker registry ----

    [Fact]
    public void Registry_lists_devices_by_name_and_updates_names_without_losing_state()
    {
        var r = new SpeakerRegistry();
        r.Upsert("2", "Zed phone");
        r.Upsert("1", "alpha phone");
        r.SetState("1", SpeakerState.Ready);
        r.Upsert("1", "Alpha");
        var list = r.Snapshot();
        Assert.Equal(new[] { "Alpha", "Zed phone" }, list.Select(e => e.Name));
        Assert.Equal(SpeakerState.Ready, r.Find("1")!.State);
    }

    [Fact]
    public void Registry_remove_and_unknown_ids_are_safe()
    {
        var r = new SpeakerRegistry();
        r.Upsert("1", "");
        Assert.Equal("Unnamed device", r.Find("1")!.Name);
        r.Remove("1");
        r.SetState("1", SpeakerState.Failed);
        Assert.Equal(0, r.Count);
    }

    [Fact]
    public void Speaker_text_and_allowed_actions()
    {
        Assert.True(SpeakerText.CanEnable(SpeakerState.Off));
        Assert.True(SpeakerText.CanEnable(SpeakerState.Failed));
        Assert.False(SpeakerText.CanEnable(SpeakerState.Ready));
        Assert.True(SpeakerText.CanOpen(SpeakerState.Ready));
        Assert.False(SpeakerText.CanOpen(SpeakerState.Off));
        Assert.True(SpeakerText.CanDisable(SpeakerState.Connected));
        Assert.Equal("boom", SpeakerText.Describe(new SpeakerEntry("1", "x", SpeakerState.Failed, "boom")));
        Assert.Contains("Connected", SpeakerText.Describe(new SpeakerEntry("1", "x", SpeakerState.Connected, "")));
    }

    [Fact]
    public void Auto_enable_picks_only_off_devices_on_the_list()
    {
        var entries = new[]
        {
            new SpeakerEntry("a", "A", SpeakerState.Off, ""),
            new SpeakerEntry("b", "B", SpeakerState.Ready, ""),
            new SpeakerEntry("c", "C", SpeakerState.Off, ""),
        };
        Assert.Equal(new[] { "a" }, SpeakerAutoEnable.ToEnable(new[] { "a", "b" }, entries));
    }

    [Fact]
    public void Auto_enable_toggle_adds_once_and_removes()
    {
        var list = SpeakerAutoEnable.Toggle(new[] { "a" }, "b", true);
        list = SpeakerAutoEnable.Toggle(list, "b", true);
        Assert.Equal(new[] { "a", "b" }, list);
        Assert.Equal(new[] { "a" }, SpeakerAutoEnable.Toggle(list, "b", false));
    }

    // ---- popup layout ----

    private static readonly PixelRect Screen = new(0, 0, 1920, 1040);

    [Fact]
    public void Bottom_right_matches_the_old_behaviour()
    {
        var r = PopupLayout.Compute(PopupSize.Normal, PopupPosition.BottomRight, 340, 130, Screen, 1.0);
        Assert.Equal(new PixelRect(1920 - 340 - 16, 1040 - 130 - 16, 340, 130), r);
    }

    [Fact]
    public void Centre_positions_are_centred()
    {
        var r = PopupLayout.Compute(PopupSize.Normal, PopupPosition.TopCentre, 340, 130, Screen, 1.0);
        Assert.Equal((1920 - 340) / 2, r.X);
        Assert.Equal(16, r.Y);
        var b = PopupLayout.Compute(PopupSize.Normal, PopupPosition.BottomCentre, 340, 130, Screen, 1.0);
        Assert.Equal(1040 - 130 - 16, b.Y);
    }

    [Fact]
    public void Top_right_sits_in_the_corner()
    {
        var r = PopupLayout.Compute(PopupSize.Normal, PopupPosition.TopRight, 340, 130, Screen, 1.0);
        Assert.Equal(new PixelRect(1920 - 340 - 16, 16, 340, 130), r);
    }

    [Fact]
    public void Size_and_dpi_scale_the_window()
    {
        var small = PopupLayout.Compute(PopupSize.Small, PopupPosition.BottomRight, 340, 130, Screen, 1.0);
        var large = PopupLayout.Compute(PopupSize.Large, PopupPosition.BottomRight, 340, 130, Screen, 1.0);
        Assert.True(small.Width < 340 && large.Width > 340);
        var dpi = PopupLayout.Compute(PopupSize.Normal, PopupPosition.BottomRight, 340, 130, Screen, 2.0);
        Assert.Equal(680, dpi.Width);
        Assert.Equal(1920 - 680 - 32, dpi.X);
    }

    [Fact]
    public void Layout_respects_a_work_area_offset_and_tiny_screens()
    {
        var offset = new PixelRect(-1920, 100, 1920, 1000);
        var r = PopupLayout.Compute(PopupSize.Normal, PopupPosition.TopRight, 340, 130, offset, 1.0);
        Assert.Equal(-340 - 16, r.X);
        Assert.Equal(116, r.Y);

        var tiny = PopupLayout.Compute(PopupSize.Large, PopupPosition.BottomRight, 340, 130, new PixelRect(0, 0, 200, 100), 1.0);
        Assert.True(tiny.Width <= 200 && tiny.Height <= 100 && tiny.X >= 0 && tiny.Y >= 0);
        Assert.Equal(1.0, PopupLayout.SizeFactor(PopupSize.Normal));
        var bad = PopupLayout.Compute(PopupSize.Normal, PopupPosition.BottomRight, 340, 130, Screen, 0);
        Assert.Equal(340, bad.Width);
    }

    // ---- custom picture ----

    [Theory]
    [InlineData(".png", true)]
    [InlineData(".JPG", true)]
    [InlineData(".webp", false)]
    [InlineData(".exe", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Picture_extensions(string? ext, bool ok) => Assert.Equal(ok, CustomPicture.IsSupportedExtension(ext));

    [Fact]
    public void Picture_validate_messages()
    {
        Assert.Null(CustomPicture.Validate(".png", 1000));
        Assert.NotNull(CustomPicture.Validate(".txt", 1000));
        Assert.NotNull(CustomPicture.Validate(".png", 0));
        Assert.NotNull(CustomPicture.Validate(".png", CustomPicture.MaxBytes + 1));
    }

    [Fact]
    public void Picture_stored_names_round_trip_and_reject_anything_else()
    {
        string name = CustomPicture.StoredFileName(".PNG", 12345);
        Assert.Equal("custom-headphones-12345.png", name);
        Assert.True(CustomPicture.IsStoredName(name));
        Assert.False(CustomPicture.IsStoredName(@"..\..\evil.png"));
        Assert.False(CustomPicture.IsStoredName("C:\\x\\custom-headphones-1.png"));
        Assert.False(CustomPicture.IsStoredName("custom-headphones-1.exe"));
        Assert.False(CustomPicture.IsStoredName(""));
        Assert.False(CustomPicture.IsStoredName(null));
    }

    // ---- phase 4 settings ----

    [Fact]
    public void Phase4_defaults()
    {
        var s = new AppSettings();
        Assert.False(s.ReadNotificationsAloud);
        Assert.True(s.ReadOnlyWhenInEar);
        Assert.False(s.NotificationReadFullText);
        Assert.Empty(s.NotificationAllowedApps);
        Assert.Equal(PopupSize.Normal, s.PopupSize);
        Assert.Equal(PopupPosition.BottomRight, s.PopupPosition);
        Assert.Equal(string.Empty, s.CustomPictureFile);
    }

    [Fact]
    public void Phase4_values_are_clamped_and_cleaned()
    {
        var n = new AppSettings
        {
            SpeechRatePercent = 9999,
            SpeechVolumePercent = -3,
            NotificationAllowedApps = new() { "a", "a", "", "b" },
            AutoEnableSpeakerDevices = null!,
            NotificationSeenApps = new() { new SeenApp { Id = "x", Name = "X" }, new SeenApp { Id = "X", Name = "dup" }, new SeenApp { Id = " " } },
            CustomPictureFile = @"..\secret.png",
            PopupSize = (PopupSize)42,
            PopupPosition = (PopupPosition)42,
        }.Normalized();
        Assert.Equal(AppSettings.MaxSpeechRate, n.SpeechRatePercent);
        Assert.Equal(AppSettings.MinSpeechVolume, n.SpeechVolumePercent);
        Assert.Equal(new[] { "a", "b" }, n.NotificationAllowedApps);
        Assert.Empty(n.AutoEnableSpeakerDevices);
        Assert.Single(n.NotificationSeenApps);
        Assert.Equal(string.Empty, n.CustomPictureFile);
        Assert.Equal(PopupSize.Normal, n.PopupSize);
        Assert.Equal(PopupPosition.BottomRight, n.PopupPosition);
    }

    [Fact]
    public void Phase4_settings_round_trip_through_the_store()
    {
        string dir = Path.Combine(Path.GetTempPath(), "podbridge-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(dir);
            store.Save(new AppSettings
            {
                ReadNotificationsAloud = true,
                ReadOnlyWhenInEar = false,
                SpeechVoiceId = "voice-1",
                SpeechRatePercent = 150,
                NotificationAllowedApps = new() { "app1" },
                NotificationSeenApps = new() { new SeenApp { Id = "app1", Name = "App One" } },
                AutoEnableSpeakerDevices = new() { "dev1" },
                PopupSize = PopupSize.Large,
                PopupPosition = PopupPosition.TopCentre,
                CustomPictureFile = "custom-headphones-7.png",
            });
            var s = store.Load();
            Assert.True(s.ReadNotificationsAloud);
            Assert.False(s.ReadOnlyWhenInEar);
            Assert.Equal("voice-1", s.SpeechVoiceId);
            Assert.Equal(150, s.SpeechRatePercent);
            Assert.Equal("App One", s.NotificationSeenApps[0].Name);
            Assert.Equal(new[] { "dev1" }, s.AutoEnableSpeakerDevices);
            Assert.Equal(PopupSize.Large, s.PopupSize);
            Assert.Equal(PopupPosition.TopCentre, s.PopupPosition);
            Assert.Equal("custom-headphones-7.png", s.CustomPictureFile);
            Assert.Contains("\"TopCentre\"", File.ReadAllText(store.FilePath));
        }
        finally
        {
            if (Directory.Exists(dir)) { Directory.Delete(dir, true); }
        }
    }

    [Fact]
    public void Old_settings_files_still_load()
    {
        string dir = Path.Combine(Path.GetTempPath(), "podbridge-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            var store = new SettingsStore(dir);
            File.WriteAllText(store.FilePath, "{\"LowBatteryThreshold\": 30, \"Theme\": \"Dark\"}");
            var s = store.Load();
            Assert.Equal(30, s.LowBatteryThreshold);
            Assert.True(s.ReadOnlyWhenInEar);
            Assert.Equal(80, s.SpeechVolumePercent);
        }
        finally
        {
            if (Directory.Exists(dir)) { Directory.Delete(dir, true); }
        }
    }
}
