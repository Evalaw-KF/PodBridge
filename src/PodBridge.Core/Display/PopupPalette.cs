using PodBridge.Core.Settings;

namespace PodBridge.Core.Display;

/// <summary>
/// The solid colours of the pop-up cards (as #RRGGBB text). The pop-ups never rely on the system backdrop or on
/// theme resources: they take their colours from here, chosen by the effective theme, so text always contrasts.
/// </summary>
public sealed record PopupPalette(
    string Background,
    string Border,
    string Primary,
    string Secondary,
    string Track,
    string Good,
    string Warning,
    string Critical,
    string Illustration)
{
    public static readonly PopupPalette Light = new(
        Background: "#F5F5F7", Border: "#D2D2D7", Primary: "#1D1D1F", Secondary: "#636366", Track: "#E1E1E6",
        Good: "#248A3D", Warning: "#C93400", Critical: "#D70015", Illustration: "#C7C7CC");

    public static readonly PopupPalette Dark = new(
        Background: "#1C1C1E", Border: "#3A3A3C", Primary: "#F5F5F7", Secondary: "#AEAEB2", Track: "#3A3A3C",
        Good: "#30D158", Warning: "#FF9F0A", Critical: "#FF453A", Illustration: "#58585C");

    public static PopupPalette For(bool dark) => dark ? Dark : Light;

    public string ToneColour(BatteryTone tone) => tone switch
    {
        BatteryTone.Normal => Good,
        BatteryTone.Low => Warning,
        BatteryTone.Critical => Critical,
        _ => Track,
    };
}

public static class ThemeResolver
{
    /// <summary>
    /// True when the dark palette applies. "System" follows the Windows app mode (AppsUseLightTheme: 0 = dark, 1 = light);
    /// when that setting cannot be read, Windows' own default (light) is assumed.
    /// </summary>
    public static bool IsDark(ThemePreference preference, int? appsUseLightTheme) => preference switch
    {
        ThemePreference.Light => false,
        ThemePreference.Dark => true,
        _ => appsUseLightTheme == 0,
    };
}

/// <summary>WCAG 2.x contrast maths, used to check that pop-up colours stay readable.</summary>
public static class Contrast
{
    public static double Ratio(string hexA, string hexB)
    {
        double a = Luminance(hexA), b = Luminance(hexB);
        double hi = Math.Max(a, b), lo = Math.Min(a, b);
        return (hi + 0.05) / (lo + 0.05);
    }

    public static double Luminance(string hex)
    {
        var (r, g, b) = Parse(hex);
        return 0.2126 * Linear(r) + 0.7152 * Linear(g) + 0.0722 * Linear(b);
    }

    /// <summary>Parses "#RRGGBB".</summary>
    public static (byte R, byte G, byte B) Parse(string hex)
    {
        if (hex.Length != 7 || hex[0] != '#')
        {
            throw new FormatException($"Not a #RRGGBB colour: {hex}");
        }
        return (Convert.ToByte(hex.Substring(1, 2), 16), Convert.ToByte(hex.Substring(3, 2), 16), Convert.ToByte(hex.Substring(5, 2), 16));
    }

    private static double Linear(byte channel)
    {
        double c = channel / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
