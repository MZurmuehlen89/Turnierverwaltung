using FluentAssertions;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Tabelle;

/// <summary>Issue #355 K3, K4, K8: Der Punktevortrag geht als Startwert je Team in die Tabelle ein, bei Punktgleichheit
/// entscheidet vor allen Kriterien des Regelsets der ungerundete Wert, und <see cref="RankingRegelArt.PlatzVorrunde"/>
/// ordnet nach dem Platz in der Quellrunde.</summary>
public class PunktevortragTabelleTests
{
    private static readonly PunkteRegel _dreiPunkte =
        new() { PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 };

    private static readonly Dictionary<int, Mannschaft> _teams = Enumerable.Range(1, 6)
        .ToDictionary(id => id, id => new Mannschaft { Id = id, Name = $"Team {id}" });

    private static int _naechsteSpielnummer;

    private static Spiel Spiel(int heimId, int auswaertsId, int heimTore, int auswaertsTore) => new()
    {
        Spielnummer = Interlocked.Increment(ref _naechsteSpielnummer),
        HeimMannschaftId = heimId, HeimMannschaft = _teams[heimId],
        AuswaertsMannschaftId = auswaertsId, AuswaertsMannschaft = _teams[auswaertsId],
        Status = SpielStatus.Abgeschlossen,
        Ergebnis = new SpielErgebnis
        {
            HeimToreRegulaer = heimTore, AuswaertsToreRegulaer = auswaertsTore,
            EntscheidungArt = EntscheidungArt.RegulaereSpielzeit
        }
    };

    private static RankingRegel[] Regeln(params RankingRegelArt[] arten) =>
        [.. arten.Select(art => new RankingRegel
        {
            Regelart = art,
            Sortierrichtung = art is RankingRegelArt.Losentscheid or RankingRegelArt.PlatzVorrunde
                ? Sortierrichtung.Aufsteigend
                : Sortierrichtung.Absteigend
        })];

    private static TabellenRegeln MitVortrag(
        RankingRegelArt[] arten, Dictionary<int, Vortragswert> vortraege, Dictionary<int, int>? plaetze = null) =>
        new(_dreiPunkte, Regeln(arten), Vortraege: vortraege, VorrundenPlaetze: plaetze);

    private static readonly RankingRegelArt[] _punkteTordifferenzLos =
        [RankingRegelArt.Punkte, RankingRegelArt.Tordifferenz, RankingRegelArt.Losentscheid];

    private static int[] Reihenfolge(IReadOnlyList<TabellenEintrag> tabelle) =>
        [.. tabelle.OrderBy(e => e.Rang).Select(e => e.Mannschaft.Id)];

    // ── K3: Startwert ─────────────────────────────────────────────────────

    [Fact]
    public void Tabelle_VorDemErstenSpiel_ZeigtNurDenVortrag()
    {
        var regeln = MitVortrag(_punkteTordifferenzLos, new()
        {
            [1] = PunktevortragBerechnung.Berechnen(31, Punktevortrag.HalbiertAbgerundet),
            [2] = PunktevortragBerechnung.Berechnen(24, Punktevortrag.HalbiertAbgerundet)
        });

        var tabelle = TabellenRechner.TabelleBerechnen([], regeln, teilnehmer: [_teams[1], _teams[2]]);

        var erster = tabelle.Single(e => e.Mannschaft.Id == 1);
        erster.Punkte.Should().Be(15);
        erster.Spiele.Should().Be(0);
        erster.Siege.Should().Be(0);
        erster.Tore.Should().Be(0);
        tabelle.Single(e => e.Mannschaft.Id == 2).Punkte.Should().Be(12);
        Reihenfolge(tabelle).Should().Equal(1, 2);
    }

    [Fact]
    public void Tabelle_NachSpielen_ZaehltVortragPlusPunkteDerRunde_SpieleUndToreNurDieDerRunde()
    {
        var regeln = MitVortrag(_punkteTordifferenzLos, new()
        {
            [1] = PunktevortragBerechnung.Berechnen(30, Punktevortrag.HalbiertAbgerundet),
            [2] = PunktevortragBerechnung.Berechnen(30, Punktevortrag.HalbiertAbgerundet)
        });

        var tabelle = TabellenRechner.TabelleBerechnen([Spiel(2, 1, 2, 0)], regeln);

        var sieger = tabelle.Single(e => e.Mannschaft.Id == 2);
        sieger.Punkte.Should().Be(18);
        sieger.Spiele.Should().Be(1);
        sieger.Tore.Should().Be(2);
        var verlierer = tabelle.Single(e => e.Mannschaft.Id == 1);
        verlierer.Punkte.Should().Be(15);
        verlierer.Niederlagen.Should().Be(1);
    }

