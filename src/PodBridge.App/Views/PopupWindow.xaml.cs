using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PodBridge.App.Native;
using PodBridge.Core.Bluetooth;
using PodBridge.Core.Settings;
using PodBridge.Core.State;
using Windows.Graphics;

namespace PodBridge.App.Views;

/// <summary>
/// A small card that slides into the bottom-right corner when the case lid opens and
/// hides itself after a few seconds. It never takes keyboard focus.
/// </summary>
public sealed partial class PopupWindow : Window
{
    private const int LogicalWidth = 340;
    private const int LogicalHeight = 130;
    private const int LogicalMargin = 16;
    private static readonly TimeSpan VisibleFor = TimeSpan.FromSeconds(6);

    private readonly IntPtr _hwnd;
    private readonly DispatcherQueueTimer _hideTimer;
    private bool _visible;

    public PopupWindow()
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

        // Tool window + no-activate: no taskbar button, and it never steals focus.
        long style = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        style |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(style));

        // Rounded corners on Windows 11 (ignored on Windows 10).
        int round = NativeMethods.DWMWCP_ROUND;
        NativeMethods.DwmSetWindowAttribute(_hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));

        _hideTimer = this.DispatcherQueue.CreateTimer();
        _hideTimer.IsRepeating = false;
        _hideTimer.Interval = VisibleFor;
        _hideTimer.Tick += (_, _) => Hide();

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

    public void ShowFor(DeviceSnapshot snapshot)
    {
        Fill(snapshot);
        PositionBottomRight();

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
            LeftLabel.Text = s.LeftCharging ? "Battery (charging)" : "Battery";
            LeftValue.Text = Format.Percent(s.LeftBattery);
            RightValue.Text = string.Empty;
            RightLabel.Text = string.Empty;
            CaseValue.Text = string.Empty;
            CaseLabel.Text = string.Empty;
            return;
        }

        LeftValue.Text = Format.Percent(s.LeftBattery);
        RightValue.Text = Format.Percent(s.RightBattery);
        CaseValue.Text = Format.Percent(s.CaseBattery);
        LeftLabel.Text = s.LeftCharging ? "Left (charging)" : "Left";
        RightLabel.Text = s.RightCharging ? "Right (charging)" : "Right";
        CaseLabel.Text = s.CaseCharging ? "Case (charging)" : "Case";
    }

    private void PositionBottomRight()
    {
        // Two passes: the first may run at the DPI of the wrong monitor, the second corrects it.
        for (int pass = 0; pass < 2; pass++)
        {
            double scale = WindowHelpers.GetScale(this);
            int width = (int)(LogicalWidth * scale);
            int height = (int)(LogicalHeight * scale);
            int margin = (int)(LogicalMargin * scale);

            var work = DisplayArea.Primary.WorkArea;
            int x = work.X + work.Width - width - margin;
            int y = work.Y + work.Height - height - margin;
            AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
        }
    }

    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e) => Hide();
}
