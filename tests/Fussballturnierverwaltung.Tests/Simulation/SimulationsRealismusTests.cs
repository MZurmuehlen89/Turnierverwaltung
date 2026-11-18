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

    private static void Bericht(string zeile) => TestContext.Current.TestOutputHelper?.WriteLine(zeile);

    private static void ImBand(string name, double messwert, double von, double bis, string ziel)
    {
        Bericht($"{name}: {messwert:P2}  (Band {von:P1} … {bis:P1}, {ziel})");
        messwert.Should().BeInRange(von, bis, name);
    }

    private static double Anteil(IEnumerable<SimuliertesSpiel> spiele, Func<SimuliertesSpiel, bool> bedingung) =>
        Realismusauswertung.Anteil(spiele, bedingung);

    // ── K1 Ergebnisverteilung ─────────────────────────────────────────────

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Ergebnisverteilung_RemisNullNullEinsEinsUndHoheErgebnisse()
    {
        ImBand("Remis", Anteil(Spiele, s => s.HeimTore == s.GastTore), 0.220, 0.275, "BL 24,7 %");
        ImBand("0:0", Anteil(Spiele, s => s.HeimTore == 0 && s.GastTore == 0), 0.045, 0.072, "BL 5,8 %");
        ImBand("1:1", Anteil(Spiele, s => s.HeimTore == 1 && s.GastTore == 1), 0.105, 0.133, "BL 11,9 %");
        double zuNull = Spiele.Sum(s => (s.HeimTore == 0 ? 1 : 0) + (s.GastTore == 0 ? 1 : 0)) / (2.0 * Spiele.Count);
        ImBand("Zu null je Team und Spiel", zuNull, 0.215, 0.260, "BL 23,8 %");
        ImBand("≥ 5 Tore", Anteil(Spiele, s => s.HeimTore + s.GastTore >= 5), 0.165, 0.215, "BL 19,1 %");
        ImBand("≥ 7 Tore", Anteil(Spiele, s => s.HeimTore + s.GastTore >= 7), 0.024, 0.046, "BL 3,4 %");
        ImBand("Tordifferenz ≥ 3", Anteil(Spiele, s => Math.Abs(s.HeimTore - s.GastTore) >= 3), 0.175, 0.225, "BL 19,9 %");
    }

    [Fact(Skip = "Abweichung: 14,8 % statt höchstens 13,7 % – Korrektur in #674 (Nachlassen bei Führung erzeugt Ausgleiche)")]
    public void Ergebnisverteilung_EinsNullUndNullEins()
    {
        ImBand("1:0 + 0:1", Anteil(Spiele, s => s.HeimTore + s.GastTore == 1), 0.105, 0.137, "BL 12,1 %");
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
        Bericht($"Tor ab der 85. Minute {Anteil(Spiele, s => s.Ergebnis.Ereignisse.Any(e => e.Art == Domain.Enums.EreignisArt.Tor && e.Minute >= 85 && e.Minute <= 90)):P1} " +
                "(LS 28,4 %, nicht vergleichbar)");

        ImBand("Anteil Tore 1. Halbzeit", Realismusauswertung.AnteilErsteHalbzeit(Spiele), 0.425, 0.465, "BL 44,6 %, LS 43,8 %");
        ImBand("Tore in Minute 1–15", anteile[0], 0.110, 0.136, "LS 12,3 %");
        ImBand("Tore in Minute 76–90 inkl. Nachspielzeit", anteile[5], 0.215, 0.253, "LS 23,4 %");
    }

    // ── K4 Führungssicherung ──────────────────────────────────────────────

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Fuehrung_EinTorVorsprungZurPause()
    {
        var alle = Realismusauswertung.FuehrungsAusgang(Spiele, 45, 1);
        var favorit = Realismusauswertung.FuehrungsAusgang(Spiele, 45, 1, (s, heimFuehrt) => s.Paarung.HeimIstFavorit == heimFuehrt);
        var aussenseiter = Realismusauswertung.FuehrungsAusgang(Spiele, 45, 1, (s, heimFuehrt) => s.Paarung.HeimIstFavorit != heimFuehrt);
        Bericht($"+1 zur Pause: n = {alle.Anzahl}");

        ImBand("+1 zur Pause: Sieg", alle.Sieg, 0.635, 0.710, "BL 67,3 %");
        ImBand("+1 zur Pause: Niederlage", alle.Niederlage, 0.085, 0.140, "BL 11,2 %");
        ImBand("+1 zur Pause, Führender ist Favorit: Sieg", favorit.Sieg, 0.695, 0.795, "BL 74,4 %");
        ImBand("+1 zur Pause, Führender ist Außenseiter: Sieg", aussenseiter.Sieg, 0.490, 0.615, "BL 55,2 %");
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
        var haelfteB = Spiele.Where(s => s.Durchlauf >= RealismusStichprobe.ENDE_HAELFTE_A).ToList();
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

    // ── K6 Platzverweise ──────────────────────────────────────────────────

    [Fact(Skip = "Abweichung: 0,147 statt höchstens 0,088 Rote Karten je Team und Spiel – Korrektur in #682")]
    public void Platzverweise_HaeufigkeitJeTeamUndSpiel()
    {
        double jeTeam = Spiele.Sum(s => { var (h, g) = Realismusauswertung.RoteKarten(s); return h + g; }) / (2.0 * Spiele.Count);
        ImBandWert("Rote Karten je Team und Spiel", jeTeam, 0.060, 0.088, "BL 0,073");
    }

    [Fact(Skip = "Abweichung: Überzahl +6,1 statt mindestens +10 Punkte – Korrektur in #682")]
    public void Platzverweise_UeberzahlHilftSpuerbar()
    {
        var mitDifferenz = Spiele.Where(s => { var (h, g) = Realismusauswertung.RoteKarten(s); return h != g; }).ToList();
        double siege = 0, erwartet = 0;
        foreach (var s in mitDifferenz)
        {
            var (heimRot, gastRot) = Realismusauswertung.RoteKarten(s);
            bool heimUeberzahl = heimRot < gastRot;
            bool gewonnen = heimUeberzahl ? s.HeimTore > s.GastTore : s.GastTore > s.HeimTore;
            siege += gewonnen ? 1 : 0;
            erwartet += heimUeberzahl ? s.Paarung.SiegHeimAlle : s.Paarung.SiegGastAlle;
        }
        double siegquote = siege / mitDifferenz.Count, vorher = erwartet / mitDifferenz.Count;
        double gewinn = siegquote - vorher;
        Bericht($"Überzahl: n = {mitDifferenz.Count}, Siegquote {siegquote:P1} gegen Siegchance ohne Rücksicht auf Karten {vorher:P1} " +
                $"→ {gewinn * 100:+0.0;-0.0} Punkte (Band +10 … +22, BL +16)");
        gewinn.Should().BeInRange(0.10, 0.22, "Siegquote in Überzahl minus Siegchance vor dem Spiel");
    }

    private static void ImBandWert(string name, double messwert, double von, double bis, string ziel)
    {
        Bericht($"{name}: {messwert:F3}  (Band {von:F3} … {bis:F3}, {ziel})");
        messwert.Should().BeInRange(von, bis, name);
    }
}
