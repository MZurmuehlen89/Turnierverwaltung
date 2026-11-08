using FluentAssertions;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Turniere;

public class TurnierausgangBerechnungTests
{
    // ── Hilfsmethoden ─────────────────────────────────────────────────────

    private static readonly Mannschaft _arg = Team(1, "Argentinien");
    private static readonly Mannschaft _fra = Team(2, "Frankreich");
    private static readonly Mannschaft _cro = Team(3, "Kroatien");
    private static readonly Mannschaft _mar = Team(4, "Marokko");

    private static Mannschaft Team(int id, string name) => new() { Id = id, Name = name };

    private static TurnierRunde Runde(int id, Rundenart art, int eingang = 2) =>
        new() { Id = id, Name = $"Runde {id}", Reihenfolge = id, Rundenart = art, EingangsMannschaftsanzahl = eingang };

    private static int _naechsteSpielId = 100;

    private static Spiel Spiel(TurnierRunde runde, Mannschaft? heim, Mannschaft? auswaerts, int? spieltag = null) => new()
    {
        Id = _naechsteSpielId++, TurnierRundeId = runde.Id, TurnierRunde = runde, Spielnummer = _naechsteSpielId,
        Spieltag = spieltag,
        HeimMannschaft = heim, HeimMannschaftId = heim?.Id, AuswaertsMannschaft = auswaerts, AuswaertsMannschaftId = auswaerts?.Id,
        Status = SpielStatus.Geplant
    };

    private static Spiel Gespielt(Spiel spiel, int heimTore, int auswaertsTore, Mannschaft? sieger,
        EntscheidungArt art = EntscheidungArt.RegulaereSpielzeit)
    {
        spiel.Status = SpielStatus.Abgeschlossen;
        spiel.Ergebnis = new SpielErgebnis
        {
            HeimToreRegulaer = heimTore, AuswaertsToreRegulaer = auswaertsTore,
            SiegerMannschaftId = sieger?.Id, EntscheidungArt = art
        };
        return spiel;
    }

    private static TabellenEintrag Eintrag(Mannschaft team, int rang, bool sicher) => new()
    {
        Mannschaft = team, Rang = rang, SicherPositioniert = sicher,
        Spiele = 0, Siege = 0, Unentschieden = 0, Niederlagen = 0, Tore = 0, Gegentore = 0, Punkte = 0
    };

    // ── Schlussrunde ──────────────────────────────────────────────────────

    [Fact]
    public void Schlussrunde_IstDieLetzteRundeOhneSpielUmPlatzDrei()
    {
        var finale = Runde(2, Rundenart.Finale);
        var platzDrei = Runde(3, Rundenart.SpielUmPlatzDrei);

        TurnierausgangBerechnung.Schlussrunde([Runde(1, Rundenart.KoRunde, 4), finale, platzDrei]).Should().BeSameAs(finale);
    }

    // ── K.-o. ─────────────────────────────────────────────────────────────

    [Fact]
    public void AusKoRunden_FinaleUndSpielUmPlatzDrei_ErgebenPlaetzeEinsBisVier()
    {
        var halbfinale = Runde(1, Rundenart.KoRunde, 4);
        var finale = Runde(2, Rundenart.Finale);
        var platzDrei = Runde(3, Rundenart.SpielUmPlatzDrei);
        Spiel[] spiele =
        [
            Gespielt(Spiel(halbfinale, _arg, _cro), 3, 0, _arg),
            Gespielt(Spiel(halbfinale, _fra, _mar), 2, 0, _fra),
            Gespielt(Spiel(finale, _arg, _fra), 3, 3, _arg, EntscheidungArt.Elfmeterschiessen),
            Gespielt(Spiel(platzDrei, _cro, _mar), 2, 1, _cro)
        ];

        var ausgang = TurnierausgangBerechnung.AusKoRunden([halbfinale, finale, platzDrei], spiele);

        ausgang.Art.Should().Be(TurnierausgangArt.Finale);
        ausgang.Sieger.Should().BeSameAs(_arg);
        ausgang.Finalisten.Should().BeEmpty();
        ausgang.Plaetze.Should().Equal(
            new Platzierung(1, _arg, PlatzierungsGrund.FinaleGewonnen, EntscheidungArt.Elfmeterschiessen),
            new Platzierung(2, _fra, PlatzierungsGrund.FinaleVerloren, EntscheidungArt.Elfmeterschiessen),
            new Platzierung(3, _cro, PlatzierungsGrund.SpielUmPlatzDreiGewonnen, EntscheidungArt.RegulaereSpielzeit),
            new Platzierung(4, _mar, PlatzierungsGrund.SpielUmPlatzDreiVerloren, EntscheidungArt.RegulaereSpielzeit));
    }

