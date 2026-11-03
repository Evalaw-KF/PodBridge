using Microsoft.UI.Xaml;
using PodBridge.App.Native;
using PodBridge.Core.Settings;

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
}
