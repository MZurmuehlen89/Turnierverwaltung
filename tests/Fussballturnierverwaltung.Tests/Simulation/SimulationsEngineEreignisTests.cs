using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using static Fussballturnierverwaltung.Tests.Simulation.SimulationsEngineTestdaten;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>
/// Reguläre Spielzeit der Schnellsimulation mit vorgegebener Zufallsfolge (#615): Torerwartung und Poisson-Ziehung.
/// Heimteam λ = 1,95 (e^-λ = 0,142), Gastteam λ = 0,975 (e^-λ = 0,377) — Poisson-Ziehung siehe
/// <see cref="SimulationsEngineEntscheidungTests"/>. Die ausführliche Simulation (Minute für Minute, #361) prüft
/// <see cref="SpielablaufTests"/>.
/// </summary>
public class SimulationsEngineEreignisTests
{
    private static SimulationsErgebnis Simulieren(
        VorgegebenerZufall zufall, Mannschaft? heim = null, Mannschaft? gast = null, params VerfahrenArt[] verfahren) =>
        SimulationsEngine.Simulieren(
            heim ?? HeimTeam(), gast ?? GastTeam(),
            VerfahrenKette(verfahren), ausfuehrlich: false, zufall);

    // ── Torerwartung und Poisson-Ziehung ──────────────────────────────────

    [Theory]
    [InlineData(0.15, 1)]
    [InlineData(0.14, 0)]
    public void Torerwartung_HeimteamDaheim(double ersterWert, int tore)
    {
        Simulieren(new VorgegebenerZufall([ersterWert], danach: 0.0))
            .HeimToreRegulaer.Should().Be(tore);
    }

    [Theory]
    [InlineData(0.38, 1)]
    [InlineData(0.37, 0)]
    public void Torerwartung_GastteamAuswaerts(double ersterWert, int tore)
    {
        Simulieren(new VorgegebenerZufall([0.0, ersterWert], danach: 0.0))
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

        Simulieren(new VorgegebenerZufall([ersterWert], danach: 0.0), angreifer, verteidiger)
            .HeimToreRegulaer.Should().Be(tore);
    }

    [Fact]
    public void Torerwartung_HoechstensFuenf()
    {
        // λ = 5 (e^-λ = 0,00674): 0,5^7 · 0,9 = 0,00703 liegt darüber, · 0,9 = 0,00633 darunter → 8 Tore.
        double[] werte = [0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.9, 0.9];
        var uebermaechtig = Team(1, off: 100, mid: 100, def: 100, heim: 100);
        var chancenlos = Team(2, off: 0, mid: 0, def: 0, ausw: 100);

        Simulieren(new VorgegebenerZufall(werte, danach: 0.0), uebermaechtig, chancenlos)
            .HeimToreRegulaer.Should().Be(8);
    }

    [Theory]
    // λ = 0,1 (e^-λ = 0,905)
    [InlineData(0.91, 1)]
    [InlineData(0.90, 0)]
    public void Torerwartung_MindestensEinZehntel(double ersterWert, int tore)
    {
        var uebermaechtig = Team(1, off: 100, mid: 100, def: 100, heim: 100);
        var chancenlos = Team(2, off: 0, mid: 0, def: 0, ausw: 100);

        Simulieren(new VorgegebenerZufall([0.0, ersterWert], danach: 0.0), uebermaechtig, chancenlos)
            .AuswaertsToreRegulaer.Should().Be(tore);
    }

    [Fact]
    public void Poisson_HoechstensVierundzwanzigTore()
    {
        var e = Simulieren(new VorgegebenerZufall([], danach: 0.9999999));

        (e.HeimToreRegulaer, e.AuswaertsToreRegulaer).Should().Be((24, 24));
    }

    // ── Entscheidungsverfahren aus Simulieren ─────────────────────────────

    [Fact]
    public void Sieger_NachNeunzigMinuten_KeinEntscheidungsverfahren()
    {
        var zufall = new VorgegebenerZufall([0.5, 0.0, /* Gast */ 0.0]);

        var e = Simulieren(zufall, verfahren: VerfahrenArt.Elfmeterschiessen);

        e.EntscheidungArt.Should().Be(EntscheidungArt.RegulaereSpielzeit);
        e.HeimElfmetertore.Should().BeNull();
        zufall.Verbraucht.Should().Be(3);
    }
}
