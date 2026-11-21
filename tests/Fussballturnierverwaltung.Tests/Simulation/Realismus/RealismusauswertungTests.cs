using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Simulation.Realismus;

public class RealismusauswertungTests
{
    private static SimuliertesEreignis Tor(int minute, bool heim, int? nachspiel = null) =>
        new(minute, EreignisArt.Tor, heim, "Tor", nachspiel);

    private static SimuliertesSpiel Spiel(int heimTore, int gastTore, params SimuliertesEreignis[] ereignisse) =>
        new(1, 2, 0,
            new SimulationsErgebnis(heimTore, gastTore, null, null, null, null,
                EntscheidungArt.RegulaereSpielzeit, ereignisse),
            new PaarungsDaten(0.5, 0.3, 1.5, 1.2, 0.5, 0.3));

    [Fact]
    public void Stand_NachspielzeitDerErstenHalbzeitZaehltZurPause()
    {
        // 45+2 trägt Minute 45 und gehört zum Halbzeitstand, ein Tor in Minute 46 nicht
        var ereignisse = new[] { Tor(20, true), Tor(45, false, nachspiel: 2), Tor(46, true) };
        Realismusauswertung.Stand(ereignisse, 45).Should().Be((1, 1));
        Realismusauswertung.Stand(ereignisse, 60).Should().Be((2, 1));
        Realismusauswertung.Stand(ereignisse, 19).Should().Be((0, 0));
    }

    [Fact]
    public void Stand_ZaehltNurTore()
    {
        var ereignisse = new[] { Tor(10, true), new SimuliertesEreignis(12, EreignisArt.RoteKarte, false, "Rot") };
        Realismusauswertung.Stand(ereignisse, 90).Should().Be((1, 0));
    }

    [Fact]
    public void TorminutenAnteile_NachspielzeitZaehltZu45Und90()
    {
        var spiel = Spiel(3, 1, Tor(1, true), Tor(15, true), Tor(45, false, 3), Tor(90, true, 4));
        var anteile = Realismusauswertung.TorminutenAnteile([spiel]);
        anteile.Should().Equal(0.5, 0, 0.25, 0, 0, 0.25); // 1 und 15 → Abschnitt 1; 45 → 3; 90 → 6
        Realismusauswertung.AnteilErsteHalbzeit([spiel]).Should().Be(0.75);
        Realismusauswertung.AnteilNachspielzeit([spiel]).Should().Be(0.5);
    }

    [Fact]
    public void AnteilMitTorAb_NachspielzeitZaehltRoteKarteNicht()
    {
        var spaetesTor = Spiel(1, 0, Tor(90, true, 3));
        var fruehesTor = Spiel(1, 0, Tor(84, true));
        var nurRot = Spiel(0, 0, new SimuliertesEreignis(88, EreignisArt.RoteKarte, true, "Rot"));
        Realismusauswertung.AnteilMitTorAb([spaetesTor, fruehesTor, nurRot, Spiel(0, 0)], 85).Should().Be(0.25);
    }

    [Fact]
    public void LaengsteTorlosePhase_ZaehltAnfangUndEnde()
    {
        Realismusauswertung.LaengsteTorlosePhase([Tor(30, true), Tor(50, false)]).Should().Be(40);
        Realismusauswertung.LaengsteTorlosePhase([]).Should().Be(90);
    }

    [Fact]
    public void FuehrungsAusgang_ZaehltAusSichtDesFuehrenden()
    {
        var heimFuehrtUndGewinnt = Spiel(2, 0, Tor(10, true), Tor(70, true));
        var gastFuehrtUndVerliert = Spiel(2, 1, Tor(10, false), Tor(50, true), Tor(80, true));
        var heimFuehrtUndRemis = Spiel(1, 1, Tor(10, true), Tor(60, false));
        var ohneFuehrung = Spiel(0, 0);

        var ausgang = Realismusauswertung.FuehrungsAusgang(
            [heimFuehrtUndGewinnt, gastFuehrtUndVerliert, heimFuehrtUndRemis, ohneFuehrung], minute: 45, vorsprung: 1);

        ausgang.Anzahl.Should().Be(3);
        ausgang.Sieg.Should().BeApproximately(1.0 / 3, 1e-12);
        ausgang.Remis.Should().BeApproximately(1.0 / 3, 1e-12);
        ausgang.Niederlage.Should().BeApproximately(1.0 / 3, 1e-12);
    }

    [Fact]
    public void FuehrungsAusgang_FilterUndLeereMenge()
    {
        var spiel = Spiel(2, 0, Tor(10, true), Tor(70, true));
        Realismusauswertung.FuehrungsAusgang([spiel], 45, 1, (_, fuehrerIstHeim) => !fuehrerIstHeim)
            .Should().Be(new Fuehrungsausgang(0, 0, 0, 0));
        Realismusauswertung.FuehrungsAusgang([spiel], 45, 2).Anzahl.Should().Be(0);
    }

    [Fact]
    public void FuehrungsAusgang_Mindestens_ZaehltGroessereVorspruengeMit()
    {
        var dreiNull = Spiel(3, 0, Tor(10, true), Tor(20, true), Tor(30, true));
        var gastVierNull = Spiel(2, 4, Tor(10, false), Tor(11, false), Tor(12, false), Tor(13, false), Tor(50, true), Tor(60, true));

        Realismusauswertung.FuehrungsAusgang([dreiNull, gastVierNull], 45, 3).Anzahl.Should().Be(1);
        var mindestens = Realismusauswertung.FuehrungsAusgang([dreiNull, gastVierNull], 45, 3, mindestens: true);
        mindestens.Anzahl.Should().Be(2);
        mindestens.Sieg.Should().Be(1);
    }