    [Fact]
    public void Tabelle_TeamOhneVortrag_StartetBeiNull()
    {
        var regeln = MitVortrag(_punkteTordifferenzLos, new()
        {
            [1] = PunktevortragBerechnung.Berechnen(30, Punktevortrag.Voll)
        });

        var tabelle = TabellenRechner.TabelleBerechnen([], regeln, teilnehmer: [_teams[1], _teams[2]]);

        tabelle.Single(e => e.Mannschaft.Id == 2).Punkte.Should().Be(0);
    }

    // ── K4: Rundung bei Punktgleichheit ───────────────────────────────────

    // Beide Teams gewinnen ihr erstes Spiel; Team 1 hat dabei die bessere Tordifferenz.
    private static IReadOnlyList<Spiel> ZweiSiege() => [Spiel(1, 3, 5, 0), Spiel(2, 4, 1, 0)];

    [Fact]
    public void Tabelle_Abgerundet_GleichePunkte_VorTordifferenzDerAbgerundeteVorn()
    {
        // Team 1: 30 → 15 (ungerundet 15), Team 2: 31 → 15 (ungerundet 15,5, abgerundet).
        var regeln = MitVortrag(_punkteTordifferenzLos, new()
        {
            [1] = PunktevortragBerechnung.Berechnen(30, Punktevortrag.HalbiertAbgerundet),
            [2] = PunktevortragBerechnung.Berechnen(31, Punktevortrag.HalbiertAbgerundet)
        });

        var tabelle = TabellenRechner.TabelleBerechnen(ZweiSiege(), regeln);

        tabelle.Single(e => e.Mannschaft.Id == 1).Punkte.Should().Be(18);
        tabelle.Single(e => e.Mannschaft.Id == 2).Punkte.Should().Be(18);
        Reihenfolge(tabelle).Take(2).Should().Equal(2, 1);
    }

    [Fact]
    public void Tabelle_Aufgerundet_GleichePunkte_VorTordifferenzDerAufgerundeteHinten()
    {
        // Team 1: 31 → 16 (ungerundet 15,5, aufgerundet), Team 2: 32 → 16 (ungerundet 16).
        var regeln = MitVortrag(_punkteTordifferenzLos, new()
        {
            [1] = PunktevortragBerechnung.Berechnen(31, Punktevortrag.HalbiertAufgerundet),
            [2] = PunktevortragBerechnung.Berechnen(32, Punktevortrag.HalbiertAufgerundet)
        });

        var tabelle = TabellenRechner.TabelleBerechnen(ZweiSiege(), regeln);

        Reihenfolge(tabelle).Take(2).Should().Equal(2, 1);
    }

    [Fact]
    public void Tabelle_BeideGerundet_EntscheidetDasRegelset()
    {
        var regeln = MitVortrag(_punkteTordifferenzLos, new()
        {
            [1] = PunktevortragBerechnung.Berechnen(31, Punktevortrag.HalbiertAbgerundet),
            [2] = PunktevortragBerechnung.Berechnen(31, Punktevortrag.HalbiertAbgerundet)
        });

        var tabelle = TabellenRechner.TabelleBerechnen(ZweiSiege(), regeln);

        Reihenfolge(tabelle).Take(2).Should().Equal(1, 2);
    }

