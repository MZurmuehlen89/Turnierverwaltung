using FluentAssertions;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Tabelle;

/// <summary>Issue #358: Tabellenzonen aus den ausgehenden Übergangsregeln einer Runde.</summary>
public class TabellenZonenTests
{
    private static readonly TurnierRunde _achtelfinale = new() { Id = 10, Name = "Achtelfinale" };
    private static readonly TurnierRunde _playoff = new() { Id = 11, Name = "Playoff-Runde" };

    private static Uebergangsregel Regel(TurnierRunde ziel, int? gruppe, int von, int bis, int? anzahlBeste = null) => new()
    {
        ZielRundeId = ziel.Id, ZielRunde = ziel, GruppeIndex = gruppe,
        PlatzierungVon = von, PlatzierungBis = bis, AnzahlBeste = anzahlBeste
    };

    // Ligaphase der Champions League: 1–8 direkt ins Achtelfinale, 9–24 in die Playoff-Runde.
    private static IReadOnlyList<TabellenZone> LigaphasenZonen() =>
        TabellenZonen.AusRegeln([Regel(_playoff, null, 9, 24), Regel(_achtelfinale, null, 1, 8)]);

    // EM-Gruppenphase mit zwei Gruppen: Erste/Zweite direkt, die besten Dritten unter Vorbehalt.
    private static IReadOnlyList<TabellenZone> GruppenZonenMitBestenDritten() =>
        TabellenZonen.AusRegeln(
        [
            Regel(_achtelfinale, 1, 1, 2), Regel(_achtelfinale, 2, 1, 2),
            Regel(_achtelfinale, null, 3, 3, anzahlBeste: 1)
        ]);

    private static TabellenEintrag Eintrag(int rang, int? bester = null, int? schlechtester = null) => new()
    {
        Mannschaft = new Mannschaft { Id = rang, Name = $"Team {rang}" },
        Spiele = 0, Siege = 0, Unentschieden = 0, Niederlagen = 0, Tore = 0, Gegentore = 0, Punkte = 0,
        Rang = rang, BesterRang = bester, SchlechtesterRang = schlechtester
    };

    [Fact]
    public void AusRegeln_VergibtQualifikationsfarbenInReihenfolgeDerBestenPlatzierung()
    {
        var zonen = LigaphasenZonen();

        zonen.Select(z => (z.PlatzVon, z.ZielRundeName, z.Farbe)).Should().Equal(
            (1, "Achtelfinale", ZonenFarbe.Gruen),
            (9, "Playoff-Runde", ZonenFarbe.Blau));
    }

    [Fact]
    public void AusRegeln_GruppenuebergreifendeRegelIstZoneUnterVorbehalt()
    {
        var dritte = GruppenZonenMitBestenDritten().Single(z => z.PlatzVon == 3);

        dritte.UnterVorbehalt.Should().BeTrue();
        dritte.Farbe.Should().Be(ZonenFarbe.Amber);
        TabellenZonen.ZielFarbe(GruppenZonenMitBestenDritten(), _achtelfinale.Id).Should().Be(ZonenFarbe.Gruen);
    }

    [Fact]
    public void Zuweisen_SetztZonenfarbeNachRangUndLaesstAusgeschiedeneOhneFarbe()
    {
        var tabelle = new[] { Eintrag(8), Eintrag(9), Eintrag(24), Eintrag(25) };

        TabellenZonen.Zuweisen(tabelle, LigaphasenZonen(), gruppenNummer: null, mitSicherheit: true);

        tabelle.Select(e => e.ZonenFarbe).Should().Equal(ZonenFarbe.Gruen, ZonenFarbe.Blau, ZonenFarbe.Blau, null);
        tabelle.Should().OnlyContain(e => e.PunktFarbe == null, "ohne bekannte Rang-Schranken steht nichts fest");
    }

    [Fact]
    public void Zuweisen_PunktErstWennAlleMoeglichenRaengeInDieselbeZoneFuehren()
    {
        var sicherDirekt = Eintrag(3, bester: 1, schlechtester: 8);
        var nochOffen = Eintrag(8, bester: 5, schlechtester: 12);
        var sicherPlayoff = Eintrag(15, bester: 9, schlechtester: 24);
        var sicherRaus = Eintrag(30, bester: 25, schlechtester: 36);
        var rausOderPlayoff = Eintrag(24, bester: 20, schlechtester: 27);

        TabellenZonen.Zuweisen(
            [sicherDirekt, nochOffen, sicherPlayoff, sicherRaus, rausOderPlayoff],
            LigaphasenZonen(), gruppenNummer: null, mitSicherheit: true);

        sicherDirekt.PunktFarbe.Should().Be(ZonenFarbe.Gruen);
        nochOffen.PunktFarbe.Should().BeNull();
        sicherPlayoff.PunktFarbe.Should().Be(ZonenFarbe.Blau);
        sicherRaus.PunktFarbe.Should().Be(ZonenFarbe.Rot);
        rausOderPlayoff.PunktFarbe.Should().BeNull();
    }

    [Fact]
    public void Zuweisen_SichererGruppendritterBekommtBalkenAberKeinenPunkt()
    {
        var zweiter = Eintrag(2, bester: 1, schlechtester: 2);
        var dritter = Eintrag(3, bester: 3, schlechtester: 3);
        var vierter = Eintrag(4, bester: 4, schlechtester: 4);

        TabellenZonen.Zuweisen([zweiter, dritter, vierter], GruppenZonenMitBestenDritten(), gruppenNummer: 2, mitSicherheit: true);

        zweiter.PunktFarbe.Should().Be(ZonenFarbe.Gruen);
        dritter.ZonenFarbe.Should().Be(ZonenFarbe.Amber);
        dritter.PunktFarbe.Should().BeNull("über das Weiterkommen entscheidet erst die Rangfolge der Gruppendritten");
        vierter.ZonenFarbe.Should().BeNull();
        vierter.PunktFarbe.Should().Be(ZonenFarbe.Rot);
    }

