using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Lässt ein Element bis zum unteren Rand des sichtbaren Bereichs wachsen statt mit fester Höhe (#593). In einer
/// Seite, die als Ganzes scrollt, gibt das Layout dafür kein Maß her — gerechnet wird darum über
/// <see cref="FrameworkElement.EffectiveViewportChanged"/>, das auch feuert, wenn sich das Element durch eine
/// eingeblendete Meldung verschiebt. Der Scrollversatz des umgebenden <see cref="ScrollViewer"/> wird herausgerechnet,
/// sonst wüchse das Element beim Scrollen mit. Gesetzt wird die Mindesthöhe; unter ihr scrollt die Seite.</summary>
public static class HoeheBisFensterende
{
    private const double ABSTAND_UNTEN = 16;

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
        double versatz = UmgebenderScrollViewer(element)?.VerticalOffset ?? 0;
        double hoehe = Math.Max(GetMindestHoehe(element), args.EffectiveViewport.Bottom - versatz - ABSTAND_UNTEN);
        if (double.IsNaN(element.Height) || Math.Abs(element.Height - hoehe) > 0.5)
            element.Height = hoehe;
    }

    private static ScrollViewer? UmgebenderScrollViewer(DependencyObject element)
    {
        for (var eltern = VisualTreeHelper.GetParent(element); eltern is not null; eltern = VisualTreeHelper.GetParent(eltern))
            if (eltern is ScrollViewer scrollViewer) return scrollViewer;
        return null;
    }
}
