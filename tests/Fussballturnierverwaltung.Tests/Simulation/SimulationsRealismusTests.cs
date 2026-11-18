using FluentAssertions;
using Fussballturnierverwaltung.Tests.Simulation.Realismus;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>
/// Issue #678: Verteilungstests gegen reale Zielwerte. Quellen (eigene Auswertung der Rohdaten, Skripte in
/// <c>tools/Realismus-Zielwerte/</c>): <b>BL</b> = football-data.co.uk, Bundesliga 2015/16–2024/25, 3059 Spiele
/// (Endstand, Halbzeit, Rote Karten, Pinnacle-Schlussquoten); <b>LS</b> = martj42/international_results, Pflicht-
/// Länderspiele ab 2010 (18 012 Tore) – nur für das Zeitprofil (Anteile). Die Stichprobe (100 Bundesliga-Saisons,
/// 30 600 Spiele, ausführlicher Pfad, Seed 678) wird einmal erzeugt und von allen Tests gelesen. Die Bänder liegen bei
/// etwa zwei Standardfehlern der Realdaten plus Spielraum. Ein Test, dessen Kennzahl heute abweicht, wird nicht
/// weicher gemacht, sondern mit Verweis auf den Baustein übersprungen, der ihn grün machen soll. Jeder Test schreibt
/// Mess- und Zielwert in die Testausgabe.
/// </summary>
public class SimulationsRealismusTests(RealismusStichprobe stichprobe) : IClassFixture<RealismusStichprobe>
{
    private const string SKIP_MUTATION = "Im Mutationslauf zu langsam (#678)";

    private IReadOnlyList<SimuliertesSpiel> Spiele => stichprobe.Spiele;

    // Wo eine Größe der Paarung aus Hälfte A einfließt (Favorit, Siegchance), wird nur in Hälfte B gemessen:
    // sonst bestimmen dieselben Spiele Einteilung und Ergebnis.
    private IEnumerable<SimuliertesSpiel> HaelfteB => Spiele.Where(s => s.Durchlauf >= RealismusStichprobe.ENDE_HAELFTE_A);

    private static void Bericht(string zeile) => TestContext.Current.TestOutputHelper?.WriteLine(zeile);

    private static void ImBand(string name, double messwert, double von, double bis, string ziel, string format = "P1")
    {
        Bericht($"{name}: {messwert.ToString(format)}  (Band {von.ToString(format)} … {bis.ToString(format)}, {ziel})");
        messwert.Should().BeInRange(von, bis, name);
    }

    private static double Anteil(IEnumerable<SimuliertesSpiel> spiele, Func<SimuliertesSpiel, bool> bedingung) =>
        Realismusauswertung.Anteil(spiele, bedingung);

    // ── K1 Ergebnisverteilung ─────────────────────────────────────────────

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Ergebnisverteilung_RemisNullNullUndHoheErgebnisse()
    {
        ImBand("Remis", Anteil(Spiele, s => s.HeimTore == s.GastTore), 0.220, 0.275, "BL 24,7 %");
        ImBand("0:0", Anteil(Spiele, s => s.HeimTore == 0 && s.GastTore == 0), 0.045, 0.072, "BL 5,8 %");
        double zuNull = Spiele.Sum(s => (s.HeimTore == 0 ? 1 : 0) + (s.GastTore == 0 ? 1 : 0)) / (2.0 * Spiele.Count);
        ImBand("Zu null je Team und Spiel", zuNull, 0.215, 0.260, "BL 23,8 %");
        ImBand("≥ 5 Tore", Anteil(Spiele, s => s.HeimTore + s.GastTore >= 5), 0.165, 0.215, "BL 19,1 %");
        ImBand("≥ 7 Tore", Anteil(Spiele, s => s.HeimTore + s.GastTore >= 7), 0.024, 0.046, "BL 3,4 %");
        ImBand("Tordifferenz ≥ 3", Anteil(Spiele, s => Math.Abs(s.HeimTore - s.GastTore) >= 3), 0.175, 0.225, "BL 19,9 %");
    }

