using Fussballturnierverwaltung.Wpf.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Fussballturnierverwaltung.Wpf.Views;

public partial class TurnierBaumView : UserControl
{
    // Issue #244: Verbindungslinien zwischen den Bracket-Karten der beiden sichtbaren Rundenspalten.
    // Pixelgenaue Positionen lassen sich nicht im ViewModel vorausberechnen (variable Kartenhöhe je
    // nach Label-/Datum-Kopfzeile) — stattdessen werden nach jedem Layout-Durchlauf die real gerenderten
    // Kartenpositionen gemessen und die Linien neu gezeichnet. Beide Spalten teilen sich einen
    // ScrollViewer (siehe TurnierBaumView.xaml, Nutzer-Feedback: zwei unabhängige Scrollbalken sahen
    // unruhig aus), daher ist kein manueller Scroll-Offset-Abgleich nötig. Ein Signatur-Vergleich
    // verhindert, dass das eigene Neuzeichnen (das selbst einen Layout-Durchlauf auslöst) eine
    // LayoutUpdated-Rückkopplungsschleife erzeugt.
    private readonly List<(double X1, double Y1, double X2, double Y2)> _letzteSignatur = [];

    public TurnierBaumView()
    {
        InitializeComponent();
        LayoutUpdated += (_, _) => VerbindungsLinienAktualisieren();
    }

    private void VerbindungsLinienAktualisieren()
    {
        if (DataContext is not TurnierBaumViewModel vm || !vm.ZeigtBracketInhalt || !vm.ZeigtZweiteSpalte)
        {
            LeereCanvas();
            return;
        }

        var segmente = new List<(Point Von, Point Nach)>();
        foreach (var eltern in vm.RechteSpalteKnoten)
        {
            Point? links = null, rechts = null;

            if (eltern.LinkerKindKnotenId is { } lid)
            {
                var kind = vm.LinkeSpalteKnoten.FirstOrDefault(k => k.Id == lid);
                if (kind is not null && TryKartenMitte(LinkeSpalteItems, kind, rechteKante: true, out var p))
                    links = p;
            }
            if (eltern.RechterKindKnotenId is { } rid)
            {
                var kind = vm.LinkeSpalteKnoten.FirstOrDefault(k => k.Id == rid);
                if (kind is not null && TryKartenMitte(LinkeSpalteItems, kind, rechteKante: true, out var p))
                    rechts = p;
            }
            if (links is null && rechts is null) continue;
            if (!TryKartenMitte(RechteSpalteItems, eltern, rechteKante: false, out var nach)) continue;

            double midX = ((links ?? rechts!.Value).X + nach.X) / 2;

            if (links is { } l) segmente.Add((l, new Point(midX, l.Y)));
            if (rechts is { } r) segmente.Add((r, new Point(midX, r.Y)));
            if (links is { } lv && rechts is { } rv && Math.Abs(lv.Y - rv.Y) > 0.5)
                segmente.Add((new Point(midX, lv.Y), new Point(midX, rv.Y)));
            segmente.Add((new Point(midX, nach.Y), nach));
        }

        // Noch kein Container gemessen werden konnte (z.B. direkt nach einem Rundenwechsel, bevor der
        // nächste Layout-Durchlauf die ItemsControls neu generiert hat) — nächster LayoutUpdated-Aufruf
        // versucht es erneut, kein Fehlerfall.
        if (segmente.Count == 0)
        {
            LeereCanvas();
            return;
        }

        var signatur = segmente.Select(s => (Round(s.Von.X), Round(s.Von.Y), Round(s.Nach.X), Round(s.Nach.Y))).ToList();
        if (signatur.SequenceEqual(_letzteSignatur)) return;

        _letzteSignatur.Clear();
        _letzteSignatur.AddRange(signatur);

        VerbindungsCanvas.Children.Clear();
        var pinsel = (Brush)FindResource("BorderStrongBrush");
        foreach (var (von, nach) in segmente)
        {
            VerbindungsCanvas.Children.Add(new Line
            {
                X1 = von.X, Y1 = von.Y, X2 = nach.X, Y2 = nach.Y,
                Stroke = pinsel, StrokeThickness = 1.5
            });
        }
    }

    private void LeereCanvas()
    {
        if (VerbindungsCanvas.Children.Count > 0) VerbindungsCanvas.Children.Clear();
        _letzteSignatur.Clear();
    }

    private static double Round(double wert) => Math.Round(wert, 1);

    private bool TryKartenMitte(ItemsControl liste, BracketKnotenAnzeige knoten, bool rechteKante, out Point punkt)
    {
        punkt = default;
        if (liste.ItemContainerGenerator.ContainerFromItem(knoten) is not ContentPresenter praesentator) return false;
        if (VisualTreeHelper.GetChildrenCount(praesentator) == 0) return false;
        if (VisualTreeHelper.GetChild(praesentator, 0) is not FrameworkElement element) return false;
        if (element.ActualWidth <= 0 || element.ActualHeight <= 0) return false;

        // TransformToAncestor scheitert hier, da VerbindungsCanvas ein Geschwister-Element der
        // DockPanel-Spalten ist (beide Kind von Grid), nicht deren Vorfahre — TransformToVisual
        // funktioniert dagegen für beliebige Visuals im selben Baum, unabhängig von der Hierarchie.
        // Direkt nach einem Rundenwechsel kann ein Container kurzzeitig noch aus dem alten Baum
        // stammen (ItemsControl hat ihn noch nicht abgehängt) — dann ist auch TransformToVisual
        // nicht sicher, daher defensiv abfangen statt den nächsten LayoutUpdated-Versuch zu riskieren.
        var lokalePosition = new Point(rechteKante ? element.ActualWidth : 0, element.ActualHeight / 2);
        try
        {
            punkt = element.TransformToVisual(VerbindungsCanvas).Transform(lokalePosition);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        return true;
    }
}
