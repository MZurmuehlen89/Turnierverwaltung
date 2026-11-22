using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using static Fussballturnierverwaltung.Tests.Simulation.SimulationsEngineTestdaten;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>#692 K4: Der Schwung nach dem Ausgleich zieht keine Zufallszahl. Ein Spiel, in dem die reguläre Spielzeit
/// keinen Ausgleich enthält, verläuft deshalb bitgleich wie vor der Änderung. Die Referenzwerte wurden vor der Änderung
/// auf main (d6b692c) erzeugt: Ergebnis, Zahl der Ereignisse und der nächste Wert des Zufallsstroms.</summary>
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
        (1, "2:0/:/:/10/1095394276"),
        (2, "0:3/:/:/13/1607840487"),
        (4, "0:2/:/:/14/384309337"),
        (7, "4:2/:/:/24/1656421412"),
        (11, "5:0/:/:/17/142790330"),
        (12, "0:1/:/:/8/681201683"),
        (15, "2:0/:/:/16/481981010"),
        (16, "0:3/:/:/15/2061367703"),
        (17, "0:2/:/:/13/727942354"),
        (18, "0:1/:/:/11/1452770357"),
        (20, "2:0/:/:/9/125321505"),
        (22, "0:0/0:0/:/13/739955273"),
        (26, "2:0/:/:/14/1986078368"),
        (28, "0:3/:/:/13/327786862"),
        (29, "1:0/:/:/11/1476304597"),
    ];
}
