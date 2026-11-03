using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using PodBridge.App.Services;
using PodBridge.Core.Audio;
using PodBridge.Core.Input;
using PodBridge.Core.Settings;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;

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

        AutoPauseToggle.IsOn = s.AutoPauseEnabled;
        ResumeToggle.IsOn = s.ResumeEnabled;
        OneBudToggle.IsOn = s.OnlyUseOneBud;
        NameMatchBox.Text = s.HeadphoneNameMatch;
        AutoSwitchToggle.IsOn = s.AutoSwitchOutput;
        OutputPopupToggle.IsOn = s.OutputPopupEnabled;
        KeepAliveToggle.IsOn = s.KeepAliveEnabled;
        PopulateFallbackChoices(s);
        HotkeysToggle.IsOn = s.HotkeysEnabled;
        ConnectHotkeyBox.Text = s.ConnectHotkey;
        SwitchHotkeyBox.Text = s.SwitchHotkey;
        ShowHotkeyStatus();

        AdvertFileText.Text = $"Also saved to {AdvertLog.FilePath}";
        Loaded += (_, _) =>
        {
            App.Host.AdvertLog.Changed += RefreshAdverts;
            RefreshAdverts();
        };
        Unloaded += (_, _) => App.Host.AdvertLog.Changed -= RefreshAdverts;

        _loading = false;
    }

    /// <summary>Entry 0 means "the output used before the AirPods"; the others are the playback devices that are active now.</summary>
    private void PopulateFallbackChoices(AppSettings s)
    {
        FallbackCombo.Items.Clear();
        FallbackCombo.Items.Add(new ComboBoxItem { Content = "Automatic (the output used before the AirPods)", Tag = string.Empty });

        var endpoints = App.Host.Audio.ListRenderEndpoints(includeUnplugged: false);
        bool selectedFound = string.IsNullOrEmpty(s.FallbackEndpointId);
        foreach (var endpoint in endpoints)
        {
            if (EndpointMatcher.IsMatch(endpoint.Name, s.HeadphoneNameMatch))
            {
                continue; // the AirPods cannot be their own fallback
            }
            FallbackCombo.Items.Add(new ComboBoxItem { Content = endpoint.Name, Tag = endpoint.Id });
            selectedFound |= endpoint.Id == s.FallbackEndpointId;
        }
        if (!selectedFound)
        {
            // Keep the saved choice even though the device is not plugged in right now.
            FallbackCombo.Items.Add(new ComboBoxItem { Content = "Saved device (not available now)", Tag = s.FallbackEndpointId });
        }

        FallbackCombo.SelectedIndex = 0;
        for (int i = 0; i < FallbackCombo.Items.Count; i++)
        {
            if (FallbackCombo.Items[i] is ComboBoxItem item && (item.Tag as string ?? string.Empty) == s.FallbackEndpointId)
            {
                FallbackCombo.SelectedIndex = i;
                break;
            }
        }
    }

    private void ShowHotkeyStatus()
    {
        var hotkeys = App.Host.Hotkeys;
        SetStatus(ConnectHotkeyStatus, hotkeys?.ConnectStatus);
        SetStatus(SwitchHotkeyStatus, hotkeys?.SwitchStatus);
    }

    private static void SetStatus(TextBlock block, string? text)
    {
        block.Text = text ?? string.Empty;
        block.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private static bool IsDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);

    private void HotkeyBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var box = (TextBox)sender;
        int vk = (int)e.Key;

        if (Hotkey.IsModifierKey(vk))
        {
            e.Handled = true; // wait for the real key
            return;
        }

        bool ctrl = IsDown(VirtualKey.Control);
        bool alt = IsDown(VirtualKey.Menu);
        bool shift = IsDown(VirtualKey.Shift);
        bool win = IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows);
        bool anyModifier = ctrl || alt || shift || win;

        if (!anyModifier && e.Key is VirtualKey.Back or VirtualKey.Delete or VirtualKey.Escape)
        {
            box.Text = string.Empty;
            e.Handled = true;
            Save();
            return;
        }

        if (Hotkey.FromKeyPress(vk, ctrl, alt, shift, win) is { } hotkey)
        {
            box.Text = hotkey.ToString();
            e.Handled = true;
            Save();
        }
        else if (anyModifier)
        {
            e.Handled = true; // unsupported combination: ignore it rather than type into the box
        }
    }

    private void NameMatchBox_LostFocus(object sender, RoutedEventArgs e) => Save();

    private void FallbackCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => Save();

    private void RefreshAdverts() => AdvertText.Text = App.Host.AdvertLog.Text;

    private void CopyAdvertsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var package = new DataPackage();
            package.SetText(App.Host.AdvertLog.Text);
            Clipboard.SetContent(package);
        }
        catch (Exception ex)
        {
            Log.Error("Could not copy adverts to the clipboard", ex);
        }
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
            AutoPauseEnabled = AutoPauseToggle.IsOn,
            ResumeEnabled = ResumeToggle.IsOn,
            OnlyUseOneBud = OneBudToggle.IsOn,
            HeadphoneNameMatch = NameMatchBox.Text,
            AutoSwitchOutput = AutoSwitchToggle.IsOn,
            FallbackEndpointId = (FallbackCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? string.Empty,
            OutputPopupEnabled = OutputPopupToggle.IsOn,
            KeepAliveEnabled = KeepAliveToggle.IsOn,
            HotkeysEnabled = HotkeysToggle.IsOn,
            ConnectHotkey = ConnectHotkeyBox.Text,
            SwitchHotkey = SwitchHotkeyBox.Text,
        });
        ShowHotkeyStatus();
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
