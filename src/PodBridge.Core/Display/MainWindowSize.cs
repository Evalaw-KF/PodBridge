namespace PodBridge.Core.Display;

/// <summary>Works out the main window's starting size: landscape, scaled for the display, never larger than the work area.</summary>
public static class MainWindowSize
{
    public const int DefaultWidth = 820;
    public const int DefaultHeight = 460;

    /// <summary>Logical size scaled to physical pixels, capped to the work area (0 or less means "no cap").</summary>
    public static (int Width, int Height) Compute(double scale, int workWidth, int workHeight,
        int logicalWidth = DefaultWidth, int logicalHeight = DefaultHeight)
    {
        if (double.IsNaN(scale) || scale <= 0)
        {
            scale = 1.0;
        }
        int width = (int)Math.Round(logicalWidth * scale);
        int height = (int)Math.Round(logicalHeight * scale);
        if (workWidth > 0)
        {
            width = Math.Min(width, workWidth);
        }
        if (workHeight > 0)
        {
            height = Math.Min(height, workHeight);
        }
        return (Math.Max(1, width), Math.Max(1, height));
    }
}

/// <summary>How battery levels are written. Unknown is a dash, never "0%".</summary>
public static class BatteryText
{
    public const string Unknown = "–";

    public static string Percent(int? value) => value is int v ? $"{v}%" : Unknown;

    /// <summary>The share of the ring to fill: none when unknown, so an unknown battery shows an empty ring.</summary>
    public static int? RingFill(int? value) => value is int v ? Math.Clamp(v, 0, 100) : null;
}
