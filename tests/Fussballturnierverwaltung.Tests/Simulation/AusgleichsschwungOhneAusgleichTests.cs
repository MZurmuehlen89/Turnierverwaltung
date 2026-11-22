using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using static Fussballturnierverwaltung.Tests.Simulation.SimulationsEngineTestdaten;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>#692 K4: Der Schwung nach dem Ausgleich zieht keine Zufallszahl. Ein Spiel, in dem die reguläre Spielzeit
/// keinen Ausgleich enthält, verläuft deshalb bitgleich wie vor der Änderung. Die Referenzwerte wurden vor der Änderung
/// auf main (d6b692c) erzeugt, seit #694 (Heimvorteil, Spreizung, Zeitprofil verschieben den Zufallsstrom) auf dem Stand 6cfa16a + #694 mit Seeds ohne Ausgleich neu erzeugt: Ergebnis, Zahl der Ereignisse und der nächste Wert des Zufallsstroms.</summary>
public class AusgleichsschwungOhneAusgleichTests
{
    private static readonly EntscheidungsVerfahren[] _kette =
        VerfahrenKette(VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Wiederholungsspiel, VerfahrenArt.Elfmeterschiessen);

    private static (Mannschaft Heim, Mannschaft Gast) Teams(int seed) =>
        (Team(1, off: 70 + seed % 7, mid: 72, def: 68), Team(2, off: 66 + seed % 5, mid: 70, def: 71));

    private static (SimulationsErgebnis Ergebnis, string Fingerabdruck) Spielen(int seed)
    {
        var (heim, gast) = Teams(seed);
        var rng = new Random(seed);
        var e = SimulationsEngine.Simulieren(heim, gast, _kette, ausfuehrlich: true, rng);
        string fingerabdruck =
            $"{e.HeimToreRegulaer}:{e.AuswaertsToreRegulaer}" +
            $"/{e.HeimToreVerlaengerung}:{e.AuswaertsToreVerlaengerung}" +
            $"/{e.HeimElfmetertore}:{e.AuswaertsElfmetertore}" +
            $"/{e.Ereignisse.Count}/{rng.Next()}";
        return (e, fingerabdruck);
    }

    /// <summary>Ob ein Tor der regulären Spielzeit des ersten Spiels den Stand ausgleicht (Gleichstand nach Toren).</summary>
    private static bool HatAusgleich(SimulationsErgebnis e)
    {
        int heim = 0, gast = 0;
        foreach (var tor in e.Ereignisse.Where(x => x.Art == EreignisArt.Tor && x.Minute <= 90 && !x.ImWiederholungsspiel))
        {
            if (tor.IstHeim) heim++;
            else gast++;
            if (heim == gast) return true;
        }

        return false;
    }

    [Fact]
    public void ReferenzEnthaeltNurSpieleOhneAusgleich()
    {
        foreach (var (seed, _) in _referenz)
            HatAusgleich(Spielen(seed).Ergebnis).Should().BeFalse($"Seed {seed}");
    }

    [Fact]
    public void OhneAusgleich_RechnetBitgleichWieVorDemSchwung()
    {
        foreach (var (seed, erwartet) in _referenz)
            Spielen(seed).Fingerabdruck.Should().Be(erwartet, $"Seed {seed}");
    }

    private static readonly (int Seed, string Fingerabdruck)[] _referenz = [
        (1, "2:0/:/:/12/1095394276"),
        (2, "1:4/:/:/17/1607840487"),
        (5, "1:0/:/:/8/1425839066"),
        (7, "3:1/:/:/20/1169798271"),
        (8, "0:2/:/:/11/1039358267"),
        (11, "5:1/:/:/19/1322849083"),
        (12, "0:1/:/:/7/1037445581"),
        (15, "1:0/:/:/15/1162606148"),
        (16, "0:1/:/:/13/1741174867"),
        (17, "0:1/:/:/13/699481726"),
        (22, "0:1/:/:/5/931643317"),
        (25, "4:2/:/:/19/1695090397"),
        (26, "3:0/:/:/15/1634643521"),
        (27, "1:2/:/:/14/1833735827"),
        (28, "0:3/:/:/14/821370125"),
    ];
}
