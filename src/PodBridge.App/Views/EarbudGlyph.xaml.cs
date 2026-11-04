using Microsoft.UI.Xaml.Controls;
using PodBridge.Core.Display;

namespace PodBridge.App.Views;

/// <summary>An original, stylised earbud drawn with XAML shapes.</summary>
public sealed partial class EarbudGlyph : UserControl
{
    public EarbudGlyph()
    {
        InitializeComponent();
        ApplyPalette(PopupPalette.Light);
    }

    public void ApplyPalette(PopupPalette p)
    {
        var body = Palette.Brush(p.Illustration);
        HeadShape.Fill = body;
        StemShape.Fill = body;
        DetailShape.Fill = Palette.Brush(p.Background);
    }
}
