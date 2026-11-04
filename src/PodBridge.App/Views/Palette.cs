using Microsoft.UI.Xaml.Media;
using PodBridge.Core.Display;

namespace PodBridge.App.Views;

/// <summary>Turns the Core palette's #RRGGBB text into brushes.</summary>
internal static class Palette
{
    public static SolidColorBrush Brush(string hex)
    {
        var (r, g, b) = Contrast.Parse(hex);
        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));
    }
}
