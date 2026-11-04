using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Lässt ein Element bis zum unteren Rand des sichtbaren Bereichs wachsen statt mit fester Höhe (#593). In einer
/// Seite, die als Ganzes scrollt, gibt das Layout dafür kein Maß her — gerechnet wird darum über
/// <see cref="FrameworkElement.EffectiveViewportChanged"/>, das auch feuert, wenn sich das Element durch eine
/// eingeblendete Meldung verschiebt. Der Scrollversatz des umgebenden <see cref="ScrollViewer"/> wird herausgerechnet,
/// sonst wüchse das Element beim Scrollen mit, und die unteren Ränder seiner Vorfahren bis zum ScrollViewer bleiben frei,
/// sonst wäre die Seite um sie zu hoch und scrollte. Annahme: unter dem Element steht nichts Sichtbares mehr. Gesetzt
/// wird die Mindesthöhe; unter ihr scrollt die Seite.</summary>
public static class HoeheBisFensterende
{
    public static readonly DependencyProperty MindestHoeheProperty = DependencyProperty.RegisterAttached(
        "MindestHoehe", typeof(double), typeof(HoeheBisFensterende), new PropertyMetadata(double.NaN, BeiGeaendert));

    public static double GetMindestHoehe(DependencyObject element) => (double)element.GetValue(MindestHoeheProperty);

    public static void SetMindestHoehe(DependencyObject element, double wert) => element.SetValue(MindestHoeheProperty, wert);

    private static void BeiGeaendert(DependencyObject objekt, DependencyPropertyChangedEventArgs e)
    {
        if (objekt is not FrameworkElement element) return;
        element.EffectiveViewportChanged -= Anpassen;
        if (!double.IsNaN((double)e.NewValue)) element.EffectiveViewportChanged += Anpassen;
    }

    // EffectiveViewport steht in Koordinaten des Elements: Bottom ist der Abstand von seiner Oberkante zum unteren Rand
    // des sichtbaren Bereichs. Scrollt die Seite um d nach unten, wächst Bottom um d — darum der Versatz abgezogen.
    private static void Anpassen(FrameworkElement element, EffectiveViewportChangedEventArgs args)
    {
        if (args.EffectiveViewport.IsEmpty) return;
        var (scrollViewer, raenderUnten) = UmgebenderScrollViewer(element);
        double versatz = scrollViewer?.VerticalOffset ?? 0;
        // Ein Pixel Luft gegen Rundung — sonst bliebe eine Scrollleiste für den Bruchteil stehen.
        double hoehe = Math.Max(GetMindestHoehe(element), Math.Floor(args.EffectiveViewport.Bottom - versatz - raenderUnten) - 1);
        if (double.IsNaN(element.Height) || Math.Abs(element.Height - hoehe) > 0.5)
            element.Height = hoehe;
    }

    /// <summary>Der nächste ScrollViewer über dem Element und die Summe der unteren Ränder (Margin, Padding) des Elements
    /// und seiner Vorfahren bis dorthin.</summary>
    private static (ScrollViewer? ScrollViewer, double RaenderUnten) UmgebenderScrollViewer(FrameworkElement element)
    {
        double raender = element.Margin.Bottom;
        for (var eltern = VisualTreeHelper.GetParent(element); eltern is not null; eltern = VisualTreeHelper.GetParent(eltern))
        {
            if (eltern is ScrollViewer scrollViewer) return (scrollViewer, raender);
            raender += RandUnten(eltern);
        }
        return (null, raender);
    }

    private static double RandUnten(DependencyObject eltern) =>
        ((eltern as FrameworkElement)?.Margin.Bottom ?? 0)
        + eltern switch
        {
            Grid grid => grid.Padding.Bottom,
            StackPanel stackPanel => stackPanel.Padding.Bottom,
            Border border => border.Padding.Bottom + border.BorderThickness.Bottom,
            Control control => control.Padding.Bottom + control.BorderThickness.Bottom,
            _ => 0,
        };
}
