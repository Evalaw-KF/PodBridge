using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using PodBridge.Core.Settings;
using Windows.Graphics;

namespace PodBridge.App.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Title = "PodBridge";
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "podbridge.ico");
        if (File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
        }

        double scale = WindowHelpers.GetScale(this);
        AppWindow.Resize(new SizeInt32((int)(440 * scale), (int)(720 * scale)));

        // Closing the window only hides it; PodBridge keeps running in the tray until "Exit".
        AppWindow.Closing += (sender, args) =>
        {
            if (!App.Host.IsExiting)
            {
                args.Cancel = true;
                sender.Hide();
            }
        };

        ContentFrame.Navigate(typeof(MainPage));
    }

    public void ApplyTheme(ThemePreference preference) =>
        WindowHelpers.ApplyTheme(RootGrid, preference);

    /// <summary>Shows the window in front, on the device page or the settings page.</summary>
    public void Present(bool showSettings)
    {
        var target = showSettings ? typeof(SettingsPage) : typeof(MainPage);
        if (ContentFrame.CurrentSourcePageType != target)
        {
            ContentFrame.Navigate(target);
        }

        AppWindow.Show();
        Activate();
    }
}
