using PodBridge.Core.Audio;
using PodBridge.Core.Display;
using PodBridge.Core.Settings;

namespace PodBridge.Core.Tests;

public sealed class RingGeometryTests
{
    private static void Near(double expected, double actual) => Assert.InRange(actual, expected - 1e-6, expected + 1e-6);

    [Theory]
    [InlineData(0, 50, 0)]      // top
    [InlineData(90, 100, 50)]   // right
    [InlineData(180, 50, 100)]  // bottom
    [InlineData(270, 0, 50)]    // left
    public void PointAt_walks_clockwise_from_the_top(double angle, double x, double y)
    {
        var p = RingGeometry.PointAt(50, 50, 50, angle);
        Near(x, p.X);
        Near(y, p.Y);
    }

    [Fact]
    public void Quarter_is_a_small_arc_ending_at_three_oclock()
    {
        var arc = RingGeometry.ForPercent(50, 50, 40, 25);
        Near(90, arc.SweepDegrees);
        Assert.False(arc.IsLargeArc);
        Near(50, arc.Start.X);
        Near(10, arc.Start.Y);
        Near(90, arc.End.X);
        Near(50, arc.End.Y);
    }

    [Fact]
    public void Exactly_half_is_not_large_and_just_over_half_is()
    {
        Assert.False(RingGeometry.ForPercent(0, 0, 10, 50).IsLargeArc);
        Assert.True(RingGeometry.ForPercent(0, 0, 10, 51).IsLargeArc);
        Assert.True(RingGeometry.ForPercent(0, 0, 10, 75).IsLargeArc);
    }

    [Fact]
    public void Zero_is_empty_and_full_stays_just_short_of_a_closed_circle()
    {
        Assert.True(RingGeometry.ForPercent(50, 50, 40, 0).IsEmpty);
        var full = RingGeometry.ForPercent(50, 50, 40, 100);
        Assert.False(full.IsEmpty);
        Assert.True(full.IsLargeArc);
        Near(RingGeometry.MaxSweep, full.SweepDegrees);
        Assert.NotEqual(full.Start, full.End); // an arc whose ends coincide would draw nothing
    }

    [Theory]
    [InlineData(-20, 0)]
    [InlineData(250, 359.9)]
    [InlineData(double.NaN, 0)]
    public void Out_of_range_values_are_clamped(double percent, double sweep)
    {
        Near(sweep, RingGeometry.ForPercent(0, 0, 10, percent).SweepDegrees);
    }
}

public sealed class BatteryToneTests
{
    [Theory]
    [InlineData(100, BatteryTone.Normal)]
    [InlineData(21, BatteryTone.Normal)]
    [InlineData(20, BatteryTone.Low)]
    [InlineData(11, BatteryTone.Low)]
    [InlineData(10, BatteryTone.Critical)]
    [InlineData(0, BatteryTone.Critical)]
    public void Levels_map_to_tones(int percent, BatteryTone tone) =>
        Assert.Equal(tone, BatteryToneRules.For(percent));

    [Fact]
    public void Unknown_and_charging()
    {
        Assert.Equal(BatteryTone.Unknown, BatteryToneRules.For(null));
        Assert.Equal(BatteryTone.Normal, BatteryToneRules.For(5, charging: true));
    }
}

