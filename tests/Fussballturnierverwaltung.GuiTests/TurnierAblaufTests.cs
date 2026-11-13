using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;
using GuiTest.Framework;
using static Fussballturnierverwaltung.GuiTests.MiniTurnier;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Kernablauf (#522): Turnier aus Vorlage anlegen → Runde für Runde auslosen und simulieren → das Ergebnis
/// ist in sich stimmig — im Spielplan, im Turnierbaum (#534: rundenweise und Gesamtübersicht zeigen dieselben Spiele,
/// das Siegerfeld den Finalsieger) und in der Statistik (Gesamttabelle, Platzierungen). Geprüft werden nur Invarianten, keine festen Werte: Der Seed (4711) macht einen Fehlschlag
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
            [GRUPPENPHASE] = turnier.RundeSimulieren(GRUPPENPHASE, SPIELE_GRUPPENPHASE),
        };
        foreach (var (runde, anzahl) in KoRunden)
        {
            var koAuslosung = turnier.Auslosung();
            koAuslosung.Anlegen(runde);
            koAuslosung.ToepfeAutomatischBefuellen();
            koAuslosung.Durchfuehren();
            spiele[runde] = turnier.RundeSimulieren(runde, anzahl);
        }

        var spielplan = turnier.Spielplan();
        var fortschritt = spielplan.Fortschritt;
        var baum = BaumLesen(spielplan);
        var statistik = turnier.Statistik();
        statistik.AuswertungWaehlen("Gesamt");
        var tabelle = statistik.Tabelle();
        statistik.AuswertungWaehlen("Turnier");
        var platzierungen = statistik.Platzierungen();

        AlleSpieleGespielt(fortschritt, spiele);
        string sieger = KoRundenFolgerichtig(spiele);
        BaumZeigtDieKoSpiele(baum, spiele, sieger);
        GesamttabelleStimmig(tabelle, spiele);
        PlatzierungenStimmig(platzierungen, spiele);
        TestContext.Current.TestOutputHelper?.WriteLine($"Turniersieger: {sieger}");
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    // ── Turnierbaum lesen ─────────────────────────────────────────────────────

    private sealed record BaumStand(
        IReadOnlyList<BaumKarte> Rundenweise, string SiegerRundenweise,
        IReadOnlyList<BaumKarte> Gesamt, string SiegerGesamt);

    /// <summary>Rundenweise: Halbfinale | Spiel um Platz 3, dann Spiel um Platz 3 | Finale mit dem Siegerfeld unter
    /// der Finalkarte. Danach die Gesamtübersicht mit allen K.-o.-Spielen und dem Siegerfeld über dem Finale.</summary>
    private static BaumStand BaumLesen(SpielplanReiter spielplan)
    {
        spielplan.RundeWaehlen(HALBFINALE);
        var baum = spielplan.Turnierbaum();
        baum.WarteAufFenster(HALBFINALE, SPIEL_UM_PLATZ_DREI);
        var rundenweise = baum.KartenNachAufbau(3).ToList();
        baum.NaechsteRunde();
        baum.WarteAufFenster(SPIEL_UM_PLATZ_DREI, FINALE);
        rundenweise.AddRange(baum.KartenNachAufbau(2));
        string siegerRundenweise = Wait.UntilFound(() => baum.Turniersieger is { Length: > 0 } s ? s : null,
            Wait.DefaultTimeout, "Siegerfeld unter der Finalkarte");

        baum.GesamtuebersichtZeigen();
        var gesamt = baum.KartenNachAufbau(SPIELE_GESAMT - SPIELE_GRUPPENPHASE);
        string siegerGesamt = Wait.UntilFound(() => baum.Turniersieger is { Length: > 0 } s ? s : null,
            Wait.DefaultTimeout, "Siegerfeld der Gesamtübersicht");
        baum.RundenweiseZeigen();
        return new BaumStand(rundenweise.Distinct().ToList(), siegerRundenweise, gesamt, siegerGesamt);
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

    /// <summary>Beide Ansichten des Baums zeigen genau die K.-o.-Spiele des Spielplans mit ihrem Stand, das
    /// Siegerfeld den Finalsieger.</summary>
    private static void BaumZeigtDieKoSpiele(
        BaumStand baum, Dictionary<string, IReadOnlyList<SpielZeile>> spiele, string sieger)
    {
        var koSpiele = KoRunden.SelectMany(r => spiele[r.Runde]).Select(s => (s.Heim, s.Ergebnis, s.Auswaerts)).ToList();
        baum.Rundenweise.Select(k => (k.Heim, k.Ergebnis, k.Auswaerts)).Should().BeEquivalentTo(koSpiele,
            "die rundenweise Ansicht zeigt die K.-o.-Spiele wie der Spielplan");
        baum.Gesamt.Select(k => (k.Heim, k.Ergebnis, k.Auswaerts)).Should().BeEquivalentTo(koSpiele,
            "die Gesamtübersicht zeigt die K.-o.-Spiele wie der Spielplan");
        (baum.SiegerRundenweise, baum.SiegerGesamt).Should().Be((sieger, sieger), "das Siegerfeld zeigt den Finalsieger");
    }

    /// <summary>Platzierungen der Statistik: Finalsieger, Finalverlierer, Sieger und Verlierer des Spiels um Platz 3.</summary>
    private static void PlatzierungenStimmig(
        IReadOnlyList<string> platzierungen, Dictionary<string, IReadOnlyList<SpielZeile>> spiele)
    {
        var finale = spiele[FINALE].Single();
        var platzDrei = spiele[SPIEL_UM_PLATZ_DREI].Single();
        platzierungen.Should().Equal(finale.Sieger, finale.Verlierer, platzDrei.Sieger, platzDrei.Verlierer);
    }

    /// <summary>Gesamttabelle: die 8 Teams, Halbfinalisten mit 5 Spielen, alle anderen mit 3.</summary>
    private static void GesamttabelleStimmig(
        IReadOnlyList<TabellenZeile> tabelle, Dictionary<string, IReadOnlyList<SpielZeile>> spiele)
    {
        tabelle.Should().HaveCount(TEAMS);
        var halbfinalisten = Teams(spiele[HALBFINALE]);
        TabellenPruefung.Stimmig(tabelle,
            Teams(spiele[GRUPPENPHASE]).ToDictionary(t => t, t => halbfinalisten.Contains(t) ? 5 : 3));
    }

    private static HashSet<string> Teams(IEnumerable<SpielZeile> spiele) =>
        [.. spiele.SelectMany(s => new[] { s.Heim, s.Auswaerts })];
}
