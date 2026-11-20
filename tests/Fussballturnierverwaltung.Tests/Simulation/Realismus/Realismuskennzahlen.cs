namespace Fussballturnierverwaltung.Tests.Simulation.Realismus;

/// <summary>
/// Eine Kennzahl der Realismus-Prüfung (#683): Name, Band (<paramref name="Von"/> … <paramref name="Bis"/>, einseitige
/// Bänder mit ±∞), Zieltext und die Messung auf einer Spieleliste (voll oder ohne einen Jackknife-Block).
/// </summary>
internal sealed record Realismuskennzahl(
    string Name, double Von, double Bis, string Ziel, string Format, Func<IReadOnlyList<SimuliertesSpiel>, double> Messen);

/// <summary>
/// #683: der Katalog aller Bänder der Realismus-Tests – jedes Band genau einmal definiert, damit die Tests
/// (<c>SimulationsRealismusTests</c>) und das Messwerkzeug (<c>RealismusMessreihe</c>) dieselben Grenzen nutzen.
/// Die Favoritenklassen der Remisquote werden einmal auf der vollen Stichprobe gewählt (mindestens 1000 Spiele in
/// Hälfte B) und im Jackknife nicht neu bestimmt.
/// </summary>
internal sealed class Realismuskennzahlen
{
    private const int MINDESTSPIELE_KLASSE = 1000;

    // Klassen der Siegchance des Favoriten (wie im Auswertungsskript) – in der Simulation nur eine Gruppierung.
    private static readonly (double Von, double Bis)[] _favoritenklassen =
        [(0.33, 0.40), (0.40, 0.50), (0.50, 0.60), (0.60, 0.70), (0.70, 0.80), (0.80, 1.01)];

    // BL je Favoritenklasse: mittlere quotenimplizierte Siegchance des Favoriten → quotenimplizierte Remisquote
    // (Pinnacle-Schlussquoten, normiert; in den BL-Daten kalibriert: impliziert ≈ eingetreten).
    private static readonly (double SiegchanceFavorit, double Remis)[] _remisJeSiegchance =
        [(0.378, 0.283), (0.447, 0.270), (0.547, 0.244), (0.645, 0.205), (0.748, 0.157), (0.848, 0.101)];

    private readonly IReadOnlyList<SimuliertesSpiel> _voll;
    private readonly List<(double Von, double Bis)> _geprueft;

    public Realismuskennzahlen(IReadOnlyList<SimuliertesSpiel> voll)
    {
        _voll = voll;
        _geprueft = voll.Count == 0
            ? []
            : [.. _favoritenklassen.Where(k => Klasse(HaelfteB(voll), k).Count >= MINDESTSPIELE_KLASSE)];
        Remisklassen = new("Remisquote je Favoritenklasse: größte Abweichung vom Ziel", 0, 0.030,
            "Ziel je Klasse: quotenimplizierte Remisquote", "P1", RemisklassenAbweichung);
        Alle =
        [
            Remis, NullNull, ZuNull, FuenfTore, SiebenTore, Tordifferenz3, EinsNull, EinsEins,
            Rho, DcEinsEins, DcEinsNull, ErsteHalbzeit, Minute1Bis15, Minute76Bis90,
            Niederlage1Pause, FavoritSieg, AussenseiterSieg, Sieg1Pause, Sieg2Pause, Zuwachs, Remisklassen,
            Grosschancen, Verwertung, ElfmeterJeSpiel, ElfmeterQuote, ElfmeterAnteil, RoteKarten, Ueberzahl,
        ];
    }

    public IReadOnlyList<Realismuskennzahl> Alle { get; }

    /// <summary>Zahl der Favoritenklassen mit genug Spielen (der Test verlangt mindestens 4).</summary>
    public int GepruefteRemisklassen => _geprueft.Count;

    // ── Hilfen ────────────────────────────────────────────────────────────

    private static double Anteil(IReadOnlyList<SimuliertesSpiel> s, Func<SimuliertesSpiel, bool> bedingung) =>
        Realismusauswertung.Anteil(s, bedingung);