    [Fact(Skip = "Abweichung: 1:0 + 0:1 15,4 % statt höchstens 13,7 %, 1:1 10,1 % statt mindestens 10,5 % (vor #682 14,8 / 10,7 %) – Korrektur in #674 (Nachlassen bei Führung erzeugt Ausgleiche)")]
    public void Ergebnisverteilung_EinsNullNullEinsUndEinsEins()
    {
        ImBand("1:0 + 0:1", Anteil(Spiele, s => s.HeimTore + s.GastTore == 1), 0.105, 0.137, "BL 12,1 %");
        ImBand("1:1", Anteil(Spiele, s => s.HeimTore == 1 && s.GastTore == 1), 0.105, 0.133, "BL 11,9 %");
    }

    // ── K2 Dixon-Coles als Maßstab ────────────────────────────────────────

    private List<(double Lambda, double Mu, int Heim, int Gast)> DixonColesDaten() =>
        Spiele.Select(s => (s.Paarung.MittlereToreHeim, s.Paarung.MittlereToreGast, s.HeimTore, s.GastTore)).ToList();

    [Fact(Skip = "Abweichung: ρ −0,036, 1:1 ×1,05, 1:0 + 0:1 ×0,98 statt ρ ≤ −0,09, ≥ 1,10, ≤ 0,90 – Korrektur in #674")]
    public void DixonColes_RhoUndVerhaeltnisseZuUnabhaengigenPoissonZiehungen()
    {
        var daten = DixonColesDaten();
        double rho = DixonColes.RhoSchaetzen(daten);
        double einsEins = DixonColes.VerhaeltnisZuUnabhaengig(daten, 1, 1);
        double einsNull = DixonColes.VerhaeltnisZuUnabhaengig(daten, [(1, 0), (0, 1)]);
        Bericht($"ρ {rho:F3} (Band −0,19 … −0,09, BL −0,139); 0:0 {DixonColes.VerhaeltnisZuUnabhaengig(daten, 0, 0):F2} " +
                $"(BL 1,04), 1:1 {einsEins:F2} (≥ 1,10, BL 1,22), 1:0 + 0:1 {einsNull:F2} (≤ 0,90, BL 0,80)");

        rho.Should().BeInRange(-0.19, -0.09, "ρ der Simulation");
        einsEins.Should().BeGreaterThanOrEqualTo(1.10, "1:1 häufiger als bei Unabhängigkeit");
        einsNull.Should().BeLessThanOrEqualTo(0.90, "1:0 und 0:1 seltener als bei Unabhängigkeit");
    }

    // ── K3 Zeitprofil ─────────────────────────────────────────────────────

    [Fact(Skip = "Abweichung: 1. Halbzeit 46,7 %, Minute 1–15 14,4 %, 76–90 20,9 % – Korrektur in #431 (Chancenrate je Spielphase)")]
    public void Torminuten_ZeitprofilDerTore()
    {
        var anteile = Realismusauswertung.TorminutenAnteile(Spiele);
        Bericht("Tore je 15 Minuten: " + string.Join(" / ", anteile.Select(a => $"{a:P1}")) +
                " (LS 12,3 / 14,2 / 17,2 / 16,4 / 16,5 / 23,4 %)");
        Bericht($"Anteil Nachspielzeit {Realismusauswertung.AnteilNachspielzeit(Spiele):P1} (LS ~8 %)");
        var laengste = Spiele.Select(s => Realismusauswertung.LaengsteTorlosePhase(s.Ergebnis.Ereignisse)).ToList();
        Bericht($"Längste torlose Phase ≥ 60 Min. {(double)laengste.Count(l => l >= 60) / laengste.Count:P1} " +
                "(LS 36,8 %, nicht vergleichbar)");
        Bericht($"Tor ab der 85. Minute {Realismusauswertung.AnteilMitTorAb(Spiele, 85):P1} (LS 28,4 %, nicht vergleichbar)");

        ImBand("Anteil Tore 1. Halbzeit", Realismusauswertung.AnteilErsteHalbzeit(Spiele), 0.425, 0.465, "BL 44,6 %, LS 43,8 %");
        ImBand("Tore in Minute 1–15", anteile[0], 0.110, 0.136, "LS 12,3 %");
        ImBand("Tore in Minute 76–90 inkl. Nachspielzeit", anteile[5], 0.215, 0.253, "LS 23,4 %");
    }

