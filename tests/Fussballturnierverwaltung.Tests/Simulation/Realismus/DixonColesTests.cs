using FluentAssertions;
using Fussballturnierverwaltung.Tests.Simulation.Realismus;

namespace Fussballturnierverwaltung.Tests.Simulation.Realismus;

/// <summary>#678 K2: Hilfsfunktionen des Dixon-Coles-Vergleichs (nur Maßstab, kein Simulator).</summary>
public class DixonColesTests
{
    [Fact]
    public void Poisson_KennwerteStimmen()
    {
        DixonColes.Poisson(0, 1.5).Should().BeApproximately(Math.Exp(-1.5), 1e-12);
        DixonColes.Poisson(2, 1.5).Should().BeApproximately(Math.Exp(-1.5) * 1.5 * 1.5 / 2, 1e-12);
    }

    [Theory]
    [InlineData(0, 0, 1 - 1.7 * 1.3 * -0.13)]
    [InlineData(0, 1, 1 + 1.7 * -0.13)]
    [InlineData(1, 0, 1 + 1.3 * -0.13)]
    [InlineData(1, 1, 1 - -0.13)]
    [InlineData(2, 0, 1.0)]
    [InlineData(0, 2, 1.0)]
    [InlineData(2, 2, 1.0)]
    [InlineData(3, 1, 1.0)]
    public void Tau_SonderfaelleUndSonst(int x, int y, double erwartet)
        => DixonColes.Tau(x, y, 1.7, 1.3, -0.13).Should().BeApproximately(erwartet, 1e-12);

    [Fact]
    public void RhoSchaetzen_FindetWertAufSynthetischenDatenAufPlusMinus003()
    {
        const double RHO = -0.13, LAMBDA = 1.7, MU = 1.3;
        var rng = new Random(678_001);
        var gitter = new List<(int X, int Y, double P)>();
        for (int x = 0; x <= 10; x++)
        for (int y = 0; y <= 10; y++)
            gitter.Add((x, y, DixonColes.Poisson(x, LAMBDA) * DixonColes.Poisson(y, MU) * DixonColes.Tau(x, y, LAMBDA, MU, RHO)));
        double summe = gitter.Sum(g => g.P);

        var spiele = new List<(double, double, int, int)>();
        for (int i = 0; i < 20_000; i++)
        {
            double u = rng.NextDouble() * summe, kumuliert = 0;
            var gewaehlt = gitter[^1];
            foreach (var g in gitter)
            {
                kumuliert += g.P;
                if (u <= kumuliert) { gewaehlt = g; break; }
            }
            spiele.Add((LAMBDA, MU, gewaehlt.X, gewaehlt.Y));
        }

        DixonColes.RhoSchaetzen(spiele.Select(s => (s.Item1, s.Item2, s.Item3, s.Item4)).ToList())
            .Should().BeApproximately(RHO, 0.03);
    }

    [Fact]
    public void VerhaeltnisZuUnabhaengig_BeobachtetDurchErwartet()
    {
        // 4 Spiele, λ = μ = 1: erwartet 0:0 = 4 · e^-2, beobachtet 2 mal
        var spiele = new List<(double, double, int, int)> { (1, 1, 0, 0), (1, 1, 0, 0), (1, 1, 1, 0), (1, 1, 2, 2) };
        DixonColes.VerhaeltnisZuUnabhaengig(spiele, 0, 0)
            .Should().BeApproximately(2 / (4 * Math.Exp(-2)), 1e-9);
    }
}
