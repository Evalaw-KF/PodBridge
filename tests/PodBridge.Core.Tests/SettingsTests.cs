using PodBridge.Core.Settings;

namespace PodBridge.Core.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "podbridge-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Missing_file_gives_defaults()
    {
        var s = new SettingsStore(_dir).Load();
        Assert.Equal(20, s.LowBatteryThreshold);
        Assert.True(s.PopupEnabled);
        Assert.False(s.StartWithWindows);
        Assert.Equal(ThemePreference.System, s.Theme);
    }

    [Fact]
    public void Round_trips()
    {
        var store = new SettingsStore(_dir);
        store.Save(new AppSettings { LowBatteryThreshold = 35, PopupEnabled = false, StartWithWindows = true, Theme = ThemePreference.Dark });

        var loaded = new SettingsStore(_dir).Load();
        Assert.Equal(35, loaded.LowBatteryThreshold);
        Assert.False(loaded.PopupEnabled);
        Assert.True(loaded.StartWithWindows);
        Assert.Equal(ThemePreference.Dark, loaded.Theme);
    }

    [Fact]
    public void Creates_the_directory_and_stores_theme_as_text()
    {
        var store = new SettingsStore(Path.Combine(_dir, "nested"));
        store.Save(new AppSettings { Theme = ThemePreference.Light });
        Assert.Contains("\"Light\"", File.ReadAllText(store.FilePath));
    }

    [Fact]
    public void Corrupt_file_gives_defaults()
    {
        Directory.CreateDirectory(_dir);
        var store = new SettingsStore(_dir);
        File.WriteAllText(store.FilePath, "{ this is not json");
        Assert.Equal(20, store.Load().LowBatteryThreshold);
    }

    [Fact]
    public void Out_of_range_values_are_clamped()
    {
        Directory.CreateDirectory(_dir);
        var store = new SettingsStore(_dir);
        File.WriteAllText(store.FilePath, "{\"LowBatteryThreshold\": 999, \"Theme\": \"Dark\"}");
        var s = store.Load();
        Assert.Equal(AppSettings.MaxThreshold, s.LowBatteryThreshold);
        Assert.Equal(ThemePreference.Dark, s.Theme);

        File.WriteAllText(store.FilePath, "{\"LowBatteryThreshold\": -4}");
        Assert.Equal(AppSettings.MinThreshold, store.Load().LowBatteryThreshold);
    }

    [Fact]
    public void Unknown_theme_text_falls_back_to_defaults()
    {
        Directory.CreateDirectory(_dir);
        var store = new SettingsStore(_dir);
        File.WriteAllText(store.FilePath, "{\"Theme\": \"Neon\"}");
        Assert.Equal(20, store.Load().LowBatteryThreshold);
    }

    [Fact]
    public void Save_leaves_no_temp_file()
    {
        var store = new SettingsStore(_dir);
        store.Save(new AppSettings());
        store.Save(new AppSettings { LowBatteryThreshold = 10 });
        Assert.False(File.Exists(store.FilePath + ".tmp"));
        Assert.Equal(10, store.Load().LowBatteryThreshold);
    }

    [Fact]
    public void Default_directory_ends_with_PodBridge()
    {
        Assert.EndsWith("PodBridge", SettingsStore.DefaultDirectory());
    }
}
