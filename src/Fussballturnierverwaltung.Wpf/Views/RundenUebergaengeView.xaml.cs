using Fussballturnierverwaltung.Wpf.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Fussballturnierverwaltung.Wpf.Views;

public partial class RundenUebergaengeView : UserControl
{
    // Issue #249: Verbindungslinien zwischen den Runden-Karten, analog zu TurnierBaumView/#244 —
    // Kartenpositionen werden nach jedem Layout-Durchlauf real gemessen statt im ViewModel vorausberechnet
    // (die Karten liegen in einer schlichten horizontalen StackPanel-Reihe, keine Canvas-Positionierung nötig).
    private readonly List<(double X1, double Y1, double X2, double Y2)> _letzteSignatur = [];

    public RundenUebergaengeView()
    {
        InitializeComponent();
        LayoutUpdated += (_, _) => VerbindungsLinienAktualisieren();
    }

    private void VerbindungsLinienAktualisieren()
    {
        if (DataContext is not RundenUebergaengeViewModel vm || vm.Verbindungen.Count == 0)
        {
            LeereCanvas();
            return;
        }

        var segmente = new List<(RundenUebergangVerbindung Verbindung, Point Von, Point Nach)>();
        foreach (var verbindung in vm.Verbindungen)
        {
            var quellKarte = vm.Karten.FirstOrDefault(k => k.TurnierRundeId == verbindung.QuellRundeId);
            var zielKarte = vm.Karten.FirstOrDefault(k => k.TurnierRundeId == verbindung.ZielRundeId);
            if (quellKarte is null || zielKarte is null) continue;
            if (!TryKartenKante(quellKarte, rechteKante: true, out var von)) continue;
            if (!TryKartenKante(zielKarte, rechteKante: false, out var nach)) continue;
            segmente.Add((verbindung, von, nach));
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
        var linienPinsel = (Brush)FindResource("BorderStrongBrush");
        var implizitPinsel = (Brush)FindResource("TextSubtleBrush");
        var textPinsel = (Brush)FindResource("TextMutedBrush");
        var labelHintergrund = (Brush)FindResource("SurfaceBrush");

        foreach (var (verbindung, von, nach) in segmente)
        {
            var stroke = verbindung.IstImplizit ? implizitPinsel : linienPinsel;
            var strichelung = verbindung.IstImplizit ? new DoubleCollection([3, 2]) : null;

            if (verbindung.Ausweichspur is { } spur)
            {
                // Springt die Verbindung über mindestens eine Spalte, würde eine gerade Linie optisch durch
                // die Zwischenspalte laufen (Nutzerfeedback 2026-08-30) — stattdessen ein Schwung über eine
                // eigene Ausweichspur oberhalb der Kartenreihen (Layout siehe ViewModel). Statt eines
                // kantigen Knicks (Nutzerfeedback: "wie ein Dreieck") eine weiche S-Kurve aus zwei
                // Bezier-Segmenten mit horizontaler Tangente an Karten UND am Übergabepunkt zur Spur, analog
                // zu einer Bahnschienen-Weiche — dadurch kein sichtbarer Kantenknick an irgendeiner Stelle.
                var spurY = spur * RundenUebergaengeViewModel.AUSWEICHSPUR_H + RundenUebergaengeViewModel.AUSWEICHSPUR_H / 2;
                var spurMitte = new Point((von.X + nach.X) / 2, spurY);

                var figur = new PathFigure { StartPoint = von, IsClosed = false, IsFilled = false };
                figur.Segments.Add(new BezierSegment(
                    new Point(von.X + (spurMitte.X - von.X) / 2, von.Y),
                    new Point(spurMitte.X - (spurMitte.X - von.X) / 2, spurMitte.Y),
                    spurMitte, isStroked: true));
                figur.Segments.Add(new BezierSegment(
                    new Point(spurMitte.X + (nach.X - spurMitte.X) / 2, spurMitte.Y),
                    new Point(nach.X - (nach.X - spurMitte.X) / 2, nach.Y),
                    nach, isStroked: true));

                VerbindungsCanvas.Children.Add(new Path
                {
                    Data = new PathGeometry([figur]),
                    Stroke = stroke, StrokeThickness = 1.5, StrokeDashArray = strichelung
                });
            }
            else
            {
                VerbindungsCanvas.Children.Add(new Line
                {
                    X1 = von.X, Y1 = von.Y, X2 = nach.X, Y2 = nach.Y,
                    Stroke = stroke, StrokeThickness = 1.5, StrokeDashArray = strichelung
                });
            }

            // Implizite Übergänge (keine eigene Uebergangsregel, siehe RundenUebergaengeViewModel) tragen
            // keine erfundene Platzierungsangabe — die gestrichelte Linie allein zeigt den Übergang.
            if (string.IsNullOrEmpty(verbindung.Beschriftung)) continue;

            var label = new Border
            {
                Background = labelHintergrund,
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1, 4, 1),
                Child = new TextBlock
                {
                    Text = verbindung.Beschriftung,
                    FontSize = 10,
                    Foreground = textPinsel
                }
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var mitte = new Point((von.X + nach.X) / 2, (von.Y + nach.Y) / 2);
            Canvas.SetLeft(label, mitte.X - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, mitte.Y - label.DesiredSize.Height / 2);
            VerbindungsCanvas.Children.Add(label);
        }
    }

    private void LeereCanvas()
    {
        if (VerbindungsCanvas.Children.Count > 0) VerbindungsCanvas.Children.Clear();
        _letzteSignatur.Clear();
    }

    private static double Round(double wert) => Math.Round(wert, 1);

    private bool TryKartenKante(RundenUebergangKarte karte, bool rechteKante, out Point punkt)
    {
        punkt = default;
        if (KartenItems.ItemContainerGenerator.ContainerFromItem(karte) is not ContentPresenter praesentator) return false;
        if (VisualTreeHelper.GetChildrenCount(praesentator) == 0) return false;
        if (VisualTreeHelper.GetChild(praesentator, 0) is not FrameworkElement element) return false;
        if (element.ActualWidth <= 0 || element.ActualHeight <= 0) return false;

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