    // Wo eine Größe der Paarung aus Hälfte A einfließt (Favorit, Siegchance), wird nur in Hälfte B gemessen:
    // sonst bestimmen dieselben Spiele Einteilung und Ergebnis.
    private static IEnumerable<SimuliertesSpiel> HaelfteB(IEnumerable<SimuliertesSpiel> s) =>
        s.Where(x => x.Durchlauf >= RealismusStichprobe.ENDE_HAELFTE_A);

    private static List<(double Lambda, double Mu, int Heim, int Gast)> DixonColesDaten(IReadOnlyList<SimuliertesSpiel> s) =>
        s.Select(x => (x.Paarung.MittlereToreHeim, x.Paarung.MittlereToreGast, x.HeimTore, x.GastTore)).ToList();

    private static List<SimuliertesSpiel> Klasse(IEnumerable<SimuliertesSpiel> haelfteB, (double Von, double Bis) klasse) =>
        [.. haelfteB.Where(x => x.Paarung.FavoritenChanceA >= klasse.Von && x.Paarung.FavoritenChanceA < klasse.Bis)];

    // ── K1 Ergebnisverteilung ─────────────────────────────────────────────

    public Realismuskennzahl Remis { get; } = new("Remis", 0.220, 0.275, "BL 24,7 %", "P1",
        s => Anteil(s, x => x.HeimTore == x.GastTore));

    public Realismuskennzahl NullNull { get; } = new("0:0", 0.045, 0.072, "BL 5,8 %", "P1",
        s => Anteil(s, x => x.HeimTore == 0 && x.GastTore == 0));

    public Realismuskennzahl ZuNull { get; } = new("Zu null je Team und Spiel", 0.215, 0.260, "BL 23,8 %", "P1",
        s => s.Sum(x => (x.HeimTore == 0 ? 1 : 0) + (x.GastTore == 0 ? 1 : 0)) / (2.0 * s.Count));

    public Realismuskennzahl FuenfTore { get; } = new("≥ 5 Tore", 0.165, 0.215, "BL 19,1 %", "P1",
        s => Anteil(s, x => x.HeimTore + x.GastTore >= 5));

    public Realismuskennzahl SiebenTore { get; } = new("≥ 7 Tore", 0.024, 0.046, "BL 3,4 %", "P1",
        s => Anteil(s, x => x.HeimTore + x.GastTore >= 7));

    public Realismuskennzahl Tordifferenz3 { get; } = new("Tordifferenz ≥ 3", 0.175, 0.225, "BL 19,9 %", "P1",
        s => Anteil(s, x => Math.Abs(x.HeimTore - x.GastTore) >= 3));

    public Realismuskennzahl EinsNull { get; } = new("1:0 + 0:1", 0.105, 0.137, "BL 12,1 %", "P1",
        s => Anteil(s, x => x.HeimTore + x.GastTore == 1));

    public Realismuskennzahl EinsEins { get; } = new("1:1", 0.105, 0.133, "BL 11,9 %", "P1",
        s => Anteil(s, x => x.HeimTore == 1 && x.GastTore == 1));

    // ── K2 Dixon-Coles als Maßstab ────────────────────────────────────────

    public Realismuskennzahl Rho { get; } = new("ρ der Simulation", -0.19, -0.09, "BL −0,139", "F3",
        s => DixonColes.RhoSchaetzen(DixonColesDaten(s)));

    public Realismuskennzahl DcEinsEins { get; } = new("1:1 häufiger als bei Unabhängigkeit", 1.10, double.PositiveInfinity,
        "BL 1,22", "F2", s => DixonColes.VerhaeltnisZuUnabhaengig(DixonColesDaten(s), 1, 1));

    public Realismuskennzahl DcEinsNull { get; } = new("1:0 und 0:1 seltener als bei Unabhängigkeit", double.NegativeInfinity, 0.90,
        "BL 0,80", "F2", s => DixonColes.VerhaeltnisZuUnabhaengig(DixonColesDaten(s), [(1, 0), (0, 1)]));

    // ── K3 Zeitprofil ─────────────────────────────────────────────────────

    public Realismuskennzahl ErsteHalbzeit { get; } = new("Anteil Tore 1. Halbzeit", 0.425, 0.465, "BL 44,6 %, LS 43,8 %", "P1",
        Realismusauswertung.AnteilErsteHalbzeit);

