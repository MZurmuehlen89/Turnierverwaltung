using FluentAssertions;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Tabelle;

/// <summary>Issue #439: Rang-Schranken der Liga und Qualifikationsstatus der Gruppe direkt gegen die Prognose,
/// ohne Datenbank. Die Sonderfälle der Ergebniskombinationen (#201/#202/#205/#206/#427) bleiben in
/// <see cref="TabellenServiceTests"/>.</summary>
public class RangPrognoseTests
{
    private static readonly PunkteRegel _dreiPunkte =
        new() { PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 };

    private static readonly Dictionary<int, Mannschaft> _teams = Enumerable.Range(1, 4)
        .ToDictionary(id => id, id => new Mannschaft { Id = id, Name = $"Team {id}" });

    private static readonly TurnierRunde _liga = new() { Rundenart = Rundenart.Liga };

    private static int _naechsteSpielnummer;

    private static Spiel Spiel(int heimId, int auswaertsId, int? heimTore = null, int? auswaertsTore = null, int? spieltag = null)
    {
        var spiel = new Spiel
        {
            Spielnummer = Interlocked.Increment(ref _naechsteSpielnummer),
            HeimMannschaftId = heimId, HeimMannschaft = _teams[heimId],
            AuswaertsMannschaftId = auswaertsId, AuswaertsMannschaft = _teams[auswaertsId],
            Spieltag = spieltag,
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

    private static IReadOnlyList<TabellenEintrag> LigaMitRaengen(TurnierRunde runde, params Spiel[] spiele)
    {
        var tabelle = TabellenRechner.TabelleBerechnen(spiele, _dreiPunkte, []);
        RangPrognose.LigaRaengeSetzen(tabelle, runde, spiele, _dreiPunkte);
        return tabelle;
    }

    private static GruppenTabelle GruppeMitRaengen(int qualifiziertePlaetze, params Spiel[] spiele)
    {
        var gruppe = new GruppenTabelle(1, TabellenRechner.TabelleBerechnen(spiele, _dreiPunkte, []));
        RangPrognose.GruppenRaengeSetzen(gruppe, spiele, qualifiziertePlaetze, _dreiPunkte, []);
        return gruppe;
    }

    private static TabellenEintrag Team(IReadOnlyList<TabellenEintrag> tabelle, int id) =>
        tabelle.Single(e => e.Mannschaft.Id == id);

    // ── Liga ──────────────────────────────────────────────────────────────

    [Fact]
    public void LigaRaengeSetzen_AllesGespielt_JederRangIstFinal()
    {
        var tabelle = LigaMitRaengen(_liga, Spiel(1, 2, 2, 0), Spiel(2, 3, 1, 0), Spiel(3, 1, 0, 1));

        tabelle.Should().OnlyContain(e => e.BesterRang == e.Rang && e.SchlechtesterRang == e.Rang);
    }

    [Fact]
    public void LigaRaengeSetzen_UneinholbarerVorsprung_LegtDenRangFest()
    {
        // Team 1 hat 6 Punkte und ist fertig, 2 und 3 können mit ihrem einen offenen Spiel höchstens 3 erreichen.
        var tabelle = LigaMitRaengen(_liga, Spiel(1, 2, 3, 0), Spiel(1, 3, 3, 0), Spiel(2, 3));

        (Team(tabelle, 1).BesterRang, Team(tabelle, 1).SchlechtesterRang).Should().Be((1, 1));
        (Team(tabelle, 2).BesterRang, Team(tabelle, 2).SchlechtesterRang).Should().Be((2, 3));
        (Team(tabelle, 3).BesterRang, Team(tabelle, 3).SchlechtesterRang).Should().Be((2, 3));
    }

    [Fact]
    public void LigaRaengeSetzen_ErreichbarePunktgleichheit_ZaehltAlsVorbeiziehen()
    {
        // Team 1 ist mit 3 Punkten fertig; 2 und 3 können im offenen Spiel beide noch auf 3 kommen (nicht
        // gleichzeitig, aber die Schranken sind bewusst konservativ).
        var tabelle = LigaMitRaengen(_liga, Spiel(1, 2, 1, 0), Spiel(2, 3));

        (Team(tabelle, 1).BesterRang, Team(tabelle, 1).SchlechtesterRang).Should().Be((1, 3));
    }

    [Fact]
    public void LigaRaengeSetzen_MitUnaufgeloestemPlatzhalterSpiel_SetztKeineRaenge()
    {
        var platzhalter = new Spiel { Spielnummer = 99, HeimMannschaftId = 3, HeimMannschaft = _teams[3], Status = SpielStatus.Geplant };
        Spiel[] spiele = [Spiel(1, 2, 1, 0), platzhalter];
        var tabelle = TabellenRechner.TabelleBerechnen(spiele, _dreiPunkte, []);

        RangPrognose.LigaRaengeSetzen(tabelle, _liga, spiele, _dreiPunkte);

        tabelle.Should().OnlyContain(e => e.BesterRang == null && e.SchlechtesterRang == null);
    }

    [Fact]
    public void LigaRaengeSetzen_SchweizerSystem_RechnetNochNichtGenerierteSpieltageEin()
    {
        // Erster von drei Spieltagen gespielt: jedes Team hat noch zwei Spiele vor sich, die es noch nicht gibt.
        var runde = new TurnierRunde
        {
            Rundenart = Rundenart.SchweizerSystem,
            KonfigurationJson = new RundenKonfiguration(AnzahlSpieltage: 3).ZuJson()
        };

        var tabelle = LigaMitRaengen(runde, Spiel(1, 2, 1, 0, spieltag: 1), Spiel(3, 4, 1, 0, spieltag: 1));

        tabelle.Should().OnlyContain(e => e.BesterRang == 1 && e.SchlechtesterRang == 4);
    }

    [Fact]
    public void LigaRaengeSetzen_SchweizerSystemNachDemLetztenSpieltag_JederRangIstFinal()
    {
        var runde = new TurnierRunde
        {
            Rundenart = Rundenart.SchweizerSystem,
            KonfigurationJson = new RundenKonfiguration(AnzahlSpieltage: 1).ZuJson()
        };

        var tabelle = LigaMitRaengen(runde, Spiel(1, 2, 1, 0, spieltag: 1), Spiel(3, 4, 2, 0, spieltag: 1));

        tabelle.Should().OnlyContain(e => e.BesterRang == e.Rang && e.SchlechtesterRang == e.Rang);
    }

    [Fact]
    public void LigaRaengeSetzen_SchweizerSystemOhneSpieltagAnzahl_SetztKeineRaenge()
    {
        var runde = new TurnierRunde { Rundenart = Rundenart.SchweizerSystem };

        var tabelle = LigaMitRaengen(runde, Spiel(1, 2, 1, 0, spieltag: 1), Spiel(3, 4, 1, 0, spieltag: 1));

        tabelle.Should().OnlyContain(e => e.BesterRang == null && e.SchlechtesterRang == null);
    }

    [Fact]
    public void SicherPositionierteMarkieren_MarkiertNurFeststehendeRaenge()
    {
        var tabelle = LigaMitRaengen(_liga, Spiel(1, 2, 3, 0), Spiel(1, 3, 3, 0), Spiel(2, 3));

        RangPrognose.SicherPositionierteMarkieren(tabelle);

        Team(tabelle, 1).SicherPositioniert.Should().BeTrue();
        Team(tabelle, 2).SicherPositioniert.Should().BeFalse();
        Team(tabelle, 3).SicherPositioniert.Should().BeFalse();
    }

    // ── Gruppe ────────────────────────────────────────────────────────────

    [Fact]
    public void GruppenRaengeSetzen_OhneOffeneSpiele_EntscheidetNachDemRang()
    {
        var gruppe = GruppeMitRaengen(1, Spiel(1, 2, 2, 0), Spiel(2, 3, 1, 0), Spiel(3, 1, 0, 1));

        gruppe.Tabelle.Should().OnlyContain(e => e.SicherPositioniert && e.BesterRang == e.Rang && e.SchlechtesterRang == e.Rang);
        Team(gruppe.Tabelle, 1).Qualifikationsstatus.Should().Be(Qualifikationsstatus.SicherQualifiziert);
        Team(gruppe.Tabelle, 2).Qualifikationsstatus.Should().Be(Qualifikationsstatus.SicherAusgeschieden);
        Team(gruppe.Tabelle, 3).Qualifikationsstatus.Should().Be(Qualifikationsstatus.SicherAusgeschieden);
    }

    [Fact]
    public void GruppenRaengeSetzen_VorDemErstenErgebnis_IstFuerJedesTeamNochAllesOffen()
    {
        var gruppe = GruppeMitRaengen(2, Spiel(1, 2), Spiel(3, 4), Spiel(1, 3), Spiel(2, 4), Spiel(1, 4), Spiel(2, 3));

        gruppe.Tabelle.Should().OnlyContain(e =>
            e.BesterRang == 1 && e.SchlechtesterRang == 4 && !e.SicherPositioniert
            && e.Qualifikationsstatus == Qualifikationsstatus.Offen);
    }

    [Fact]
    public void GruppenRaengeSetzen_MitOffenenSpielen_GrenztDieRaengeJeTeamEin()
    {
        // Team 1 hat alle drei Spiele gewonnen. Dahinter: 2 mit 3 Punkten, 3 und 4 ohne; offen sind 2–4 und 3–4.
        Spiel[] spiele =
        [
            Spiel(1, 2, 1, 0), Spiel(1, 3, 1, 0), Spiel(1, 4, 1, 0), Spiel(2, 3, 1, 0),
            Spiel(2, 4), Spiel(3, 4)
        ];

        var zweiPlaetze = GruppeMitRaengen(2, spiele).Tabelle;
        var einPlatz = GruppeMitRaengen(1, spiele).Tabelle;

        (Team(zweiPlaetze, 1).BesterRang, Team(zweiPlaetze, 1).SchlechtesterRang).Should().Be((1, 1));
        Team(zweiPlaetze, 1).SicherPositioniert.Should().BeTrue();
        Team(zweiPlaetze, 1).Qualifikationsstatus.Should().Be(Qualifikationsstatus.SicherQualifiziert);

        // 2 kann mit einem Sieg Zweiter bleiben, fällt nach einer hohen Niederlage gegen 4 aber bis auf Platz 4.
        (Team(zweiPlaetze, 2).BesterRang, Team(zweiPlaetze, 2).SchlechtesterRang).Should().Be((2, 4));
        Team(zweiPlaetze, 2).Qualifikationsstatus.Should().Be(Qualifikationsstatus.Offen);
        (Team(zweiPlaetze, 4).BesterRang, Team(zweiPlaetze, 4).SchlechtesterRang).Should().Be((2, 4));

        // Bei nur einem Platz sind 2, 3 und 4 ausgeschieden, auch wenn ihr Schlussrang noch offen ist.
        foreach (int id in new[] { 2, 3, 4 })
        {
            Team(einPlatz, id).Qualifikationsstatus.Should().Be(Qualifikationsstatus.SicherAusgeschieden);
            Team(einPlatz, id).SicherPositioniert.Should().BeFalse();
        }
    }

    [Fact]
    public void GruppenRaengeSetzen_MitOffenemPlatz_LaesstStatusUndRaengeOffen()
    {
        // Issue #397: die Spiele des noch unbekannten Teilnehmers fehlen in der Rechnung.
        Spiel[] spiele = [Spiel(1, 2, 2, 0), Spiel(2, 3, 1, 0), Spiel(3, 1, 0, 1)];
        var gruppe = new GruppenTabelle(1, TabellenRechner.TabelleBerechnen(spiele, _dreiPunkte, []))
        {
            OffenePlaetze = ["Sieger Playoff A"]
        };

        RangPrognose.GruppenRaengeSetzen(gruppe, spiele, 2, _dreiPunkte, []);

        gruppe.Tabelle.Should().OnlyContain(e =>
            e.Qualifikationsstatus == Qualifikationsstatus.Offen && !e.SicherPositioniert && e.BesterRang == null);
    }

    [Theory]
    [InlineData(8, 16, 2)]    // acht Gruppen, 16 Direktqualifikanten
    [InlineData(6, 12, 2)]    // EM: die besten Dritten zählen nicht zu den Direktqualifikanten
    [InlineData(4, 4, 1)]
    [InlineData(4, 2, 1)]     // mindestens ein Platz
    [InlineData(null, 16, 2)] // ohne Gruppenanzahl
    public void QualifiziertePlaetzeJeGruppe_VerteiltDieDirektqualifikantenAufDieGruppen(
        int? gruppenAnzahl, int direktqualifikanten, int erwartet)
    {
        var runde = new TurnierRunde { GruppenAnzahl = gruppenAnzahl, AusgangsMannschaftsanzahl = direktqualifikanten };

        RangPrognose.QualifiziertePlaetzeJeGruppe(runde).Should().Be(erwartet);
    }
}
