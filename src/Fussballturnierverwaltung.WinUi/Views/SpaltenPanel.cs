using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>#247: Ordnet die sichtbaren Kinder als gleich breite Spalten nebeneinander an, jede in voller Höhe (eigener
/// Bildlauf je Karte). Bleibt je Spalte weniger als <see cref="MinSpaltenbreite"/>, stehen sie untereinander und ein
/// umgebender <see cref="ScrollViewer"/> scrollt alle gemeinsam – er wird dafür vom Panel umgeschaltet (nebeneinander
/// kein senkrechter Bildlauf, damit die Karten die Höhe des Fensters bekommen). Die Breite misst das Panel selbst,
/// weil ein <c>AdaptiveTrigger</c> nur die Fensterbreite kennt.</summary>
public sealed class SpaltenPanel : Panel
{
    private bool? _untereinander;

    /// <summary>Unterhalb dieser Breite je Spalte rutschen die Spalten untereinander.</summary>
    public double MinSpaltenbreite { get; set; } = 380;

    protected override Size MeasureOverride(Size verfuegbar)
    {
        var sichtbar = SichtbareKinder();
        if (sichtbar.Count == 0) return new Size(0, 0);

        double breite = double.IsInfinity(verfuegbar.Width) ? MinSpaltenbreite * sichtbar.Count : verfuegbar.Width;
        bool untereinander = SpaltenUmbruch.Untereinander(breite, sichtbar.Count, MinSpaltenbreite, _untereinander == true);
        ModusSetzen(untereinander);

        if (untereinander)
        {
            double summe = 0;
            foreach (var kind in sichtbar)
            {
                kind.Measure(new Size(breite, double.PositiveInfinity));
                summe += kind.DesiredSize.Height;
            }
            return new Size(breite, summe + SpaltenUmbruch.ABSTAND * (sichtbar.Count - 1));
        }

        double spalte = SpaltenUmbruch.Spaltenbreite(breite, sichtbar.Count);
        double hoehe = 0;
        foreach (var kind in sichtbar)
        {
            kind.Measure(new Size(spalte, verfuegbar.Height));
            hoehe = Math.Max(hoehe, kind.DesiredSize.Height);
        }
        return new Size(breite, double.IsInfinity(verfuegbar.Height) ? hoehe : verfuegbar.Height);
    }

    protected override Size ArrangeOverride(Size finale)
    {
        var sichtbar = SichtbareKinder();
        foreach (var kind in Children.Where(k => !sichtbar.Contains(k)))
            kind.Arrange(new Rect(0, 0, 0, 0));
        if (sichtbar.Count == 0) return finale;

        if (_untereinander == true)
        {
            double y = 0;
            foreach (var kind in sichtbar)
            {
                kind.Arrange(new Rect(0, y, finale.Width, kind.DesiredSize.Height));
                y += kind.DesiredSize.Height + SpaltenUmbruch.ABSTAND;
            }
            return finale;
        }

        double spalte = SpaltenUmbruch.Spaltenbreite(finale.Width, sichtbar.Count);
        for (int i = 0; i < sichtbar.Count; i++)
            sichtbar[i].Arrange(new Rect(i * (spalte + SpaltenUmbruch.ABSTAND), 0, spalte, finale.Height));
        return finale;
    }

    private List<UIElement> SichtbareKinder() => Children.Where(k => k.Visibility == Visibility.Visible).ToList();

    // Der umgebende ScrollViewer wird erst nach dem Layoutdurchlauf umgestellt – im Durchlauf selbst ändern wäre ein Zyklus.
    private void ModusSetzen(bool untereinander)
    {
        if (_untereinander == untereinander) return;
        _untereinander = untereinander;
        DispatcherQueue.TryEnqueue(() =>
        {
            for (DependencyObject? p = VisualTreeHelper.GetParent(this); p is not null; p = VisualTreeHelper.GetParent(p))
            {
                if (p is not ScrollViewer bildlauf) continue;
                bildlauf.VerticalScrollBarVisibility = untereinander ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
                return;
            }
        });
    }
}
