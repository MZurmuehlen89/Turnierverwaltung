using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Spielplan;

public class SchweizerSystemGeneratorTests
{
    private readonly SchweizerSystemGenerator _generator = new();

    private static TurnierRunde TestRunde() =>
        new() { Id = 1, TurnierInstanzId = 1, Name = "Schweizer System", Rundenart = Rundenart.SchweizerSystem, RegelwerkId = 1 };

    private static IReadOnlyList<Mannschaft> Teams(int anzahl) =>
        Enumerable.Range(1, anzahl)
            .Select(i => new Mannschaft { Id = i, Name = $"Team {i}" })
            .ToList();

    private static SpielplanKontext Kontext(
        IReadOnlyList<Mannschaft> teams,
        IReadOnlyList<(int, int)>? bereitsGespielt = null,
        int spieltag = 1) =>
        new(TestRunde(), teams, 1, Paarungen: null, BereitsGespielt: bereitsGespielt, Spieltag: spieltag);

    // ── Grundinvarianten ──────────────────────────────────────────────────

    [Theory]
    [InlineData(4, 2)]
    [InlineData(6, 3)]
    [InlineData(8, 4)]
    [InlineData(2, 1)]
    public void AnzahlSpiele_IstHalbTeamanzahl(int teamAnzahl, int erwarteteSpiele)
    {
        var ergebnis = _generator.Generieren(Kontext(Teams(teamAnzahl)));
        ergebnis.Spiele.Should().HaveCount(erwarteteSpiele);
    }

    [Theory]
    [InlineData(3, 1)]   // 3 → 2 aktiv + 1 Freilos → 1 Spiel
    [InlineData(5, 2)]   // 5 → 4 aktiv + 1 Freilos → 2 Spiele
    [InlineData(7, 3)]
    public void UngeeradeTeamAnzahl_EinTeamSitztAus(int teamAnzahl, int erwarteteSpiele)
    {
        var ergebnis = _generator.Generieren(Kontext(Teams(teamAnzahl)));
        ergebnis.Spiele.Should().HaveCount(erwarteteSpiele);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(5)]
    public void JedesTeam_SpieltHoechstensEinmalProRunde(int teamAnzahl)
    {
        var ergebnis = _generator.Generieren(Kontext(Teams(teamAnzahl)));

        var alleTeams = ergebnis.Spiele
            .SelectMany(s => new[] { s.HeimMannschaftId, s.AuswaertsMannschaftId })
            .ToList();

        alleTeams.Should().OnlyHaveUniqueItems("kein Team darf in einer Runde zweimal spielen");
    }

    // ── Rematch-Vermeidung ────────────────────────────────────────────────

    [Fact]
    public void MitBereitsGespieltenPaaren_KeineWiederholung()
    {
        // Runde 1: 1vs2, 3vs4 → Runde 2 muss 1vs3/4 und 2vs3/4 paaren
        var bereitsGespielt = new List<(int, int)> { (1, 2), (3, 4) };
        var kontext = Kontext(Teams(4), bereitsGespielt, spieltag: 2);

        var ergebnis = _generator.Generieren(kontext);

        ergebnis.Spiele.Should().HaveCount(2);
        ergebnis.Spiele.Should().NotContain(s =>
            (s.HeimMannschaftId == 1 && s.AuswaertsMannschaftId == 2) ||
            (s.HeimMannschaftId == 2 && s.AuswaertsMannschaftId == 1),
            "Rematch 1vs2 darf nicht wiederholt werden");
        ergebnis.Spiele.Should().NotContain(s =>
            (s.HeimMannschaftId == 3 && s.AuswaertsMannschaftId == 4) ||
            (s.HeimMannschaftId == 4 && s.AuswaertsMannschaftId == 3),
            "Rematch 3vs4 darf nicht wiederholt werden");
    }

    [Fact]
    public void VollesTurnier_VierTeams_DreiRunden_KeineWiederholung()
    {
        // 4 Teams spielen 3 Runden (Vollständiges Rundensystem)
        var teams = Teams(4);
        var gespielt = new List<(int, int)>();

        for (int runde = 1; runde <= 3; runde++)
        {
            var kontext = Kontext(teams, gespielt, spieltag: runde);
            var ergebnis = _generator.Generieren(kontext);

            ergebnis.Spiele.Should().HaveCount(2, $"Runde {runde}: 4 Teams → 2 Spiele");

            foreach (var s in ergebnis.Spiele)
            {
                var key = (Math.Min(s.HeimMannschaftId, s.AuswaertsMannschaftId),
                           Math.Max(s.HeimMannschaftId, s.AuswaertsMannschaftId));
                gespielt.Should().NotContain(key, $"Runde {runde}: Rematch entdeckt");
                gespielt.Add(key);
            }
        }

        // Nach 3 Runden sollte jedes Paar genau einmal gespielt haben
        gespielt.Should().HaveCount(6, "4 Teams × 3 Runden = alle 6 möglichen Paarungen");
    }

    [Fact]
    public void AlleGegnerBereitsGespielt_FallbackAufWiederholung()
    {
        // 2 Teams haben schon gespielt — Fallback muss trotzdem ein Spiel erzeugen
        var bereitsGespielt = new List<(int, int)> { (1, 2) };
        var kontext = Kontext(Teams(2), bereitsGespielt, spieltag: 2);

        var ergebnis = _generator.Generieren(kontext);

        ergebnis.Spiele.Should().HaveCount(1, "Fallback auf Wiederholung wenn keine freie Paarung möglich");
    }

    // ── Metadaten ─────────────────────────────────────────────────────────

    [Fact]
    public void SpieltageWerdenKorrektGesetzt()
    {
        var kontext = Kontext(Teams(4), spieltag: 5);
        var ergebnis = _generator.Generieren(kontext);

        ergebnis.Spiele.Should().AllSatisfy(s => s.Spieltag.Should().Be(5));
    }

    [Fact]
    public void StartSpielNummer_WirdKorrektAngewendet()
    {
        var kontext = new SpielplanKontext(TestRunde(), Teams(4), StartSpielNummer: 10, Spieltag: 1);
        var ergebnis = _generator.Generieren(kontext);

        ergebnis.Spiele.First().Spielnummer.Should().Be(10);
        ergebnis.Spiele.Should().BeInAscendingOrder(s => s.Spielnummer);
    }

    [Fact]
    public void KeinHinRueckspielPaare()
    {
        var ergebnis = _generator.Generieren(Kontext(Teams(4)));
        ergebnis.HatHinUndRueckspiele.Should().BeFalse();
    }

    [Fact]
    public void UnterstützteRundenarten_EnthaeltSchweizerSystem()
    {
        _generator.UnterstützteRundenarten.Should().Contain(Rundenart.SchweizerSystem);
    }

    [Fact]
    public void WenigerAlsZweiTeams_WirftException()
    {
        var kontext = Kontext(Teams(1));
        var act = () => _generator.Generieren(kontext);
        act.Should().Throw<InvalidOperationException>();
    }
}