    [Fact]
    public void AusKoRunden_OhneSpielUmPlatzDrei_TeilenSichDieHalbfinalVerliererPlatzDrei()
    {
        var halbfinale = Runde(1, Rundenart.KoRunde, 4);
        var finale = Runde(2, Rundenart.Finale);
        Spiel[] spiele =
        [
            Gespielt(Spiel(halbfinale, _arg, _cro), 3, 0, _arg),
            Gespielt(Spiel(halbfinale, _mar, _fra), 0, 2, _fra),
            Gespielt(Spiel(finale, _arg, _fra), 2, 1, _arg)
        ];

        var ausgang = TurnierausgangBerechnung.AusKoRunden([halbfinale, finale], spiele);

        ausgang.Plaetze.Select(p => (p.Platz, p.Mannschaft.Name, p.Grund)).Should().Equal(
            (1, "Argentinien", PlatzierungsGrund.FinaleGewonnen),
            (2, "Frankreich", PlatzierungsGrund.FinaleVerloren),
            (3, "Kroatien", PlatzierungsGrund.HalbfinaleVerloren),
            (3, "Marokko", PlatzierungsGrund.HalbfinaleVerloren));
    }

    [Fact]
    public void AusKoRunden_RundeZwischenHalbfinaleUndFinale_PlatzDreiAusDerQuellrundeDesFinales()
    {
        var halbfinale = Runde(1, Rundenart.KoRunde, 4);
        var platzierungsspiele = Runde(2, Rundenart.KoRunde, 4);
        var finale = Runde(3, Rundenart.Finale);
        var hf1 = Gespielt(Spiel(halbfinale, _arg, _cro), 3, 0, _arg);
        var hf2 = Gespielt(Spiel(halbfinale, _fra, _mar), 2, 0, _fra);
        var endspiel = Gespielt(Spiel(finale, _arg, _fra), 2, 1, _arg);
        endspiel.HeimQuellSpielId = hf1.Id;
        endspiel.AuswaertsQuellSpielId = hf2.Id;
        Spiel[] spiele =
        [
            hf1, hf2, endspiel,
            Gespielt(Spiel(platzierungsspiele, Team(5, "Spanien"), Team(6, "England")), 1, 0, null),
            Gespielt(Spiel(platzierungsspiele, Team(7, "Brasilien"), Team(8, "Portugal")), 1, 0, null)
        ];

        var ausgang = TurnierausgangBerechnung.AusKoRunden([halbfinale, platzierungsspiele, finale], spiele);

        ausgang.Plaetze.Where(p => p.Platz == 3).Select(p => p.Mannschaft).Should().Equal(_cro, _mar);
    }

    [Fact]
    public void AusKoRunden_FinaleAngesetztAberNichtGespielt_NenntDieFinalistenUndPlatzDrei()
    {
        var halbfinale = Runde(1, Rundenart.KoRunde, 4);
        var finale = Runde(2, Rundenart.Finale);
        Spiel[] spiele =
        [
            Gespielt(Spiel(halbfinale, _arg, _cro), 3, 0, _arg),
            Gespielt(Spiel(halbfinale, _fra, _mar), 2, 0, _fra),
            Spiel(finale, _arg, _fra)
        ];

        var ausgang = TurnierausgangBerechnung.AusKoRunden([halbfinale, finale], spiele);

        ausgang.Sieger.Should().BeNull();
        ausgang.Finalisten.Should().Equal(_arg, _fra);
        ausgang.Plaetze.Select(p => (p.Platz, p.Mannschaft.Name)).Should().Equal((3, "Kroatien"), (3, "Marokko"));
    }

    [Fact]
    public void AusKoRunden_HalbfinaleNochOffen_NochNichtsEntschieden()
    {
        var halbfinale = Runde(1, Rundenart.KoRunde, 4);
        var finale = Runde(2, Rundenart.Finale);
        Spiel[] spiele =
        [
            Gespielt(Spiel(halbfinale, _arg, _cro), 3, 0, _arg),
            Spiel(halbfinale, _fra, _mar),
            Spiel(finale, _arg, null)
        ];

        var ausgang = TurnierausgangBerechnung.AusKoRunden([halbfinale, finale], spiele);

        ausgang.Art.Should().Be(TurnierausgangArt.Finale);
        ausgang.Plaetze.Should().BeEmpty();
        ausgang.Finalisten.Should().BeEmpty();
    }

    [Fact]
    public void AusKoRunden_FinaleMitHinUndRueckspiel_EntscheidetDasRueckspiel()
    {
        var finale = Runde(1, Rundenart.Finale);
        finale.MitRueckspiel = true;
        var hinspiel = Gespielt(Spiel(finale, _arg, _fra), 2, 0, _arg);
        var rueckspiel = Gespielt(Spiel(finale, _fra, _arg), 3, 0, _fra);
        rueckspiel.IstRueckspiel = true;
        rueckspiel.HinspielId = hinspiel.Id;

        var ausgang = TurnierausgangBerechnung.AusKoRunden([finale], [hinspiel, rueckspiel]);

        ausgang.Sieger.Should().BeSameAs(_fra);
        ausgang.Plaetze.Single(p => p.Platz == 2).Mannschaft.Should().BeSameAs(_arg);
    }

