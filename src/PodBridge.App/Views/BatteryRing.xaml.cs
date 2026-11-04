using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PodBridge.Core.Display;
using Windows.Foundation;

namespace PodBridge.App.Views;

/// <summary>
/// Circular battery gauge. The arc is a Path with an ArcSegment whose end point and large-arc flag
/// come from <see cref="RingGeometry"/>. Colours come from a <see cref="PopupPalette"/>, never from theme resources.
/// </summary>
public sealed partial class BatteryRing : UserControl
{
    private double _diameter = 64;
    private double _thickness = 6;
    private string _label = string.Empty;
    private int? _percent;
    private bool _charging;
    private string? _detail;
    private PopupPalette _palette = PopupPalette.Light;

    public BatteryRing()
    {
        InitializeComponent();
        Render();
    }

    /// <summary>Sets the ring's size in logical pixels and its text sizes.</summary>
    public void Configure(double diameter, double thickness, double valueFontSize, double labelFontSize)
    {
        _diameter = diameter;
        _thickness = thickness;
        RingHost.Width = diameter;
        RingHost.Height = diameter;
        TrackRing.StrokeThickness = thickness;
        ArcPath.StrokeThickness = thickness;
        ValueText.FontSize = valueFontSize;
        BoltIcon.FontSize = Math.Max(9, valueFontSize * 0.66);
        LabelText.FontSize = labelFontSize;
        DetailText.FontSize = Math.Max(10, labelFontSize - 1);
        Render();
    }

    public void ApplyPalette(PopupPalette palette)
    {
        _palette = palette;
        Render();
    }

    public void Set(string label, int? percent, bool charging, string? detail = null)
    {
        _label = label;
        _percent = percent;
        _charging = charging;
        _detail = detail;
        Render();
    }

    private void Render()
    {
        try
        {
            var p = _palette;
            var tone = BatteryToneRules.For(_percent, _charging);
            var toneBrush = Palette.Brush(p.ToneColour(tone));

            TrackRing.Stroke = Palette.Brush(p.Track);
            ArcPath.Stroke = toneBrush;
            ValueText.Foreground = Palette.Brush(p.Primary);
            LabelText.Foreground = Palette.Brush(p.Secondary);
            DetailText.Foreground = Palette.Brush(p.Secondary);
            BoltIcon.Foreground = Palette.Brush(p.Good);

            ValueText.Text = Format.Percent(_percent);
            LabelText.Text = _label;
            BoltIcon.Visibility = _charging && _percent is not null ? Visibility.Visible : Visibility.Collapsed;
            DetailText.Text = _detail ?? string.Empty;
            DetailText.Visibility = string.IsNullOrEmpty(_detail) ? Visibility.Collapsed : Visibility.Visible;

            double centre = _diameter / 2;
            double radius = Math.Max(1, (_diameter - _thickness) / 2);
            var arc = RingGeometry.ForPercent(centre, centre, radius, _percent ?? 0);
            if (_percent is null || arc.IsEmpty)
            {
                ArcPath.Data = null;
                ArcPath.Visibility = Visibility.Collapsed;
            }
            else
            {
                var segment = new ArcSegment
                {
                    Point = new Point(arc.End.X, arc.End.Y),
                    Size = new Size(radius, radius),
                    SweepDirection = SweepDirection.Clockwise,
                    IsLargeArc = arc.IsLargeArc,
                };
                var figure = new PathFigure { StartPoint = new Point(arc.Start.X, arc.Start.Y), IsClosed = false };
                figure.Segments.Add(segment);
                var geometry = new PathGeometry();
                geometry.Figures.Add(figure);
                ArcPath.Data = geometry;
                ArcPath.Visibility = Visibility.Visible;
            }

            string state = _percent is int v ? $"{v} percent" : "unknown";
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(this,
                $"{_label}, {state}{(_charging ? ", charging" : string.Empty)}");
        }
        catch (Exception ex)
        {
            Log.Error("Could not draw a battery ring", ex);
        }
    }
}
