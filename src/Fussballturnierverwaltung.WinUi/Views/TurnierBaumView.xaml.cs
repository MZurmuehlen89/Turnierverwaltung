using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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

    private TurnierBaumViewModel? _vm;

    public TurnierBaumView()
    {
        InitializeComponent();
        LayoutUpdated += (_, _) => VerbindungsLinienAktualisieren();
        // Bei Theme-Wechsel ändern sich nur die Farben, nicht die Positionen — Signatur verwerfen, damit neu
        // gezeichnet wird.
        ActualThemeChanged += (_, _) =>
        {
            _letzteSignatur.Clear();
            VerbindungsLinienAktualisieren();
            GesamtbaumAufbauen();
        };

        DataContextChanged += (_, _) => ViewModelWechseln(DataContext as TurnierBaumViewModel);
        GesamtScroll.ViewChanged += (_, _) => ZoomAnzeigeAktualisieren();
        // Fenstergröße geändert → vertikale Streckung und Zoom passen nicht mehr, neu einpassen.
        GesamtScroll.SizeChanged += (_, _) => Einpassen();
        GesamtCanvas.PointerPressed += GesamtCanvas_PointerPressed;
        GesamtCanvas.PointerMoved += GesamtCanvas_PointerMoved;
        GesamtCanvas.PointerReleased += (_, e) => ZiehenBeenden(e.Pointer);
        GesamtCanvas.PointerCaptureLost += (_, e) => ZiehenBeenden(e.Pointer);
    }

    private void ViewModelWechseln(TurnierBaumViewModel? neu)
    {
        if (ReferenceEquals(_vm, neu)) return;
        if (_vm is not null)
        {
            _vm.GesamtbaumNeuBerechnet -= Vm_GesamtbaumNeuBerechnet;
            _vm.PropertyChanged -= Vm_PropertyChanged;
        }
        _vm = neu;
        if (_vm is not null)
        {
            _vm.GesamtbaumNeuBerechnet += Vm_GesamtbaumNeuBerechnet;
            _vm.PropertyChanged += Vm_PropertyChanged;
        }
        GesamtbaumAufbauen();
    }

    // ── Gesamtübersicht (Issue #238) ──────────────────────────────────────────

    /// <summary>Vertikale Streckung der Kartenabstände (≥ 1): Ein breiter Baum wird beim Einpassen auf die
    /// Fensterbreite verkleinert und bliebe sonst ein flaches Band in der Mitte — die Zeilenabstände wachsen deshalb,
    /// bis der Baum auch die Höhe ausfüllt. Die Karten selbst behalten ihre Größe.</summary>
    private double _streckung = 1.0;
    private const double MAX_STRECKUNG = 3.0;
    private (double Breite, double Hoehe) _letzteBaumGroesse;

    private void Vm_GesamtbaumNeuBerechnet(object? sender, EventArgs e)
    {
        // Neuer Baum mit anderer Größe (anderes Turnier) → neu einpassen; nach einem Ergebnis-Update (gleiche
        // Größe) bleiben Zoom und Ausschnitt erhalten.
        bool groesseGeaendert = _vm is not null && _letzteBaumGroesse != (_vm.GesamtBreite, _vm.GesamtHoehe);
        if (_vm is not null) _letzteBaumGroesse = (_vm.GesamtBreite, _vm.GesamtHoehe);
        GesamtbaumAufbauen();
        if (groesseGeaendert) EinpassenAnfordern();
    }

    private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TurnierBaumViewModel.IstGesamtansicht) && _vm?.IstGesamtansicht == true)
            EinpassenAnfordern();
    }

    /// <summary>Baut Linien, Beschriftungen und Karten der Gesamtübersicht aus den im ViewModel berechneten
    /// Positionen auf. Die kompakten Karten haben eine feste Größe — die Linien lassen sich deshalb direkt aus den
    /// Koordinaten zeichnen, ohne die gerenderten Karten zu messen (anders als in der rundenweisen Ansicht).</summary>
    private void GesamtbaumAufbauen()
    {
        GesamtCanvas.Children.Clear();
        if (_vm is null) return;

        const double kopf = TurnierBaumViewModel.GESAMT_KOPF_H;
        const double rand = TurnierBaumViewModel.GESAMT_RAND;
        double streckung = _streckung;
        // Kartenmitten unterhalb des Spaltenkopfs werden gestreckt, die Kartengröße bleibt.
        double Strecken(double y) => kopf + (y - kopf) * streckung;

        GesamtCanvas.Width = _vm.GesamtBreite;
        GesamtCanvas.Height = kopf + (_vm.GesamtHoehe - kopf - rand) * streckung + rand;

        const double w = TurnierBaumViewModel.KOMPAKT_W;
        const double h = TurnierBaumViewModel.KOMPAKT_H;
        var knoten = _vm.GesamtKnoten;
        var knotenJeId = knoten.ToDictionary(k => k.Id);

        var pinsel = LinienFarbQuelle.Fill ?? new SolidColorBrush(Microsoft.UI.Colors.Gray);
        foreach (var eltern in knoten)
        {
            foreach (var kindId in new[] { eltern.LinkerKindKnotenId, eltern.RechterKindKnotenId })
            {
                if (kindId is not int id || !knotenJeId.TryGetValue(id, out var kind)) continue;

                // Kinder liegen weiter außen: links vom Elternknoten → rechte Kante zur linken, sonst gespiegelt.
                bool kindLinks = kind.GesamtX < eltern.GesamtX;
                double x1 = kindLinks ? kind.GesamtX + w : kind.GesamtX;
                double x2 = kindLinks ? eltern.GesamtX : eltern.GesamtX + w;
                double y1 = Strecken(kind.GesamtY + h / 2);
                double y2 = Strecken(eltern.GesamtY + h / 2);
                double mitteX = (x1 + x2) / 2;

                var linie = new Polyline { Stroke = pinsel, StrokeThickness = 1.5, IsHitTestVisible = false };
                linie.Points.Add(new Point(x1, y1));
                linie.Points.Add(new Point(mitteX, y1));
                linie.Points.Add(new Point(mitteX, y2));
                linie.Points.Add(new Point(x2, y2));
                GesamtCanvas.Children.Add(linie);
            }
        }

        foreach (var b in _vm.GesamtBeschriftungen)
        {
            var text = new TextBlock
            {
                Text = b.Name, Width = b.Breite, TextAlignment = TextAlignment.Center,
                FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis, IsHitTestVisible = false
            };
            Canvas.SetLeft(text, b.X);
            double abstand = TurnierBaumViewModel.GESAMT_BESCHRIFTUNG_ABSTAND;
            Canvas.SetTop(text, b.GehoertZuKarte ? Strecken(b.Y + abstand + h / 2) - h / 2 - abstand : b.Y);
            GesamtCanvas.Children.Add(text);
        }

        var vorlage = (DataTemplate)Resources["KompaktKartenTemplate"];
        foreach (var k in knoten)
        {
            var karte = new Button
            {
                Content = k, ContentTemplate = vorlage, Width = w, Height = h, Padding = new Thickness(0),
                BorderThickness = new Thickness(0), Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Command = _vm.KnotenAngeklicktBefehl, CommandParameter = k
            };
            SpielKontextmenue.SetKarte(karte, k);
            Canvas.SetLeft(karte, k.GesamtX);
            Canvas.SetTop(karte, Strecken(k.GesamtY + h / 2) - h / 2);
            GesamtCanvas.Children.Add(karte);
        }
    }

    // ── Zoom und Verschieben (Issue #50) ──────────────────────────────────────

    private void EinpassenAnfordern()
    {
        // Die ScrollViewer-Größe steht erst nach dem nächsten Layout-Durchlauf fest (z. B. direkt nach dem
        // Umschalten aus Collapsed) — SizeChanged oder dieser Nachlauf übernimmt das Einpassen.
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, Einpassen);
    }

    private void Einpassen()
    {
        if (_vm is null || GesamtScroll.ViewportWidth <= 0 || GesamtScroll.ViewportHeight <= 0
            || _vm.GesamtBreite <= 0 || _vm.GesamtHoehe <= 0)
            return;

        // Zoom richtet sich nach der Breite; die bei diesem Zoom sichtbare Höhe füllen die gestreckten Abstände.
        const double kopf = TurnierBaumViewModel.GESAMT_KOPF_H;
        const double rand = TurnierBaumViewModel.GESAMT_RAND;
        double breitenFaktor = Math.Clamp(GesamtScroll.ViewportWidth / _vm.GesamtBreite, GesamtScroll.MinZoomFactor, 1.0);
        double verfuegbareHoehe = GesamtScroll.ViewportHeight / breitenFaktor;
        double inhaltsHoehe = _vm.GesamtHoehe - kopf - rand;
        double streckung = inhaltsHoehe > 0
            ? Math.Clamp((verfuegbareHoehe - kopf - rand) / inhaltsHoehe, 1.0, MAX_STRECKUNG)
            : 1.0;
        if (Math.Abs(streckung - _streckung) > 0.001)
        {
            _streckung = streckung;
            GesamtbaumAufbauen();
        }

        double faktor = Math.Min(GesamtScroll.ViewportWidth / GesamtCanvas.Width, GesamtScroll.ViewportHeight / GesamtCanvas.Height);
        faktor = Math.Clamp(faktor, GesamtScroll.MinZoomFactor, 1.0);
        GesamtScroll.ChangeView(0, 0, (float)faktor, disableAnimation: true);
        ZoomAnzeigeAktualisieren();
    }

    private void AllesAnzeigen_Click(object sender, RoutedEventArgs e) => Einpassen();

    private void ZoomAnzeigeAktualisieren() =>
        ZoomAnzeige.Text = $"{Math.Round(GesamtScroll.ZoomFactor * 100)} %";

    private uint? _ziehPointerId;
    private Point _ziehStart;
    private double _ziehStartH, _ziehStartV;

    // Verschieben per Ziehen mit der linken Maustaste auf freier Fläche — Karten sind Buttons und behandeln
    // PointerPressed selbst, ein Klick darauf bleibt also ein Klick.
    private void GesamtCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse) return;
        if (!e.GetCurrentPoint(GesamtScroll).Properties.IsLeftButtonPressed) return;

        _ziehPointerId = e.Pointer.PointerId;
        _ziehStart = e.GetCurrentPoint(GesamtScroll).Position;
        _ziehStartH = GesamtScroll.HorizontalOffset;
        _ziehStartV = GesamtScroll.VerticalOffset;
        GesamtCanvas.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void GesamtCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_ziehPointerId != e.Pointer.PointerId) return;

        var position = e.GetCurrentPoint(GesamtScroll).Position;
        GesamtScroll.ChangeView(
            _ziehStartH - (position.X - _ziehStart.X),
            _ziehStartV - (position.Y - _ziehStart.Y),
            null, disableAnimation: true);
        e.Handled = true;
    }

    private void ZiehenBeenden(Pointer pointer)
    {
        if (_ziehPointerId != pointer.PointerId) return;
        _ziehPointerId = null;
        GesamtCanvas.ReleasePointerCapture(pointer);
    }

    // ── Rundenweise Ansicht: Verbindungslinien zwischen den beiden sichtbaren Spalten ─────────────────────

    private void VerbindungsLinienAktualisieren()
    {
        if (DataContext is not TurnierBaumViewModel vm || !vm.ZeigtBracketInhalt || vm.IstGesamtansicht)
        {
            LeereCanvas();
            return;
        }

        // Canvas.Top per ItemContainerStyle-Binding wird in WinUI3 nicht zuverlässig ausgewertet (siehe
        // TurnierBaumView.xaml) — deshalb hier programmatisch gesetzt, bei jedem Layout-Durchlauf neu.
        PositioniereKarten(LinkeSpalteItems, vm.LinkeSpalteKnoten);
        if (vm.ZeigtZweiteSpalte)
            PositioniereKarten(RechteSpalteItems, vm.RechteSpalteKnoten);

        if (!vm.ZeigtZweiteSpalte)
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
        var pinsel = LinienFarbQuelle.Fill ?? new SolidColorBrush(Microsoft.UI.Colors.Gray);
        foreach (var (von, nach) in segmente)
        {
            VerbindungsCanvas.Children.Add(new Line
            {
                X1 = von.X, Y1 = von.Y, X2 = nach.X, Y2 = nach.Y,
                Stroke = pinsel, StrokeThickness = 1.5
            });
        }
    }

    // FensterY ist die Kartenmitte, nicht die Oberkante (Issue #369): Karten sind unterschiedlich hoch (Label,
    // Datum, Hin-/Rück-Fußzeile) — oberkantenbündig lägen die Mitten, an denen die Linien ansetzen, versetzt.
    // Solange eine Karte noch nicht gemessen ist (ActualHeight 0), korrigiert der nächste Layout-Durchlauf.
    private static void PositioniereKarten(ItemsControl liste, IReadOnlyList<BracketKnotenAnzeige> knoten)
    {
        foreach (var k in knoten)
        {
            if (liste.ContainerFromItem(k) is ContentPresenter praesentator)
                Canvas.SetTop(praesentator, Math.Max(0, k.FensterY - praesentator.ActualHeight / 2));
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
