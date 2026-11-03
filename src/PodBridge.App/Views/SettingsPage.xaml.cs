using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using PodBridge.Core.Settings;

namespace PodBridge.App.Views;

public sealed partial class SettingsPage : Page
{
    private bool _loading = true;

    public SettingsPage()
    {
        InitializeComponent();

        var s = App.Host.Settings;
        NotifyToggle.IsOn = s.LowBatteryNotifications;
        PopupToggle.IsOn = s.PopupEnabled;
        StartupToggle.IsOn = s.StartWithWindows;
        ThresholdSlider.Value = s.LowBatteryThreshold;
        ThresholdLabel.Text = $"Warn me at {s.LowBatteryThreshold}%";
        ThemeCombo.SelectedIndex = (int)s.Theme;

        _loading = false;
    }

    private void Save()
    {
        if (_loading)
        {
            return;
        }

        App.Host.UpdateSettings(new AppSettings
        {
            LowBatteryNotifications = NotifyToggle.IsOn,
            PopupEnabled = PopupToggle.IsOn,
            StartWithWindows = StartupToggle.IsOn,
            LowBatteryThreshold = (int)Math.Round(ThresholdSlider.Value),
            Theme = ThemeCombo.SelectedIndex switch
            {
                1 => ThemePreference.Light,
                2 => ThemePreference.Dark,
                _ => ThemePreference.System,
            },
        });
    }

    private void Setting_Changed(object sender, RoutedEventArgs e) => Save();

    private void ThresholdSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        ThresholdLabel.Text = $"Warn me at {(int)Math.Round(e.NewValue)}%";
        Save();
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => Save();

    private void TestNotificationButton_Click(object sender, RoutedEventArgs e) =>
        App.Host.ShowTestNotification();

    private void PreviewPopupButton_Click(object sender, RoutedEventArgs e) =>
        App.Host.ShowPopupPreview();

    private void BackButton_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(MainPage));
}
