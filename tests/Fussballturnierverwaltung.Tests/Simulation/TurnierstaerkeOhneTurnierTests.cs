using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Zufall;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using static Fussballturnierverwaltung.Tests.Simulation.SimulationsEngineTestdaten;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>#679 K6: Ohne Turnier (freie Einzelspiel- und Hin-/Rückspiel-Simulation) trägt ein Spiel keine Turnierstärke und
/// zieht keinen zusätzlichen Zufall. Die Referenzwerte wurden vor der Änderung auf main erzeugt (neu erzeugt mit #692 und #694, weil Heimvorteil, Spreizung und Zeitprofil jeden Zufallsstrom verschieben): Ergebnis, Zahl der
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
    [InlineData(1, "2:0/:/:/:/12/1095394276", "1:2/:/:/:/0/929393559", "2:0/:/:/:/12/1400467361 | 1:0/:/:/:/12/1015481018")]
    [InlineData(2, "1:4/:/:/:/17/1607840487", "2:1/:/:/:/0/658591161", "1:4/:/:/:/17/107409588 | 2:2/:/:/:/13/729725434")]
    [InlineData(3, "1:1/0:1/:/:/22/717665865", "2:0/:/:/:/0/1203643911", "1:1/:/:/:/14/241672395 | 1:1/0:0/:/1:4/28/46120757")]
    [InlineData(4, "1:1/0:0/:/1:4/31/1795790063", "3:0/:/:/:/0/116986365", "1:1/:/:/:/13/1372710919 | 0:3/:/:/:/11/190562635")]
    [InlineData(5, "1:0/:/:/:/8/1425839066", "1:0/:/:/:/0/1342984399", "1:0/:/:/:/8/1097122538 | 2:1/0:0/:/0:1/29/570568967")]
    [InlineData(6, "1:1/0:2/:/:/19/1062480034", "6:2/:/:/:/0/1529629951", "1:1/:/:/:/7/2028380248 | 1:1/0:0/:/3:1/23/1389267804")]
    [InlineData(7, "3:1/:/:/:/20/1169798271", "3:1/:/:/:/0/91104737", "3:1/:/:/:/20/1018129064 | 0:2/:/:/:/12/97646952")]
    [InlineData(8, "0:2/:/:/:/11/1039358267", "1:1/1:0/:/:/0/978507572", "0:2/:/:/:/11/1429198162 | 0:1/:/:/:/11/180101690")]
    [InlineData(9, "1:1/0:1/:/:/19/2109604421", "1:0/:/:/:/0/1028961564", "1:1/:/:/:/11/2087967685 | 2:0/:/:/:/16/1610359286")]
    [InlineData(10, "1:3/:/:/:/18/1080098015", "5:1/:/:/:/0/461100725", "1:3/:/:/:/18/891238442 | 2:0/:/:/:/12/1034217854")]
    [InlineData(11, "5:1/:/:/:/19/1322849083", "1:2/:/:/:/0/368853226", "5:1/:/:/:/19/118220628 | 0:0/:/:/:/11/2105099585")]
    [InlineData(12, "0:1/:/:/:/7/1037445581", "2:0/:/:/:/0/1339733510", "0:1/:/:/:/7/1118809424 | 4:2/:/:/:/16/71244829")]
    [InlineData(13, "2:1/:/:/:/7/1835295543", "3:0/:/:/:/0/1974732077", "2:1/:/:/:/7/546853892 | 2:1/0:0/:/1:8/39/1339104139")]
    [InlineData(14, "1:1/0:2/:/:/19/2068654746", "0:2/:/:/:/0/1131366349", "1:1/:/:/:/7/349173816 | 3:2/:/:/:/16/251586404")]
    [InlineData(15, "1:0/:/:/:/15/1162606148", "1:0/:/:/:/0/1631669135", "1:0/:/:/:/15/1967172875 | 1:3/:/:/:/22/862191165")]
    public void OhneTurnier_RechnetBitgleichWieVorDerTurnierstaerke(
        int seed, string ausfuehrlich, string schnell, string hinUndRueck)
    {
        Einzelspiel(seed, ausfuehrlich: true).Should().Be(ausfuehrlich);
        Einzelspiel(seed, ausfuehrlich: false).Should().Be(schnell);
        HinUndRueckspiel(seed).Should().Be(hinUndRueck);
    }
}
