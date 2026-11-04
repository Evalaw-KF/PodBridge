using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PodBridge.App.Services;
using PodBridge.Core.Bluetooth;
using PodBridge.Core.Display;
using PodBridge.Core.State;

namespace PodBridge.App.Views;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
        LeftRing.Configure(84, 8, 20, 14);
        RightRing.Configure(84, 8, 20, 14);
        CaseRing.Configure(84, 8, 20, 14);
        ActualThemeChanged += (_, _) => Refresh();
        Loaded += (_, _) =>
        {
            App.Host.StateChanged += Refresh;
            App.Host.SettingsChanged += RefreshPicture;
            Refresh();
            RefreshPicture();
        };
        Unloaded += (_, _) =>
        {
            App.Host.StateChanged -= Refresh;
            App.Host.SettingsChanged -= RefreshPicture;
        };
    }

    /// <summary>Page width (logical px) from which the cards sit side by side.</summary>
    private const double WideBreakpoint = 640;

    private bool? _wide;

    private void LayoutRoot_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyLayout(e.NewSize.Width);

    /// <summary>Side by side when wide (left card spans the two right-hand cards), one column when narrow.</summary>
    private void ApplyLayout(double width)
    {
        try
        {
            bool wide = width >= WideBreakpoint;
            if (_wide == wide)
            {
                return;
            }
            _wide = wide;
            if (wide)
            {
                LeftColumn.Width = new GridLength(300);
                RightColumn.Width = new GridLength(1, GridUnitType.Star);
                Place(HeaderCard, row: 1, column: 0, rowSpan: 2);
                Place(RingsCard, row: 1, column: 1, rowSpan: 1);
                Place(AudioCard, row: 2, column: 1, rowSpan: 1);
                HeaderCard.Margin = new Thickness(0, 0, 16, 0);
                RingsCard.Margin = new Thickness(0, 0, 0, 16);
            }
            else
            {
                LeftColumn.Width = new GridLength(1, GridUnitType.Star);
                RightColumn.Width = new GridLength(0);
                Place(HeaderCard, row: 1, column: 0, rowSpan: 1);
                Place(RingsCard, row: 2, column: 0, rowSpan: 1);
                Place(AudioCard, row: 3, column: 0, rowSpan: 1);
                HeaderCard.Margin = new Thickness(0, 0, 0, 16);
                RingsCard.Margin = new Thickness(0, 0, 0, 16);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Could not lay out the main page", ex);
        }
    }

    private static void Place(FrameworkElement element, int row, int column, int rowSpan)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        Grid.SetRowSpan(element, rowSpan);
    }

    private void RefreshPicture()
    {
        CustomPictureService.ApplyTo(PictureImage, App.Host.Settings.CustomPictureFile);
        DefaultPicture.Visibility = PictureImage.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Refresh()
    {
        try
        {
            RefreshAudio();
            RefreshLook();
            RefreshDevice();
        }
        catch (Exception ex)
        {
            Log.Error("Could not refresh the main page", ex);
        }
    }

    /// <summary>Colours of the rings, pill and picture, taken from the palette that matches the page's effective theme.</summary>
    private void RefreshLook()
    {
        var p = PopupPalette.For(ActualTheme == ElementTheme.Dark);
        LeftRing.ApplyPalette(p);
        RightRing.ApplyPalette(p);
        CaseRing.ApplyPalette(p);
        DefaultPicture.ApplyPalette(p);

        bool connected = App.Host.PodsConnected;
        StatusPill.Background = Palette.Brush(p.Track);
        StatusDot.Fill = Palette.Brush(connected ? p.Good : p.Secondary);
        StatusPillText.Foreground = Palette.Brush(p.Primary);
        StatusPillText.Text = connected ? "Connected" : "Not connected";
    }

    private void RefreshDevice()
    {
        var snapshot = App.Host.Latest;
        if (snapshot is null)
        {
            DeviceName.Text = "No AirPods nearby";
            StatusText.Text = App.Host.ScannerStatus == "Scanning"
                ? "Searching. Open your case lid or put an earbud in your ear."
                : App.Host.ScannerStatus;
            LeftRing.Set("Left", null, false);
            RightRing.Set("Right", null, false);
            CaseRing.Set("Case", null, false);
            RightRing.Visibility = Visibility.Visible;
            CaseRing.Visibility = Visibility.Visible;
            return;
        }

        DeviceName.Text = snapshot.DisplayName;
        StatusText.Text = $"Last updated {DateTimeOffset.Now:t}";

        if (snapshot.Kind == DeviceKind.Headphones)
        {
            LeftRing.Set("Battery", snapshot.LeftBattery, snapshot.LeftCharging);
            RightRing.Visibility = Visibility.Collapsed;
            CaseRing.Visibility = Visibility.Collapsed;
            return;
        }

        RightRing.Visibility = Visibility.Visible;
        CaseRing.Visibility = Visibility.Visible;
        LeftRing.Set("Left", snapshot.LeftBattery, snapshot.LeftCharging, snapshot.LeftInEar ? "In ear" : null);
        RightRing.Set("Right", snapshot.RightBattery, snapshot.RightCharging, snapshot.RightInEar ? "In ear" : null);
        CaseRing.Set("Case", snapshot.CaseBattery, snapshot.CaseCharging, snapshot.LidOpen ? "Lid open" : null);
    }

    private void RefreshAudio()
    {
        var host = App.Host;
        AudioStatusText.Text = host.AudioStatus;
        AudioMessageText.Text = host.AudioMessage;
        AudioMessageText.Visibility = string.IsNullOrEmpty(host.AudioMessage) ? Visibility.Collapsed : Visibility.Visible;
        var toggle = host.ConnectionToggle;
        ConnectToggleButton.Content = toggle.Label;
        ConnectToggleButton.IsEnabled = toggle.Enabled;
        ConnectToggleButton.Style = (Style)Application.Current.Resources[toggle.Accent ? "AccentButtonStyle" : "DefaultButtonStyle"];
        SwitchOutputButton.IsEnabled = host.CanChangeConnection;
    }

    private void ConnectToggleButton_Click(object sender, RoutedEventArgs e)
    {
        // Decide from the live state, so a click right after a change does the sensible thing.
        if (App.Host.PodsConnected)
        {
            App.Host.RequestDisconnect();
        }
        else
        {
            App.Host.RequestConnect();
        }
    }

    private void SwitchOutputButton_Click(object sender, RoutedEventArgs e) => App.Host.RequestSwitchOutput();

    private void SpeakerButton_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(SpeakerPage));

    private void SettingsButton_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(SettingsPage));
}