    private static SimuliertesSpiel MitPause(int heimPause, int gastPause, int heimTore, int gastTore) =>
        new(1, 2, 0,
            new SimulationsErgebnis(heimTore, gastTore, null, null, null, null,
                EntscheidungArt.RegulaereSpielzeit, [], heimPause, gastPause),
            new PaarungsDaten(0.5, 0.3, 1.5, 1.2, 0.5, 0.3));

    [Fact]
    public void ToreZweiteHalbzeit_AusSichtDesFuehrendenZurPause()
    {
        var heimFuehrt = MitPause(1, 0, 2, 2);  // 2. HZ: Führender 1, Zurückliegender 2
        var gastFuehrt = MitPause(0, 1, 1, 1);  // 2. HZ: Führender 0, Zurückliegender 1
        var gleich     = MitPause(1, 1, 3, 1);  // Gleichstand: Heim 2, Gast 0

        Realismusauswertung.ToreZweiteHalbzeit([heimFuehrt, gastFuehrt, gleich], 1).Should().Be((0.5, 1.5));
        Realismusauswertung.ToreZweiteHalbzeit([heimFuehrt, gastFuehrt, gleich], 0).Should().Be((2.0, 0.0));
        Realismusauswertung.ToreZweiteHalbzeit([heimFuehrt, gleich], 2).Should().Be((0.0, 0.0), "keine Spiele");
        Realismusauswertung.ToreZweiteHalbzeit([heimFuehrt, gastFuehrt, gleich], 1, mindestens: true).Should().Be((0.5, 1.5));
    }

    [Fact]
    public void RoteKarten_ZaehltJeMannschaftNurInRegulaererSpielzeit()
    {
        var spiel = Spiel(0, 0,
            new SimuliertesEreignis(30, EreignisArt.RoteKarte, true, "Rot"),
            new SimuliertesEreignis(40, EreignisArt.RoteKarte, false, "Rot"),
            new SimuliertesEreignis(95, EreignisArt.RoteKarte, false, "Rot"));
        Realismusauswertung.RoteKarten(spiel).Should().Be((1, 1));
    }

    [Theory]
    [InlineData(0.30, 0.30)] // links außerhalb: Randwert
    [InlineData(0.40, 0.30)] // Stützstelle
    [InlineData(0.45, 0.25)] // Mitte zwischen 0,40 und 0,50
    [InlineData(0.55, 0.15)] // Mitte zwischen 0,50 und 0,60
    [InlineData(0.90, 0.10)] // rechts außerhalb: Randwert
    public void Interpolieren_LinearZwischenStuetzstellenRandwertAussen(double x, double erwartet)
    {
        (double, double)[] stuetzstellen = [(0.40, 0.30), (0.50, 0.20), (0.60, 0.10)];
        Realismusauswertung.Interpolieren(stuetzstellen, x).Should().BeApproximately(erwartet, 1e-12);
    }

    [Fact]
    public void Anteil_ZaehltErfuellteBedingung()
    {
        var spiele = new[] { Spiel(1, 0), Spiel(0, 0), Spiel(2, 2), Spiel(0, 1) };
        Realismusauswertung.Anteil(spiele, s => s.HeimTore == s.GastTore).Should().Be(0.5);
    }

    // ── Chancen (#431, K3) ────────────────────────────────────────────────

    private static SimuliertesEreignis Chance(EreignisArt art, Chancenart chancenart, int minute = 10, int? nachspiel = null) =>
        new(minute, art, true, "x", nachspiel, chancenart);

    [Fact]
    public void Chancen_ZaehltGrosschancenInklusiveElfmeterUndDerenTore()
    {
        var spiel = Spiel(2, 0,
            Chance(EreignisArt.Tor, Chancenart.Grosschance),           // Großchance, Tor
            Chance(EreignisArt.Grosschance, Chancenart.Grosschance),   // Großchance vergeben
            Chance(EreignisArt.Tor, Chancenart.Elfmeter),              // Elfmeter, Tor
            Chance(EreignisArt.Grosschance, Chancenart.Elfmeter),      // Elfmeter vergeben
            Chance(EreignisArt.Tor, Chancenart.Normal));               // normales Tor: keine Großchance

        var c = Realismusauswertung.Chancen([spiel]);

        c.Should().Be(new Chancenbilanz(Spiele: 1, Grosschancen: 4, GrosschancenTore: 2, Elfmeter: 2, ElfmeterTore: 1, Tore: 3));
        c.GrosschancenJeTeam.Should().Be(2.0);
        c.Verwertung.Should().Be(0.5);
        c.ElfmeterJeSpiel.Should().Be(2.0);
        c.ElfmeterQuote.Should().Be(0.5);
        c.AnteilElfmeterTore.Should().BeApproximately(1.0 / 3, 1e-12);
    }

    [Fact]
    public void Chancen_ZaehltNurRegulaereSpielzeitUndIgnoriertGoldenGoalUndAltdaten()
    {
        var spiel = Spiel(0, 0,
            Chance(EreignisArt.Grosschance, Chancenart.Grosschance, minute: 91),
            Chance(EreignisArt.GoldenGoal, Chancenart.Normal, minute: 100),
            new SimuliertesEreignis(20, EreignisArt.Tor, true, "Tor ohne Chancenart"));

        var c = Realismusauswertung.Chancen([spiel]);

        c.Should().Be(new Chancenbilanz(1, 0, 0, 0, 0, 0));
    }

    [Fact]
    public void Chancen_OhneElfmeterOderGrosschancen_GibtNullStattDivisionDurchNull()
    {
        var c = Realismusauswertung.Chancen([Spiel(0, 0)]);

        (c.Verwertung, c.ElfmeterQuote, c.AnteilElfmeterTore).Should().Be((0.0, 0.0, 0.0));
    }
}
