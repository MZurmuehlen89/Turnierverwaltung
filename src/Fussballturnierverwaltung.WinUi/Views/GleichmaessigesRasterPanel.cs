using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Issue #407: Karten nebeneinander in gleichmäßig gefüllten Zeilen, wie die Gruppen-Grafik einer
/// Fernsehauslosung (8 Gruppen = 2 × 4, 6 = 2 × 3, 12 = 3 × 4). Ein freier Umbruch nach Fensterbreite ergäbe
/// schiefe Zeilen (5 + 3); hier bestimmt die Breite nur, wie viele Spalten höchstens passen, danach werden
/// die Zeilen ausgeglichen. Als <c>ItemsPanel</c> eines <see cref="ItemsControl"/> gedacht.</summary>
public sealed class GleichmaessigesRasterPanel : Panel
{
    public static readonly DependencyProperty MaxSpaltenProperty = DependencyProperty.Register(
        nameof(MaxSpalten), typeof(int), typeof(GleichmaessigesRasterPanel), new PropertyMetadata(4, LayoutGeaendert));

    public static readonly DependencyProperty MinSpaltenBreiteProperty = DependencyProperty.Register(
        nameof(MinSpaltenBreite), typeof(double), typeof(GleichmaessigesRasterPanel), new PropertyMetadata(200d, LayoutGeaendert));

    public static readonly DependencyProperty MaxSpaltenBreiteProperty = DependencyProperty.Register(
        nameof(MaxSpaltenBreite), typeof(double), typeof(GleichmaessigesRasterPanel), new PropertyMetadata(300d, LayoutGeaendert));

    public static readonly DependencyProperty AbstandProperty = DependencyProperty.Register(
        nameof(Abstand), typeof(double), typeof(GleichmaessigesRasterPanel), new PropertyMetadata(8d, LayoutGeaendert));

    private int _spalten = 1;
    private double _spaltenBreite;
    private double[] _zeilenHoehen = [];

    /// <summary>Obergrenze der Karten je Zeile.</summary>
    public int MaxSpalten
    {
        get => (int)GetValue(MaxSpaltenProperty);
        set => SetValue(MaxSpaltenProperty, value);
    }

    /// <summary>Unterschreitet eine Karte diese Breite, sinkt die Spaltenzahl.</summary>
    public double MinSpaltenBreite
    {
        get => (double)GetValue(MinSpaltenBreiteProperty);
        set => SetValue(MinSpaltenBreiteProperty, value);
    }

    /// <summary>Breiter werden die Karten nicht — wenige Karten bleiben linksbündig statt sich aufzublähen.</summary>
    public double MaxSpaltenBreite
    {
        get => (double)GetValue(MaxSpaltenBreiteProperty);
        set => SetValue(MaxSpaltenBreiteProperty, value);
    }

    public double Abstand
    {
        get => (double)GetValue(AbstandProperty);
        set => SetValue(AbstandProperty, value);
    }

    private static void LayoutGeaendert(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((GleichmaessigesRasterPanel)d).InvalidateMeasure();

    protected override Size MeasureOverride(Size availableSize)
    {
        int anzahl = Children.Count;
        if (anzahl == 0)
        {
            _zeilenHoehen = [];
            return new Size(0, 0);
        }

        int maxSpalten = Math.Max(1, MaxSpalten);
        int passend = double.IsInfinity(availableSize.Width)
            ? maxSpalten
            : (int)Math.Floor((availableSize.Width + Abstand) / (MinSpaltenBreite + Abstand));
        int spalten = Math.Clamp(passend, 1, Math.Min(maxSpalten, anzahl));

        // Zeilen ausgleichen: 5 Karten bei 4 möglichen Spalten → 2 Zeilen → 3 + 2 statt 4 + 1.
        int zeilen = (int)Math.Ceiling(anzahl / (double)spalten);
        spalten = (int)Math.Ceiling(anzahl / (double)zeilen);

        double breite = double.IsInfinity(availableSize.Width)
            ? MaxSpaltenBreite
            : Math.Min(MaxSpaltenBreite, (availableSize.Width - Abstand * (spalten - 1)) / spalten);
        breite = Math.Max(0, breite);

        var hoehen = new double[zeilen];
        for (int i = 0; i < anzahl; i++)
        {
            Children[i].Measure(new Size(breite, double.PositiveInfinity));
            hoehen[i / spalten] = Math.Max(hoehen[i / spalten], Children[i].DesiredSize.Height);
        }

        _spalten = spalten;
        _spaltenBreite = breite;
        _zeilenHoehen = hoehen;

        return new Size(
            spalten * breite + Abstand * (spalten - 1),
            hoehen.Sum() + Abstand * (zeilen - 1));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double y = 0;
        for (int i = 0; i < Children.Count; i++)
        {
            int zeile = i / _spalten, spalte = i % _spalten;
            if (spalte == 0 && zeile > 0) y += _zeilenHoehen[zeile - 1] + Abstand;

            // Volle Zeilenhöhe: Karten einer Zeile enden auf derselben Linie, auch bei ungleich vielen Teams.
            Children[i].Arrange(new Rect(spalte * (_spaltenBreite + Abstand), y, _spaltenBreite, _zeilenHoehen[zeile]));
        }
        return finalSize;
    }
}
