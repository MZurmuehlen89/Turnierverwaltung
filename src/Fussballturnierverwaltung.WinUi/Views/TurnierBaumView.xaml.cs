using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Verbindungslinien-Canvas-Overlay zwischen den Bracket-Karten der beiden sichtbaren
/// Rundenspalten — WinUI3-Portierung von Wpf/Views/TurnierBaumView.xaml.cs (Issue #244). Erste
/// Nutzung dieses Patterns in WinUi (kein Precedent im Projekt). Kernmechanismus 1:1 übernommen:
/// nach jedem Layout-Durchlauf werden die real gerenderten Kartenpositionen gemessen
/// (<see cref="FrameworkElement.TransformToVisual"/> statt TransformToAncestor, da das Overlay-
/// Canvas ein Geschwister-, kein Vorfahre-Element der Spalten ist) und die Linien neu gezeichnet.
/// Ein Signatur-Vergleich verhindert, dass das eigene Neuzeichnen (das selbst LayoutUpdated auslöst)
/// eine Rückkopplungsschleife erzeugt.</summary>
public sealed partial class TurnierBaumView : UserControl
{
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
        var pinsel = Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"] as Brush
            ?? new SolidColorBrush(Microsoft.UI.Colors.Gray);
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
        if (liste.ContainerFromItem(knoten) is not ContentPresenter praesentator) return false;
        if (VisualTreeHelper.GetChildrenCount(praesentator) == 0) return false;
        if (VisualTreeHelper.GetChild(praesentator, 0) is not FrameworkElement element) return false;
        if (element.ActualWidth <= 0 || element.ActualHeight <= 0) return false;

        var lokalePosition = new Point(rechteKante ? element.ActualWidth : 0, element.ActualHeight / 2);
        try
        {
            punkt = element.TransformToVisual(VerbindungsCanvas).TransformPoint(lokalePosition);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            // Container kann kurz nach einem Rundenwechsel noch aus dem alten visuellen Baum stammen
            // (ItemsControl hat ihn noch nicht abgehängt) — nächster LayoutUpdated-Versuch behebt das.
            return false;
        }
        return true;
    }
}
