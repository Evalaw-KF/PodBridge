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
        LeftRing.Configure(88, 8, 20, 14);
        RightRing.Configure(88, 8, 20, 14);
        CaseRing.Configure(88, 8, 20, 14);
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
        ConnectButton.IsEnabled = host.CanChangeConnection;
        DisconnectButton.IsEnabled = host.CanChangeConnection;
        SwitchOutputButton.IsEnabled = host.CanChangeConnection;
    }

    private void ConnectButton_Click(object sender, RoutedEventArgs e) => App.Host.RequestConnect();

    private void DisconnectButton_Click(object sender, RoutedEventArgs e) => App.Host.RequestDisconnect();

    private void SwitchOutputButton_Click(object sender, RoutedEventArgs e) => App.Host.RequestSwitchOutput();

    private void SpeakerButton_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(SpeakerPage));

    private void SettingsButton_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(SettingsPage));
}
