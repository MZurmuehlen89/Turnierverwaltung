using System.Diagnostics;
using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using static Fussballturnierverwaltung.Tests.Auslosungen.AuslosungTestdaten;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>Ziehung von Hand (#482): Ansage, gesperrte Ziele mit Grund, Vorausschau, Zurücknehmen, Ergebnis.</summary>
public class ManuelleZiehungTests
{
    private static readonly IReadOnlyDictionary<int, string> _keineGruppen = new Dictionary<int, string>();
    private static readonly IReadOnlyDictionary<int, int> _keineKoepfe = new Dictionary<int, int>();

    private static RegelKontext Regeln(
        bool keinGleichesLand = false, bool freilos = false, bool konfoederation = false,
        Spielklassen? unterklassige = null, bool untererTopf = false) =>
        new(keinGleichesLand, false, false, false, freilos, konfoederation, false, unterklassige, untererTopf);

    private static ManuelleZiehung Starten(
        Rundenart rundenart, RegelKontext regeln, IEnumerable<Lostopf> toepfe, int? gruppenAnzahl = null,
        Gruppenposition position = Gruppenposition.NachTopf, IReadOnlyDictionary<int, int>? koepfe = null)
    {
        var auslosung = NeueAuslosung(rundenart, Auslosungsart.Manuell, toepfe, gruppenAnzahl);
        auslosung.Gruppenposition = position;
        return ManuelleZiehung.Starten(
            auslosung, new LosVorbereitung(regeln, _keineGruppen, koepfe ?? _keineKoepfe), Qualifikanten.Keine);
    }

    private static int Idx(ManuelleZiehung z, int mannschaftId) =>
        z.Teilnehmer.Single(t => t.Eintrag.MannschaftId == mannschaftId).Index;

    private static void Ziehe(ManuelleZiehung z, int mannschaftId) => z.Waehlen(Idx(z, mannschaftId));

    private static string? Grund(ManuelleZiehung z, int wert) => z.Ansage.Optionen.Single(o => o.Wert == wert).SperrGrund;

    private static Mannschaft TeamIm(int id, int landId, int verbandId) =>
        new() { Id = id, Name = $"Team {id}", LandId = landId, Land = new Land { Id = landId, VerbandId = verbandId } };

    // ── K.-o. ─────────────────────────────────────────────────────────────────

    [Fact]
    public void KoRunde_EinTopf_TeamDannGegner_ZuerstGezogenerSpieltZuHause()
    {
        var z = Starten(Rundenart.KoRunde, Regeln(), [TopfMitTeams(1, 1, 4)]);

        z.Ansage.Art.Should().Be(ZiehungsSchrittArt.Team);
        z.Ansage.Text.Should().Be("Ziehe ein Team aus Topf 1");
        z.Ansage.Optionen.Should().HaveCount(4).And.OnlyContain(o => o.IstFrei);

        Ziehe(z, 3);
        z.Ansage.Art.Should().Be(ZiehungsSchrittArt.Gegner);
        z.Ansage.Text.Should().Be("Team 3 – ziehe den Gegner");
        z.Ansage.Optionen.Select(o => o.Wert).Should().BeEquivalentTo([Idx(z, 1), Idx(z, 2), Idx(z, 4)]);
        z.Paarungen.Should().ContainSingle().Which.IstOffen.Should().BeTrue();

        Ziehe(z, 1);
        Ziehe(z, 4);
        Ziehe(z, 2);

        z.IstFertig.Should().BeTrue();
        z.Ergebnis().Select(p => (p.PaarungsNummer, p.HeimMannschaftId, p.AuswaertsMannschaftId))
            .Should().Equal((1, 3, 1), (2, 4, 2));
    }

    [Fact]
    public void KoRunde_GegnerGesperrtDurchRegelUndVorausschau()
    {
        // 1 und 2 aus demselben Land: zieht 3 die 4, bleiben 1 gegen 2 — nicht erlaubt.
        var toepfe = new[] { Topf(1, Eintrag(Team(1, 10)), Eintrag(Team(2, 10)), Eintrag(Team(3)), Eintrag(Team(4))) };
        var z = Starten(Rundenart.KoRunde, Regeln(keinGleichesLand: true), toepfe);

        Ziehe(z, 3);
        Grund(z, Idx(z, 4)).Should().Be(ManuelleZiehung.NICHT_LOESBAR);
        Grund(z, Idx(z, 1)).Should().BeNull();

        z.Zuruecknehmen();
        Ziehe(z, 1);
        Grund(z, Idx(z, 2)).Should().Be("Kein gleiches Land: Team 1 darf nicht auf Team 2 treffen.");
    }

    [Fact]
    public void GesperrteOption_LaesstSichNichtWaehlen()
    {
        var toepfe = new[] { Topf(1, Eintrag(Team(1, 10)), Eintrag(Team(2, 10)), Eintrag(Team(3)), Eintrag(Team(4))) };
        var z = Starten(Rundenart.KoRunde, Regeln(keinGleichesLand: true), toepfe);
        Ziehe(z, 1);

        var wahl = () => Ziehe(z, 2);

        wahl.Should().Throw<InvalidOperationException>().WithMessage("Kein gleiches Land*");
    }

    [Fact]
    public void Zuruecknehmen_MehrfachBisZumStart()
    {
        var z = Starten(Rundenart.KoRunde, Regeln(), [TopfMitTeams(1, 1, 4)]);
        Ziehe(z, 1);
        Ziehe(z, 2);
        Ziehe(z, 3);

        z.Zuruecknehmen();
        z.Ansage.Art.Should().Be(ZiehungsSchrittArt.Team);
        z.Paarungen.Should().ContainSingle();

        z.Zuruecknehmen();
        z.Zuruecknehmen();
        z.IstBegonnen.Should().BeFalse();
        z.Paarungen.Should().BeEmpty();
        z.Ansage.Optionen.Should().HaveCount(4);
    }

    [Fact]
    public void KoRunde_ZweiToepfe_GegnerAusDemAnderenTopf_UeberhangUntereinander()
    {
        // DFB-Pokal ab der 2. Runde: 2 Amateure gegen 6 Profis — danach die übrigen 4 Profis untereinander.
        var z = Starten(Rundenart.KoRunde, Regeln(), [TopfMitTeams(1, 1, 2), TopfMitTeams(2, 11, 6)]);

        z.Ansage.Optionen.Select(o => o.Wert).Should().BeEquivalentTo([Idx(z, 1), Idx(z, 2)]);
        Ziehe(z, 1);
        z.Ansage.Text.Should().Be("Team 1 – ziehe den Gegner aus Topf 2");
        z.Ansage.Optionen.Should().HaveCount(6);
        Ziehe(z, 11);
        Ziehe(z, 2);
        Ziehe(z, 12);

        z.Ansage.Text.Should().Be("Die übrigen Teams werden untereinander gezogen – ziehe ein Team");
        z.Ansage.Optionen.Select(o => o.Wert).Should().BeEquivalentTo([.. new[] { 13, 14, 15, 16 }.Select(id => Idx(z, id))]);
        Ziehe(z, 13);
        Ziehe(z, 14);
        Ziehe(z, 15);
        Ziehe(z, 16);
        z.IstFertig.Should().BeTrue();
        z.Ergebnis().Should().HaveCount(4);
    }

    [Fact]
    public void KoRunde_UngeradeMitFreilos_LetztesTeamBekommtFreilos()
    {
        var z = Starten(Rundenart.KoRunde, Regeln(freilos: true), [TopfMitTeams(1, 1, 3)]);

        Ziehe(z, 2);
        Ziehe(z, 3);

        z.IstFertig.Should().BeTrue();
        var freilos = z.Ergebnis().Last();
        freilos.HeimMannschaftId.Should().Be(1);
        freilos.AuswaertsMannschaftId.Should().BeNull();
        z.Paarungen.Last().IstFreilos.Should().BeTrue();
    }

    [Fact]
    public void KoRunde_UngeradeOhneFreilos_StartetNicht()
    {
        var start = () => Starten(Rundenart.KoRunde, Regeln(), [TopfMitTeams(1, 1, 3)]);

        start.Should().Throw<InvalidOperationException>().WithMessage("*Freilos möglich*");
    }

    [Fact]
    public void KoRunde_NichtErfuellbareRegeln_StartetNicht()
    {
        var toepfe = new[] { Topf(1, Eintrag(Team(1, 10)), Eintrag(Team(2, 10))) };

        var start = () => Starten(Rundenart.KoRunde, Regeln(keinGleichesLand: true), toepfe);

        start.Should().Throw<InvalidOperationException>().WithMessage("Keine gültige Auslosung*");
    }

    [Fact]
    public void KoRunde_HeimrechtFuerUnterklassige_GehtDerZiehungVor()
    {
        // Team 1 spielt in der 4. Klasse, Team 2 in der 1. — Profigrenze 2. Liga.
        var klassen = new Spielklassen(new Dictionary<int, int> { [1] = 4, [2] = 1 }, Profigrenze: 2);
        var z = Starten(Rundenart.KoRunde, Regeln(unterklassige: klassen), [TopfMitTeams(1, 1, 2)]);

        Ziehe(z, 2);
        Ziehe(z, 1);

        z.Ergebnis().Single().HeimMannschaftId.Should().Be(1);
    }

    [Fact]
    public void KoRunde_HeimrechtFuerUnterenTopf_GehtDerZiehungVor()
    {
        var z = Starten(Rundenart.KoRunde, Regeln(untererTopf: true), [TopfMitTeams(1, 1, 1), TopfMitTeams(2, 11, 1)]);

        Ziehe(z, 1);
        Ziehe(z, 11);

        z.Ergebnis().Single().HeimMannschaftId.Should().Be(11);
    }

    // ── Gruppenphase ──────────────────────────────────────────────────────────

    [Fact]
    public void Gruppenphase_JeDurchgangEinTeamJeGruppe_RegelnUndVorausschau()
    {
        // Team 1 und Team 3 aus demselben Land.
        var toepfe = new[]
        {
            Topf(1, Eintrag(Team(1, 10)), Eintrag(Team(2))),
            Topf(2, Eintrag(Team(3, 10)), Eintrag(Team(4)))
        };
        var z = Starten(Rundenart.Gruppenphase, Regeln(keinGleichesLand: true), toepfe, gruppenAnzahl: 2);

        Ziehe(z, 1);
        z.Ansage.Text.Should().Be("Team 1 – ziehe die Gruppe");
        z.Waehlen(1);
        Ziehe(z, 2);
        Grund(z, 1).Should().Be("Gruppe A hat in diesem Durchgang schon ein Team aus Topf 1.");
        z.Waehlen(2);

        z.Ansage.Text.Should().Be("Ziehe ein Team aus Topf 2");
        Ziehe(z, 4);
        Grund(z, 2).Should().Be(ManuelleZiehung.NICHT_LOESBAR);
        z.Zuruecknehmen();
        Ziehe(z, 3);
        Grund(z, 1).Should().Be("Kein gleiches Land: Team 1 ist schon in Gruppe A.");
        z.Waehlen(2);
        Ziehe(z, 4);
        z.Waehlen(1);

        z.IstFertig.Should().BeTrue();
        z.Ergebnis().Select(p => (p.PaarungsNummer, p.HeimMannschaftId)).Should().Equal((1, 1), (1, 4), (2, 2), (2, 3));
    }

    [Fact]
    public void Gruppenphase_Gruppenkopf_StehtVorabFest()
    {
        var z = Starten(Rundenart.Gruppenphase, Regeln(), [TopfMitTeams(1, 1, 2), TopfMitTeams(2, 3, 2)],
            gruppenAnzahl: 2, koepfe: new Dictionary<int, int> { [2] = 1 });

        z.Gruppen[1].Plaetze[0].Should().Be(Idx(z, 2));
        z.Ansage.Optionen.Select(o => o.Wert).Should().Equal(Idx(z, 1));
        Ziehe(z, 1);
        Grund(z, 2).Should().StartWith("Gruppe B hat in diesem Durchgang schon ein Team");
    }

    [Fact]
    public void Gruppenphase_AlleGelost_PositionWirdNachDerGruppeGezogen()
    {
        var z = Starten(Rundenart.Gruppenphase, Regeln(), [TopfMitTeams(1, 1, 2), TopfMitTeams(2, 3, 2)],
            gruppenAnzahl: 2, position: Gruppenposition.AlleGelost);

        Ziehe(z, 1);
        z.Waehlen(1);
        z.Ansage.Art.Should().Be(ZiehungsSchrittArt.Position);
        z.Ansage.Text.Should().Be("Team 1 kommt in Gruppe A – ziehe die Position");
        z.Waehlen(2);

        z.Gruppen[0].Plaetze.Should().Equal(null, Idx(z, 1));
        Ziehe(z, 2);
        z.Waehlen(2);
        z.Waehlen(1);
        Ziehe(z, 3);
        z.Waehlen(1);
        Grund(z, 2).Should().Be("Position 2 ist in Gruppe A schon vergeben.");
        z.Waehlen(1);
        Ziehe(z, 4);
        z.Waehlen(2);
        z.Waehlen(2);

        z.Ergebnis().Select(p => p.HeimMannschaftId).Should().Equal(3, 1, 2, 4);
    }

    [Fact]
    public void Gruppenphase_NachTopf_KeinPositionsZug()
    {
        var z = Starten(Rundenart.Gruppenphase, Regeln(), [TopfMitTeams(1, 1, 2), TopfMitTeams(2, 3, 2)],
            gruppenAnzahl: 2);

        Ziehe(z, 1);
        z.Waehlen(1);

        z.Ansage.Art.Should().Be(ZiehungsSchrittArt.Team);
        z.Gruppen[0].Plaetze[0].Should().Be(Idx(z, 1));
    }

    [Fact]
    public void Gruppenphase_Konfoederationsverteilung_SperrtVolleGruppe()
    {
        // Zwei UEFA-Teams (Verband 1) auf zwei Gruppen: je Gruppe höchstens eins.
        var toepfe = new[]
        {
            Topf(1, Eintrag(TeamIm(1, 10, 1)), Eintrag(TeamIm(2, 20, 2))),
            Topf(2, Eintrag(TeamIm(3, 30, 1)), Eintrag(TeamIm(4, 40, 3)))
        };
        var z = Starten(Rundenart.Gruppenphase, Regeln(konfoederation: true), toepfe, gruppenAnzahl: 2);
        Ziehe(z, 1);
        z.Waehlen(1);
        Ziehe(z, 2);
        z.Waehlen(2);

        Ziehe(z, 3);

        Grund(z, 1).Should().StartWith("Konföderationsverteilung: Gruppe A");
    }

    // ── Liga ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Liga_ZiehtDieReihenfolge()
    {
        var z = Starten(Rundenart.Liga, Regeln(), [TopfMitTeams(1, 1, 3)]);

        z.Ansage.Text.Should().Be("Ziehe das Team für Schlüsselzahl 1");
        Ziehe(z, 3);
        Ziehe(z, 1);
        Ziehe(z, 2);

        z.Ergebnis().Select(p => (p.PaarungsNummer, p.HeimMannschaftId)).Should().Equal((1, 3), (1, 1), (1, 2));
    }

    // ── Schweizer System ──────────────────────────────────────────────────────

    // #627: die Ziehung von Hand paart wie die automatische Auslosung — Topf 1 gegen Topf 2, Überhang zuletzt
    // mit Freilos (im Schweizer System immer möglich, #548).
    [Fact]
    public void SchweizerSystem_ZweiToepfeUngerade_PaartWieDieAutomatischeAuslosung()
    {
        static Lostopf[] Toepfe() => [TopfMitTeams(1, 1, 3), TopfMitTeams(2, 4, 2)];
        var z = Starten(Rundenart.SchweizerSystem, Regeln(freilos: true), Toepfe());

        z.IstGruppenmodus.Should().BeFalse();
        z.Ansage.Text.Should().Be("Ziehe ein Team aus Topf 1");
        Ziehe(z, 2);
        Ziehe(z, 5);
        Ziehe(z, 1);
        Ziehe(z, 4);

        z.IstFertig.Should().BeTrue();
        z.Ergebnis().Select(p => (p.PaarungsNummer, p.HeimMannschaftId, p.AuswaertsMannschaftId))
            .Should().Equal((1, 2, 5), (2, 1, 4), (3, 3, null));
        z.Paarungen[^1].IstFreilos.Should().BeTrue();

        var automatisch = Losverfahren.Ausloesen(
            NeueAuslosung(Rundenart.SchweizerSystem, Auslosungsart.NachToepfen, Toepfe()),
            new LosVorbereitung(Regeln(freilos: true), _keineGruppen, _keineKoepfe), new Random(1));
        automatisch.Select(e => (e.PaarungsNummer, HeimAusTopf1: e.HeimMannschaftId <= 3,
                                 MitGegner: e.AuswaertsMannschaftId.HasValue, e.IstFreilos))
            .Should().Equal((1, true, true, false), (2, true, true, false), (3, true, false, true));
    }

    // ── Belastung ─────────────────────────────────────────────────────────────

    // Wer immer die erste freie Option wählt, muss jede Ziehung zu Ende bringen — und das zügig.
    [Fact]
    public void WmFeld_48TeamsMitLandUndVerband_GehtImmerAuf()
    {
        var zufall = new Random(7);
        var toepfe = Enumerable.Range(0, 4).Select(t => Topf(t + 1,
            [.. Enumerable.Range(t * 12 + 1, 12).Select(id => Eintrag(TeamIm(id, 100 + zufall.Next(30), zufall.Next(3))))]));
        var z = Starten(Rundenart.Gruppenphase, Regeln(keinGleichesLand: true, konfoederation: true), toepfe,
            gruppenAnzahl: 12);

        var uhr = Stopwatch.StartNew();
        while (!z.IstFertig) z.Waehlen(z.Ansage.Optionen.First(o => o.IstFrei).Wert);

        z.Ergebnis().Should().HaveCount(48);
        uhr.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Pokal_64TeamsMitLand_GehtImmerAuf()
    {
        var zufall = new Random(3);
        var toepfe = new[] { Topf(1, [.. Enumerable.Range(1, 64).Select(id => Eintrag(Team(id, 100 + zufall.Next(8))))]) };
        var z = Starten(Rundenart.KoRunde, Regeln(keinGleichesLand: true), toepfe);

        var uhr = Stopwatch.StartNew();
        while (!z.IstFertig) z.Waehlen(z.Ansage.Optionen.First(o => o.IstFrei).Wert);

        z.Ergebnis().Should().HaveCount(32);
        // Einzeln rund 0,7 s, unter Last bis zum Zehnfachen (#549). Die Grenze fängt eine Vorausschau ab, die ins
        // Zurücksetzen gerät (dann Minuten) — keine langsame Maschine.
        uhr.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30));
    }
}
