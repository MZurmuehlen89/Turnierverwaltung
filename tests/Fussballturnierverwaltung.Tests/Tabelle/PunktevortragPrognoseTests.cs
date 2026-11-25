using FluentAssertions;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Tabelle;

/// <summary>Issue #355 K5: Mögliche Ränge (Liga und Gruppe), sicher positioniert und der Stand nach Spieltag n rechnen mit
/// dem Punktevortrag. Zwei Teams, ein Spiel offen: Team 1 startet mit 20, Team 2 mit 10 Punkten – ohne Vortrag wäre alles offen.</summary>
public class PunktevortragPrognoseTests
{
    private static readonly PunkteRegel _dreiPunkte =
        new() { PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 };

    private static readonly Dictionary<int, Mannschaft> _teams = Enumerable.Range(1, 3)
        .ToDictionary(id => id, id => new Mannschaft { Id = id, Name = $"Team {id}" });

    private static readonly RankingRegel[] _regeln =
    [
        new() { Regelart = RankingRegelArt.Punkte, Sortierrichtung = Sortierrichtung.Absteigend },
        new() { Regelart = RankingRegelArt.Tordifferenz, Sortierrichtung = Sortierrichtung.Absteigend },
        new() { Regelart = RankingRegelArt.Losentscheid, Sortierrichtung = Sortierrichtung.Aufsteigend }
    ];

    private static readonly Dictionary<int, Vortragswert> _vortrag = new()
    {
        [1] = PunktevortragBerechnung.Berechnen(20, Punktevortrag.Voll),
        [2] = PunktevortragBerechnung.Berechnen(10, Punktevortrag.Voll)
    };

    private static int _naechsteSpielnummer;

    private static Spiel Spiel(int heimId, int auswaertsId, int spieltag, (int Heim, int Gast)? ergebnis = null) => new()
    {
        Spielnummer = Interlocked.Increment(ref _naechsteSpielnummer), Spieltag = spieltag,
        HeimMannschaftId = heimId, HeimMannschaft = _teams[heimId],
        AuswaertsMannschaftId = auswaertsId, AuswaertsMannschaft = _teams[auswaertsId],
        Status = ergebnis is null ? SpielStatus.Geplant : SpielStatus.Abgeschlossen,
        Ergebnis = ergebnis is { } e
            ? new SpielErgebnis { HeimToreRegulaer = e.Heim, AuswaertsToreRegulaer = e.Gast, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit }
            : null
    };

    private static TabellenRegeln Regeln(bool mitVortrag) => new(_dreiPunkte, _regeln, Vortraege: mitVortrag ? _vortrag : null);

    // ── Liga ──────────────────────────────────────────────────────────────

    [Fact]
    public void Liga_MitVortrag_RaengeStehenSchonFest()
    {
        var offen = Spiel(1, 2, 1);
        var tabelle = TabellenRechner.TabelleBerechnen([], Regeln(true), teilnehmer: [_teams[1], _teams[2]], spielplan: [offen]);

        RangPrognose.LigaRaengeSetzen(tabelle, new TurnierRunde { Rundenart = Rundenart.Liga }, [offen], _dreiPunkte);
        RangPrognose.SicherPositionierteMarkieren(tabelle);

        // Team 2 kann mit einem Sieg höchstens auf 13 kommen, Team 1 hat schon 20.
        tabelle.Single(e => e.Mannschaft.Id == 1).BesterRang.Should().Be(1);
        tabelle.Single(e => e.Mannschaft.Id == 1).SchlechtesterRang.Should().Be(1);
        tabelle.Should().OnlyContain(e => e.SicherPositioniert);
    }

    [Fact]
    public void Liga_OhneVortrag_RaengeBleibenOffen()
    {
        var offen = Spiel(1, 2, 1);
        var tabelle = TabellenRechner.TabelleBerechnen([], Regeln(false), teilnehmer: [_teams[1], _teams[2]], spielplan: [offen]);

        RangPrognose.LigaRaengeSetzen(tabelle, new TurnierRunde { Rundenart = Rundenart.Liga }, [offen], _dreiPunkte);
        RangPrognose.SicherPositionierteMarkieren(tabelle);

        tabelle.Should().OnlyContain(e => !e.SicherPositioniert);
    }

    // ── Gruppe ────────────────────────────────────────────────────────────

    [Fact]
    public void Gruppe_MitVortrag_RaengeStehenSchonFest()
    {
        var offen = Spiel(1, 2, 1);
        var gruppe = GruppeAus(offen, mitVortrag: true);

        RangPrognose.GruppenRaengeSetzen(gruppe, [offen], Regeln(true));

        var erster = gruppe.Tabelle.Single(e => e.Mannschaft.Id == 1);
        erster.BesterRang.Should().Be(1);
        erster.SchlechtesterRang.Should().Be(1);
        gruppe.Tabelle.Should().OnlyContain(e => e.SicherPositioniert);
    }

    [Fact]
    public void Gruppe_OhneVortrag_RaengeBleibenOffen()
    {
        var offen = Spiel(1, 2, 1);
        var gruppe = GruppeAus(offen, mitVortrag: false);

        RangPrognose.GruppenRaengeSetzen(gruppe, [offen], Regeln(false));

        gruppe.Tabelle.Should().OnlyContain(e => !e.SicherPositioniert);
    }

    private static GruppenTabelle GruppeAus(Spiel offen, bool mitVortrag) =>
        GruppenRechner.GruppenTabelleBerechnen(1, [offen], bisSpieltag: null, Regeln(mitVortrag));

    // ── Stand nach Spieltag ───────────────────────────────────────────────

    [Fact]
    public void StandNachSpieltag_ZaehltDenVortragVonAnfangAn()
    {
        // Team 2 startet mit 10 Punkten und verliert das Spiel am 1. Spieltag: 10, nicht 0.
        var spiele = new List<Spiel> { Spiel(1, 2, 1, (0, 2)) };

        var vorher = TabellenRechner.TabelleBerechnen(
            TabellenRechner.BisSpieltag(spiele, 1), Regeln(true), teilnehmer: [_teams[1], _teams[2]], spielplan: spiele);

        vorher.Single(e => e.Mannschaft.Id == 2).Punkte.Should().Be(13);
        vorher.Single(e => e.Mannschaft.Id == 1).Punkte.Should().Be(20);
        vorher.OrderBy(e => e.Rang).Select(e => e.Mannschaft.Id).Should().Equal(1, 2);
    }

    [Fact]
    public void Verlauf_StartetMitDemVortrag()
    {
        var spiele = new List<Spiel> { Spiel(1, 2, 1, (0, 2)), Spiel(1, 2, 2, (0, 2)) };

        var verlauf = TabellenVerlaufRechner.Berechnen(gruppenNummer: null, spiele, Regeln(true));

        var team2 = verlauf.Mannschaften.Single(m => m.Mannschaft.Id == 2).Punkte;
        team2.Select(p => p.Punkte).Should().Equal(13, 16);
        team2.Select(p => p.Platz).Should().Equal(2, 2);
        verlauf.Mannschaften.Single(m => m.Mannschaft.Id == 1).Punkte.Select(p => p.Punkte).Should().Equal(20, 20);
    }
}
