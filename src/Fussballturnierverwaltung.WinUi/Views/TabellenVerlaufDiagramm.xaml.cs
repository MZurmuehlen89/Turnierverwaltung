using Fussballturnierverwaltung.Application.Tabelle;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Zeichnet den Tabellenplatz-Verlauf (#480). Klick auf eine Linie meldet <see cref="MannschaftGeklickt"/>,
/// Klick in die Spalte eines Spieltags <see cref="SpieltagGeklickt"/> — was daraus folgt, entscheidet das
/// ViewModel.</summary>
public sealed partial class TabellenVerlaufDiagramm : UserControl
{
    private const double LINIE_GRAU = 1.5;
    private const double LINIE_HERVORGEHOBEN = 2.5;
    private const double TREFFERBREITE = 10;
    private const double MARKE = 9;
    private const double RING = 2;
    private const double SCHRIFT = 12;
    private const double ZONEN_DECKKRAFT = 0.14;

    public static readonly DependencyProperty VerlaufProperty = Register(nameof(Verlauf), typeof(TabellenVerlauf));
    public static readonly DependencyProperty ZonenProperty = Register(nameof(Zonen), typeof(IReadOnlyList<TabellenZone>));
    public static readonly DependencyProperty HervorhebungenProperty =
        Register(nameof(Hervorhebungen), typeof(IReadOnlyList<VerlaufHervorhebung>));
    public static readonly DependencyProperty AusgewaehlterSpieltagProperty = Register(nameof(AusgewaehlterSpieltag), typeof(object));

    public TabellenVerlauf? Verlauf
    {
        get => (TabellenVerlauf?)GetValue(VerlaufProperty);
        set => SetValue(VerlaufProperty, value);
    }

    public IReadOnlyList<TabellenZone>? Zonen
    {
        get => (IReadOnlyList<TabellenZone>?)GetValue(ZonenProperty);
        set => SetValue(ZonenProperty, value);
    }

    public IReadOnlyList<VerlaufHervorhebung>? Hervorhebungen
    {
        get => (IReadOnlyList<VerlaufHervorhebung>?)GetValue(HervorhebungenProperty);
        set => SetValue(HervorhebungenProperty, value);
    }

    /// <summary>Spieltag, dessen Tabelle gerade darunter steht (<c>int</c>) — als <c>object</c>, damit ein Binding auf
    /// <c>int?</c> ohne Converter auskommt; <c>null</c> = aktueller Stand.</summary>
    public object? AusgewaehlterSpieltag
    {
        get => GetValue(AusgewaehlterSpieltagProperty);
        set => SetValue(AusgewaehlterSpieltagProperty, value);
    }

    public event EventHandler<int>? MannschaftGeklickt;
    public event EventHandler<int>? SpieltagGeklickt;

    private double _gezeichneteBreite = double.NaN;

    public TabellenVerlaufDiagramm()
    {
        InitializeComponent();
        ActualThemeChanged += (_, _) => Zeichnen();
        SizeChanged += (_, e) =>
        {
            if (Math.Abs(e.NewSize.Width - _gezeichneteBreite) >= 1) Zeichnen();
        };
    }

    private static DependencyProperty Register(string name, Type typ) => DependencyProperty.Register(
        name, typ, typeof(TabellenVerlaufDiagramm),
        new PropertyMetadata(null, (d, _) => ((TabellenVerlaufDiagramm)d).Zeichnen()));

    // ── Zeichnen ──────────────────────────────────────────────────────────

    private void Zeichnen()
    {
        Flaeche.Children.Clear();
        _gezeichneteBreite = ActualWidth;
        if (Verlauf is not { HatSpieltage: true } verlauf || ActualWidth <= 0)
        {
            Flaeche.Width = Flaeche.Height = 0;
            return;
        }

        var layout = VerlaufDiagrammRechner.Berechnen(verlauf, Zonen ?? [], ActualWidth);
        Flaeche.Width = layout.Breite;
        Flaeche.Height = layout.Hoehe;

        var hervorhebungen = (Hervorhebungen ?? []).Where(h => layout.Linien.Any(l => l.MannschaftId == h.MannschaftId)).ToList();
        BaenderZeichnen(layout);
        SpaltenZeichnen(layout);
        AchsenZeichnen(layout);
        foreach (var linie in layout.Linien.Where(l => hervorhebungen.All(h => h.MannschaftId != l.MannschaftId)))
            GraueLinieZeichnen(linie, verlauf);
        foreach (var hervorhebung in hervorhebungen)
            HervorgehobeneLinieZeichnen(layout.Linien.Single(l => l.MannschaftId == hervorhebung.MannschaftId), hervorhebung, verlauf);
        NamenZeichnen(layout, hervorhebungen);
    }

    private void BaenderZeichnen(VerlaufDiagrammLayout layout)
    {
        double rand = VerlaufDiagrammRechner.MIN_SPALTENBREITE / 2;
        foreach (var band in layout.Baender)
            Hinzufuegen(new Rectangle
            {
                Width = layout.PlotRechts - layout.PlotLinks + 2 * rand,
                Height = band.Unten - band.Oben,
                Fill = ThemeFarben.Brush(this, $"ZonenFarbe{band.Farbe}Brush"),
                Opacity = ZONEN_DECKKRAFT,
                IsHitTestVisible = false
            }, layout.PlotLinks - rand, band.Oben);
    }

    // Klickflächen je Spieltag; der gewählte ist hinterlegt.
    private void SpaltenZeichnen(VerlaufDiagrammLayout layout)
    {
        foreach (var spalte in layout.Spalten)
        {
            bool gewaehlt = AusgewaehlterSpieltag is int spieltag && spieltag == spalte.Spieltag;
            var flaeche = new Rectangle
            {
                Width = spalte.Rechts - spalte.Links,
                Height = layout.Hoehe - layout.PlotOben,
                Fill = gewaehlt ? Auswahl.Fill : new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            };
            ToolTipService.SetToolTip(flaeche, gewaehlt
                ? $"Darunter steht die Tabelle nach dem {spalte.Spieltag}. Spieltag – erneut klicken für den aktuellen Stand"
                : $"Tabelle nach dem {spalte.Spieltag}. Spieltag zeigen");
            flaeche.Tapped += (_, _) => SpieltagGeklickt?.Invoke(this, spalte.Spieltag);
            Hinzufuegen(flaeche, spalte.Links, layout.PlotOben);
        }
    }

    private void AchsenZeichnen(VerlaufDiagrammLayout layout)
    {
        double rand = VerlaufDiagrammRechner.MIN_SPALTENBREITE / 2;
        foreach (var marke in layout.PlatzMarken)
        {
            Hinzufuegen(new Line
            {
                X1 = layout.PlotLinks - rand, X2 = layout.PlotRechts + rand, Y1 = marke.Position, Y2 = marke.Position,
                Stroke = Raster.Fill, StrokeThickness = 1, IsHitTestVisible = false
            }, 0, 0);
            Hinzufuegen(Text(marke.Text, TextSekundaer.Fill, TextAlignment.Right, layout.PlotLinks - rand - 4),
                0, marke.Position - SCHRIFT * 0.7);
        }
        foreach (var marke in layout.SpieltagMarken)
            Hinzufuegen(Text(marke.Text, TextSekundaer.Fill, TextAlignment.Center, 32), marke.Position - 16, layout.PlotUnten + 6);
    }

    private void GraueLinieZeichnen(VerlaufDiagrammLinie linie, TabellenVerlauf verlauf)
    {
        Hinzufuegen(Polylinie(linie, LinieGrau.Fill, LINIE_GRAU), 0, 0);
        Trefferflaeche(linie, $"{MannschaftsName(verlauf, linie.MannschaftId)} – klicken zum Hervorheben");
    }

    private void HervorgehobeneLinieZeichnen(VerlaufDiagrammLinie linie, VerlaufHervorhebung hervorhebung, TabellenVerlauf verlauf)
    {
        var farbe = ThemeFarben.Brush(this, $"VerlaufFarbe{hervorhebung.Farbe}Brush");
        Hinzufuegen(Polylinie(linie, farbe, LINIE_HERVORGEHOBEN), 0, 0);
        Trefferflaeche(linie, $"{hervorhebung.Name} – klicken zum Ausblenden");

        var punkte = verlauf.Mannschaften.Single(m => m.Mannschaft.Id == hervorhebung.MannschaftId).Punkte;
        foreach (var (punkt, position) in punkte.Zip(linie.Punkte))
        {
            // Ausstehendes Spiel: hohler Punkt — nicht nur über die Farbe erkennbar.
            var marke = new Ellipse
            {
                Width = MARKE + 2 * RING, Height = MARKE + 2 * RING,
                Fill = punkt.SpielAusstehend ? Hintergrund.Fill : farbe,
                Stroke = punkt.SpielAusstehend ? farbe : Hintergrund.Fill,
                StrokeThickness = RING
            };
            ToolTipService.SetToolTip(marke, $"{hervorhebung.Name}\n{punkt.Beschreibung(hervorhebung.MannschaftId)}");
            marke.Tapped += (_, e) =>
            {
                SpieltagGeklickt?.Invoke(this, punkt.Spieltag);
                e.Handled = true;
            };
            Hinzufuegen(marke, position.X - marke.Width / 2, position.Y - marke.Height / 2);
        }
    }

    // Namen der hervorgehobenen Mannschaften am rechten Rand, auseinandergeschoben, mit Verbindungsstrich zur Linie.
    private void NamenZeichnen(VerlaufDiagrammLayout layout, IReadOnlyList<VerlaufHervorhebung> hervorhebungen)
    {
        var enden = hervorhebungen
            .Select(h => layout.Linien.Single(l => l.MannschaftId == h.MannschaftId).Punkte[^1].Y)
            .ToList();
        var positionen = VerlaufDiagrammRechner.BeschriftungenVerteilen(enden);
        double x = layout.PlotRechts + 20;

        for (int i = 0; i < hervorhebungen.Count; i++)
        {
            Hinzufuegen(new Line
            {
                X1 = layout.PlotRechts + MARKE, Y1 = enden[i], X2 = x - 4, Y2 = positionen[i],
                Stroke = ThemeFarben.Brush(this, $"VerlaufFarbe{hervorhebungen[i].Farbe}Brush"), StrokeThickness = 1,
                IsHitTestVisible = false
            }, 0, 0);
            Hinzufuegen(Text(hervorhebungen[i].Name, TextPrimaer.Fill, TextAlignment.Left,
                VerlaufDiagrammRechner.RAND_RECHTS - 28), x, positionen[i] - SCHRIFT * 0.7);
        }
    }

    // ── Bausteine ─────────────────────────────────────────────────────────

    private static Polyline Polylinie(VerlaufDiagrammLinie linie, Brush? farbe, double dicke)
    {
        var polylinie = new Polyline
        {
            Stroke = farbe, StrokeThickness = dicke, IsHitTestVisible = false,
            StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
        };
        foreach (var punkt in linie.Punkte) polylinie.Points.Add(new Point(punkt.X, punkt.Y));
        return polylinie;
    }

    // Breite, unsichtbare Linie über der eigentlichen — eine 1,5 px dünne Linie träfe man sonst kaum.
    private void Trefferflaeche(VerlaufDiagrammLinie linie, string tooltip)
    {
        var treffer = Polylinie(linie, new SolidColorBrush(Microsoft.UI.Colors.Transparent), TREFFERBREITE);
        treffer.IsHitTestVisible = true;
        ToolTipService.SetToolTip(treffer, tooltip);
        treffer.Tapped += (_, e) =>
        {
            MannschaftGeklickt?.Invoke(this, linie.MannschaftId);
            e.Handled = true;
        };
        Hinzufuegen(treffer, 0, 0);
    }

    private static TextBlock Text(string text, Brush? farbe, TextAlignment ausrichtung, double breite) => new()
    {
        Text = text, FontSize = SCHRIFT, Foreground = farbe, TextAlignment = ausrichtung, Width = breite,
        TextTrimming = TextTrimming.CharacterEllipsis, IsHitTestVisible = false
    };

    private static string MannschaftsName(TabellenVerlauf verlauf, int mannschaftId) =>
        verlauf.Mannschaften.First(m => m.Mannschaft.Id == mannschaftId).Mannschaft.Name;

    private void Hinzufuegen(UIElement element, double links, double oben)
    {
        Canvas.SetLeft(element, links);
        Canvas.SetTop(element, oben);
        Flaeche.Children.Add(element);
    }
}
