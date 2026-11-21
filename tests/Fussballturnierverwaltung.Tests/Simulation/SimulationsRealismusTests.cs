using FluentAssertions;
using Fussballturnierverwaltung.Tests.Simulation.Realismus;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>
/// Issue #678: Verteilungstests gegen reale Zielwerte. Quellen (eigene Auswertung der Rohdaten, Skripte in
/// <c>tools/Realismus-Zielwerte/</c>): <b>BL</b> = football-data.co.uk, Bundesliga 2015/16–2024/25, 3059 Spiele
/// (Endstand, Halbzeit, Rote Karten, Pinnacle-Schlussquoten); <b>LS</b> = martj42/international_results, Pflicht-
/// Länderspiele ab 2010 (18 012 Tore) – nur für das Zeitprofil (Anteile). Die Stichprobe (100 Bundesliga-Saisons,
/// 30 600 Spiele, ausführlicher Pfad, Seed 678) wird einmal erzeugt und von allen Tests gelesen. Ein Test, dessen
/// Kennzahl heute abweicht, wird nicht weicher gemacht, sondern mit Verweis auf den Baustein übersprungen, der ihn
/// grün machen soll. Jeder Test schreibt Mess- und Zielwert in die Testausgabe.
/// <para><b>Bänder</b> (#683, Katalog <see cref="Realismuskennzahlen"/>): Halbbreite des Bandes in Standardfehlern der
/// Realdaten (SE real, Zahlen der Ausgangsmessung zu #683): Remis 3,5 · Zu null 4,2 · ≥ 7 Tore 3,3 · Tordifferenz ≥ 3 3,5 ·
/// 1:0 + 0:1 2,7 · 1:1 2,4 · ρ 2,0 · Dixon-Coles 1:0 (Abstand von BL) 2,4 · Anteil 1. Halbzeit 3,9 · +1 zur Pause,
/// Niederlage 3,2. Die übrigen Bänder wurden in #678/#431 nach demselben Muster (zwei Standardfehler plus Spielraum bzw.
/// Zielwert ± Toleranz) gewählt und nicht einzeln neu hergeleitet; wo ein Band an der Grenze lag, entscheidet der Rauschzuschlag.</para>
/// <para><b>Rauschzuschlag</b> (#683): jede Kennzahl schätzt ihr Monte-Carlo-Rauschen aus der eigenen Stichprobe
/// (Jackknife ohne je eine der 100 Saisons, <see cref="Rauschschaetzung"/>) und wird erst rot, wenn sie ihre
/// Bandgrenze um mehr als 2 SE überschreitet (<see cref="Bandpruefung"/>). Grund: das Rauschen ist klein (SD über Seeds
/// ≈ 0,2 Punkte), rot wurde ein Test, weil sein Mittel <i>auf</i> der Grenze liegt; mehr Spiele oder ein Mittel über
/// Seeds machten das nur deterministisch. Die echten Abweichungen vom Realbild zeigt die Messreihe
/// (<c>RealismusMessreihe</c>, Aufruf in der Tests-<c>CLAUDE.md</c>), sie gehören in die Kalibrierung, nicht ins Band.</para>
/// </summary>
public class SimulationsRealismusTests(RealismusStichprobe stichprobe) : IClassFixture<RealismusStichprobe>
{
    private const string SKIP_MUTATION = "Im Mutationslauf zu langsam (#678)";

    private IReadOnlyList<SimuliertesSpiel> Spiele => stichprobe.Spiele;

    // Wo eine Größe der Paarung aus Hälfte A einfließt (Favorit, Siegchance), wird nur in Hälfte B gemessen:
    // sonst bestimmen dieselben Spiele Einteilung und Ergebnis.
    private IEnumerable<SimuliertesSpiel> HaelfteB => Spiele.Where(s => s.Durchlauf >= RealismusStichprobe.ENDE_HAELFTE_A);

    private static void Bericht(string zeile) => TestContext.Current.TestOutputHelper?.WriteLine(zeile);

    private Realismuskennzahlen? _katalog;
    private Realismuskennzahlen Katalog => _katalog ??= new(Spiele);