    public Realismuskennzahl Minute1Bis15 { get; } = new("Tore in Minute 1–15", 0.110, 0.136, "LS 12,3 %", "P1",
        s => Realismusauswertung.TorminutenAnteile(s)[0]);

    public Realismuskennzahl Minute76Bis90 { get; } = new("Tore in Minute 76–90 inkl. Nachspielzeit", 0.215, 0.253, "LS 23,4 %", "P1",
        s => Realismusauswertung.TorminutenAnteile(s)[5]);

    // ── K4 Führungssicherung ──────────────────────────────────────────────

    public Realismuskennzahl Niederlage1Pause { get; } = new("+1 zur Pause: Niederlage", 0.085, 0.140, "BL 11,2 %", "P1",
        s => Realismusauswertung.FuehrungsAusgang(s, 45, 1).Niederlage);

    public Realismuskennzahl FavoritSieg { get; } = new("+1 zur Pause, Führender ist Favorit: Sieg", 0.695, 0.795, "BL 74,4 %", "P1",
        s => Realismusauswertung.FuehrungsAusgang(HaelfteB(s), 45, 1, (x, heimFuehrt) => x.Paarung.HeimIstFavorit == heimFuehrt).Sieg);

    public Realismuskennzahl AussenseiterSieg { get; } = new("+1 zur Pause, Führender ist Außenseiter: Sieg", 0.490, 0.615, "BL 55,2 %", "P1",
        s => Realismusauswertung.FuehrungsAusgang(HaelfteB(s), 45, 1, (x, heimFuehrt) => x.Paarung.HeimIstFavorit != heimFuehrt).Sieg);

    public Realismuskennzahl Sieg1Pause { get; } = new("+1 zur Pause: Sieg", 0.635, 0.710, "BL 67,3 %", "P1",
        s => Realismusauswertung.FuehrungsAusgang(s, 45, 1).Sieg);

    public Realismuskennzahl Sieg2Pause { get; } = new("+2 zur Pause: Sieg", 0.880, 0.945, "BL 91,3 %", "P1",
        s => Realismusauswertung.FuehrungsAusgang(s, 45, 2).Sieg);

    public Realismuskennzahl Zuwachs { get; } = new("Zuwachs der Siegquote bei +1 von 45′ auf 85′", 0.10, 0.21, "LS +15,4 Punkte", "P1",
        s => Realismusauswertung.FuehrungsAusgang(s, 85, 1).Sieg - Realismusauswertung.FuehrungsAusgang(s, 45, 1).Sieg);

    // ── K5 Struktur der Ausgangswahrscheinlichkeiten ──────────────────────

    /// <summary>Größte Abweichung der Remisquote vom Quotenbild über die geprüften Favoritenklassen: Klassen aus Hälfte A,
    /// Siegquote des Favoriten und Remisquote beide in Hälfte B – die A-Schätzung selbst taugt nicht als Siegchance der
    /// Klasse (Regression zur Mitte, in #678 nachgerechnet).</summary>
    public Realismuskennzahl Remisklassen { get; }

    private double RemisklassenAbweichung(IReadOnlyList<SimuliertesSpiel> spiele)
    {
        var haelfteB = HaelfteB(spiele).ToList();
        double max = 0;
        foreach (var klasse in _geprueft)
        {
            var spieleKlasse = Klasse(haelfteB, klasse);
            if (spieleKlasse.Count == 0) continue;
            var (_, remis, ziel) = Remisbild(spieleKlasse);
            max = Math.Max(max, Math.Abs(remis - ziel));
        }
        return max;
    }

    private static (double SiegFavorit, double Remis, double Ziel) Remisbild(List<SimuliertesSpiel> klasse)
    {
        double siegFavorit = Anteil(klasse, x => x.Paarung.HeimIstFavorit ? x.HeimTore > x.GastTore : x.GastTore > x.HeimTore);
        double remis = Anteil(klasse, x => x.HeimTore == x.GastTore);
        return (siegFavorit, remis, Realismusauswertung.Interpolieren(_remisJeSiegchance, siegFavorit));
    }

