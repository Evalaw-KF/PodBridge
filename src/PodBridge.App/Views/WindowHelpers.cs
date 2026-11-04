using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    /// <summary>Display scale of the primary monitor (where pop-ups go), falling back to the window's own scale.</summary>
    private static double GetPrimaryScale(Window window)
    {
        try
        {
            IntPtr monitor = Microsoft.UI.Win32Interop.GetMonitorFromDisplayId(DisplayArea.Primary.DisplayId);
            if (monitor != IntPtr.Zero && NativeMethods.GetDpiForMonitor(monitor, 0, out uint dpiX, out _) == 0 && dpiX > 0)
            {
                return dpiX / 96.0;
            }
        }
        catch (Exception ex)
        {
            Log.Error("Could not read the primary monitor's scale", ex);
        }
        return GetScale(window);
    }

    /// <summary>
    /// Sizes and places a pop-up window. The content is laid out at its normal logical size inside a Viewbox, which
    /// scales it to the chosen pop-up size, so text and controls grow or shrink together with the window.
    /// (A RenderTransform on the window's root would not work for "Small": the layout clip is applied before
    /// the transform, so the right and bottom of the card would be cut off.)
    /// </summary>
    public static void PlacePopup(Window window, FrameworkElement root, int logicalWidth, int logicalHeight,
        PopupSize size, PopupPosition position)
    {
        try
        {
            if (ReferenceEquals(window.Content, root))
            {
                window.Content = null;
                window.Content = new Viewbox { Stretch = Stretch.Fill, Child = root };
            }
            root.Width = logicalWidth;
            root.Height = logicalHeight;
        }
        catch (Exception ex)
        {
            Log.Error("Could not scale the pop-up", ex);
        }

        // Two passes: moving onto the primary monitor can change the window's DPI, and Windows may then resize it;
        // the second pass puts the size back.
        for (int pass = 0; pass < 2; pass++)
        {
            var work = DisplayArea.Primary.WorkArea;
            var rect = PopupLayout.Compute(size, position, logicalWidth, logicalHeight,
                new PixelRect(work.X, work.Y, work.Width, work.Height), GetPrimaryScale(window));
            window.AppWindow.MoveAndResize(new RectInt32(rect.X, rect.Y, rect.Width, rect.Height));
        }
    }
}
