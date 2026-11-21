using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Enums;
using static Fussballturnierverwaltung.Tests.Simulation.SimulationsEngineTestdaten;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Nervenstärke im Elfmeterschießen (#669).</summary>
public class NervenstaerkeTests
{
    // ── K1 Normal bitgleich ───────────────────────────────────────────────
    // Die Referenzwerte wurden vor der Änderung auf main erzeugt: Stand der Schüsse, Zahl der Ereignisse und der
    // nächste Wert des Zufallsstroms (Zufallsverbrauch). Mit Nervenstärke normal muss alles identisch bleiben.

    private static string Elfmeterschiessen(int seed, int offensivHeim, int offensivGast)
    {
        var rng = new Random(seed);
        var e = SimulationsEngine.EntscheidungsverfahrenAnwenden(
            Team(1, off: offensivHeim), Team(2, off: offensivGast),
            VerfahrenKette(VerfahrenArt.Elfmeterschiessen), ausfuehrlich: true, rng);
        return $"{e.HeimElf}:{e.GastElf}/{e.Ereignisse.Count}/{rng.Next()}";
    }

    [Theory]
    [InlineData(1, 70, 70, "4:2/8/217468053")]
    [InlineData(2, 70, 70, "3:4/10/1643613848")]
    [InlineData(3, 70, 70, "5:4/12/1492527440")]
    [InlineData(4, 70, 70, "4:3/10/512880076")]
    [InlineData(5, 70, 70, "4:2/8/1280186417")]
    [InlineData(6, 80, 60, "2:3/10/1529629951")]
    [InlineData(7, 80, 60, "4:2/8/1811545599")]
    [InlineData(8, 80, 60, "2:4/8/2077225190")]
    [InlineData(9, 60, 85, "4:5/12/1760332294")]
    [InlineData(10, 60, 85, "0:3/6/830204383")]
    [InlineData(11, 60, 85, "4:2/9/708597731")]
    [InlineData(12, 75, 75, "4:5/10/284912282")]
    [InlineData(13, 75, 75, "4:2/9/144887988")]
    [InlineData(14, 75, 75, "4:1/7/1000210937")]
    [InlineData(15, 75, 75, "3:2/10/736295271")]
    public void Normal_RechnetBitgleichWieVorDerNervenstaerke(int seed, int offHeim, int offGast, string erwartet)
        => Elfmeterschiessen(seed, offHeim, offGast).Should().Be(erwartet);
}
