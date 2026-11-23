using FluentAssertions;

namespace Fussballturnierverwaltung.Tests.Simulation.Realismus;

/// <summary>#683, K4: der Jackknife-Standardfehler.</summary>
public class RauschschaetzungTests
{
    private readonly record struct Beobachtung(int Block, double Wert);

    private static double Mittel(IReadOnlyList<Beobachtung> daten) => daten.Average(d => d.Wert);

    [Fact]
    public void Jackknife_VierBloeckeMitBekanntenMitteln_LiefertDieFormel()
    {
        // Blockmittel 1, 2, 3, 4 (je drei gleiche Werte): θᵢ = 3; 2,667; 2,333; 2 → SE = √(3/4 · 0,5556) = 0,6455.
        var daten = Enumerable.Range(0, 12).Select(i => new Beobachtung(i / 3, i / 3 + 1)).ToList();

        double se = Rauschschaetzung.JackknifeStandardfehler(daten, d => d.Block, 4, Mittel);

        se.Should().BeApproximately(0.6455, 0.0001);
    }

    /// <summary>#685, K1: Blöcke 0–4 (Hälfte A) liegen weit daneben, die Kennzahl misst nur Block 5–9 (Blockmittel 1 … 5).
    /// k = 5: θᵢ = 3,5; 3,25; 3; 2,75; 2,5 → SE = √(4/5 · 0,625) = 0,7071. Mit k = 10 und fünf unveränderten θᵢ wären es 0,75.</summary>
    [Fact]
    public void Jackknife_NurHaelfteBBloecke_ZaehltNurDieseBloecke()
    {
        var daten = Enumerable.Range(0, 100)
            .Select(i => new Beobachtung(i / 10, i / 10 < 5 ? 100 + i : i / 10 - 4))
            .ToList();
        static double MittelAbBlock5(IReadOnlyList<Beobachtung> d) => d.Where(x => x.Block >= 5).Average(x => x.Wert);

        double se = Rauschschaetzung.JackknifeStandardfehler(daten, d => d.Block, 5, 10, MittelAbBlock5);

        se.Should().BeApproximately(0.7071, 0.0001);
    }

    [Fact]
    public void Jackknife_ErsterBlockHinterDemLetzten_WirdAbgewiesen()
    {
        var daten = new List<Beobachtung> { new(0, 1) };

        var aufruf = () => Rauschschaetzung.JackknifeStandardfehler(daten, d => d.Block, 9, 10, Mittel);

        aufruf.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Jackknife_KonstanteDaten_KeinRauschen()
    {
        var daten = Enumerable.Range(0, 100).Select(i => new Beobachtung(i / 10, 7)).ToList();

        Rauschschaetzung.JackknifeStandardfehler(daten, d => d.Block, 10, Mittel).Should().Be(0);
    }

    /// <summary>Ein einzelner Jackknife-SE aus 10 Blöcken streut selbst um rund 24 % (≈ 1/√(2·9)); über 20 feste Wiederholungen
    /// gemittelt liegt er auf ±15 % beim theoretischen √(p(1−p)/n).</summary>
    [Fact]
    public void Jackknife_BernoulliDaten_TrifftDenTheoretischenStandardfehler()
    {
        const double p = 0.1;
        const int n = 20_000, wiederholungen = 20;
        double theorie = Math.Sqrt(p * (1 - p) / n);

        var schaetzungen = Enumerable.Range(1, wiederholungen).Select(seed =>
        {
            var rng = new Random(seed);
            var daten = Enumerable.Range(0, n).Select(i => new Beobachtung(i / (n / 10), rng.NextDouble() < p ? 1 : 0)).ToList();
            return Rauschschaetzung.JackknifeStandardfehler(daten, d => d.Block, 10, Mittel);
        }).ToList();

        schaetzungen.Average().Should().BeInRange(theorie * 0.85, theorie * 1.15);
    }
}
