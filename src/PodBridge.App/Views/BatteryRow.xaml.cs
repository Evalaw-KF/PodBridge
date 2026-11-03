using Microsoft.UI.Xaml.Controls;

namespace PodBridge.App.Views;

public sealed partial class BatteryRow : UserControl
{
    public BatteryRow()
    {
        InitializeComponent();
    }

    public void Set(string label, int? percent, bool charging, string? extra = null)
    {
        LabelText.Text = label;
        Bar.Value = percent ?? 0;
        ValueText.Text = Format.Percent(percent);

        var parts = new List<string>();
        if (charging)
        {
            parts.Add("Charging");
        }
        if (!string.IsNullOrEmpty(extra))
        {
            parts.Add(extra);
        }
        DetailText.Text = string.Join(" · ", parts);
        DetailText.Visibility = parts.Count == 0
            ? Microsoft.UI.Xaml.Visibility.Collapsed
            : Microsoft.UI.Xaml.Visibility.Visible;
    }
}
