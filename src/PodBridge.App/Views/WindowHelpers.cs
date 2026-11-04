using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using PodBridge.App.Native;
using PodBridge.Core.Display;
using PodBridge.Core.Settings;
using Windows.Graphics;

namespace PodBridge.App.Views;

internal static class WindowHelpers
{
    public static IntPtr GetHandle(Window window) => WinRT.Interop.WindowNative.GetWindowHandle(window);

    /// <summary>Display scale for the window's current monitor (1.0 = 100%).</summary>
    public static double GetScale(Window window)
    {
        uint dpi = NativeMethods.GetDpiForWindow(GetHandle(window));
        return dpi == 0 ? 1.0 : dpi / 96.0;
    }

    public static void ApplyTheme(FrameworkElement? root, ThemePreference preference)
    {
        if (root is null)
        {
            return;
        }
        root.RequestedTheme = preference switch
        {
            ThemePreference.Light => ElementTheme.Light,
            ThemePreference.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }

    /// <summary>
    /// Sizes and places a pop-up window. The content is laid out at its normal logical size and scaled to the chosen
    /// pop-up size, so text and controls grow or shrink together with the window.
    /// </summary>
    public static void PlacePopup(Window window, FrameworkElement root, int logicalWidth, int logicalHeight,
        PopupSize size, PopupPosition position)
    {
        // Two passes: the first may run at the DPI of the wrong monitor, the second corrects it.
        for (int pass = 0; pass < 2; pass++)
        {
            var work = DisplayArea.Primary.WorkArea;
            var rect = PopupLayout.Compute(size, position, logicalWidth, logicalHeight,
                new PixelRect(work.X, work.Y, work.Width, work.Height), GetScale(window));
            window.AppWindow.MoveAndResize(new RectInt32(rect.X, rect.Y, rect.Width, rect.Height));
        }

        double factor = PopupLayout.SizeFactor(size);
        root.Width = logicalWidth;
        root.Height = logicalHeight;
        root.HorizontalAlignment = HorizontalAlignment.Left;
        root.VerticalAlignment = VerticalAlignment.Top;
        root.RenderTransformOrigin = new Windows.Foundation.Point(0, 0);
        root.RenderTransform = new ScaleTransform { ScaleX = factor, ScaleY = factor };
    }
}
