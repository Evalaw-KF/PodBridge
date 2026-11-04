using Microsoft.UI.Xaml.Controls;
using PodBridge.Core.Display;

namespace PodBridge.App.Views;

/// <summary>An original illustration of earbuds and a case, made of XAML shapes (no artwork files).</summary>
public sealed partial class PodsArt : UserControl
{
    public PodsArt()
    {
        InitializeComponent();
        ApplyPalette(PopupPalette.Light);
    }

    public void ApplyPalette(PopupPalette p)
    {
        LeftBud.ApplyPalette(p);
        RightBud.ApplyPalette(p);
        CaseBody.Background = Palette.Brush(p.Illustration);
        CaseLid.Fill = Palette.Brush(p.Background);
        CaseLed.Fill = Palette.Brush(p.Good);
    }
}