public sealed class PaletteTests
{
    [Theory]
    [InlineData(ThemePreference.Light, 0, false)]
    [InlineData(ThemePreference.Light, 1, false)]
    [InlineData(ThemePreference.Dark, 1, true)]
    [InlineData(ThemePreference.Dark, null, true)]
    [InlineData(ThemePreference.System, 0, true)]
    [InlineData(ThemePreference.System, 1, false)]
    [InlineData(ThemePreference.System, null, false)]
    public void Theme_resolves_from_choice_and_windows_app_mode(ThemePreference pref, int? appsUseLight, bool dark) =>
        Assert.Equal(dark, ThemeResolver.IsDark(pref, appsUseLight));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Text_is_readable_on_the_card(bool dark)
    {
        var p = PopupPalette.For(dark);
        Assert.True(Contrast.Ratio(p.Primary, p.Background) >= 7, "primary text");
        Assert.True(Contrast.Ratio(p.Secondary, p.Background) >= 4.5, "secondary text");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ring_colours_stand_out_from_the_card_and_the_track(bool dark)
    {
        var p = PopupPalette.For(dark);
        foreach (var colour in new[] { p.Good, p.Warning, p.Critical })
        {
            Assert.True(Contrast.Ratio(colour, p.Background) >= 3, colour);
        }
        Assert.NotEqual(p.Track, p.Background);
    }

    [Fact]
    public void Light_and_dark_cards_really_differ_in_brightness()
    {
        Assert.True(Contrast.Luminance(PopupPalette.Light.Background) > 0.8);
        Assert.True(Contrast.Luminance(PopupPalette.Dark.Background) < 0.05);
    }

    [Fact]
    public void Every_colour_parses_and_tones_map()
    {
        foreach (var p in new[] { PopupPalette.Light, PopupPalette.Dark })
        {
            foreach (var c in new[] { p.Background, p.Border, p.Primary, p.Secondary, p.Track, p.Good, p.Warning, p.Critical, p.Illustration })
            {
                Contrast.Parse(c);
            }
            Assert.Equal(p.Warning, p.ToneColour(BatteryTone.Low));
            Assert.Equal(p.Critical, p.ToneColour(BatteryTone.Critical));
            Assert.Equal(p.Good, p.ToneColour(BatteryTone.Normal));
        }
    }
}

public sealed class ConnectionWatcherTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static DateTimeOffset At(double seconds) => T0.AddSeconds(seconds);

    [Fact]
    public void First_observation_is_a_baseline_and_announces_nothing()
    {
        var w = new ConnectionWatcher();
        w.Observe(true, At(0));
        Assert.Equal(ConnectionChange.None, w.Poll(At(10)));
        Assert.True(w.Connected);
    }

    [Fact]
    public void Change_is_reported_after_the_debounce_and_only_once()
    {
        var w = new ConnectionWatcher();
        w.Observe(false, At(0));
        w.Observe(true, At(5));
        Assert.Equal(ConnectionChange.None, w.Poll(At(6)));
        Assert.Equal(ConnectionChange.Connected, w.Poll(At(7)));
        Assert.Equal(ConnectionChange.None, w.Poll(At(8)));
        Assert.True(w.Connected);
    }

    [Fact]
    public void Disconnect_is_reported()
    {
        var w = new ConnectionWatcher();
        w.Observe(true, At(0));
        w.Observe(false, At(1));
        Assert.Equal(ConnectionChange.Disconnected, w.Poll(At(3.5)));
        Assert.False(w.Connected);
    }

    [Fact]
    public void Flapping_inside_the_debounce_is_ignored()
    {
        var w = new ConnectionWatcher();
        w.Observe(true, At(0));
        w.Observe(false, At(1));
        w.Observe(true, At(2)); // came back
        Assert.Equal(ConnectionChange.None, w.Poll(At(10)));
        Assert.True(w.Connected);
    }

    [Fact]
    public void Repeated_observations_do_not_restart_the_debounce()
    {
        var w = new ConnectionWatcher();
        w.Observe(false, At(0));
        w.Observe(true, At(1));
        w.Observe(true, At(2));
        w.Observe(true, At(2.9));
        Assert.Equal(ConnectionChange.Connected, w.Poll(At(3)));
    }

    [Fact]
    public void Connect_then_disconnect_gives_two_events()
    {
        var w = new ConnectionWatcher();
        w.Observe(false, At(0));
        w.Observe(true, At(1));
        Assert.Equal(ConnectionChange.Connected, w.Poll(At(3)));
        w.Observe(false, At(20));
        Assert.Equal(ConnectionChange.Disconnected, w.Poll(At(22)));
    }
}

public sealed class BannerSettingTests
{
    [Fact]
    public void Banner_is_on_by_default_and_survives_normalising()
    {
        Assert.True(new AppSettings().ShowConnectionBanner);
        Assert.False(new AppSettings { ShowConnectionBanner = false }.Normalized().ShowConnectionBanner);
        Assert.True(new AppSettings().Normalized().ShowConnectionBanner);
    }
}
