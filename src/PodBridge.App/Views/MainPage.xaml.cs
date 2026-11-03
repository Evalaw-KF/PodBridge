using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PodBridge.Core.Bluetooth;
using PodBridge.Core.State;

namespace PodBridge.App.Views;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            App.Host.StateChanged += Refresh;
            Refresh();
        };
        Unloaded += (_, _) => App.Host.StateChanged -= Refresh;
    }

    private void Refresh()
    {
        RefreshAudio();

        var snapshot = App.Host.Latest;
        if (snapshot is null)
        {
            DeviceName.Text = "No AirPods nearby";
            StatusText.Text = App.Host.ScannerStatus == "Scanning"
                ? "Searching. Open your case lid or put an earbud in your ear."
                : App.Host.ScannerStatus;
            LeftRow.Set("Left", null, false);
            RightRow.Set("Right", null, false);
            CaseRow.Set("Case", null, false);
            RightRow.Visibility = Visibility.Visible;
            CaseRow.Visibility = Visibility.Visible;
            return;
        }

        DeviceName.Text = snapshot.DisplayName;
        StatusText.Text = $"Last updated {DateTimeOffset.Now:t}";

        if (snapshot.Kind == DeviceKind.Headphones)
        {
            LeftRow.Set("Battery", snapshot.LeftBattery, snapshot.LeftCharging);
            RightRow.Visibility = Visibility.Collapsed;
            CaseRow.Visibility = Visibility.Collapsed;
            return;
        }

        RightRow.Visibility = Visibility.Visible;
        CaseRow.Visibility = Visibility.Visible;
        LeftRow.Set("Left", snapshot.LeftBattery, snapshot.LeftCharging, snapshot.LeftInEar ? "In ear" : null);
        RightRow.Set("Right", snapshot.RightBattery, snapshot.RightCharging, snapshot.RightInEar ? "In ear" : null);
        CaseRow.Set("Case", snapshot.CaseBattery, snapshot.CaseCharging, snapshot.LidOpen ? "Lid open" : null);
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

    private void SettingsButton_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(SettingsPage));
}
