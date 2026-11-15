using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using static Fussballturnierverwaltung.Tests.Simulation.SimulationsEngineTestdaten;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>
/// Reguläre Spielzeit mit vorgegebener Zufallsfolge (#615): Torerwartung, Poisson-Ziehung, ausführliche Ereignisse
/// und Halbzeitstand. Heimteam λ = 1,95 (e^-λ = 0,142), Gastteam λ = 0,975 (e^-λ = 0,377) — Poisson-Ziehung siehe
/// <see cref="SimulationsEngineEntscheidungTests"/>.
/// Reihenfolge der Zufallswerte bei ausführlicher Simulation: Heimtore, Gasttore, Minuten der Heim-, dann der
/// Gasttore, Torstil je Tor (Stil, Konter, Zone), gelbe Karten Heim und Gast, deren Minuten, rote Karte Heim (mit
/// Minute), rote Karte Gast (mit Minute).
/// </summary>
public class SimulationsEngineEreignisTests
{
    private static readonly double[] _torstilOhneKonter = [0.0, 0.5, 0.0];

    private static SimulationsErgebnis Simulieren(
        VorgegebenerZufall zufall, bool ausfuehrlich = true, Mannschaft? heim = null, Mannschaft? gast = null,
        params VerfahrenArt[] verfahren) =>
        SimulationsEngine.Simulieren(
            heim ?? HeimTeam(), gast ?? GastTeam(),
            [.. verfahren.Select((art, i) => Verfahren(art, rang: i + 1))], ausfuehrlich, zufall);

    // ── Torerwartung und Poisson-Ziehung ──────────────────────────────────

    [Theory]
    [InlineData(0.15, 1)]
    [InlineData(0.14, 0)]
    public void Torerwartung_HeimteamDaheim(double ersterWert, int tore)
    {
        Simulieren(new VorgegebenerZufall([ersterWert], danach: 0.0), ausfuehrlich: false)
            .HeimToreRegulaer.Should().Be(tore);
    }

    [Theory]
    [InlineData(0.38, 1)]
    [InlineData(0.37, 0)]
    public void Torerwartung_GastteamAuswaerts(double ersterWert, int tore)
    {
        Simulieren(new VorgegebenerZufall([0.0, ersterWert], danach: 0.0), ausfuehrlich: false)
            .AuswaertsToreRegulaer.Should().Be(tore);
    }

    [Theory]
    [InlineData(0.15, 1)]
    [InlineData(0.14, 0)]
    public void Torerwartung_SchwacheAbwehrZaehltHoechstensAlsDreissigProzent(double ersterWert, int tore)
    {
        // Angriff 30 gegen Abwehr 20: die Abwehr zählt als 30 → Kräfteverhältnis 1, λ = 1,95 wie bei Gleichstand.
        var angreifer = Team(1, off: 30, mid: 30, heim: 100);
        var verteidiger = Team(2, mid: 20, def: 20);

        Simulieren(new VorgegebenerZufall([ersterWert], danach: 0.0), ausfuehrlich: false, angreifer, verteidiger)
            .HeimToreRegulaer.Should().Be(tore);
    }

    [Theory]
    // λ = 5 (e^-λ = 0,00674): 0,5^7 · 0,9 = 0,00703 liegt darüber, · 0,9 = 0,00633 darunter → 8 Tore.
    [InlineData(new[] { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.9, 0.9 }, 8)]
    public void Torerwartung_HoechstensFuenf(double[] werte, int tore)
    {
        var uebermaechtig = Team(1, off: 100, mid: 100, def: 100, heim: 100);
        var chancenlos = Team(2, off: 0, mid: 0, def: 0, ausw: 100);

        Simulieren(new VorgegebenerZufall(werte, danach: 0.0), ausfuehrlich: false, uebermaechtig, chancenlos)
            .HeimToreRegulaer.Should().Be(tore);
    }

    [Theory]
    // λ = 0,1 (e^-λ = 0,905)
    [InlineData(0.91, 1)]
    [InlineData(0.90, 0)]
    public void Torerwartung_MindestensEinZehntel(double ersterWert, int tore)
    {
        var uebermaechtig = Team(1, off: 100, mid: 100, def: 100, heim: 100);
        var chancenlos = Team(2, off: 0, mid: 0, def: 0, ausw: 100);

        Simulieren(new VorgegebenerZufall([0.0, ersterWert], danach: 0.0), ausfuehrlich: false, uebermaechtig, chancenlos)
            .AuswaertsToreRegulaer.Should().Be(tore);
    }

