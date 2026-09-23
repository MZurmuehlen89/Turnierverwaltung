using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Spielplan;

public class KoRundeGeneratorTests
{
    private readonly KoRundeGenerator _generator = new();

    private static TurnierRunde TestRunde(Rundenart art = Rundenart.KoRunde, bool mitRueckspiel = false) =>
        new() { Id = 2, TurnierInstanzId = 1, Name = "Achtelfinale", Rundenart = art, RegelwerkId = 1, MitRueckspiel = mitRueckspiel };

    private static IReadOnlyList<Mannschaft> Teams(int n) =>
        Enumerable.Range(1, n).Select(i => new Mannschaft { Id = i, Name = $"Team {i}" }).ToList();

    [Theory]
    [InlineData(2,  1)]
    [InlineData(4,  2)]
    [InlineData(8,  4)]
    [InlineData(16, 8)]
    public void AnzahlSpiele_IstHalbTeamAnzahl(int teams, int erwarteteSpiele)
    {
        var kontext = new SpielplanKontext(TestRunde(), Teams(teams), 1);
        var ergebnis = _generator.Generieren(kontext);
        ergebnis.Spiele.Should().HaveCount(erwarteteSpiele);
    }

    [Fact]
    public void JedesTeam_SpieltGenauEinmal()
    {
        var kontext = new SpielplanKontext(TestRunde(), Teams(8), 1);
        var ergebnis = _generator.Generieren(kontext);

        for (int i = 1; i <= 8; i++)
        {
            int teamId = i;
            ergebnis.Spiele
                .Count(s => s.HeimMannschaftId == teamId || s.AuswaertsMannschaftId == teamId)
                .Should().Be(1, $"Team {i} soll genau einmal spielen.");
        }
    }

    [Fact]
    public void Paarungen_ErfolgenSequentiell()
    {
        var kontext = new SpielplanKontext(TestRunde(), Teams(4), 1);
        var ergebnis = _generator.Generieren(kontext);

        ergebnis.Spiele[0].HeimMannschaftId.Should().Be(1);
        ergebnis.Spiele[0].AuswaertsMannschaftId.Should().Be(2);
        ergebnis.Spiele[1].HeimMannschaftId.Should().Be(3);
        ergebnis.Spiele[1].AuswaertsMannschaftId.Should().Be(4);
    }

    [Fact]
    public void UngeeradeTeamAnzahl_WirftException()
    {
        var kontext = new SpielplanKontext(TestRunde(), Teams(5), 1);
        var act = () => _generator.Generieren(kontext);
        act.Should().Throw<InvalidOperationException>().WithMessage("*gerade*");
    }

    // --- Hin-/Rückspiel-Tests ---

    [Fact]
    public void HinRueckspiel_GeneriertDoppelteAnzahl()
    {
        var runde = TestRunde(mitRueckspiel: true);
        var kontext = new SpielplanKontext(runde, Teams(8), 1);
        var ergebnis = _generator.Generieren(kontext);

        ergebnis.Spiele.Should().HaveCount(8); // 4 Paare × 2
        ergebnis.HatHinUndRueckspiele.Should().BeTrue();
        ergebnis.HinRueckspielPaare.Should().HaveCount(4);
    }

    [Fact]
    public void HinRueckspiel_HeimAuswaertsGetauscht()
    {
        var runde = TestRunde(mitRueckspiel: true);
        var kontext = new SpielplanKontext(runde, Teams(2), 1);
        var ergebnis = _generator.Generieren(kontext);

        var (hinIdx, rueckIdx) = ergebnis.HinRueckspielPaare[0];
        var hinspiel   = ergebnis.Spiele[hinIdx];
        var rueckspiel = ergebnis.Spiele[rueckIdx];

        hinspiel.HeimMannschaftId.Should().Be(rueckspiel.AuswaertsMannschaftId);
        hinspiel.AuswaertsMannschaftId.Should().Be(rueckspiel.HeimMannschaftId);
        hinspiel.IstRueckspiel.Should().BeFalse();
        rueckspiel.IstRueckspiel.Should().BeTrue();
    }

    [Fact]
    public void HinRueckspiel_ErstAlleHinspieleDannAlleRueckspiele()
    {
        // Issue #346: Nummerierung in der Reihenfolge, in der gespielt wird — nicht Hin/Rück abwechselnd.
        var runde = TestRunde(mitRueckspiel: true);
        var ergebnis = _generator.Generieren(new SpielplanKontext(runde, Teams(8), 1));

        ergebnis.Spiele.Select(s => s.Spielnummer).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);
        ergebnis.Spiele.Select(s => s.IstRueckspiel).Should().Equal(false, false, false, false, true, true, true, true);
        ergebnis.HinRueckspielPaare.Should().Equal((0, 4), (1, 5), (2, 6), (3, 7));
    }

    [Fact]
    public void HinRueckspiel_SpieltageKorrekt()
    {
        var runde = TestRunde(mitRueckspiel: true);
        var kontext = new SpielplanKontext(runde, Teams(4), 1);
        var ergebnis = _generator.Generieren(kontext);

        ergebnis.Spiele.Where(s => !s.IstRueckspiel).Should().AllSatisfy(s => s.Spieltag.Should().Be(1));
        ergebnis.Spiele.Where(s =>  s.IstRueckspiel).Should().AllSatisfy(s => s.Spieltag.Should().Be(2));
    }
}
