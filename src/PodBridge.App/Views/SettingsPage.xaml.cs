using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using PodBridge.App.Services;
using PodBridge.Core.Audio;
using PodBridge.Core.Display;
using PodBridge.Core.Notifications;
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
        BannerToggle.IsOn = s.ShowConnectionBanner;
        KeepAliveToggle.IsOn = s.KeepAliveEnabled;
        PopulateFallbackChoices(s);
        HotkeysToggle.IsOn = s.HotkeysEnabled;
        ConnectHotkeyBox.Text = s.ConnectHotkey;
        SwitchHotkeyBox.Text = s.SwitchHotkey;
        ShowHotkeyStatus();

        PopupSizeCombo.SelectedIndex = (int)s.PopupSize;
        PopupPositionCombo.SelectedIndex = (int)s.PopupPosition;
        RefreshPicture();
        ReadAloudToggle.IsOn = s.ReadNotificationsAloud;
        InEarToggle.IsOn = s.ReadOnlyWhenInEar;
        FullTextToggle.IsOn = s.NotificationReadFullText;
        RateSlider.Value = s.SpeechRatePercent;
        RateLabel.Text = $"Speed {s.SpeechRatePercent}%";
        SpeechVolumeSlider.Value = s.SpeechVolumePercent;
        SpeechVolumeLabel.Text = $"Volume {s.SpeechVolumePercent}%";
        PopulateVoices(s);
        RefreshReadAloud();
        RefreshApps();

        AdvertFileText.Text = $"Also saved to {AdvertLog.FilePath}";
        Loaded += (_, _) =>
        {
            App.Host.AdvertLog.Changed += RefreshAdverts;
            App.Host.SeenAppsChanged += RefreshApps;
            App.Host.SettingsChanged += RefreshReadAloud;
            RefreshAdverts();
        };
        Unloaded += (_, _) =>
        {
            App.Host.AdvertLog.Changed -= RefreshAdverts;
            App.Host.SeenAppsChanged -= RefreshApps;
            App.Host.SettingsChanged -= RefreshReadAloud;
        };

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

        var s = App.Host.Settings.Normalized(); // keep values this page does not edit (app lists, picture, ...)
        s.LowBatteryNotifications = NotifyToggle.IsOn;
        s.PopupEnabled = PopupToggle.IsOn;
        s.StartWithWindows = StartupToggle.IsOn;
        s.LowBatteryThreshold = (int)Math.Round(ThresholdSlider.Value);
        s.Theme = ThemeCombo.SelectedIndex switch { 1 => ThemePreference.Light, 2 => ThemePreference.Dark, _ => ThemePreference.System };
        s.AutoPauseEnabled = AutoPauseToggle.IsOn;
        s.ResumeEnabled = ResumeToggle.IsOn;
        s.OnlyUseOneBud = OneBudToggle.IsOn;
        s.HeadphoneNameMatch = NameMatchBox.Text;
        s.AutoSwitchOutput = AutoSwitchToggle.IsOn;
        s.FallbackEndpointId = (FallbackCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? string.Empty;
        s.OutputPopupEnabled = OutputPopupToggle.IsOn;
        s.ShowConnectionBanner = BannerToggle.IsOn;
        s.KeepAliveEnabled = KeepAliveToggle.IsOn;
        s.HotkeysEnabled = HotkeysToggle.IsOn;
        s.ConnectHotkey = ConnectHotkeyBox.Text;
        s.SwitchHotkey = SwitchHotkeyBox.Text;
        s.PopupSize = (PopupSize)Math.Max(0, PopupSizeCombo.SelectedIndex);
        s.PopupPosition = (PopupPosition)Math.Max(0, PopupPositionCombo.SelectedIndex);
        s.ReadNotificationsAloud = ReadAloudToggle.IsOn;
        s.ReadOnlyWhenInEar = InEarToggle.IsOn;
        s.NotificationReadFullText = FullTextToggle.IsOn;
        s.SpeechVoiceId = (VoiceCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? string.Empty;
        s.SpeechRatePercent = (int)Math.Round(RateSlider.Value);
        s.SpeechVolumePercent = (int)Math.Round(SpeechVolumeSlider.Value);
        s.NotificationAllowedApps = AppsPanel.Children.OfType<CheckBox>()
            .Where(c => c.IsChecked == true && c.Tag is string)
            .Select(c => (string)c.Tag)
            .ToList();
        App.Host.UpdateSettings(s);
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

    // ------------------------------------------------------------------
    // Pop-up looks and picture
    // ------------------------------------------------------------------

    private void PopupLook_SelectionChanged(object sender, SelectionChangedEventArgs e) => Save();

    private void RefreshPicture()
    {
        CustomPictureService.ApplyTo(PicturePreview, App.Host.Settings.CustomPictureFile);
        ResetPictureButton.IsEnabled = !string.IsNullOrEmpty(App.Host.Settings.CustomPictureFile);
    }

    private async void ChoosePictureButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string? error = await App.Host.ChangePictureAsync();
            SetStatus(PictureMessage, error);
            RefreshPicture();
        }
        catch (Exception ex)
        {
            Log.Error("Choosing a picture failed", ex);
        }
    }

    private void ResetPictureButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            App.Host.ResetPicture();
            SetStatus(PictureMessage, null);
            RefreshPicture();
        }
        catch (Exception ex)
        {
            Log.Error("Resetting the picture failed", ex);
        }
    }

    // ------------------------------------------------------------------
    // Read notifications aloud
    // ------------------------------------------------------------------

    private void PopulateVoices(AppSettings s)
    {
        VoiceCombo.Items.Clear();
        VoiceCombo.Items.Add(new ComboBoxItem { Content = "Windows default voice", Tag = string.Empty });
        bool found = string.IsNullOrEmpty(s.SpeechVoiceId);
        foreach (var voice in SpeechService.ListVoices())
        {
            VoiceCombo.Items.Add(new ComboBoxItem { Content = voice.Name, Tag = voice.Id });
            found |= voice.Id == s.SpeechVoiceId;
        }
        if (!found)
        {
            VoiceCombo.Items.Add(new ComboBoxItem { Content = "Saved voice (not installed now)", Tag = s.SpeechVoiceId });
        }

        VoiceCombo.SelectedIndex = 0;
        for (int i = 0; i < VoiceCombo.Items.Count; i++)
        {
            if (VoiceCombo.Items[i] is ComboBoxItem item && (item.Tag as string ?? string.Empty) == s.SpeechVoiceId)
            {
                VoiceCombo.SelectedIndex = i;
                break;
            }
        }
    }

    /// <summary>Updates the access message, buttons and which controls can be used.</summary>
    private void RefreshReadAloud()
    {
        var reader = App.Host.Reader;
        var access = reader?.Listener.Access ?? ListenerAccess.Unavailable;
        bool identity = reader?.Listener.HasPackageIdentity ?? false;
        bool usable = reader is not null && ListenerAccessText.FeatureUsable(access);

        ReadAccessText.Text = reader is null
            ? "Reading notifications aloud could not be started on this PC."
            : ListenerAccessText.Describe(access, identity);
        AllowAccessButton.Visibility = ListenerAccessText.CanRequest(access) ? Visibility.Visible : Visibility.Collapsed;
        OpenNotificationSettingsButton.Visibility = ListenerAccessText.ShowOpenSettings(access) ? Visibility.Visible : Visibility.Collapsed;

        ReadAloudToggle.IsEnabled = usable;
        ReadOptionsPanel.Visibility = usable ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Rebuilds the ticked list of apps seen so far.</summary>
    private void RefreshApps()
    {
        var s = App.Host.Settings;
        bool wasLoading = _loading;
        _loading = true; // building the list must not trigger saves
        try
        {
            AppsPanel.Children.Clear();
            var allowed = new HashSet<string>(s.NotificationAllowedApps, StringComparer.OrdinalIgnoreCase);
            foreach (var app in SeenApps.Sorted(s.NotificationSeenApps))
            {
                var box = new CheckBox { Content = app.Name, Tag = app.Id, IsChecked = allowed.Contains(app.Id) };
                box.Checked += Setting_Changed;
                box.Unchecked += Setting_Changed;
                AppsPanel.Children.Add(box);
            }
            // Keep ticked apps that are no longer in the seen list, so a save never drops them silently.
            foreach (var id in allowed.Where(id => !s.NotificationSeenApps.Any(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase))))
            {
                var box = new CheckBox { Content = id, Tag = id, IsChecked = true };
                box.Checked += Setting_Changed;
                box.Unchecked += Setting_Changed;
                AppsPanel.Children.Add(box);
            }
        }
        finally
        {
            _loading = wasLoading;
        }
    }

    private async void AllowAccessButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await App.Host.RequestNotificationAccessAsync();
            RefreshReadAloud();
        }
        catch (Exception ex)
        {
            Log.Error("Requesting notification access failed", ex);
        }
    }

    private async void OpenNotificationSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-notifications"));
        }
        catch (Exception ex)
        {
            Log.Error("Could not open Windows notification settings", ex);
        }
    }

    private void VoiceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => Save();

    private void RateSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }
        RateLabel.Text = $"Speed {(int)Math.Round(e.NewValue)}%";
        Save();
    }

    private void SpeechVolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }
        SpeechVolumeLabel.Text = $"Volume {(int)Math.Round(e.NewValue)}%";
        Save();
    }

    private void TestSpeechButton_Click(object sender, RoutedEventArgs e) => App.Host.SpeakTest();
}
