using FluentAssertions;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Tabelle;

/// <summary>Issue #480: Tabellenplatz je Mannschaft nach jedem Spieltag.</summary>
public class TabellenVerlaufRechnerTests
{
    private static readonly PunkteRegel _dreiPunkte =
        new() { PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 };

    private static readonly Dictionary<int, Mannschaft> _teams = Enumerable.Range(1, 6)
        .ToDictionary(id => id, id => new Mannschaft { Id = id, Name = $"Team {id}" });

    private static int _naechsteSpielnummer;

    private static Spiel Spiel(int spieltag, int heimId, int auswaertsId, int? heimTore = null, int? auswaertsTore = null)
    {
        var spiel = new Spiel
        {
            Spielnummer = Interlocked.Increment(ref _naechsteSpielnummer),
            Spieltag = spieltag,
            HeimMannschaftId = heimId, HeimMannschaft = _teams[heimId],
            AuswaertsMannschaftId = auswaertsId, AuswaertsMannschaft = _teams[auswaertsId],
            Status = SpielStatus.Geplant
        };
        if (heimTore is null || auswaertsTore is null) return spiel;

        spiel.Status = SpielStatus.Abgeschlossen;
        spiel.Ergebnis = new SpielErgebnis
        {
            HeimToreRegulaer = heimTore.Value,
            AuswaertsToreRegulaer = auswaertsTore.Value,
            EntscheidungArt = EntscheidungArt.RegulaereSpielzeit
        };
        return spiel;
    }

    private static TabellenVerlauf Verlauf(params Spiel[] spiele) =>
        TabellenVerlaufRechner.Berechnen(gruppenNummer: null, spiele, _dreiPunkte, []);

    private static IReadOnlyList<VerlaufPunkt> PunkteVon(TabellenVerlauf verlauf, int mannschaftId) =>
        verlauf.Mannschaften.Single(m => m.Mannschaft.Id == mannschaftId).Punkte;

    [Fact]
    public void Berechnen_HaeltDenPlatzNachJedemSpieltagFest()
    {
        var verlauf = Verlauf(
            Spiel(1, 1, 2, 2, 0), Spiel(1, 3, 4, 1, 0),
            Spiel(2, 2, 3, 3, 0), Spiel(2, 4, 1, 4, 0));

        verlauf.Spieltage.Should().Equal(1, 2);
        PunkteVon(verlauf, 1).Select(p => p.Platz).Should().Equal(1, 3);
        PunkteVon(verlauf, 4).Select(p => p.Platz).Should().Equal(3, 1);
        PunkteVon(verlauf, 4).Select(p => p.Punkte).Should().Equal(0, 3);
    }

    [Fact]
    public void Berechnen_SpieltageOhneErgebnisErscheinenNicht()
    {
        var verlauf = Verlauf(Spiel(1, 1, 2, 1, 0), Spiel(2, 2, 1), Spiel(3, 1, 2));

        verlauf.Spieltage.Should().Equal(1);
        PunkteVon(verlauf, 1).Should().ContainSingle();
    }

    [Fact]
    public void Berechnen_NachholspielStehtMitEinemSpielWenigerUndAusstehend()
    {
        // Spieltag 1: 3-4 noch offen, Spieltag 2 schon gespielt.
        var verlauf = Verlauf(
            Spiel(1, 1, 2, 1, 0), Spiel(1, 3, 4),
            Spiel(2, 1, 3, 0, 2), Spiel(2, 2, 4, 1, 1));

        var team3 = PunkteVon(verlauf, 3);
        team3.Select(p => p.Spiele).Should().Equal(0, 1);
        team3.Select(p => p.SpielAusstehend).Should().Equal(true, false);
        PunkteVon(verlauf, 1).Should().OnlyContain(p => !p.SpielAusstehend);
    }

    [Fact]
    public void Berechnen_NachgetragenesNachholspielZaehltZuSeinemSpieltag()
    {
        var verlauf = Verlauf(
            Spiel(1, 1, 2, 1, 0), Spiel(1, 3, 4, 3, 0),
            Spiel(2, 1, 3, 0, 0), Spiel(2, 2, 4, 1, 1));

        var nachSpieltag1 = PunkteVon(verlauf, 3)[0];
        nachSpieltag1.Spiele.Should().Be(1);
        nachSpieltag1.Platz.Should().Be(1);
    }

    [Fact]
    public void Berechnen_SpielfreieMannschaftStehtInJedemStand()
    {
        // Team 5 hat am 1. Spieltag spielfrei.
        var verlauf = Verlauf(
            Spiel(1, 1, 2, 1, 0), Spiel(1, 3, 4, 1, 0),
            Spiel(2, 5, 1, 2, 0));

        var team5 = PunkteVon(verlauf, 5);
        team5.Select(p => p.Spieltag).Should().Equal(1, 2);
        team5[0].SpieleAmSpieltag.Should().BeEmpty();
        team5[0].SpielAusstehend.Should().BeFalse();
        verlauf.AnzahlPlaetze.Should().Be(5);
    }

    [Fact]
    public void Berechnen_MannschaftenNachLetztemStandGeordnet()
    {
        var verlauf = Verlauf(Spiel(1, 1, 2, 0, 3), Spiel(1, 3, 4, 0, 1));

        verlauf.Mannschaften.Select(m => m.Mannschaft.Id).Should().Equal(2, 4, 3, 1);
    }

    [Fact]
    public void Berechnen_OhneErgebnisIstDerVerlaufLeer()
    {
        var verlauf = Verlauf(Spiel(1, 1, 2));

        verlauf.HatSpieltage.Should().BeFalse();
        verlauf.Mannschaften.Should().BeEmpty();
    }

    [Fact]
    public void Beschreibung_AusSichtDerMannschaftEigeneToreZuerst()
    {
        var verlauf = Verlauf(Spiel(1, 1, 2, 3, 1), Spiel(2, 2, 1));

        PunkteVon(verlauf, 2)[0].Beschreibung(2).Should().Be(
            "1. Spieltag: Platz 2 · 0 Punkte aus 1 Spiel\n1:3 gegen Team 1 (A)");
    }

    [Fact]
    public void Beschreibung_SpielfreiUndAusstehend()
    {
        var verlauf = Verlauf(Spiel(1, 1, 2, 1, 0), Spiel(1, 3, 4), Spiel(2, 5, 1, 0, 0));

        PunkteVon(verlauf, 5)[0].Beschreibung(5).Should().EndWith("\nspielfrei");
        PunkteVon(verlauf, 3)[0].Beschreibung(3).Should().EndWith("\nSpiel gegen Team 4 (H) ausstehend");
    }
}
