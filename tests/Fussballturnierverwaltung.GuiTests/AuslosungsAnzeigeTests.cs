using FluentAssertions;
using static Fussballturnierverwaltung.GuiTests.MiniTurnier;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Die beiden Ziehungsanzeigen (#522, offen aus dem Spike #520): die TV-Ziehung Schritt für Schritt und die
/// Ziehung von Hand über die KlickFlaechen. Beide müssen dieselben Gruppen ergeben, die danach im Spielplan stehen.</summary>
public sealed class AuslosungsAnzeigeTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    [Fact]
    public void TvZiehung_SchrittweiseBisZumEnde_ZeigtDieGruppenDesSpielplans()
    {
        var (turnier, auslosung) = GruppenauslosungVorbereiten(app.Hauptfenster);

        auslosung.MitTvModusManuellDurchfuehren();
        int schritte = auslosung.ZiehungBisZumEndeWeiterklicken();
        var ergebnis = auslosung.ZiehungsErgebnis();

        schritte.Should().BeGreaterThan(0, "die manuelle TV-Ziehung wartet auf „Weiter“");
        ergebnis.Should().HaveCount(2);
        ergebnis.Values.Should().OnlyContain(teams => teams.Count == TEAMS / 2);
        var gezogen = ergebnis.Values.SelectMany(t => t).ToList();
        gezogen.Should().OnlyHaveUniqueItems().And.HaveCount(TEAMS);
        GruppenphaseTeams(turnier).Should().BeEquivalentTo(gezogen);
    }

    [Fact]
    public void HandZiehung_PerKlickFlaeche_LostAlleTeamsAus()
    {
        var (turnier, auslosung) = GruppenauslosungVorbereiten(app.Hauptfenster, "Manuell");

        auslosung.HandZiehungStarten();
        auslosung.HandZiehungDurchspielen();
        auslosung.HandFortschritt.Should().Be($"{TEAMS} von {TEAMS} Teams gezogen");
        auslosung.HandZiehungAbschliessen();

        auslosung.Status.Should().Be(Seiten.AuslosungReiter.ABGESCHLOSSEN);
        GruppenphaseTeams(turnier).Should().HaveCount(TEAMS);
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private static List<string> GruppenphaseTeams(Seiten.TurnierdurchfuehrungSeite turnier)
    {
        var spielplan = turnier.Spielplan();
        spielplan.RundeWaehlen(GRUPPENPHASE);
        var spiele = spielplan.Spiele();
        spiele.Should().HaveCount(SPIELE_GRUPPENPHASE);
        return [.. spiele.SelectMany(s => new[] { s.Heim, s.Auswaerts }).Distinct()];
    }
}
