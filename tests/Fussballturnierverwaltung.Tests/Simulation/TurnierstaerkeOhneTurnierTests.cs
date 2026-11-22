using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Zufall;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using static Fussballturnierverwaltung.Tests.Simulation.SimulationsEngineTestdaten;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>#679 K6: Ohne Turnier (freie Einzelspiel- und Hin-/Rückspiel-Simulation) trägt ein Spiel keine Turnierstärke und
/// zieht keinen zusätzlichen Zufall. Die Referenzwerte wurden vor der Änderung auf main erzeugt: Ergebnis, Zahl der
/// Ereignisse und der nächste Wert des Zufallsstroms (Zufallsverbrauch).</summary>
public class TurnierstaerkeOhneTurnierTests
{
    private static string Fingerabdruck(SimulationsErgebnis e, Random rng) =>
        $"{e.HeimToreRegulaer}:{e.AuswaertsToreRegulaer}" +
        $"/{e.HeimToreVerlaengerung}:{e.AuswaertsToreVerlaengerung}" +
        $"/{e.HeimElfmetertore}:{e.AuswaertsElfmetertore}" +
        $"/{e.Wiederholung?.HeimGesamt}:{e.Wiederholung?.AuswaertsGesamt}" +
        $"/{e.Ereignisse.Count}/{rng.Next()}";

    private static readonly EntscheidungsVerfahren[] _kette =
        VerfahrenKette(VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Wiederholungsspiel, VerfahrenArt.Elfmeterschiessen);

    private static (Mannschaft Heim, Mannschaft Gast) Teams(int seed) =>
        (Team(1, off: 70 + seed % 7, mid: 72, def: 68), Team(2, off: 66 + seed % 5, mid: 70, def: 71));

    private static string Einzelspiel(int seed, bool ausfuehrlich)
    {
        var (heim, gast) = Teams(seed);
        var rng = new Random(seed);
        var e = SimulationsEngine.Simulieren(heim, gast, _kette, ausfuehrlich, rng);
        return Fingerabdruck(e, rng);
    }

    private static string HinUndRueckspiel(int seed)
    {
        var (heim, gast) = Teams(seed);
        var service = SimulationsDienste.Service(SimulationsDienste.NeuerContext(), new Zufallsquelle());
        var rng = new Random(seed);
        var hin = service.EinzelspielSimulieren(heim, gast, [], SimulationsModus.AusfuehrlicheSimulation, rng);
        var rueck = service.RueckspielSimulieren(gast, heim, hin, _kette, rng);
        return Fingerabdruck(hin, rng) + " | " + Fingerabdruck(rueck, rng);
    }

    [Theory]
    [InlineData(1, "2:0/:/:/:/10/1095394276", "1:2/:/:/:/0/929393559", "2:0/:/:/:/10/643122221 | 0:1/:/:/:/15/706110378")]
    [InlineData(2, "0:3/:/:/:/13/1607840487", "2:1/:/:/:/0/658591161", "0:3/:/:/:/13/748657886 | 1:1/:/:/:/10/578174797")]
    [InlineData(3, "1:1/0:1/:/:/20/889752298", "1:1/0:0/:/1:2/0/1738297895", "1:1/:/:/:/14/325179797 | 0:0/0:0/:/0:3/28/964529405")]
    [InlineData(4, "0:2/:/:/:/14/384309337", "3:0/:/:/:/0/116986365", "0:2/:/:/:/14/2117847616 | 0:2/0:0/:/1:0/19/268241947")]
    [InlineData(5, "1:1/0:1/:/:/15/1860355743", "1:0/:/:/:/0/1342984399", "1:1/:/:/:/11/933399322 | 1:1/0:0/:/0:1/20/1986348344")]
    [InlineData(6, "1:1/0:1/:/:/15/700384668", "6:2/:/:/:/0/1529629951", "1:1/:/:/:/7/1720391192 | 2:0/:/:/:/14/1314036126")]
    [InlineData(7, "4:2/:/:/:/24/1656421412", "2:0/:/:/:/0/786909589", "4:2/:/:/:/24/609102960 | 1:1/:/:/:/14/525478509")]
    [InlineData(8, "1:2/:/:/:/15/1039358267", "1:1/1:0/:/:/0/978507572", "1:2/:/:/:/15/1242708543 | 1:1/:/:/:/13/2139215623")]
    [InlineData(9, "1:1/0:0/:/1:3/28/680186855", "1:0/:/:/:/0/1028961564", "1:1/:/:/:/11/943354850 | 1:0/:/:/:/11/1692178766")]
    [InlineData(10, "1:3/:/:/:/14/2044926871", "5:1/:/:/:/0/461100725", "1:3/:/:/:/14/1925498083 | 1:2/:/:/:/18/1351237122")]
    [InlineData(11, "5:0/:/:/:/17/142790330", "1:2/:/:/:/0/368853226", "5:0/:/:/:/17/1959485238 | 0:1/:/:/:/9/942182531")]
    [InlineData(12, "0:1/:/:/:/8/681201683", "2:0/:/:/:/0/1339733510", "0:1/:/:/:/8/715886842 | 2:0/:/:/:/9/1118673538")]
    [InlineData(13, "1:1/1:0/:/:/16/380234280", "3:0/:/:/:/0/1974732077", "1:1/:/:/:/10/299927847 | 2:0/:/:/:/10/609055240")]
    [InlineData(14, "1:2/:/:/:/18/1686202196", "0:3/:/:/:/0/1703929679", "1:2/:/:/:/18/890755522 | 2:0/:/:/:/11/1638566447")]
    [InlineData(15, "2:0/:/:/:/16/481981010", "1:0/:/:/:/0/1631669135", "2:0/:/:/:/16/230950056 | 2:2/:/:/:/19/1910615518")]
    public void OhneTurnier_RechnetBitgleichWieVorDerTurnierstaerke(
        int seed, string ausfuehrlich, string schnell, string hinUndRueck)
    {
        Einzelspiel(seed, ausfuehrlich: true).Should().Be(ausfuehrlich);
        Einzelspiel(seed, ausfuehrlich: false).Should().Be(schnell);
        HinUndRueckspiel(seed).Should().Be(hinUndRueck);
    }
}