    [Fact]
    public void Poisson_HoechstensVierundzwanzigTore()
    {
        var e = Simulieren(new VorgegebenerZufall([], danach: 0.9999999), ausfuehrlich: false);

        (e.HeimToreRegulaer, e.AuswaertsToreRegulaer).Should().Be((24, 24));
    }

    // ── Entscheidungsverfahren aus Simulieren ─────────────────────────────

    [Fact]
    public void Sieger_NachNeunzigMinuten_KeinEntscheidungsverfahren()
    {
        var zufall = new VorgegebenerZufall([0.5, 0.0, /* Gast */ 0.0]);

        var e = Simulieren(zufall, ausfuehrlich: false, verfahren: VerfahrenArt.Elfmeterschiessen);

        e.EntscheidungArt.Should().Be(EntscheidungArt.RegulaereSpielzeit);
        e.HeimElfmetertore.Should().BeNull();
        zufall.Verbraucht.Should().Be(3);
    }

    [Fact]
    public void Gleichstand_EreignisseDerVerlaengerungFolgenDenenDerSpielzeit()
    {
        // 0:0 ohne Karten (vier Werte), dann trifft das Heimteam in Minute 91 per Golden Goal.
        var zufall = new VorgegebenerZufall([0.0, 0.0, 0.0, 0.0, 0.0, 0.0, /* Golden Goal */ 0.0, 0.5]);

        var e = Simulieren(zufall, verfahren: VerfahrenArt.GoldenGoal);

        e.EntscheidungArt.Should().Be(EntscheidungArt.GoldenGoal);
        e.HeimToreVerlaengerung.Should().Be(1);
        e.Ereignisse.Should().ContainSingle()
            .Which.Should().Be(new SimuliertesEreignis(91, EreignisArt.GoldenGoal, true, "Golden Goal"));
        (e.HalbzeitHeimTore, e.HalbzeitAuswaertsTore).Should().Be(Stand(0, 0));
    }

    // ── Ereignisse der regulären Spielzeit ────────────────────────────────

    [Fact]
    public void Halbzeitstand_ZaehltToreBisEinschliesslichMinute45UndNurTore()
    {
        // Heimtor in Minute 45, Gasttor in Minute 46, gelbe Karte Heim in Minute 10.
        var zufall = new VorgegebenerZufall(
        [
            0.5, 0.0, 0.5, 0.0,
            /* Torminuten */ 0.49, 0.5, .. _torstilOhneKonter, .. _torstilOhneKonter,
            /* Gelb Heim 1, Gast 0 */ 0.5, 0.0, 0.0, /* Minute */ 0.1,
            /* keine roten */ 0.0, 0.0
        ]);

        var e = Simulieren(zufall);

        e.Ereignisse.Select(x => (x.Minute, x.Art, x.IstHeim)).Should().Equal(
            (10, EreignisArt.GelbeKarte, true), (45, EreignisArt.Tor, true), (46, EreignisArt.Tor, false));
        (e.HalbzeitHeimTore, e.HalbzeitAuswaertsTore).Should().Be(Stand(1, 0));
    }

    [Fact]
    public void Ereignisse_NachMinuteSortiert()
    {
        // Heimtor in Minute 80, Gasttor in Minute 10.
        var zufall = new VorgegebenerZufall(
            [0.5, 0.0, 0.5, 0.0, /* Torminuten */ 0.88, 0.1, .. _torstilOhneKonter, .. _torstilOhneKonter],
            danach: 0.0);

        var e = Simulieren(zufall);

        e.Ereignisse.Select(x => (x.Minute, x.IstHeim)).Should().Equal((10, false), (80, true));
        (e.HalbzeitHeimTore, e.HalbzeitAuswaertsTore).Should().Be(Stand(0, 1));
    }

    [Theory]
    [InlineData(0.0, 1)]
    [InlineData(0.9999, 90)]
    public void Torminuten_ZwischenEinsUndNeunzig(double wert, int minute)
    {
        var zufall = new VorgegebenerZufall([0.5, 0.0, 0.0, wert], danach: 0.0);

        Simulieren(zufall).Ereignisse.Should().ContainSingle().Which.Minute.Should().Be(minute);
    }

