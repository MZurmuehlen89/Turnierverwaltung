namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>Standardnormalverteilte Zufallszahlen aus einem <see cref="Random"/>; gemeinsame Hilfe von
/// <see cref="Spielkontrolle"/> (herausgelöst ohne Verhaltensänderung, #679).</summary>
internal static class Normalverteilung
{
    /// <summary>Box-Muller; 1 − u schließt log(0) aus. Zugfolge: erst u1, dann u2.</summary>
    public static double Ziehen(Random zufall)
    {
        double u1 = 1 - zufall.NextDouble();
        double u2 = zufall.NextDouble();
        return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }
}
