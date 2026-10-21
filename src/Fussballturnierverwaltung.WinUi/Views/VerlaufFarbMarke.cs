using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Farbfläche einer hervorgehobenen Mannschaft im Tabellenverlauf (#480), z. B. in der Legende — dieselbe
/// Farbe wie ihre Linie (<c>VerlaufFarbe{n}Brush</c>, nach dem Theme des Elements).</summary>
public sealed class VerlaufFarbMarke : Grid
{
    public static readonly DependencyProperty FarbeProperty = DependencyProperty.Register(
        nameof(Farbe), typeof(int), typeof(VerlaufFarbMarke),
        new PropertyMetadata(0, (d, _) => ((VerlaufFarbMarke)d).FarbeAnwenden()));

    /// <summary>Farbnummer 1 bis 3.</summary>
    public int Farbe
    {
        get => (int)GetValue(FarbeProperty);
        set => SetValue(FarbeProperty, value);
    }

    public VerlaufFarbMarke()
    {
        Loaded += (_, _) => FarbeAnwenden();
        ActualThemeChanged += (_, _) => FarbeAnwenden();
    }

    private void FarbeAnwenden() => Background = ThemeFarben.Brush(this, $"VerlaufFarbe{Farbe}Brush");
}
