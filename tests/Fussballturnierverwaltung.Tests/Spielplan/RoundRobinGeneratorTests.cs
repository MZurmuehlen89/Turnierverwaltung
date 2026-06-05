using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Spielplan;

public class RoundRobinGeneratorTests
{
    private readonly RoundRobinGenerator _generator = new();

    private static TurnierRunde TestRunde(int turnierInstanzId = 1) =>
        new() { Id = 1, TurnierInstanzId = turnierInstanzId, Name = "Liga", Rundenart = Rundenart.Liga, RegelwerkId = 1 };

    private static IReadOnlyList<Mannschaft> Teams(int anzahl) =>
        Enumerable.Range(1, anzahl)
            .Select(i => new Mannschaft { Id = i, Name = $"Team {i}" })
            .ToList();

    // n Teams → n*(n-1)/2 Spiele
    [Theory]
    [InlineData(4, 6)]
    [InlineData(6, 15)]
    [InlineData(8, 28)]
    [InlineData(3, 3)]  // ungerade
    [InlineData(5, 10)] // ungerade
    public void AnzahlSpiele_IsKorrekt(int teamAnzahl, int erwarteteSpiele)
    {
        var kontext = new SpielplanKontext(TestRunde(), Teams(teamAnzahl), 1);
        var ergebnis = _generator.Generieren(kontext);
        ergebnis.Spiele.Should().HaveCount(erwarteteSpiele);
    }

    [Fact]
    public void JedesTeamSpielt_GegenJedesAndere_GenauEinmal()
    {
        int n = 6;
        var teams = Teams(n);
        var kontext = new SpielplanKontext(TestRunde(), teams, 1);
        var ergebnis = _generator.Generieren(kontext);

        // Für jedes Paar (i,j) prüfen, dass genau ein Spiel existiert
        for (int i = 1; i <= n; i++)
        {
            for (int j = i + 1; j <= n; j++)
            {
                int ti = i, tj = j;
                ergebnis.Spiele
                    .Where(s => (s.HeimMannschaftId == ti && s.AuswaertsMannschaftId == tj) ||
                                (s.HeimMannschaftId == tj && s.AuswaertsMannschaftId == ti))
                    .Should().HaveCount(1, $"Team {i} und Team {j} sollen genau einmal gegeneinander spielen.");
            }
        }
    }

    [Fact]
    public void JedesTeam_KommmtGleichHaeufigVor()
    {
        int n = 4;
        var teams = Teams(n);
        var kontext = new SpielplanKontext(TestRunde(), teams, 1);
        var ergebnis = _generator.Generieren(kontext);

        foreach (var team in teams)
        {
            int auftritte = ergebnis.Spiele
                .Count(s => s.HeimMannschaftId == team.Id || s.AuswaertsMannschaftId == team.Id);
            auftritte.Should().Be(n - 1, $"Team {team.Id} soll {n - 1} Spiele haben.");
        }
    }

    [Fact]
    public void Spieltage_WerdenKorrektNummeriert()
    {
        var kontext = new SpielplanKontext(TestRunde(), Teams(4), 1);
        var ergebnis = _generator.Generieren(kontext);

        var spieltage = ergebnis.Spiele.Select(s => s.Spieltag!.Value).Distinct().OrderBy(x => x).ToList();
        spieltage.Should().BeEquivalentTo([1, 2, 3]); // 4 Teams → 3 Spieltage
    }

    [Fact]
    public void StartSpielNummer_WirdKorrektAngewendet()
    {
        var kontext = new SpielplanKontext(TestRunde(), Teams(4), StartSpielNummer: 10);
        var ergebnis = _generator.Generieren(kontext);

        ergebnis.Spiele.First().Spielnummer.Should().Be(10);
        ergebnis.Spiele.Should().BeInAscendingOrder(s => s.Spielnummer);
    }

    [Fact]
    public void KeinHinRueckspielPaare_BeiLiga()
    {
        var kontext = new SpielplanKontext(TestRunde(), Teams(4), 1);
        var ergebnis = _generator.Generieren(kontext);
        ergebnis.HatHinUndRueckspiele.Should().BeFalse();
    }

    [Fact]
    public void ZweiTeams_GenerierenEinSpiel()
    {
        var kontext = new SpielplanKontext(TestRunde(), Teams(2), 1);
        var ergebnis = _generator.Generieren(kontext);
        ergebnis.Spiele.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(3)]
    [InlineData(5)]
    public void JedesTeam_SpieltProSpieltag_HoechstensEinmal(int teamAnzahl)
    {
        var kontext = new SpielplanKontext(TestRunde(), Teams(teamAnzahl), 1);
        var ergebnis = _generator.Generieren(kontext);

        foreach (var spieltag in ergebnis.Spiele.Select(s => s.Spieltag).Distinct())
        {
            var spieleAmTag = ergebnis.Spiele.Where(s => s.Spieltag == spieltag).ToList();
            var alleTeams   = spieleAmTag
                .SelectMany(s => new[] { s.HeimMannschaftId, s.AuswaertsMannschaftId })
                .ToList();

            alleTeams.Should().OnlyHaveUniqueItems(
                $"Spieltag {spieltag}: jedes Team darf nur einmal spielen (n={teamAnzahl})");
        }
    }
}
