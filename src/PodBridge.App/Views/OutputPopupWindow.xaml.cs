using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PodBridge.App.Native;
using PodBridge.Core.Settings;
using Windows.Graphics;

namespace PodBridge.App.Views;

/// <summary>
/// Small card shown when the default playback device changes: the device name and a volume slider
/// bound to that device's master volume. Same look as <see cref="PopupWindow"/>, never takes focus,
/// and hides itself after a few seconds without interaction.
/// </summary>
public sealed partial class OutputPopupWindow : Window
{
    private const int LogicalWidth = 340;
    private const int LogicalHeight = 112;
    private const int LogicalMargin = 16;
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

        _hwnd = WindowHelpers.GetHandle(this);
        SystemBackdrop = new DesktopAcrylicBackdrop();

        var presenter = OverlappedPresenter.Create();
        presenter.IsAlwaysOnTop = true;
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.SetBorderAndTitleBar(false, false);
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;

        // Tool window + no-activate: no taskbar button, never steals focus (mouse clicks on the slider still work).
        long style = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        style |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(style));

        int round = NativeMethods.DWMWCP_ROUND;
        NativeMethods.DwmSetWindowAttribute(_hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));

        _hideTimer = DispatcherQueue.CreateTimer();
        _hideTimer.IsRepeating = false;
        _hideTimer.Interval = IdleHideAfter;
        _hideTimer.Tick += (_, _) => Hide();

        // Follows volume changes made elsewhere (keyboard keys, other apps) while the card is visible.
        _pollTimer = DispatcherQueue.CreateTimer();
        _pollTimer.IsRepeating = true;
        _pollTimer.Interval = VolumePollEvery;
        _pollTimer.Tick += (_, _) => { if (!_pointerInside) { ReadVolume(); } };

        AppWindow.Closing += (sender, args) =>
        {
            if (!App.Host.IsExiting)
            {
                args.Cancel = true;
                Hide();
            }
        };
    }

    public void ApplyTheme(ThemePreference preference) =>
        WindowHelpers.ApplyTheme(RootGrid, preference);

    /// <summary>Shows the card for the current default output.</summary>
    public void ShowFor(string deviceName)
    {
        TitleText.Text = deviceName;
        ReadVolume();
        PositionBottomCenter();

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

    private void PositionBottomCenter()
    {
        // Two passes: the first may run at the DPI of the wrong monitor, the second corrects it.
        for (int pass = 0; pass < 2; pass++)
        {
            double scale = WindowHelpers.GetScale(this);
            int width = (int)(LogicalWidth * scale);
            int height = (int)(LogicalHeight * scale);
            int margin = (int)(LogicalMargin * scale);

            var work = DisplayArea.Primary.WorkArea;
            int x = work.X + (work.Width - width) / 2;
            int y = work.Y + work.Height - height - margin;
            AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
        }
    }
}
