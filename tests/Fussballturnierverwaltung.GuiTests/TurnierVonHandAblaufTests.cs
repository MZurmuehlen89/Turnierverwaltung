using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;
using static Fussballturnierverwaltung.GuiTests.HandTurnier;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Ein von Hand aufgebautes Turnier lässt sich auslosen und durchspielen (#531). Eigene Klasse und damit
/// eigene App-Sitzung, siehe <see cref="HandTurnier"/>. Geprüft werden Invarianten, keine festen Werte.</summary>
public sealed class TurnierVonHandAblaufTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    /// <summary>Gruppenphase auslosen und simulieren, das vorbelegte Halbfinale füllt sich mit den Gruppenersten und
    /// -zweiten über Kreuz, das Finale mit den Halbfinalsiegern.</summary>
    [Fact]
    public void VonHandAufgebaut_LaesstSichAuslosenUndDurchspielen()
    {
        string name = EindeutigerName();
        Aufbauen(app.Hauptfenster, name);

        var turnier = app.Hauptfenster.TurnierdurchfuehrungOeffnen();
        turnier.GewaehltesTurnier.Should().Be(name);
        var auslosung = turnier.Auslosung();
        auslosung.Anlegen(GRUPPENPHASE);
        auslosung.RanglisteWaehlen(MiniTurnier.RANGLISTE);
        auslosung.ToepfeAutomatischBefuellen();
        auslosung.Durchfuehren();

        var gruppenspiele = RundeSimulieren(turnier, GRUPPENPHASE, SPIELE_GRUPPENPHASE);
        var halbfinale = RundeSimulieren(turnier, HALBFINALE, 2);
        var finale = RundeSimulieren(turnier, FINALE, 1);

        turnier.Spielplan().Fortschritt.Should().Be((SPIELE_GESAMT, SPIELE_GESAMT));
        gruppenspiele.Should().HaveCount(SPIELE_GRUPPENPHASE);
        var gruppen = Gruppen(gruppenspiele);
        gruppen.Should().HaveCount(2).And.OnlyContain(g => g.Count == 4);

        halbfinale.Should().HaveCount(2).And.OnlyContain(s => s.Sieger != null);
        foreach (var spiel in halbfinale)
            GruppeVon(gruppen, spiel.Heim).Should().NotBeSameAs(GruppeVon(gruppen, spiel.Auswaerts),
                "das Halbfinale paart über Kreuz: {0} – {1}", spiel.Heim, spiel.Auswaerts);
        foreach (var gruppe in gruppen)
            Teams(halbfinale).Count(gruppe.Contains).Should().Be(2, "je Gruppe kommen zwei Teams weiter");

        finale.Should().ContainSingle().Which.Sieger.Should().NotBeNull();
        Teams(finale).Should().BeEquivalentTo(halbfinale.Select(s => s.Sieger!));
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private static IReadOnlyList<SpielZeile> RundeSimulieren(TurnierdurchfuehrungSeite turnier, string runde, int anzahl)
    {
        var spielplan = turnier.Spielplan();
        spielplan.RundeWaehlen(runde);
        spielplan.Spiele().Should().HaveCount(anzahl, $"{runde} hat vor der Simulation {anzahl} Spiel(e)");
        spielplan.RundeSimulieren(anzahl);
        return spielplan.Spiele();
    }

    /// <summary>Die Gruppen aus den Gruppenspielen: Wer gegeneinander spielt, ist in derselben Gruppe.</summary>
    private static List<HashSet<string>> Gruppen(IEnumerable<SpielZeile> spiele)
    {
        var gruppen = new List<HashSet<string>>();
        foreach (var spiel in spiele)
        {
            var treffer = gruppen.Where(g => g.Contains(spiel.Heim) || g.Contains(spiel.Auswaerts)).ToList();
            var gruppe = new HashSet<string>(treffer.SelectMany(g => g)) { spiel.Heim, spiel.Auswaerts };
            gruppen.RemoveAll(treffer.Contains);
            gruppen.Add(gruppe);
        }
        return gruppen;
    }

    private static HashSet<string> GruppeVon(List<HashSet<string>> gruppen, string team) =>
        gruppen.Single(g => g.Contains(team));

    private static HashSet<string> Teams(IEnumerable<SpielZeile> spiele) =>
        [.. spiele.SelectMany(s => new[] { s.Heim, s.Auswaerts })];
}
