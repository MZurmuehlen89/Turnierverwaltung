using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace Fussballturnierverwaltung.WinUi.Views;

public enum SpielfeldDarstellung
{
    /// <summary>Wo der Block steht, mit Pfeil in die Richtung (Stufe −2 … 2).</summary>
    Grundausrichtung,
    /// <summary>Der Block in der Mitte, blasse Kopien und eine Spanne zeigen, wie weit er sich bewegt (Stufe −1 starr
    /// … 1 flexibel); starr bekommt einen Fixpunkt.</summary>
    Flexibilitaet
}

/// <summary>
/// Spielfeld-Symbol der Spielweise (#667): ein kleines Spielfeld (64 × 24, eigenes Tor links) mit dem
/// Mannschaftsblock, im Code gezeichnet. Die beiden Farben kommen per <c>{ThemeResource}</c> aus dem XAML, der Zustand
/// aus <see cref="IstGewaehlt"/>: Die geerbte Vordergrundfarbe des Knopfs kommt bei einem eigenen Control nicht an –
/// das Symbol blieb in der Farbe stehen, die es beim Laden hatte (im Hellmodus schwarz, im Dunkelmodus weiß, auch auf
/// der Akzentfläche; Nutzerbefund in #667).
/// </summary>
public sealed class SpielfeldSymbol : UserControl
{
    private const double FELD_DECKKRAFT = 0.45;
    private const double MITTE = 27;
    private readonly Canvas _flaeche = new() { Width = 64, Height = 24 };

    public static readonly DependencyProperty DarstellungProperty = DependencyProperty.Register(
        nameof(Darstellung), typeof(SpielfeldDarstellung), typeof(SpielfeldSymbol),
        new PropertyMetadata(SpielfeldDarstellung.Grundausrichtung, (d, _) => ((SpielfeldSymbol)d).Zeichnen()));

    public static readonly DependencyProperty StufeProperty = DependencyProperty.Register(
        nameof(Stufe), typeof(int), typeof(SpielfeldSymbol), new PropertyMetadata(0, (d, _) => ((SpielfeldSymbol)d).Zeichnen()));

    public static readonly DependencyProperty FarbeProperty = DependencyProperty.Register(
        nameof(Farbe), typeof(Brush), typeof(SpielfeldSymbol), new PropertyMetadata(null, (d, _) => ((SpielfeldSymbol)d).Zeichnen()));

    public static readonly DependencyProperty GewaehltFarbeProperty = DependencyProperty.Register(
        nameof(GewaehltFarbe), typeof(Brush), typeof(SpielfeldSymbol), new PropertyMetadata(null, (d, _) => ((SpielfeldSymbol)d).Zeichnen()));

    public static readonly DependencyProperty IstGewaehltProperty = DependencyProperty.Register(
        nameof(IstGewaehlt), typeof(bool), typeof(SpielfeldSymbol), new PropertyMetadata(false, (d, _) => ((SpielfeldSymbol)d).Zeichnen()));

    /// <summary>Farbe auf dem nicht gewählten Knopf.</summary>
    public Brush? Farbe
    {
        get => (Brush?)GetValue(FarbeProperty);
        set => SetValue(FarbeProperty, value);
    }

    /// <summary>Farbe auf der Akzentfläche des gewählten Knopfs.</summary>
    public Brush? GewaehltFarbe
    {
        get => (Brush?)GetValue(GewaehltFarbeProperty);
        set => SetValue(GewaehltFarbeProperty, value);
    }

    public bool IstGewaehlt
    {
        get => (bool)GetValue(IstGewaehltProperty);
        set => SetValue(IstGewaehltProperty, value);
    }

    public SpielfeldDarstellung Darstellung
    {
        get => (SpielfeldDarstellung)GetValue(DarstellungProperty);
        set => SetValue(DarstellungProperty, value);
    }

    public int Stufe
    {
        get => (int)GetValue(StufeProperty);
        set => SetValue(StufeProperty, value);
    }

    public SpielfeldSymbol()
    {
        Content = _flaeche;
        IsTabStop = false;
        Loaded += (_, _) => Zeichnen();
    }

    private void Zeichnen()
    {
        _flaeche.Children.Clear();
        if ((IstGewaehlt ? GewaehltFarbe : Farbe) is not { } farbe) return;

        Hinzu(new Rectangle { Width = 62, Height = 22, Stroke = farbe, StrokeThickness = 1, RadiusX = 2, RadiusY = 2, Opacity = FELD_DECKKRAFT }, 1, 1);
        Hinzu(new Line { X1 = 32, Y1 = 1, X2 = 32, Y2 = 23, Stroke = farbe, StrokeThickness = 1, Opacity = FELD_DECKKRAFT }, 0, 0);
        Hinzu(new Rectangle { Width = 3, Height = 8, Fill = farbe, Opacity = FELD_DECKKRAFT }, 1, 8);
        if (Darstellung == SpielfeldDarstellung.Grundausrichtung) GrundausrichtungZeichnen(farbe);
        else FlexibilitaetZeichnen(farbe);
    }

    private void GrundausrichtungZeichnen(Brush farbe)
    {
        double x = 6 + (Stufe + 2) * 9;
        Block(x, farbe, 1);
        if (Stufe < 0) Pfeil(x - 1, -1, farbe);
        else if (Stufe > 0) Pfeil(x + 11, 1, farbe);
    }

    private void Pfeil(double x, int richtung, Brush farbe)
    {
        Linie(farbe, new(x, 12), new(x + 4 * richtung, 12));
        Linie(farbe, new(x + 2 * richtung, 10), new(x + 4 * richtung, 12), new(x + 2 * richtung, 14));
    }

    private void FlexibilitaetZeichnen(Brush farbe)
    {
        double weite = (Stufe + 1) * 9;
        if (weite == 0)
        {
            Linie(farbe, new(30, 21), new(34, 21));
            Linie(farbe, new(32, 19), new(32, 23));
        }
        else
        {
            Block(MITTE - weite, farbe, 0.25);
            Block(MITTE + weite, farbe, 0.25);
            Linie(farbe, new(MITTE - weite, 21), new(MITTE + 10 + weite, 21));
            Linie(farbe, new(MITTE - weite, 19), new(MITTE - weite, 23));
            Linie(farbe, new(MITTE + 10 + weite, 19), new(MITTE + 10 + weite, 23));
        }
        Block(MITTE, farbe, 1);
    }

    private void Block(double x, Brush farbe, double deckkraft) =>
        Hinzu(new Rectangle { Width = 10, Height = 14, Fill = farbe, RadiusX = 1.5, RadiusY = 1.5, Opacity = deckkraft }, x, 5);

    private void Linie(Brush farbe, params Point[] punkte)
    {
        var linie = new Polyline { Stroke = farbe, StrokeThickness = 1.2 };
        foreach (var punkt in punkte) linie.Points.Add(punkt);
        Hinzu(linie, 0, 0);
    }

    private void Hinzu(UIElement element, double x, double y)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        _flaeche.Children.Add(element);
    }
}