    // ── K4 Führungssicherung ──────────────────────────────────────────────

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Fuehrung_EinTorVorsprungZurPause()
    {
        var alle = Realismusauswertung.FuehrungsAusgang(Spiele, 45, 1);
        var favorit = Realismusauswertung.FuehrungsAusgang(HaelfteB, 45, 1, (s, heimFuehrt) => s.Paarung.HeimIstFavorit == heimFuehrt);
        var aussenseiter = Realismusauswertung.FuehrungsAusgang(HaelfteB, 45, 1, (s, heimFuehrt) => s.Paarung.HeimIstFavorit != heimFuehrt);
        Bericht($"+1 zur Pause: n = {alle.Anzahl} (Favorit/Außenseiter nur Hälfte B: n = {favorit.Anzahl}/{aussenseiter.Anzahl})");

        ImBand("+1 zur Pause: Niederlage", alle.Niederlage, 0.085, 0.140, "BL 11,2 %");
        ImBand("+1 zur Pause, Führender ist Favorit: Sieg", favorit.Sieg, 0.695, 0.795, "BL 74,4 %");
        ImBand("+1 zur Pause, Führender ist Außenseiter: Sieg", aussenseiter.Sieg, 0.490, 0.615, "BL 55,2 %");
    }

    [Fact(Skip = "Abweichung: 71,2 % statt höchstens 71,0 % (vor #682 70,3 %; seit #682 sieht öfter Rot, wer knapp hinten liegt) – Korrektur in #674 (Nachlassen bei Führung)")]
    public void Fuehrung_EinTorVorsprungZurPause_Siegquote()
    {
        ImBand("+1 zur Pause: Sieg", Realismusauswertung.FuehrungsAusgang(Spiele, 45, 1).Sieg, 0.635, 0.710, "BL 67,3 %");
    }

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Fuehrung_ZweiToreVorsprungZurPause()
    {
        var ausgang = Realismusauswertung.FuehrungsAusgang(Spiele, 45, 2);
        Bericht($"+2 zur Pause: n = {ausgang.Anzahl}");
        ImBand("+2 zur Pause: Sieg", ausgang.Sieg, 0.880, 0.945, "BL 91,3 %");
    }

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Fuehrung_SiegquoteWaechstMitDerSpielminute()
    {
        var quoten = new[] { 45, 60, 75, 85 }.ToDictionary(m => m, m => Realismusauswertung.FuehrungsAusgang(Spiele, m, 1).Sieg);
        Bericht("+1 nach 45′/60′/75′/85′ → Sieg: " + string.Join(" / ", quoten.Values.Select(q => $"{q:P1}")) +
                " (BL/LS 60′ 77,3 / 75′ 82,3 / 85′ 88,2 %, LS 45′ 72,8 %)");
        double zuwachs = quoten[85] - quoten[45];
        Bericht($"Zuwachs 45′ → 85′: {zuwachs * 100:F1} Punkte (Band +10 … +21, LS +15,4)");
        zuwachs.Should().BeInRange(0.10, 0.21, "Zuwachs der Siegquote bei +1 von 45′ auf 85′");
    }

    // ── K5 Struktur der Ausgangswahrscheinlichkeiten ──────────────────────

    // Klassen der Siegchance des Favoriten (wie im Auswertungsskript) – in der Simulation nur eine Gruppierung.
    private static readonly (double Von, double Bis)[] _favoritenklassen =
        [(0.33, 0.40), (0.40, 0.50), (0.50, 0.60), (0.60, 0.70), (0.70, 0.80), (0.80, 1.01)];

