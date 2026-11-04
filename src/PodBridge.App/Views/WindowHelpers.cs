using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PodBridge.App.Native;
using Microsoft.Win32;
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
    /// Reads the Windows "app mode" (1 = light, 0 = dark). Null if it cannot be read.
    /// ElementTheme.Default is not trusted for the pop-ups: they are shown without being activated,
    /// and the theme can then resolve differently from the text colours that were chosen for it.
    /// </summary>
    public static int? ReadAppsUseLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") as int?;
        }
        catch (Exception ex)
        {
            Log.Error("Could not read the Windows app mode", ex);
            return null;
        }
    }

    public static bool ResolveDark(ThemePreference preference) =>
        ThemeResolver.IsDark(preference, ReadAppsUseLightTheme());

    /// <summary>
    /// Sets an explicit Light or Dark theme on the window's content root (never Default) and returns the
    /// matching solid palette, which the pop-up applies to its card and text.
    /// </summary>
    public static PopupPalette ApplyPopupTheme(FrameworkElement? contentRoot, ThemePreference preference)
    {
        bool dark = ResolveDark(preference);
        if (contentRoot is not null)
        {
            contentRoot.RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light;
        }
        return PopupPalette.For(dark);
    }

    /// <summary>
    /// Common set-up for the small always-on-top cards: no border or title bar, no taskbar button, never takes focus,
    /// no system backdrop (the card paints its own solid background). The corners are rounded by Windows 11 itself
    /// (antialiased, DWMWCP_ROUND); the card fills the window with square corners and no border of its own.
    /// </summary>
    public static IntPtr ConfigurePopupWindow(Window window)
    {
        IntPtr hwnd = GetHandle(window);

        var presenter = OverlappedPresenter.Create();
        presenter.IsAlwaysOnTop = true;
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.SetBorderAndTitleBar(false, false);
        window.AppWindow.SetPresenter(presenter);
        window.AppWindow.IsShownInSwitchers = false;

        try
        {
            long style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
            style |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(style));
        }
        catch (Exception ex)
        {
            Log.Error("Could not set the pop-up window style", ex);
        }

        try
        {
            // Fails harmlessly (non-zero HRESULT) before Windows 11, where the pop-up simply has square corners.
            int round = NativeMethods.DWMWCP_ROUND;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
        }
        catch (Exception ex)
        {
            Log.Error("Could not turn on the system corner rounding", ex);
        }
        return hwnd;
    }

    /// <summary>
    /// Paints the system's 1px window outline in the card's own colour, so no light edge shows at the rounded corners.
    /// Falls back to "no border" if the colour is not accepted. Fail-soft: errors are only logged.
    /// </summary>
    public static void SetPopupBorderColour(Window window, PopupPalette palette)
    {
        try
        {
            IntPtr hwnd = GetHandle(window);
            // COLORREF is 0x00BBGGRR.
            var (r, g, b) = Contrast.Parse(palette.Background);
            int colour = r | (g << 8) | (b << 16);
            if (NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_BORDER_COLOR, ref colour, sizeof(int)) != 0)
            {
                int none = NativeMethods.DWMWA_COLOR_NONE;
                NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_BORDER_COLOR, ref none, sizeof(int));
            }
        }
        catch (Exception ex)
        {
            Log.Error("Could not set the pop-up border colour", ex);
        }
    }

    /// <summary>Wires the "closing only hides" behaviour shared by the pop-ups.</summary>
    public static void HideInsteadOfClose(Window window, Action hide) =>
        window.AppWindow.Closing += (sender, args) =>
        {
            if (!App.Host.IsExiting)
            {
                args.Cancel = true;
                hide();
            }
        };

    /// <summary>
    /// Sizes and places a pop-up window. The window's content is a Viewbox (Stretch.Fill) around the fixed-size card
    /// <paramref name="root"/>, so the card is laid out at its normal logical size and then scaled with the window:
    /// text and controls grow or shrink together. (A RenderTransform on the root would not work for "Small": the layout
    /// clip is applied before the transform, so the right and bottom of the card would be cut off.)
    /// The card has square corners and fills the window; Windows 11 rounds the window itself.
    /// </summary>
    public static void PlacePopup(Window window, FrameworkElement root, int logicalWidth, int logicalHeight,
        PopupSize size, PopupPosition position)
    {
        try
        {
            root.Width = logicalWidth;
            root.Height = logicalHeight;
        }
        catch (Exception ex)
        {
            Log.Error("Could not size the pop-up card", ex);
        }

        // Two passes: moving onto the primary monitor can change the window's DPI, and Windows may then resize it;
        // the second pass puts the size back.
        PixelRect rect = default;
        double scale = 1.0;
        try
        {
            for (int pass = 0; pass < 2; pass++)
            {
                var work = DisplayArea.Primary.WorkArea;
                scale = GetPrimaryScale(window);
                rect = PopupLayout.Compute(size, position, logicalWidth, logicalHeight,
                    new PixelRect(work.X, work.Y, work.Width, work.Height), scale);
                window.AppWindow.MoveAndResize(new RectInt32(rect.X, rect.Y, rect.Width, rect.Height));
            }
        }
        catch (Exception ex)
        {
            Log.Error("Could not place the pop-up", ex);
        }
    }
}
