using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using PodBridge.App.Native;
using PodBridge.Core.Display;
using PodBridge.Core.Settings;

namespace PodBridge.App.Views;

/// <summary>
/// Small card shown when the default playback device changes: the device name and a volume slider
/// bound to that device's master volume. Same look as <see cref="PopupWindow"/>, never takes focus,
/// and hides itself after a few seconds without interaction.
/// </summary>
public sealed partial class OutputPopupWindow : Window
{
    private const int LogicalWidth = 360;
    private const int LogicalHeight = 112;
    private static readonly TimeSpan IdleHideAfter = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan VolumePollEvery = TimeSpan.FromMilliseconds(300);

    private readonly IntPtr _hwnd;
    private readonly DispatcherQueueTimer _hideTimer;
    private readonly DispatcherQueueTimer _pollTimer;
    private bool _visible;
    private bool _pointerInside;
    private bool _settingSlider;

    public OutputPopupWindow()
    {
        InitializeComponent();

        // No acrylic, never activated: the card paints its own solid, theme-aware background (see ApplyTheme).
        _hwnd = WindowHelpers.ConfigurePopupWindow(this);
        ApplyTheme(ThemePreference.System);

        _hideTimer = DispatcherQueue.CreateTimer();
        _hideTimer.IsRepeating = false;
        _hideTimer.Interval = IdleHideAfter;
        _hideTimer.Tick += (_, _) => Hide();

        // Follows volume changes made elsewhere (keyboard keys, other apps) while the card is visible.
        _pollTimer = DispatcherQueue.CreateTimer();
        _pollTimer.IsRepeating = true;
        _pollTimer.Interval = VolumePollEvery;
        _pollTimer.Tick += (_, _) => { if (!_pointerInside) { ReadVolume(); } };

        WindowHelpers.HideInsteadOfClose(this, Hide);
    }

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
            TitleIcon.Foreground = Palette.Brush(p.Secondary);
            LowIcon.Foreground = Palette.Brush(p.Secondary);
            HighIcon.Foreground = Palette.Brush(p.Secondary);
            VolumeText.Foreground = Palette.Brush(p.Primary);
        }
        catch (Exception ex)
        {
            Log.Error("Could not apply the volume pop-up theme", ex);
        }
    }

    /// <summary>Shows the card for the current default output.</summary>
    public void ShowFor(string deviceName)
    {
        var settings = App.Host.Settings;
        ApplyTheme(settings.Theme);
        TitleText.Text = deviceName;
        ReadVolume();
        // Same screen edge as the battery pop-up, but centred so the two never cover each other.
        WindowHelpers.PlacePopup(this, RootGrid, LogicalWidth, LogicalHeight, settings.PopupSize,
            PopupLayout.CentreHorizontally(settings.PopupPosition));

        if (!_visible)
        {
            AppWindow.Show(activateWindow: false);
            _visible = true;
            _pollTimer.Start();
        }
        NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

        RestartHideTimer();
    }

    private void Hide()
    {
        _hideTimer.Stop();
        _pollTimer.Stop();
        _visible = false;
        _pointerInside = false;
        AppWindow.Hide();
    }

    private void RestartHideTimer()
    {
        _hideTimer.Stop();
        if (!_pointerInside)
        {
            _hideTimer.Start();
        }
    }

    private void ReadVolume()
    {
        var level = App.Host.Audio.GetVolume();
        _settingSlider = true;
        try
        {
            VolumeSlider.IsEnabled = level is not null;
            double percent = Math.Round((level ?? 0f) * 100);
            VolumeSlider.Value = percent;
            VolumeText.Text = level is null ? "–" : $"{percent:0}%";
        }
        finally
        {
            _settingSlider = false;
        }
    }

    private void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_settingSlider)
        {
            return;
        }
        VolumeText.Text = $"{Math.Round(e.NewValue):0}%";
        App.Host.Audio.SetVolume((float)(e.NewValue / 100.0));
        RestartHideTimer();
    }

    private void RootGrid_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _pointerInside = true;
        _hideTimer.Stop();
    }

    private void RootGrid_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _pointerInside = false;
        RestartHideTimer();
    }

    private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e) => RestartHideTimer();
}