    [Fact]
    public void Zuweisen_OhneSicherheitOderOhneZonenKeinPunkt()
    {
        var historisch = Eintrag(1, bester: 1, schlechtester: 1);
        var ohneRegeln = Eintrag(1, bester: 1, schlechtester: 1);

        TabellenZonen.Zuweisen([historisch], LigaphasenZonen(), gruppenNummer: null, mitSicherheit: false);
        TabellenZonen.Zuweisen([ohneRegeln], [], gruppenNummer: null, mitSicherheit: true);

        historisch.ZonenFarbe.Should().Be(ZonenFarbe.Gruen);
        historisch.PunktFarbe.Should().BeNull();
        ohneRegeln.ZonenFarbe.Should().BeNull();
        ohneRegeln.PunktFarbe.Should().BeNull("eine Runde ohne Übergangsregeln kennt kein Ausscheiden");
    }

    // ── Rangfolge der Besten (#438) ───────────────────────────────────────────

    // Drei Gruppendritte, der beste kommt weiter.
    private static List<TabellenEintrag> DritteRangfolge() => [Eintrag(1), Eintrag(2), Eintrag(3)];

    [Fact]
    public void BesteZuweisen_PlaetzeDerBestenTragenDieFarbeDerZielrunde()
    {
        var rangfolge = DritteRangfolge();

        TabellenZonen.BesteZuweisen(rangfolge, GruppenZonenMitBestenDritten(), _achtelfinale.Id, anzahlBeste: 1, [null]);

        rangfolge.Select(e => e.ZonenFarbe).Should().Equal(ZonenFarbe.Gruen, null, null);
    }

    [Fact]
    public void BesteZuweisen_SolangeEinQualifikantOffenIstKeinStatusUndKeinPunkt()
    {
        var rangfolge = DritteRangfolge();

        TabellenZonen.BesteZuweisen(rangfolge, GruppenZonenMitBestenDritten(), _achtelfinale.Id, anzahlBeste: 2, [1, null]);

        rangfolge.Should().OnlyContain(e =>
            e.Qualifikationsstatus == Qualifikationsstatus.Offen && !e.SicherPositioniert && e.PunktFarbe == null);
    }

    [Fact]
    public void BesteZuweisen_StehenAlleQualifikantenFestBekommtJederStatusUndPunkt()
    {
        var rangfolge = DritteRangfolge();

        TabellenZonen.BesteZuweisen(rangfolge, GruppenZonenMitBestenDritten(), _achtelfinale.Id, anzahlBeste: 1, [1]);

        rangfolge.Select(e => (e.Qualifikationsstatus, e.PunktFarbe)).Should().Equal(
            (Qualifikationsstatus.SicherQualifiziert, ZonenFarbe.Gruen),
            (Qualifikationsstatus.SicherAusgeschieden, ZonenFarbe.Rot),
            (Qualifikationsstatus.SicherAusgeschieden, ZonenFarbe.Rot));
        rangfolge.Should().OnlyContain(e => e.SicherPositioniert);
    }

    [Fact]
    public void BesteZuweisen_StatusFolgtDenQualifikantenNichtDemRang()
    {
        // Der Qualifikant steht fest, die Rangfolge zeigt ihn aber (noch) nicht auf einem Platz der Besten.
        var rangfolge = DritteRangfolge();

        TabellenZonen.BesteZuweisen(rangfolge, GruppenZonenMitBestenDritten(), _achtelfinale.Id, anzahlBeste: 1, [2]);

        rangfolge.Select(e => e.Qualifikationsstatus).Should().Equal(
            Qualifikationsstatus.SicherAusgeschieden, Qualifikationsstatus.SicherQualifiziert, Qualifikationsstatus.SicherAusgeschieden);
        rangfolge.Select(e => e.ZonenFarbe).Should().Equal(ZonenFarbe.Gruen, null, null);
    }

    [Fact]
    public void BesteZuweisen_OhneDirekteZoneDerZielrundeGruen()
    {
        // Nur die besten Dritten führen in die Playoff-Runde — es gibt keine direkte Zone, deren Farbe gälte.
        var zonen = TabellenZonen.AusRegeln([Regel(_achtelfinale, null, 1, 2), Regel(_playoff, null, 3, 3, anzahlBeste: 1)]);
        var rangfolge = DritteRangfolge();

        TabellenZonen.BesteZuweisen(rangfolge, zonen, _playoff.Id, anzahlBeste: 1, [1]);

        rangfolge[0].ZonenFarbe.Should().Be(ZonenFarbe.Gruen);
        rangfolge[0].PunktFarbe.Should().Be(ZonenFarbe.Gruen);
    }

    [Fact]
    public void Legende_FasstGleicheRegelnAllerGruppenZusammen()
    {
        TabellenZonen.Legende(GruppenZonenMitBestenDritten()).Should().Equal(
            new ZonenLegendenEintrag(ZonenFarbe.Gruen, "Platz 1–2 → Achtelfinale"),
            new ZonenLegendenEintrag(ZonenFarbe.Amber, "Platz 3 → Achtelfinale (nur der beste)"));
    }
}
