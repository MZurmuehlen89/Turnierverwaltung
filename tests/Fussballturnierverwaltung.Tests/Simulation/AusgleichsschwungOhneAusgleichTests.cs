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
        (1, "2:0/:/:/11/2114587598"),
        (2, "0:3/:/:/13/1227929868"),
        (3, "0:1/:/:/12/354717332"),
        (4, "0:0/0:1/:/9/959037837"),
        (6, "2:0/:/:/13/2048324394"),
        (7, "4:1/:/:/19/1653019555"),
        (8, "0:4/:/:/13/1634063310"),
        (11, "3:0/:/:/13/541025856"),
        (12, "0:1/:/:/4/1330613471"),
        (13, "3:1/:/:/12/1869914006"),
        (15, "2:0/:/:/14/54808408"),
        (16, "0:0/0:1/:/18/1983789168"),
        (17, "0:0/0:1/:/10/1485273139"),
        (18, "0:1/:/:/10/326377058"),
        (19, "1:3/:/:/16/1683721706"),
    ];
}