    [Fact]
    public void Tabelle_BeideAbgerundetMitVerschiedenemVortrag_EntscheidetDasRegelset()
    {
        // Team 1: 31 → 15 (15,5), Team 2: 33 → 16 (16,5), beide abgerundet. Team 1 holt ein Remis und liegt mit 16 Punkten
        // gleichauf. Die Höhe des Vortrags zählt nicht, nur die Rundung: Los (Team 1 vor Team 2), nicht Team 2 wegen 16,5.
        var regeln = MitVortrag(_punkteTordifferenzLos, new()
        {
            [1] = PunktevortragBerechnung.Berechnen(31, Punktevortrag.HalbiertAbgerundet),
            [2] = PunktevortragBerechnung.Berechnen(33, Punktevortrag.HalbiertAbgerundet)
        });

        var tabelle = TabellenRechner.TabelleBerechnen([Spiel(1, 3, 0, 0)], regeln, teilnehmer: [_teams[2]]);

        tabelle.Single(e => e.Mannschaft.Id == 1).Punkte.Should().Be(16);
        tabelle.Single(e => e.Mannschaft.Id == 2).Punkte.Should().Be(16);
        tabelle.Single(e => e.Mannschaft.Id == 1).Rang.Should().BeLessThan(tabelle.Single(e => e.Mannschaft.Id == 2).Rang);
    }

    [Fact]
    public void Tabelle_BeideAufgerundetMitVerschiedenemVortrag_EntscheidetDasRegelset()
    {
        // Team 1: 29 → 15 (14,5), Team 2: 31 → 16 (15,5), beide aufgerundet; Team 1 liegt nach einem Remis gleichauf.
        var regeln = MitVortrag(_punkteTordifferenzLos, new()
        {
            [1] = PunktevortragBerechnung.Berechnen(29, Punktevortrag.HalbiertAufgerundet),
            [2] = PunktevortragBerechnung.Berechnen(31, Punktevortrag.HalbiertAufgerundet)
        });

        var tabelle = TabellenRechner.TabelleBerechnen([Spiel(1, 3, 0, 0)], regeln, teilnehmer: [_teams[2]]);

        tabelle.Single(e => e.Mannschaft.Id == 1).Punkte.Should().Be(16);
        tabelle.Single(e => e.Mannschaft.Id == 2).Punkte.Should().Be(16);
        tabelle.Single(e => e.Mannschaft.Id == 1).Rang.Should().BeLessThan(tabelle.Single(e => e.Mannschaft.Id == 2).Rang);
    }

    [Fact]
    public void Tabelle_OhneRankingRegeln_BeideGerundetMitVerschiedenemVortrag_EntscheidetDieIdStattDerVortragshoehe()
    {
        var regeln = new TabellenRegeln(_dreiPunkte, [], Vortraege: new Dictionary<int, Vortragswert>
        {
            [1] = PunktevortragBerechnung.Berechnen(31, Punktevortrag.HalbiertAbgerundet),
            [2] = PunktevortragBerechnung.Berechnen(33, Punktevortrag.HalbiertAbgerundet)
        });

        var tabelle = TabellenRechner.TabelleBerechnen([Spiel(1, 3, 0, 0)], regeln, teilnehmer: [_teams[2]]);

        tabelle.Single(e => e.Mannschaft.Id == 1).Rang.Should().BeLessThan(tabelle.Single(e => e.Mannschaft.Id == 2).Rang);
    }

    [Theory]
    [InlineData(TabellenAnsicht.Gesamt, 13)]
    [InlineData(TabellenAnsicht.Heimtabelle, 3)]
    [InlineData(TabellenAnsicht.Hinrunde, 3)]
    public void Ansicht_VortragZaehltNurImGesamtstand(TabellenAnsicht ansicht, int erwartetePunkte)
    {
        var regeln = MitVortrag(_punkteTordifferenzLos, new() { [1] = PunktevortragBerechnung.Berechnen(10, Punktevortrag.Voll) });

        var tabelle = TabellenRechner.TabelleDerAnsichtBerechnen([Spiel(1, 3, 2, 0)], ansicht, regeln);

        tabelle.Single(e => e.Mannschaft.Id == 1).Punkte.Should().Be(erwartetePunkte);
    }

    [Fact]
    public void Tabelle_KeinerGerundet_EntscheidetDasRegelset()
    {
        var regeln = MitVortrag(_punkteTordifferenzLos, new()
        {
            [1] = PunktevortragBerechnung.Berechnen(30, Punktevortrag.HalbiertAbgerundet),
            [2] = PunktevortragBerechnung.Berechnen(30, Punktevortrag.HalbiertAbgerundet)
        });

        var tabelle = TabellenRechner.TabelleBerechnen(ZweiSiege(), regeln);

        Reihenfolge(tabelle).Take(2).Should().Equal(1, 2);
    }