    [Theory]
    [InlineData(0.0, 0.149, 0.9999, "Tor per Kopfball nach Konter (unten rechts)")]
    [InlineData(0.9999, 0.151, 0.0, "Tor per Direktabnahme (oben links)")]
    public void Torbeschreibung_StilKonterUndZone(double stil, double konter, double zone, string erwartet)
    {
        var zufall = new VorgegebenerZufall([0.5, 0.0, 0.0, /* Minute */ 0.0, stil, konter, zone], danach: 0.0);

        Simulieren(zufall).Ereignisse.Should().ContainSingle().Which.Beschreibung.Should().Be(erwartet);
    }

    [Fact]
    public void GelbeKarten_ZweiEinhalbJeTeamUndNeunzigMinuten()
    {
        // λ = 2,5 (e^-λ = 0,0821): 0,3 · 0,28 = 0,084 liegt darüber (zwei Karten), 0,3 · 0,27 = 0,081 darunter (eine).
        var zufall = new VorgegebenerZufall(
        [
            0.0, 0.0,
            /* Gelb Heim */ 0.3, 0.28, 0.0, /* Gelb Gast */ 0.3, 0.27,
            /* Minuten Heim */ 0.0, 0.5, /* Gast */ 0.9999,
            /* keine roten */ 0.0, 0.0
        ]);

        var e = Simulieren(zufall);

        e.Ereignisse.Should().Equal(
            new SimuliertesEreignis(1, EreignisArt.GelbeKarte, true, "Gelbe Karte"),
            new SimuliertesEreignis(46, EreignisArt.GelbeKarte, true, "Gelbe Karte"),
            new SimuliertesEreignis(90, EreignisArt.GelbeKarte, false, "Gelbe Karte"));
        zufall.Verbraucht.Should().Be(12);
    }

    [Theory]
    // λ = 0,15 (e^-λ = 0,861)
    [InlineData(new[] { 0.87, 0.0, 0.9999, 0.85 }, 90, true)]
    [InlineData(new[] { 0.85, 0.87, 0.0, 0.9999 }, 90, false)]
    [InlineData(new[] { 0.85, 0.87, 0.0, 0.0 }, 1, false)]
    public void RoteKarte_FuenfzehnProzentJeTeamUndNeunzigMinuten(double[] roteKarten, int minute, bool heim)
    {
        var zufall = new VorgegebenerZufall([0.0, 0.0, /* keine gelben */ 0.0, 0.0, .. roteKarten]);

        var e = Simulieren(zufall);

        e.Ereignisse.Should().ContainSingle()
            .Which.Should().Be(new SimuliertesEreignis(minute, EreignisArt.RoteKarte, heim, "Rote Karte"));
        zufall.Verbraucht.Should().Be(4 + roteKarten.Length);
    }

    [Theory]
    // Verlängerung (30 Minuten): gelb λ = 0,833 (e^-λ = 0,435), rot λ = 0,05 (e^-λ = 0,951).
    [InlineData(0.44, 0.96, true)]
    [InlineData(0.43, 0.95, false)]
    public void Karten_InDerVerlaengerungAnteiligZurDauer(double gelb, double rot, bool karten)
    {
        double[] gelbHeim = karten ? [gelb, 0.0] : [gelb];
        double[] minuteGelb = karten ? [0.0] : [];
        double[] rotHeim = karten ? [rot, 0.0, /* Minute */ 0.9999] : [rot];
        var zufall = new VorgegebenerZufall(
            [0.0, 0.0, .. gelbHeim, /* Gelb Gast */ 0.0, .. minuteGelb, .. rotHeim, /* Rot Gast */ 0.0]);

        var e = SimulationsEngine.EntscheidungsverfahrenAnwenden(
            HeimTeam(), GastTeam(), [Verfahren(VerfahrenArt.VerlaengerungKlassisch)], true, zufall);

        (int, EreignisArt)[] erwartet = karten ? [(91, EreignisArt.GelbeKarte), (120, EreignisArt.RoteKarte)] : [];
        e.Ereignisse.Select(x => (x.Minute, x.Art)).Should().Equal(erwartet);
    }
}
