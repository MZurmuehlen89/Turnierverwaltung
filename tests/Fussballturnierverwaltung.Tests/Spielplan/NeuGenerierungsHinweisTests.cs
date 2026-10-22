using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Tests.Spielplan;

public class NeuGenerierungsHinweisTests
{
    private static TurnierRunde Runde(int eingang, bool festVorgegeben = false) =>
        new() { EingangsMannschaftsanzahl = eingang, PaarungFestVorgegeben = festVorgegeben };

    // #433: fest vorgegebene Paarungen entstehen in der Vorbelegung, nicht in der Auslosung — auch bei zwei Teams.
    [Theory]
    [InlineData(2)]
    [InlineData(16)]
    public void FehlendeAuslosung_FestVorgegeben_VerweistAufVorbelegung(int eingang) =>
        NeuGenerierungsHinweis.FehlendeAuslosung(Runde(eingang, festVorgegeben: true), hatSpiele: true)
            .Should().Contain("Reiter „Vorbelegung“");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FehlendeAuslosung_MehrAlsZweiTeams_VerweistAufAuslosung(bool hatSpiele) =>
        NeuGenerierungsHinweis.FehlendeAuslosung(Runde(16), hatSpiele)
            .Should().Contain("Bitte zuerst die Auslosung durchführen.");

    // #113: eine Runde mit zwei Teilnehmern bekommt ihr Spiel ohne Auslosung.
    [Fact]
    public void FehlendeAuslosung_ZweiTeamsMitSpiel_NeugenerierenNichtNoetig() =>
        NeuGenerierungsHinweis.FehlendeAuslosung(Runde(2), hatSpiele: true)
            .Should().Contain("automatisch aus der vorherigen Runde angelegt").And.NotContain("Auslosung durchführen");

    [Fact]
    public void FehlendeAuslosung_ZweiTeamsOhneSpiel_SpielEntstehtAutomatisch() =>
        NeuGenerierungsHinweis.FehlendeAuslosung(Runde(2), hatSpiele: false)
            .Should().Contain("braucht keine Auslosung").And.Contain("sobald beide Teilnehmer");
}
