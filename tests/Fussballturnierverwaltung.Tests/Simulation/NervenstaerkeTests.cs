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

    // ── K2 Verschiebung ───────────────────────────────────────────────────

    [Theory]
    [InlineData(Nervenstaerke.Normal, false, 0.75)]
    [InlineData(Nervenstaerke.Normal, true, 0.75)]
    [InlineData(Nervenstaerke.Stark, false, 0.77)]
    [InlineData(Nervenstaerke.Stark, true, 0.78)]
    [InlineData(Nervenstaerke.Schwach, false, 0.73)]
    [InlineData(Nervenstaerke.Schwach, true, 0.72)]
    public void Trefferquote_VerschiebtUmZweiPunkteUnterDruckUmDrei(Nervenstaerke stufe, bool unterDruck, double erwartet)
        => Nervenwirkung.Trefferquote(0.75, stufe, unterDruck).Should().BeApproximately(erwartet, 1e-12);

    [Theory]
    [InlineData(Nervenstaerke.Stark, true, 0.92, 0.92)]
    [InlineData(Nervenstaerke.Stark, false, 0.91, 0.92)]
    [InlineData(Nervenstaerke.Schwach, true, 0.5, 0.5)]
    [InlineData(Nervenstaerke.Schwach, false, 0.51, 0.5)]
    public void Trefferquote_BleibtInDerGrenzeZwischenHalbUndZweiundneunzigProzent(
        Nervenstaerke stufe, bool unterDruck, double basis, double erwartet)
        => Nervenwirkung.Trefferquote(basis, stufe, unterDruck).Should().BeApproximately(erwartet, 1e-12);

    [Theory]
    [InlineData(Nervenstaerke.Schwach)]
    [InlineData(Nervenstaerke.Normal)]
    [InlineData(Nervenstaerke.Stark)]
    public void Trefferquote_NormalGibtDieBasisUnveraendertZurueck(Nervenstaerke stufe)
    {
        if (stufe == Nervenstaerke.Normal)
            Nervenwirkung.Trefferquote(0.7312345, stufe, true).Should().Be(0.7312345);
        else
            Nervenwirkung.Trefferquote(0.7312345, stufe, true).Should().NotBe(0.7312345);
    }

    // ── K3 Druck ──────────────────────────────────────────────────────────
    // Eingabe: Rückstand des Schützen und seine verbleibenden Schüsse nach diesem Schuss.

    [Theory]
    [InlineData(0, 4, false)]   // erster Schuss bei 0:0
    [InlineData(1, 3, false)]   // 1 Rückstand, drei weitere Schüsse: aufholbar
    [InlineData(1, 0, true)]    // fünfter Schuss, 1 Rückstand: Fehlschuss verliert sofort
    [InlineData(2, 1, true)]    // Muss-Treffer
    [InlineData(2, 2, false)]   // gerade noch aufholbar
    [InlineData(-1, 0, false)]  // Führung: Fehlschuss verliert nicht
    [InlineData(0, 0, false)]   // fünfter Schuss bei Gleichstand: es bleibt der Sudden Death
    public void UnterDruck_NurWennEinFehlschussDasSchiessenSofortVerliertMacht(int rueckstand, int rest, bool erwartet)
        => Nervenwirkung.UnterDruck(rueckstand, rest).Should().Be(erwartet);

    // ── K4 Wirkung ────────────────────────────────────────────────────────

    /// <summary>Siegchance von <paramref name="a"/> gegen <paramref name="b"/> über beide Schussreihenfolgen.</summary>
    private static double Siegchance(Nervenstaerke a, Nervenstaerke b)
    {
        const int SCHIESSEN = 20_000;
        var rng = new Random(669);
        int siege = 0;
        for (int i = 0; i < SCHIESSEN; i++)
        {
            bool aSchiesstZuerst = i % 2 == 0;
            var heim = Team(1, off: 80); heim.Nervenstaerke = aSchiesstZuerst ? a : b;
            var gast = Team(2, off: 80); gast.Nervenstaerke = aSchiesstZuerst ? b : a;
            var e = SimulationsEngine.EntscheidungsverfahrenAnwenden(
                heim, gast, VerfahrenKette(VerfahrenArt.Elfmeterschiessen), ausfuehrlich: false, rng);
            bool heimGewinnt = e.HeimElf > e.GastElf;
            if (heimGewinnt == aSchiesstZuerst) siege++;
        }
        return 100.0 * siege / SCHIESSEN;
    }

    [Fact]
    public void Siegchance_StarkGegenNormal() =>
        Siegchance(Nervenstaerke.Stark, Nervenstaerke.Normal).Should().BeInRange(53.0, 55.2);

    [Fact]
    public void Siegchance_StarkGegenSchwach() =>
        Siegchance(Nervenstaerke.Stark, Nervenstaerke.Schwach).Should().BeInRange(57.0, 59.2);

    [Fact]
    public void Siegchance_NormalGegenNormal() =>
        Siegchance(Nervenstaerke.Normal, Nervenstaerke.Normal).Should().BeInRange(49.0, 51.0);
}