    [Fact]
    public void AusKoRunden_AlteVorlageMitFinaleAlsKoRundeZweierTeams_ZaehltAlsFinale()
    {
        var finale = Runde(1, Rundenart.KoRunde, eingang: 2);

        var ausgang = TurnierausgangBerechnung.AusKoRunden([finale], [Gespielt(Spiel(finale, _arg, _fra), 1, 0, _arg)]);

        ausgang.Sieger.Should().BeSameAs(_arg);
    }

    [Theory]
    [InlineData(Rundenart.KoRunde, 4)]
    [InlineData(Rundenart.Gruppenphase, 8)]
    public void AusKoRunden_SchlussrundeOhneFinale_KeinTurniersieger(Rundenart art, int eingang)
    {
        var schlussrunde = Runde(1, art, eingang);

        var ausgang = TurnierausgangBerechnung.AusKoRunden([schlussrunde], [Gespielt(Spiel(schlussrunde, _arg, _fra), 1, 0, _arg)]);

        ausgang.Should().BeSameAs(Turnierausgang.Keiner);
    }

    // ── Tabelle ───────────────────────────────────────────────────────────

    [Fact]
    public void AusTabelle_ErsterSicher_IstMeisterMitOffenenSpieltagen()
    {
        var liga = Runde(1, Rundenart.Liga, 3);
        Spiel[] spiele =
        [
            Gespielt(Spiel(liga, _arg, _fra, spieltag: 1), 1, 0, _arg),
            Gespielt(Spiel(liga, _arg, _cro, spieltag: 2), 1, 0, _arg),
            Spiel(liga, _fra, _cro, spieltag: 3)
        ];

        var ausgang = TurnierausgangBerechnung.AusTabelle(liga, [Eintrag(_arg, 1, sicher: true), Eintrag(_fra, 2, sicher: false)], spiele);

        ausgang.Art.Should().Be(TurnierausgangArt.Tabelle);
        ausgang.Plaetze.Should().Equal(new Platzierung(1, _arg, PlatzierungsGrund.Meister));
        (ausgang.OffeneSpieltage, ausgang.SpieltageGesamt).Should().Be((1, 3));
    }

    [Fact]
    public void AusTabelle_ErsterNochNichtSicher_NurTabellenfuehrer()
    {
        var liga = Runde(1, Rundenart.Liga, 3);
        Spiel[] spiele = [Gespielt(Spiel(liga, _arg, _fra, spieltag: 1), 1, 0, _arg), Spiel(liga, _arg, _cro, spieltag: 2)];

        var ausgang = TurnierausgangBerechnung.AusTabelle(liga, [Eintrag(_arg, 1, sicher: false)], spiele);

        ausgang.Plaetze.Should().BeEmpty();
        ausgang.Tabellenfuehrer.Should().BeSameAs(_arg);
    }

    [Fact]
    public void AusTabelle_OhneErgebnis_KeinMeisterUndKeinTabellenfuehrer()
    {
        var liga = Runde(1, Rundenart.Liga, 3);

        // Ohne offene Spiele hält die Prognose jeden Rang für sicher — ohne Ergebnis darf das nicht zählen.
        var ausgang = TurnierausgangBerechnung.AusTabelle(liga, [Eintrag(_arg, 1, sicher: true)], []);

        ausgang.Plaetze.Should().BeEmpty();
        ausgang.Tabellenfuehrer.Should().BeNull();
    }

    [Fact]
    public void AusTabelle_SpieleOhneSpieltag_ZaehlenKeineSpieltage()
    {
        var liga = Runde(1, Rundenart.Liga, 3);
        Spiel[] spiele = [Gespielt(Spiel(liga, _arg, _fra), 1, 0, _arg), Spiel(liga, _arg, _cro)];

        var ausgang = TurnierausgangBerechnung.AusTabelle(liga, [Eintrag(_arg, 1, sicher: false)], spiele);

        (ausgang.OffeneSpieltage, ausgang.SpieltageGesamt).Should().Be((0, 0));
    }

    [Fact]
    public void AusTabelle_SchweizerSystem_ZaehltNochNichtAngesetzteSpieltage()
    {
        var schweizer = Runde(1, Rundenart.SchweizerSystem, 4);
        schweizer.KonfigurationJson = new RundenKonfiguration(AnzahlSpieltage: 3).ZuJson();
        Spiel[] spiele =
        [
            Gespielt(Spiel(schweizer, _arg, _fra, spieltag: 1), 1, 0, _arg),
            Spiel(schweizer, _cro, _mar, spieltag: 1)
        ];

        var ausgang = TurnierausgangBerechnung.AusTabelle(schweizer, [Eintrag(_arg, 1, sicher: false)], spiele);

        (ausgang.OffeneSpieltage, ausgang.SpieltageGesamt).Should().Be((3, 3));
    }
}
