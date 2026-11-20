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

    /// <summary>Maximum-Likelihood-Schätzung von ρ bei festen λ und μ je Spiel, gesucht in −0,3 … +0,1.</summary>
    /// <remarks>#683: Die Log-Likelihood ist in ρ konkav (Summe von Logarithmen linearer Funktionen), ihre Ableitung also
    /// fallend – Bisektion auf der Ableitung findet das Maximum stetig. Das frühere Gitter (Schritt 0,001) war für den
    /// Jackknife zu grob: ohne eine von 100 Saisons verschiebt sich ρ um weniger als einen Gitterschritt, der SE wäre
    /// gerundet. Liegt das Maximum außerhalb des Suchbereichs, gilt wie beim Gitter der nächste Rand.</remarks>
    public static double RhoSchaetzen(IReadOnlyList<(double Lambda, double Mu, int Heim, int Gast)> spiele)
    {
        // Nur 0:0, 1:0, 0:1 und 1:1 haben τ ≠ 1 und tragen damit zur Log-Likelihood bei (log 1 = 0).
        var betroffene = spiele.Where(s => s.Heim <= 1 && s.Gast <= 1).ToList();
        double unten = RHO_MIN, oben = RHO_MAX;
        if (Ableitung(betroffene, unten) <= 0) return unten;
        if (Ableitung(betroffene, oben) >= 0) return oben;
        for (int i = 0; i < BISEKTIONSSCHRITTE; i++)
        {
            double mitte = (unten + oben) / 2;
            if (Ableitung(betroffene, mitte) > 0) unten = mitte; else oben = mitte;
        }
        return (unten + oben) / 2;
    }

    private const double RHO_MIN = -0.3, RHO_MAX = 0.1;
    private const int BISEKTIONSSCHRITTE = 40;

    /// <summary>Ableitung der Log-Likelihood nach ρ. Wo ein τ ≤ 0 würde, ist ρ unzulässig klein (τ von 1:0/0:1 fällt
    /// mit sinkendem ρ) oder zu groß (0:0, 1:1) – die Ableitung zeigt dann in den zulässigen Bereich.</summary>
    private static double Ableitung(List<(double Lambda, double Mu, int Heim, int Gast)> spiele, double rho)
    {
        double summe = 0;
        foreach (var s in spiele)
        {
            double tau = Tau(s.Heim, s.Gast, s.Lambda, s.Mu, rho);
            if (tau < MIN_TAU) return (s.Heim, s.Gast) is (0, 0) or (1, 1) ? double.NegativeInfinity : double.PositiveInfinity;
            summe += (s.Heim, s.Gast) switch
            {
                (0, 0) => -s.Lambda * s.Mu,
                (0, 1) => s.Lambda,
                (1, 0) => s.Mu,
                _ => -1.0
            } / tau;
        }
        return summe;
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
