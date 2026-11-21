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
    [InlineData(1, "2:0/:/:/:/11/2114587598", "1:2/:/:/:/0/929393559", "2:0/:/:/:/11/573798788 | 1:0/:/:/:/11/828632488")]
    [InlineData(2, "0:3/:/:/:/13/1227929868", "2:1/:/:/:/0/658591161", "0:3/:/:/:/13/1526596275 | 0:2/:/:/:/13/414696767")]
    [InlineData(3, "0:1/:/:/:/12/354717332", "1:1/0:0/:/1:2/0/1738297895", "0:1/:/:/:/12/1474439564 | 3:1/:/:/:/17/1162395111")]
    [InlineData(4, "0:0/0:1/:/:/9/959037837", "3:0/:/:/:/0/116986365", "0:0/:/:/:/5/574090188 | 1:1/0:0/:/2:0/29/1215905455")]
    [InlineData(5, "1:1/2:0/:/:/21/2681455", "1:0/:/:/:/0/1342984399", "1:1/:/:/:/10/1435261747 | 0:1/:/:/:/13/735290351")]
    [InlineData(6, "2:0/:/:/:/13/2048324394", "6:2/:/:/:/0/1529629951", "2:0/:/:/:/13/1398103056 | 3:1/0:1/:/:/22/1192200533")]
    [InlineData(7, "4:1/:/:/:/19/1653019555", "2:0/:/:/:/0/786909589", "4:1/:/:/:/19/1329725531 | 1:2/:/:/:/16/1341633689")]
    [InlineData(8, "0:4/:/:/:/13/1634063310", "1:1/1:0/:/:/0/978507572", "0:4/:/:/:/13/1174652681 | 1:0/:/:/:/15/1301797871")]
    [InlineData(9, "1:1/0:0/:/0:3/30/1439743014", "1:0/:/:/:/0/1028961564", "1:1/:/:/:/11/637432418 | 3:1/:/:/:/22/1404639537")]
    [InlineData(10, "1:4/:/:/:/18/393115044", "5:1/:/:/:/0/461100725", "1:4/:/:/:/18/49570200 | 1:1/:/:/:/16/1256771")]
    [InlineData(11, "3:0/:/:/:/13/541025856", "1:2/:/:/:/0/368853226", "3:0/:/:/:/13/411033307 | 0:1/:/:/:/12/1387889182")]
    [InlineData(12, "0:1/:/:/:/4/1330613471", "2:0/:/:/:/0/1339733510", "0:1/:/:/:/4/1150322420 | 4:0/:/:/:/15/1802529578")]
    [InlineData(13, "3:1/:/:/:/12/1869914006", "3:0/:/:/:/0/1974732077", "3:1/:/:/:/12/341263810 | 2:0/0:1/:/:/23/385483769")]
    [InlineData(14, "3:3/1:0/:/:/23/823557547", "0:3/:/:/:/0/1703929679", "3:3/:/:/:/18/333757143 | 2:1/:/:/:/12/90714003")]
    [InlineData(15, "2:0/:/:/:/14/54808408", "1:0/:/:/:/0/1631669135", "2:0/:/:/:/14/1543633930 | 2:2/:/:/:/12/418917652")]
    public void OhneTurnier_RechnetBitgleichWieVorDerTurnierstaerke(
        int seed, string ausfuehrlich, string schnell, string hinUndRueck)
    {
        Einzelspiel(seed, ausfuehrlich: true).Should().Be(ausfuehrlich);
        Einzelspiel(seed, ausfuehrlich: false).Should().Be(schnell);
        HinUndRueckspiel(seed).Should().Be(hinUndRueck);
    }
}