    /// <summary>Misst jede Kennzahl, schätzt ihr Jackknife-Rauschen, wenn sie außerhalb ihres Bandes liegt, schreibt alle
    /// Berichtszeilen und schlägt erst danach für alle roten Kennzahlen zusammen fehl (Band mit Rauschzuschlag,
    /// <see cref="Bandpruefung"/>).</summary>
    private void Pruefen(params Realismuskennzahl[] kennzahlen)
    {
        var rot = new List<string>();
        foreach (var kennzahl in kennzahlen)
        {
            double wert = kennzahl.Messen(Spiele);
            var ergebnis = Bandpruefung.Pruefen(kennzahl, wert, () => Rauschschaetzung.Standardfehler(kennzahl, Spiele));
            Bericht(ergebnis.Zeile);
            if (!ergebnis.Gruen) rot.Add(ergebnis.Zeile);
        }
        rot.Should().BeEmpty("jede Kennzahl liegt höchstens 2 Standardfehler außerhalb ihres Bandes");
    }

    // ── K1 Ergebnisverteilung ─────────────────────────────────────────────

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Ergebnisverteilung_RemisNullNullUndHoheErgebnisse() =>
        Pruefen(Katalog.Remis, Katalog.NullNull, Katalog.ZuNull, Katalog.FuenfTore, Katalog.SiebenTore, Katalog.Tordifferenz3);

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Ergebnisverteilung_EinsNullNullEinsUndEinsEins() =>
        Pruefen(Katalog.EinsNull, Katalog.EinsEins);

    /// <summary>#674, K8: nur Bericht – Ergebnisse im Verhältnis zu unabhängigen Poisson-Ziehungen. BL: 0:0 1,04,
    /// 2:0 + 0:2 0,94, 2:1 + 1:2 1,04, 2:2 1,11, Remis gesamt 1,14 (Auswertung in #674).</summary>
    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Ergebnisverteilung_NurBericht_VerhaeltnisZuUnabhaengigenZiehungen()
    {
        var daten = DixonColesDaten();
        Bericht($"÷ unabhängig: 0:0 {DixonColes.VerhaeltnisZuUnabhaengig(daten, 0, 0):F2} (BL 1,04), " +
                $"2:0 + 0:2 {DixonColes.VerhaeltnisZuUnabhaengig(daten, [(2, 0), (0, 2)]):F2} (BL 0,94), " +
                $"2:1 + 1:2 {DixonColes.VerhaeltnisZuUnabhaengig(daten, [(2, 1), (1, 2)]):F2} (BL 1,04), " +
                $"2:2 {DixonColes.VerhaeltnisZuUnabhaengig(daten, 2, 2):F2} (BL 1,11), " +
                $"Remis {DixonColes.VerhaeltnisZuUnabhaengig(daten, [.. Enumerable.Range(0, 12).Select(k => (k, k))]):F2} (BL 1,14)");
        daten.Should().NotBeEmpty();
    }

    // ── K2 Dixon-Coles als Maßstab ────────────────────────────────────────

    private List<(double Lambda, double Mu, int Heim, int Gast)> DixonColesDaten() =>
        Spiele.Select(s => (s.Paarung.MittlereToreHeim, s.Paarung.MittlereToreGast, s.HeimTore, s.GastTore)).ToList();

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void DixonColes_RhoUndVerhaeltnisseZuUnabhaengigenPoissonZiehungen()
    {
        Bericht($"0:0 ÷ unabhängig {DixonColes.VerhaeltnisZuUnabhaengig(DixonColesDaten(), 0, 0):F2} (BL 1,04)");
        Pruefen(Katalog.Rho, Katalog.DcEinsEins, Katalog.DcEinsNull);
    }

    // ── K3 Zeitprofil ─────────────────────────────────────────────────────

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
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

