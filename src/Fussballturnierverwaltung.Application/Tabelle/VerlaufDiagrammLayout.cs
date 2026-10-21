namespace Fussballturnierverwaltung.Application.Tabelle;

public sealed record DiagrammPunkt(double X, double Y);

public sealed record VerlaufDiagrammLinie(int MannschaftId, IReadOnlyList<DiagrammPunkt> Punkte);

/// <summary>Hintergrundband einer Tabellenzone zwischen <see cref="Oben"/> und <see cref="Unten"/>.</summary>
public sealed record ZonenBand(double Oben, double Unten, ZonenFarbe Farbe);

/// <summary>Achsenbeschriftung an <see cref="Position"/> (y bei Plätzen, x bei Spieltagen).</summary>
public sealed record AchsenMarke(double Position, string Text);

/// <summary>Spalte eines Spieltags: Mitte <see cref="X"/>, Klickfläche von <see cref="Links"/> bis
/// <see cref="Rechts"/> (bis zur Mitte zwischen den Nachbarn).</summary>
public sealed record SpieltagSpalte(int Spieltag, double X, double Links, double Rechts);

/// <summary>Fertige Geometrie des Tabellenplatz-Verlaufs (#480) in Pixeln: Platz 1 oben, Spieltage von links nach
/// rechts, rechts Platz für die Namen der hervorgehobenen Mannschaften.</summary>
public sealed record VerlaufDiagrammLayout(
    double Breite, double Hoehe, double PlotLinks, double PlotRechts, double PlotOben, double PlotUnten,
    IReadOnlyList<AchsenMarke> PlatzMarken, IReadOnlyList<AchsenMarke> SpieltagMarken,
    IReadOnlyList<SpieltagSpalte> Spalten, IReadOnlyList<VerlaufDiagrammLinie> Linien, IReadOnlyList<ZonenBand> Baender);

/// <summary>Rechnet die Geometrie des Tabellenplatz-Verlaufs (#480), ohne Oberfläche — die Ansicht zeichnet nur
/// noch nach.</summary>
public static class VerlaufDiagrammRechner
{
    public const double RAND_LINKS = 36;
    public const double RAND_RECHTS = 180;
    public const double RAND_OBEN = 8;
    public const double RAND_UNTEN = 28;
    public const double MIN_SPALTENBREITE = 24;
    private const double ZEILE_KLEINE_TABELLE = 22;
    private const double ZEILE_GROSSE_TABELLE = 14;
    private const int GROSSE_TABELLE_AB = 21;
    private const double MIN_BESCHRIFTUNGSABSTAND = 22;

    /// <param name="breite">Verfügbare Breite; reicht sie für <see cref="MIN_SPALTENBREITE"/> je Spieltag nicht,
    /// wird das Diagramm breiter.</param>
    public static VerlaufDiagrammLayout Berechnen(TabellenVerlauf verlauf, IReadOnlyList<TabellenZone> zonen, double breite)
    {
        int plaetze = Math.Max(1, verlauf.AnzahlPlaetze);
        double zeile = plaetze >= GROSSE_TABELLE_AB ? ZEILE_GROSSE_TABELLE : ZEILE_KLEINE_TABELLE;
        int spieltage = verlauf.Spieltage.Count;

        breite = Math.Max(breite, RAND_LINKS + RAND_RECHTS + MIN_SPALTENBREITE * Math.Max(1, spieltage - 1));
        double plotLinks = RAND_LINKS, plotRechts = breite - RAND_RECHTS;
        double plotOben = RAND_OBEN, plotUnten = RAND_OBEN + plaetze * zeile;

        double X(int index) => spieltage <= 1
            ? (plotLinks + plotRechts) / 2
            : plotLinks + index * (plotRechts - plotLinks) / (spieltage - 1);
        double Y(int platz) => plotOben + (platz - 0.5) * zeile;

        var spalten = Spalten(verlauf.Spieltage, X, plotLinks, plotRechts);
        return new VerlaufDiagrammLayout(
            breite, plotUnten + RAND_UNTEN, plotLinks, plotRechts, plotOben, plotUnten,
            PlatzMarken(plaetze, Y), SpieltagMarken(spalten), spalten,
            [.. verlauf.Mannschaften.Select(m => new VerlaufDiagrammLinie(
                m.Mannschaft.Id, [.. m.Punkte.Select((p, i) => new DiagrammPunkt(X(i), Y(p.Platz)))]))],
            Baender(zonen, verlauf.GruppenNummer, plaetze, plotOben, zeile));
    }

