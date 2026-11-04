namespace PodBridge.Core.Display;

public enum PopupSize
{
    Small,
    Normal,
    Large,
}

public enum PopupPosition
{
    BottomRight,
    BottomCentre,
    TopRight,
    TopCentre,
}

public readonly record struct PixelRect(int X, int Y, int Width, int Height);

/// <summary>Works out the size and place of a pop-up on the screen's usable area.</summary>
public static class PopupLayout
{
    public const int Margin = 16;

    public static double SizeFactor(PopupSize size) => size switch
    {
        PopupSize.Small => 0.8,
        PopupSize.Large => 1.3,
        _ => 1.0,
    };

    /// <summary>Same screen edge, but in the middle (used by the volume pop-up so it never covers the battery pop-up).</summary>
    public static PopupPosition CentreHorizontally(PopupPosition position) => position switch
    {
        PopupPosition.TopRight or PopupPosition.TopCentre => PopupPosition.TopCentre,
        _ => PopupPosition.BottomCentre,
    };

    /// <param name="logicalWidth">Normal-size width in device-independent pixels.</param>
    /// <param name="scale">Display scale (1.0 = 100%).</param>
    public static PixelRect Compute(
        PopupSize size, PopupPosition position,
        int logicalWidth, int logicalHeight,
        PixelRect workArea, double scale)
    {
        if (scale <= 0)
        {
            scale = 1.0;
        }
        double factor = SizeFactor(size) * scale;
        int width = Math.Min((int)Math.Round(logicalWidth * factor), workArea.Width);
        int height = Math.Min((int)Math.Round(logicalHeight * factor), workArea.Height);
        int margin = (int)Math.Round(Margin * scale);

        int x = position switch
        {
            PopupPosition.BottomCentre or PopupPosition.TopCentre => workArea.X + (workArea.Width - width) / 2,
            _ => workArea.X + workArea.Width - width - margin,
        };
        int y = position switch
        {
            PopupPosition.TopRight or PopupPosition.TopCentre => workArea.Y + margin,
            _ => workArea.Y + workArea.Height - height - margin,
        };

        x = Math.Max(workArea.X, x);
        y = Math.Max(workArea.Y, y);
        return new PixelRect(x, y, width, height);
    }
}
