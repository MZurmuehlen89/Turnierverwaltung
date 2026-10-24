using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;
using static Fussballturnierverwaltung.GuiTests.MiniTurnier;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Kernablauf (#522): Turnier aus Vorlage anlegen → Runde für Runde auslosen und simulieren → das Ergebnis
/// ist in sich stimmig. Geprüft werden nur Invarianten, keine festen Werte: Der Seed (4711) macht einen Fehlschlag
/// reproduzierbar, aber gewollte Änderungen an Auslosung oder Simulation sollen den Test nicht brechen.</summary>
public sealed class TurnierAblaufTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    [Fact]
    public void MiniTurnier_KomplettDurchgespielt_ErgibtStimmigesErgebnis()
    {
        var (turnier, auslosung) = GruppenauslosungVorbereiten(app.Hauptfenster);
        auslosung.Durchfuehren();
        var spiele = new Dictionary<string, IReadOnlyList<SpielZeile>>
        {
            [GRUPPENPHASE] = RundeSimulieren(turnier, GRUPPENPHASE, SPIELE_GRUPPENPHASE),
        };
        foreach (var (runde, anzahl) in KoRunden)
        {
            var koAuslosung = turnier.Auslosung();
            koAuslosung.Anlegen(runde);
            koAuslosung.ToepfeAutomatischBefuellen();
            koAuslosung.Durchfuehren();
            spiele[runde] = RundeSimulieren(turnier, runde, anzahl);
        }

        var fortschritt = turnier.Spielplan().Fortschritt;
        var statistik = turnier.Statistik();
        statistik.AuswertungWaehlen("Gesamt");
        var tabelle = statistik.Tabelle();

        AlleSpieleGespielt(fortschritt, spiele);
        string sieger = KoRundenFolgerichtig(spiele);
        GesamttabelleStimmig(tabelle, spiele);
        TestContext.Current.TestOutputHelper?.WriteLine($"Turniersieger: {sieger}");
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private static IReadOnlyList<SpielZeile> RundeSimulieren(TurnierdurchfuehrungSeite turnier, string runde, int anzahl)
    {
        var spielplan = turnier.Spielplan();
        spielplan.RundeWaehlen(runde);
        spielplan.RundeSimulieren(anzahl);
        return spielplan.Spiele();
    }

    // ── Invarianten ───────────────────────────────────────────────────────────

    private static void AlleSpieleGespielt(
        (int Gespielt, int Gesamt) fortschritt, Dictionary<string, IReadOnlyList<SpielZeile>> spiele)
    {
        fortschritt.Should().Be((SPIELE_GESAMT, SPIELE_GESAMT));
        spiele[GRUPPENPHASE].Should().HaveCount(SPIELE_GRUPPENPHASE);
        foreach (var (runde, anzahl) in KoRunden)
            spiele[runde].Should().HaveCount(anzahl, $"{runde} hat {anzahl} Spiel(e)");
        spiele.Values.SelectMany(s => s).Should().OnlyContain(s => s.IstGespielt);
        Teams(spiele[GRUPPENPHASE]).Should().HaveCount(TEAMS);
    }

    /// <summary>Halbfinalsieger spielen das Finale, Halbfinalverlierer um Platz 3, K.-o.-Spiele enden nie
    /// unentschieden. Liefert den Turniersieger.</summary>
    private static string KoRundenFolgerichtig(Dictionary<string, IReadOnlyList<SpielZeile>> spiele)
    {
        var koSpiele = KoRunden.SelectMany(r => spiele[r.Runde]).ToList();
        koSpiele.Should().OnlyContain(s => s.Sieger != null, "ein K.-o.-Spiel hat immer einen Sieger");

        var halbfinale = spiele[HALBFINALE];
        Teams(halbfinale).Should().HaveCount(4).And.BeSubsetOf(Teams(spiele[GRUPPENPHASE]));
        Teams(spiele[FINALE]).Should().BeEquivalentTo(halbfinale.Select(s => s.Sieger!));
        Teams(spiele[SPIEL_UM_PLATZ_DREI]).Should().BeEquivalentTo(halbfinale.Select(s => s.Verlierer!));

        return spiele[FINALE].Single().Sieger!;
    }

    /// <summary>Gesamttabelle: 8 Teams, Halbfinalisten mit 5 Spielen, alle anderen mit 3; je Zeile S+U+N = Sp und
    /// Pkt = 3·S + U; über alle Zeilen so viele Tore wie Gegentore.</summary>
    private static void GesamttabelleStimmig(
        IReadOnlyList<TabellenZeile> tabelle, Dictionary<string, IReadOnlyList<SpielZeile>> spiele)
    {
        tabelle.Should().HaveCount(TEAMS);
        tabelle.Select(z => z.Mannschaft).Should().BeEquivalentTo(Teams(spiele[GRUPPENPHASE]));

        var halbfinalisten = Teams(spiele[HALBFINALE]);
        foreach (var zeile in tabelle)
        {
            zeile.Zahl("Sp").Should().Be(halbfinalisten.Contains(zeile.Mannschaft) ? 5 : 3, zeile.Mannschaft);
            (zeile.Zahl("S") + zeile.Zahl("U") + zeile.Zahl("N")).Should().Be(zeile.Zahl("Sp"), zeile.Mannschaft);
            zeile.Zahl("Pkt").Should().Be(3 * zeile.Zahl("S") + zeile.Zahl("U"), zeile.Mannschaft);
        }

        var tore = tabelle.Select(z => Spielstand.Tore(z.Werte["Tore"])
            ?? throw new InvalidOperationException($"Tore von {z.Mannschaft} nicht lesbar: {z.Werte["Tore"]}")).ToList();
        tore.Sum(t => t.Heim).Should().Be(tore.Sum(t => t.Auswaerts), "jedes Tor ist ein Gegentor");
    }

    private static HashSet<string> Teams(IEnumerable<SpielZeile> spiele) =>
        [.. spiele.SelectMany(s => new[] { s.Heim, s.Auswaerts })];
}
