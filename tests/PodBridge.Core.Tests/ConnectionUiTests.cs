using PodBridge.Core.Audio;
using PodBridge.Core.Display;

namespace PodBridge.Core.Tests;

public sealed class AudioStatusTextTests
{
    [Theory]
    [InlineData("Headphones (AirPods Pro - Find My)", "AirPods Pro")]
    [InlineData("Headphones (AirPods Pro)", "AirPods Pro")]
    [InlineData("AirPods Pro", "AirPods Pro")]
    [InlineData("Speakers (Realtek(R) Audio)", "Realtek(R) Audio")]
    [InlineData("", "AirPods")]
    [InlineData(null, "AirPods")]
    public void FriendlyName_strips_the_windows_decoration(string? input, string expected) =>
        Assert.Equal(expected, AudioStatusText.FriendlyName(input));

    [Fact]
    public void Playing_through_the_pods()
    {
        var text = AudioStatusText.Build(true, true, "Headphones (AirPods Pro - Find My)", "Headphones (AirPods Pro - Find My)", true, "AirPods");
        Assert.Equal("Playing through AirPods Pro", text);
    }

    [Fact]
    public void Connected_but_output_elsewhere()
    {
        var text = AudioStatusText.Build(true, true, "Headphones (AirPods Pro)", "Speakers (Realtek Audio)", false, "AirPods");
        Assert.Equal("Connected, but sound is going to Realtek Audio", text);
    }

    [Fact]
    public void Connected_with_no_output()
    {
        Assert.Equal("Connected, but no sound output is selected",
            AudioStatusText.Build(true, true, "AirPods Pro", null, false, "AirPods"));
    }

    [Fact]
    public void Not_connected_and_not_found()
    {
        Assert.Equal("Not connected", AudioStatusText.Build(true, false, "AirPods Pro", "Speakers", false, "AirPods"));
        Assert.Contains("Pair your AirPods", AudioStatusText.Build(false, false, null, null, false, "AirPods"));
    }
}

public sealed class ConnectionToggleTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Label_follows_the_state()
    {
        Assert.Equal(new ToggleView("Connect", true, true), ToggleView.For(false, null, true));
        Assert.Equal(new ToggleView("Disconnect", false, true), ToggleView.For(true, null, true));
        Assert.False(ToggleView.For(true, null, false).Enabled);
    }

    [Fact]
    public void Pending_shows_progress_and_disables()
    {
        var connecting = ToggleView.For(false, true, true);
        Assert.Equal("Connecting…", connecting.Label);
        Assert.False(connecting.Enabled);
        Assert.Equal("Disconnecting…", ToggleView.For(true, false, true).Label);
    }

    [Fact]
    public void Pending_clears_when_the_state_changes()
    {
        var p = new PendingConnection();
        p.Begin(true, T0);
        Assert.True(p.Current(false, T0.AddSeconds(1)));
        Assert.Null(p.Current(true, T0.AddSeconds(2)));
        Assert.Null(p.Current(false, T0.AddSeconds(3))); // stays cleared
    }

    [Fact]
    public void Pending_clears_after_the_timeout()
    {
        var p = new PendingConnection();
        p.Begin(false, T0);
        Assert.False(p.Current(true, T0.AddSeconds(9.9)));
        Assert.Null(p.Current(true, T0.AddSeconds(10)));
    }

    [Fact]
    public void Clear_ends_it()
    {
        var p = new PendingConnection();
        p.Begin(true, T0);
        p.Clear();
        Assert.Null(p.Current(false, T0));
    }
}

public sealed class MainWindowSizeTests
{
    [Fact]
    public void Is_landscape_and_scales_with_dpi()
    {
        Assert.Equal((820, 460), MainWindowSize.Compute(1.0, 1920, 1040));
        Assert.Equal((1230, 690), MainWindowSize.Compute(1.5, 2560, 1400));
    }

    [Fact]
    public void Is_capped_to_the_work_area()
    {
        Assert.Equal((800, 460), MainWindowSize.Compute(1.0, 800, 600));
        Assert.Equal((1280, 680), MainWindowSize.Compute(2.0, 1280, 680));
    }

    [Fact]
    public void Bad_input_falls_back()
    {
        Assert.Equal((820, 460), MainWindowSize.Compute(0, 0, 0));
        Assert.Equal((820, 460), MainWindowSize.Compute(double.NaN, -1, -1));
    }
}

public sealed class BatteryTextTests
{
    [Fact]
    public void Unknown_is_a_dash_with_an_empty_ring()
    {
        Assert.Equal("–", BatteryText.Percent(null));
        Assert.Null(BatteryText.RingFill(null));
    }

    [Fact]
    public void Known_values_are_shown_as_percent()
    {
        Assert.Equal("0%", BatteryText.Percent(0));
        Assert.Equal("80%", BatteryText.Percent(80));
        Assert.Equal(100, BatteryText.RingFill(140));
    }
}