    /// <summary>Berichtszeilen je Favoritenklasse auf der vollen Stichprobe (Anteil, Siegquote, Remis, Ziel).</summary>
    public IEnumerable<string> Remisklassenzeilen()
    {
        var haelfteB = HaelfteB(_voll).ToList();
        foreach (var klasse in _favoritenklassen)
        {
            var spiele = Klasse(haelfteB, klasse);
            if (spiele.Count == 0) { yield return $"Favorit {klasse.Von:F2}–{klasse.Bis:F2}: keine Spiele"; continue; }
            var (siegFavorit, remis, ziel) = Remisbild(spiele);
            yield return $"Favorit {klasse.Von:F2}–{klasse.Bis:F2}: n = {spiele.Count}, Anteil {(double)spiele.Count / haelfteB.Count:P1}, " +
                         $"Siegquote Favorit {siegFavorit:P1}, Remis {remis:P1} (Ziel {ziel:P1} ± 3,0)" +
                         (spiele.Count < MINDESTSPIELE_KLASSE ? ", nicht geprüft (< 1000 Spiele)" : "");
        }
    }

    // ── Chancen (#431, K3) ────────────────────────────────────────────────
    // Zielwerte und Herleitung der Bänder: Kommentar „Schritt 1“ zu #431.

    public Realismuskennzahl Grosschancen { get; } = new("Großchancen inkl. Elfmeter je Team und Spiel", 1.6, 2.6, "Ziel 2,0", "F2",
        s => Realismusauswertung.Chancen([.. s]).GrosschancenJeTeam);

    public Realismuskennzahl Verwertung { get; } = new("Verwertung der Großchancen", 0.35, 0.45, "Ziel 40 %", "P1",
        s => Realismusauswertung.Chancen([.. s]).Verwertung);

    public Realismuskennzahl ElfmeterJeSpiel { get; } = new("Elfmeter je Spiel", 0.26, 0.38, "Ziel 0,32", "F2",
        s => Realismusauswertung.Chancen([.. s]).ElfmeterJeSpiel);

    public Realismuskennzahl ElfmeterQuote { get; } = new("Elfmeterquote", 0.72, 0.84, "Ziel 78,5 %", "P1",
        s => Realismusauswertung.Chancen([.. s]).ElfmeterQuote);

    public Realismuskennzahl ElfmeterAnteil { get; } = new("Anteil der Elfmetertore an allen Toren", 0.065, 0.100, "Ziel 8 %", "P1",
        s => Realismusauswertung.Chancen([.. s]).AnteilElfmeterTore);

    // ── K6 Platzverweise ──────────────────────────────────────────────────

    public Realismuskennzahl RoteKarten { get; } = new("Rote Karten je Team und Spiel", 0.060, 0.088, "BL 0,073", "F3",
        s => s.Sum(x => { var (h, g) = Realismusauswertung.RoteKarten(x); return h + g; }) / (2.0 * s.Count));

    /// <summary>Siegquote in Überzahl minus Siegchance ohne Rücksicht auf Karten; gemessen in Hälfte B, die Siegchance
    /// kommt aus Hälfte A – so steckt das gemessene Spiel nicht in seiner eigenen Referenz.</summary>
    public Realismuskennzahl Ueberzahl { get; } = new("Siegquote in Überzahl minus Siegchance vor dem Spiel", 0.10, 0.22,
        "BL +16 Punkte", "P1", UeberzahlGewinn);

    private static double UeberzahlGewinn(IReadOnlyList<SimuliertesSpiel> spiele)
    {
        double siege = 0, erwartet = 0;
        int n = 0;
        foreach (var s in HaelfteB(spiele))
        {
            var (heimRot, gastRot) = Realismusauswertung.RoteKarten(s);
            if (heimRot == gastRot) continue;
            bool heimUeberzahl = heimRot < gastRot;
            bool gewonnen = heimUeberzahl ? s.HeimTore > s.GastTore : s.GastTore > s.HeimTore;
            n++;
            siege += gewonnen ? 1 : 0;
            erwartet += heimUeberzahl ? s.Paarung.SiegHeimA : s.Paarung.SiegGastA;
        }
        return (siege - erwartet) / n;
    }
}
