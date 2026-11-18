namespace Fussballturnierverwaltung.Tests.Simulation.Realismus;

/// <summary>
/// Dixon-Coles (1997) als Vergleichsmaßstab für niedrige Ergebnisse (#678): unabhängige Poisson-Ziehungen mit
/// Korrekturfaktor τ für 0:0, 1:0, 0:1 und 1:1. Dient nur dem Messen, nicht dem Simulieren.
/// </summary>
internal static class DixonColes
{
    private const double MIN_TAU = 1e-12;

    public static double Poisson(int k, double lambda)
    {
        double p = Math.Exp(-lambda);
        for (int i = 1; i <= k; i++) p *= lambda / i;
        return p;
    }

    public static double Tau(int x, int y, double lambda, double mu, double rho) => (x, y) switch
    {
        (0, 0) => 1 - lambda * mu * rho,
        (0, 1) => 1 + lambda * rho,
        (1, 0) => 1 + mu * rho,
        (1, 1) => 1 - rho,
        _ => 1.0
    };

    /// <summary>Maximum-Likelihood-Schätzung von ρ bei festen λ und μ je Spiel (Gitter −0,300 … +0,100, Schritt 0,001).</summary>
    public static double RhoSchaetzen(IReadOnlyList<(double Lambda, double Mu, int Heim, int Gast)> spiele)
    {
        double bestesRho = 0, besteLogLikelihood = double.NegativeInfinity;
        for (int i = -300; i <= 100; i++)
        {
            double rho = i / 1000.0;
            double logLikelihood = 0;
            foreach (var s in spiele)
                logLikelihood += Math.Log(Math.Max(MIN_TAU, Tau(s.Heim, s.Gast, s.Lambda, s.Mu, rho)));
            if (logLikelihood > besteLogLikelihood) { besteLogLikelihood = logLikelihood; bestesRho = rho; }
        }
        return bestesRho;
    }

    /// <summary>Beobachtete Anzahl des Ergebnisses x:y geteilt durch die Anzahl, die unabhängige Poisson-Ziehungen erwarten ließen.</summary>
    public static double VerhaeltnisZuUnabhaengig(
        IReadOnlyList<(double Lambda, double Mu, int Heim, int Gast)> spiele, int x, int y) =>
        VerhaeltnisZuUnabhaengig(spiele, [(x, y)]);

    /// <summary>Wie oben für mehrere Ergebnisse zusammen (Summe beobachtet durch Summe erwartet).</summary>
    public static double VerhaeltnisZuUnabhaengig(
        IReadOnlyList<(double Lambda, double Mu, int Heim, int Gast)> spiele, IReadOnlyList<(int X, int Y)> ergebnisse)
    {
        int beobachtet = spiele.Count(s => ergebnisse.Contains((s.Heim, s.Gast)));
        double erwartet = spiele.Sum(s => ergebnisse.Sum(e => Poisson(e.X, s.Lambda) * Poisson(e.Y, s.Mu)));
        return beobachtet / erwartet;
    }
}