    /// <summary>Verteilt die Namen am rechten Rand: jede Beschriftung so nah wie möglich an ihrer Linie, aber mit
    /// mindestens <paramref name="minAbstand"/> zur vorigen. Rückgabe in der Reihenfolge der Eingabe.</summary>
    public static IReadOnlyList<double> BeschriftungenVerteilen(
        IReadOnlyList<double> ys, double minAbstand = MIN_BESCHRIFTUNGSABSTAND)
    {
        var ergebnis = new double[ys.Count];
        double letzte = double.NegativeInfinity;
        foreach (var (y, index) in ys.Select((y, i) => (y, i)).OrderBy(t => t.y))
        {
            letzte = Math.Max(y, letzte + minAbstand);
            ergebnis[index] = letzte;
        }
        return ergebnis;
    }

    private static List<SpieltagSpalte> Spalten(
        IReadOnlyList<int> spieltage, Func<int, double> x, double plotLinks, double plotRechts) =>
        [.. spieltage.Select((spieltag, i) => new SpieltagSpalte(
            spieltag, x(i),
            i == 0 ? plotLinks - MIN_SPALTENBREITE / 2 : (x(i - 1) + x(i)) / 2,
            i == spieltage.Count - 1 ? plotRechts + MIN_SPALTENBREITE / 2 : (x(i) + x(i + 1)) / 2))];

    // Kleine Tabellen: jeder Platz; große: Platz 1, jeder fünfte und der letzte.
    private static List<AchsenMarke> PlatzMarken(int plaetze, Func<int, double> y) =>
        [.. Enumerable.Range(1, plaetze)
            .Where(p => plaetze < GROSSE_TABELLE_AB || p == 1 || p % 5 == 0 || p == plaetze)
            .Select(p => new AchsenMarke(y(p), p.ToString()))];

    // Jeder Spieltag, solange die Spalten breit genug sind — sonst jeder n-te und immer der letzte.
    private static List<AchsenMarke> SpieltagMarken(IReadOnlyList<SpieltagSpalte> spalten)
    {
        double abstand = spalten.Count > 1 ? spalten[1].X - spalten[0].X : double.MaxValue;
        int jeder = (int)Math.Ceiling(MIN_SPALTENBREITE / abstand);
        return [.. spalten
            .Where((_, i) => i % jeder == 0 || i == spalten.Count - 1)
            .Select(s => new AchsenMarke(s.X, s.Spieltag.ToString()))];
    }

    // Aufeinanderfolgende Plätze derselben Zonenfarbe ergeben ein Band.
    private static List<ZonenBand> Baender(
        IReadOnlyList<TabellenZone> zonen, int? gruppenNummer, int plaetze, double plotOben, double zeile)
    {
        var baender = new List<ZonenBand>();
        for (int platz = 1; platz <= plaetze; platz++)
        {
            if (TabellenZonen.ZoneFuer(zonen, gruppenNummer, platz)?.Farbe is not { } farbe) continue;
            double oben = plotOben + (platz - 1) * zeile, unten = oben + zeile;
            if (baender.Count > 0 && baender[^1].Farbe == farbe && baender[^1].Unten == oben)
                baender[^1] = baender[^1] with { Unten = unten };
            else
                baender.Add(new ZonenBand(oben, unten, farbe));
        }
        return baender;
    }
}