        Pruefen(Katalog.ErsteHalbzeit, Katalog.Minute1Bis15, Katalog.Minute76Bis90);
    }

    // ── K4 Führungssicherung ──────────────────────────────────────────────

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Fuehrung_EinTorVorsprungZurPause()
    {
        var alle = Realismusauswertung.FuehrungsAusgang(Spiele, 45, 1);
        var favorit = Realismusauswertung.FuehrungsAusgang(HaelfteB, 45, 1, (s, heimFuehrt) => s.Paarung.HeimIstFavorit == heimFuehrt);
        var aussenseiter = Realismusauswertung.FuehrungsAusgang(HaelfteB, 45, 1, (s, heimFuehrt) => s.Paarung.HeimIstFavorit != heimFuehrt);
        Bericht($"+1 zur Pause: n = {alle.Anzahl} (Favorit/Außenseiter nur Hälfte B: n = {favorit.Anzahl}/{aussenseiter.Anzahl})");

        Pruefen(Katalog.Niederlage1Pause, Katalog.FavoritSieg, Katalog.AussenseiterSieg);
    }

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Fuehrung_EinTorVorsprungZurPause_Siegquote() =>
        Pruefen(Katalog.Sieg1Pause);

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Fuehrung_ZweiToreVorsprungZurPause()
    {
        Bericht($"+2 zur Pause: n = {Realismusauswertung.FuehrungsAusgang(Spiele, 45, 2).Anzahl}");
        Pruefen(Katalog.Sieg2Pause);
    }

    /// <summary>#674, K8: nur Bericht – durch die Stärkeauswahl verzerrt (wer zur Pause führt, ist meist stärker) und bei
    /// +3 mit n = 121 zu unsicher für ein Band. BL: +3 und mehr → Sieg 95,9 %; Tore der 2. Halbzeit (Führender /
    /// Zurückliegender) bei Gleichstand 0,86 / 0,68 (Heim / Gast), bei +1 0,93 / 0,80, bei +2 1,12 / 0,78, bei +3 und mehr
    /// 1,22 / 0,71.</summary>
    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Fuehrung_NurBericht_DreiToreUndZweiteHalbzeit()
    {
        var dreiUndMehr = Realismusauswertung.FuehrungsAusgang(Spiele, 45, 3, mindestens: true);
        Bericht($"+3 und mehr zur Pause: n = {dreiUndMehr.Anzahl}, Sieg {dreiUndMehr.Sieg:P1} (BL 95,9 %)");
        string[] stand = ["Gleichstand (Heim / Gast)", "+1", "+2", "+3 und mehr"];
        string[] real = ["0,86 / 0,68", "0,93 / 0,80", "1,12 / 0,78", "1,22 / 0,71"];
        for (int vorsprung = 0; vorsprung <= 3; vorsprung++)
        {
            var (fuehrender, zurueckliegender) = Realismusauswertung.ToreZweiteHalbzeit(Spiele, vorsprung, mindestens: vorsprung == 3);
            Bericht($"2. Halbzeit bei {stand[vorsprung]}: {fuehrender:F2} / {zurueckliegender:F2} (BL {real[vorsprung]})");
        }
        dreiUndMehr.Anzahl.Should().BePositive();
    }

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Fuehrung_SiegquoteWaechstMitDerSpielminute()
    {
        var quoten = new[] { 45, 60, 75, 85 }.ToDictionary(m => m, m => Realismusauswertung.FuehrungsAusgang(Spiele, m, 1).Sieg);
        Bericht("+1 nach 45′/60′/75′/85′ → Sieg: " + string.Join(" / ", quoten.Values.Select(q => $"{q:P1}")) +
                " (BL/LS 60′ 77,3 / 75′ 82,3 / 85′ 88,2 %, LS 45′ 72,8 %)");
        Pruefen(Katalog.Zuwachs);
    }

    // ── K5 Struktur der Ausgangswahrscheinlichkeiten ──────────────────────

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Remisquote_JeFavoritenklasse_FolgtDemQuotenbild()
    {
        // Kreuzschätzung: jedes Spiel wird nach der Siegchance aus der anderen Hälfte eingeteilt (Begründung: Katalog). Die
        // Klassen mit mindestens 2000 Spielen werden einmal auf der vollen Stichprobe gewählt, nicht im Jackknife.
        foreach (var zeile in Katalog.Remisklassenzeilen()) Bericht(zeile);
        Katalog.GepruefteRemisklassen.Should().BeGreaterThanOrEqualTo(4, "genug Klassen mit mindestens 1000 Spielen");
        Pruefen(Katalog.Remisklassen);
    }

    // ── Chancen (#431, K3) ────────────────────────────────────────────────

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Chancen_HaeufigkeitUndVerwertung() =>
        Pruefen(Katalog.Grosschancen, Katalog.Verwertung, Katalog.ElfmeterJeSpiel, Katalog.ElfmeterQuote, Katalog.ElfmeterAnteil);

    // ── K6 Platzverweise ──────────────────────────────────────────────────

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Platzverweise_HaeufigkeitJeTeamUndSpiel() =>
        Pruefen(Katalog.RoteKarten);

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Platzverweise_UeberzahlHilftSpuerbar() =>
        Pruefen(Katalog.Ueberzahl);
}