    [Fact]
    public void Tabelle_MehrPunkteSchlagenDieRundung()
    {
        // Team 1 liegt 15 + 3 = 18 Punkte, Team 2 hat 16 + 3 = 19 (aufgerundet) und bleibt trotz Rundung vorn.
        var regeln = MitVortrag(_punkteTordifferenzLos, new()
        {
            [1] = PunktevortragBerechnung.Berechnen(30, Punktevortrag.HalbiertAufgerundet),
            [2] = PunktevortragBerechnung.Berechnen(31, Punktevortrag.HalbiertAufgerundet)
        });

        var tabelle = TabellenRechner.TabelleBerechnen(ZweiSiege(), regeln);

        Reihenfolge(tabelle).Take(2).Should().Equal(2, 1);
    }

    [Fact]
    public void Tabelle_OhneRankingRegeln_RundungEntscheidetNachPunkten()
    {
        var regeln = new TabellenRegeln(_dreiPunkte, [], Vortraege: new Dictionary<int, Vortragswert>
        {
            [1] = PunktevortragBerechnung.Berechnen(30, Punktevortrag.HalbiertAbgerundet),
            [2] = PunktevortragBerechnung.Berechnen(31, Punktevortrag.HalbiertAbgerundet)
        });

        var tabelle = TabellenRechner.TabelleBerechnen(ZweiSiege(), regeln);

        Reihenfolge(tabelle).Take(2).Should().Equal(2, 1);
    }

    // ── K8: Platz in der Quellrunde ───────────────────────────────────────

    private static readonly RankingRegelArt[] _punkteVorrundeLos =
        [RankingRegelArt.Punkte, RankingRegelArt.PlatzVorrunde, RankingRegelArt.Losentscheid];

    [Fact]
    public void Tabelle_PlatzVorrunde_OrdnetPunktgleicheNachPlatzInDerQuellrunde()
    {
        var regeln = MitVortrag(_punkteVorrundeLos, new(), plaetze: new() { [1] = 4, [2] = 2 });

        var tabelle = TabellenRechner.TabelleBerechnen([], regeln, teilnehmer: [_teams[1], _teams[2]]);

        Reihenfolge(tabelle).Should().Equal(2, 1);
    }

    [Fact]
    public void Tabelle_PlatzVorrundeOhneQuelle_EntscheidetNichts()
    {
        var regeln = MitVortrag(_punkteVorrundeLos, new());

        var tabelle = TabellenRechner.TabelleBerechnen([], regeln, teilnehmer: [_teams[2], _teams[1]]);

        Reihenfolge(tabelle).Should().Equal(1, 2);
    }

    [Fact]
    public void Tabelle_PlatzVorrunde_TeamOhnePlatzSteht_HinterJedemMitPlatz()
    {
        var regeln = MitVortrag(_punkteVorrundeLos, new(), plaetze: new() { [2] = 6 });

        var tabelle = TabellenRechner.TabelleBerechnen([], regeln, teilnehmer: [_teams[1], _teams[2]]);

        Reihenfolge(tabelle).Should().Equal(2, 1);
    }

    // ── K10: Vortrag im Eintrag ───────────────────────────────────────────

    [Fact]
    public void Eintrag_TraegtDenVortrag_TeamsOhneVortragZeigenStrich()
    {
        var regeln = MitVortrag(_punkteTordifferenzLos, new()
        {
            [1] = PunktevortragBerechnung.Berechnen(31, Punktevortrag.HalbiertAbgerundet)
        });

        var tabelle = TabellenRechner.TabelleBerechnen([], regeln, teilnehmer: [_teams[1], _teams[2]]);

        var mit = tabelle.Single(e => e.Mannschaft.Id == 1);
        mit.VortragAnzeige.Should().Be("15↓");
        mit.VortragBeschreibung.Should().Be("31 Punkte in der Quellrunde, halbiert 15,5 → 15");
        var ohne = tabelle.Single(e => e.Mannschaft.Id == 2);
        ohne.Vortrag.Should().BeNull();
        ohne.VortragAnzeige.Should().Be("–");
        ohne.VortragBeschreibung.Should().BeNull();
    }
}
