using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using PodBridge.App.Native;
using PodBridge.Core.Display;
using PodBridge.Core.Settings;

namespace PodBridge.App.Views;

/// <summary>
/// Small banner shown when the AirPods audio connects or disconnects: an earbud, a title, the battery if known.
/// Same infrastructure as the other pop-ups (solid card, never takes focus), hides itself after about 3 seconds.
/// </summary>
public sealed partial class BannerWindow : Window
{
    private const int LogicalWidth = 340;
    private const int LogicalHeight = 76;
    private static readonly TimeSpan VisibleFor = TimeSpan.FromSeconds(3);

    private readonly IntPtr _hwnd;
    private readonly DispatcherQueueTimer _hideTimer;
    private bool _visible;
    private PopupPalette _palette = PopupPalette.Light;

    public BannerWindow()
    {
        InitializeComponent();

        _hwnd = WindowHelpers.ConfigurePopupWindow(this);
        ApplyTheme(ThemePreference.System);

        _hideTimer = DispatcherQueue.CreateTimer();
        _hideTimer.IsRepeating = false;
        _hideTimer.Interval = VisibleFor;
        _hideTimer.Tick += (_, _) => Hide();

        WindowHelpers.HideInsteadOfClose(this, Hide);
    }

    public void ApplyTheme(ThemePreference preference)
    {
        try
        {
            _palette = WindowHelpers.ApplyPopupTheme(ThemeRoot, preference);
            var p = _palette;
            var background = Palette.Brush(p.Background);
            ThemeRoot.Background = background;
            RootGrid.Background = background;
            RootGrid.BorderBrush = Palette.Brush(p.Border);
            GlyphDisc.Background = Palette.Brush(p.Track);
            Bud.ApplyPalette(new PopupPalette(p.Track, p.Border, p.Primary, p.Secondary, p.Track, p.Good, p.Warning,
                p.Critical, p.Secondary));
            TitleText.Foreground = Palette.Brush(p.Primary);
            DetailText.Foreground = Palette.Brush(p.Secondary);
        }
        catch (Exception ex)
        {
            Log.Error("Could not apply the banner theme", ex);
        }
    }

    public void ShowFor(string title, string? detail, bool connected)
    {
        var settings = App.Host.Settings;
        ApplyTheme(settings.Theme);
        TitleText.Text = title;
        DetailText.Text = detail ?? string.Empty;
        DetailText.Visibility = string.IsNullOrEmpty(detail) ? Visibility.Collapsed : Visibility.Visible;
        StateDot.Fill = Palette.Brush(connected ? _palette.Good : _palette.Secondary);

        WindowHelpers.PlacePopup(this, RootGrid, LogicalWidth, LogicalHeight, settings.PopupSize,
            PopupLayout.OppositeEdgeCentre(settings.PopupPosition), cornerRadius: 24);

        if (!_visible)
        {
            AppWindow.Show(activateWindow: false);
            _visible = true;
        }
        NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void Hide()
    {
        _hideTimer.Stop();
        _visible = false;
        AppWindow.Hide();
    }

    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e) => Hide();
}
