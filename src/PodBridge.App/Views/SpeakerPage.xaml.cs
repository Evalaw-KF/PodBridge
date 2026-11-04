using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PodBridge.Core.Speaker;

namespace PodBridge.App.Views;

/// <summary>Lists the paired devices that can use this PC as a Bluetooth speaker. Rebuilt whenever something changes.</summary>
public sealed partial class SpeakerPage : Page
{
    public SpeakerPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (App.Host.Speaker is { } speaker)
            {
                speaker.Changed += Rebuild;
            }
            Rebuild();
        };
        Unloaded += (_, _) =>
        {
            if (App.Host.Speaker is { } speaker)
            {
                speaker.Changed -= Rebuild;
            }
        };
    }

    private void Rebuild()
    {
        try
        {
            DevicesPanel.Children.Clear();
            var speaker = App.Host.Speaker;
            if (speaker is null || speaker.UnavailableReason is not null)
            {
                StatusText.Text = speaker?.UnavailableReason ?? "Speaker mode could not be started on this PC.";
                return;
            }

            var entries = speaker.Registry.Snapshot();
            StatusText.Text = entries.Count > 0 ? string.Empty
                : speaker.Scanning ? "Looking for paired devices..."
                : "No paired devices found. Pair your phone with this PC in Windows Settings > Bluetooth & devices first.";

            var auto = App.Host.Settings.AutoEnableSpeakerDevices;
            foreach (var entry in entries)
            {
                DevicesPanel.Children.Add(BuildCard(speaker, entry, auto.Contains(entry.Id)));
            }
        }
        catch (Exception ex)
        {
            Log.Error("Could not show the speaker devices", ex);
        }
    }

    private static Border BuildCard(Services.SpeakerService speaker, SpeakerEntry entry, bool autoEnabled)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = entry.Name,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(new TextBlock { Text = SpeakerText.Describe(entry), TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        string id = entry.Id;

        var enable = new Button { Content = "Enable", IsEnabled = SpeakerText.CanEnable(entry.State) };
        enable.Click += async (_, _) => await speaker.EnableAsync(id);
        var open = new Button { Content = "Open", IsEnabled = SpeakerText.CanOpen(entry.State) };
        open.Click += async (_, _) => await speaker.OpenAsync(id);
        var disable = new Button { Content = "Disable", IsEnabled = SpeakerText.CanDisable(entry.State) };
        disable.Click += (_, _) => speaker.Disable(id);
        buttons.Children.Add(enable);
        buttons.Children.Add(open);
        buttons.Children.Add(disable);
        panel.Children.Add(buttons);

        var check = new CheckBox { Content = "Enable automatically", IsChecked = autoEnabled };
        check.Checked += async (_, _) =>
        {
            App.Host.SetSpeakerAutoEnable(id, true);
            await speaker.EnableAsync(id);
        };
        check.Unchecked += (_, _) => App.Host.SetSpeakerAutoEnable(id, false);
        panel.Children.Add(check);

        return new Border { Style = (Style)Application.Current.Resources["CardBorderStyle"], Child = panel };
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(MainPage));
}
