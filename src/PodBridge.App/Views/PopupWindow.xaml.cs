using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using PodBridge.App.Native;
using PodBridge.App.Services;
using PodBridge.Core.Bluetooth;
using PodBridge.Core.Settings;
using PodBridge.Core.State;

namespace PodBridge.App.Views;

/// <summary>
/// A small card that slides into the bottom-right corner when the case lid opens and
/// hides itself after a few seconds. It never takes keyboard focus.
/// </summary>
public sealed partial class PopupWindow : Window
{
    private const int LogicalWidth = 360;
    private const int LogicalHeight = 200;
    private static readonly TimeSpan VisibleFor = TimeSpan.FromSeconds(6);

    private readonly IntPtr _hwnd;
    private readonly DispatcherQueueTimer _hideTimer;
    private bool _visible;

    public PopupWindow()
    {
        InitializeComponent();

        // No acrylic: shown without activation it falls back to a light colour, under text that follows the dark theme.
        // The card paints its own solid, theme-aware background instead (see ApplyTheme).
        _hwnd = WindowHelpers.ConfigurePopupWindow(this);

        LeftRing.Configure(62, 6, 15, 12);
        RightRing.Configure(62, 6, 15, 12);
        CaseRing.Configure(62, 6, 15, 12);
        ApplyTheme(ThemePreference.System);

        _hideTimer = this.DispatcherQueue.CreateTimer();
        _hideTimer.IsRepeating = false;
        _hideTimer.Interval = VisibleFor;
        _hideTimer.Tick += (_, _) => Hide();

        WindowHelpers.HideInsteadOfClose(this, Hide);
    }

    /// <summary>
    /// Sets an explicit Light/Dark theme on the content root and paints the card, border, text and rings from the
    /// matching solid palette, so the colours never depend on the system backdrop or on an inherited theme.
    /// </summary>
    public void ApplyTheme(ThemePreference preference)
    {
        try
        {
            var p = WindowHelpers.ApplyPopupTheme(ThemeRoot, preference);
            var background = Palette.Brush(p.Background);
            ThemeRoot.Background = background;
            RootGrid.Background = background;
            WindowHelpers.SetPopupBorderColour(this, p);
            TitleText.Foreground = Palette.Brush(p.Primary);
            DefaultArt.ApplyPalette(p);
            LeftRing.ApplyPalette(p);
            RightRing.ApplyPalette(p);
            CaseRing.ApplyPalette(p);
        }
        catch (Exception ex)
        {
            Log.Error("Could not apply the pop-up theme", ex);
        }
    }

    public void ShowFor(DeviceSnapshot snapshot)
    {
        var settings = App.Host.Settings;
        // Re-apply the theme every time so the pop-up always matches the Settings choice and the Windows app mode.
        ApplyTheme(settings.Theme);
        Fill(snapshot);
        CustomPictureService.ApplyTo(PictureImage, settings.CustomPictureFile);
        DefaultArt.Visibility = PictureImage.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        WindowHelpers.PlacePopup(this, RootGrid, LogicalWidth, LogicalHeight, settings.PopupSize, settings.PopupPosition);

        if (!_visible)
        {
            // Show without activating, so the pop-up never takes focus from what the user is doing.
            AppWindow.Show(activateWindow: false);
            _visible = true;
        }
        NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

        _hideTimer.Stop();
        _hideTimer.Start();
    }

    /// <summary>Refreshes the numbers while the pop-up is on screen, without restarting its timer.</summary>
    public void UpdateIfVisible(DeviceSnapshot snapshot)
    {
        if (_visible)
        {
            Fill(snapshot);
        }
    }

    private void Hide()
    {
        _hideTimer.Stop();
        _visible = false;
        AppWindow.Hide();
    }

    private void Fill(DeviceSnapshot s)
    {
        TitleText.Text = s.DisplayName;
        if (s.Kind == DeviceKind.Headphones)
        {
            LeftRing.Set("Battery", s.LeftBattery, s.LeftCharging);
            RightRing.Visibility = Visibility.Collapsed;
            CaseRing.Visibility = Visibility.Collapsed;
            return;
        }

        RightRing.Visibility = Visibility.Visible;
        CaseRing.Visibility = Visibility.Visible;
        LeftRing.Set("Left", s.LeftBattery, s.LeftCharging);
        RightRing.Set("Right", s.RightBattery, s.RightCharging);
        CaseRing.Set("Case", s.CaseBattery, s.CaseCharging);
    }

    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e) => Hide();
}