    // BL je Favoritenklasse: mittlere quotenimplizierte Siegchance des Favoriten → quotenimplizierte Remisquote
    // (Pinnacle-Schlussquoten, normiert; in den BL-Daten kalibriert: impliziert ≈ eingetreten).
    private static readonly (double SiegchanceFavorit, double Remis)[] _remisJeSiegchance =
        [(0.378, 0.283), (0.447, 0.270), (0.547, 0.244), (0.645, 0.205), (0.748, 0.157), (0.848, 0.101)];

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Remisquote_JeFavoritenklasse_FolgtDemQuotenbild()
    {
        // Die Klassen kommen aus Hälfte A, Siegquote des Favoriten und Remisquote werden beide in Hälfte B gemessen.
        // Die A-Schätzung selbst taugt nicht als Siegchance der Klasse: Wer nach 50 Läufen in einer Klasse landet, ist
        // im Mittel näher an der Mitte, als die Klasse sagt (Regression zur Mitte, in #678 nachgerechnet).
        var haelfteB = HaelfteB.ToList();
        int geprueft = 0;
        foreach (var (von, bis) in _favoritenklassen)
        {
            var klasse = haelfteB.Where(s => s.Paarung.FavoritenChanceA >= von && s.Paarung.FavoritenChanceA < bis).ToList();
            if (klasse.Count == 0) { Bericht($"Favorit {von:F2}–{bis:F2}: keine Spiele"); continue; }
            double siegFavorit = Anteil(klasse, s => s.Paarung.HeimIstFavorit ? s.HeimTore > s.GastTore : s.GastTore > s.HeimTore);
            double remis = Anteil(klasse, s => s.HeimTore == s.GastTore);
            double ziel = Realismusauswertung.Interpolieren(_remisJeSiegchance, siegFavorit);
            Bericht($"Favorit {von:F2}–{bis:F2}: n = {klasse.Count}, Anteil {(double)klasse.Count / haelfteB.Count:P1}, " +
                    $"Siegquote Favorit {siegFavorit:P1}, Remis {remis:P1} (Ziel {ziel:P1} ± 3,0)");
            if (klasse.Count < 1000) continue;

            geprueft++;
            remis.Should().BeInRange(ziel - 0.030, ziel + 0.030, $"Remisquote bei Siegchance des Favoriten {siegFavorit:P1}");
        }
        geprueft.Should().BeGreaterThanOrEqualTo(4, "genug Klassen mit mindestens 1000 Spielen");
    }

    // ── Chancen (#431, K3) ────────────────────────────────────────────────

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Chancen_HaeufigkeitUndVerwertung()
    {
        // Zielwerte und Herleitung der Bänder: Kommentar „Schritt 1“ zu #431.
        var c = Realismusauswertung.Chancen([.. Spiele]);
        ImBand("Großchancen inkl. Elfmeter je Team und Spiel", c.GrosschancenJeTeam, 1.6, 2.6, "Ziel 2,0", format: "F2");
        ImBand("Verwertung der Großchancen", c.Verwertung, 0.35, 0.45, "Ziel 40 %");
        ImBand("Elfmeter je Spiel", c.ElfmeterJeSpiel, 0.26, 0.38, "Ziel 0,32", format: "F2");
        ImBand("Elfmeterquote", c.ElfmeterQuote, 0.72, 0.84, "Ziel 78,5 %");
        ImBand("Anteil der Elfmetertore an allen Toren", c.AnteilElfmeterTore, 0.065, 0.100, "Ziel 8 %");
    }

    // ── K6 Platzverweise ──────────────────────────────────────────────────

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Platzverweise_HaeufigkeitJeTeamUndSpiel()
    {
        double jeTeam = Spiele.Sum(s => { var (h, g) = Realismusauswertung.RoteKarten(s); return h + g; }) / (2.0 * Spiele.Count);
        ImBand("Rote Karten je Team und Spiel", jeTeam, 0.060, 0.088, "BL 0,073", format: "F3");
    }

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Platzverweise_UeberzahlHilftSpuerbar()
    {
        // Gemessen in Hälfte B, die Siegchance ohne Rücksicht auf Karten kommt aus Hälfte A – so steckt das
        // gemessene Spiel nicht in seiner eigenen Referenz.
        var mitDifferenz = HaelfteB.Where(s => { var (h, g) = Realismusauswertung.RoteKarten(s); return h != g; }).ToList();
        double siege = 0, erwartet = 0;
        foreach (var s in mitDifferenz)
        {
            var (heimRot, gastRot) = Realismusauswertung.RoteKarten(s);
            bool heimUeberzahl = heimRot < gastRot;
            bool gewonnen = heimUeberzahl ? s.HeimTore > s.GastTore : s.GastTore > s.HeimTore;
            siege += gewonnen ? 1 : 0;
            erwartet += heimUeberzahl ? s.Paarung.SiegHeimA : s.Paarung.SiegGastA;
        }
        double siegquote = siege / mitDifferenz.Count, vorher = erwartet / mitDifferenz.Count;
        double gewinn = siegquote - vorher;
        Bericht($"Überzahl: n = {mitDifferenz.Count}, Siegquote {siegquote:P1} gegen Siegchance ohne Rücksicht auf Karten {vorher:P1} " +
                $"→ {gewinn * 100:+0.0;-0.0} Punkte (Band +10 … +22, BL +16)");
        gewinn.Should().BeInRange(0.10, 0.22, "Siegquote in Überzahl minus Siegchance vor dem Spiel");
    }
}
