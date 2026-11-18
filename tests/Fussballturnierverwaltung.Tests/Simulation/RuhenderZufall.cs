namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>
/// Zufallsstrom, aus dem die Spielkontrolle (#674) nur Rauschen 0 zieht: Box-Muller mit u₂ = ¼ ergibt cos(π/2) = 0. So
/// steht die Spielkontrolle in Tests mit vorgegebener Zufallsfolge still, und beide Teams behalten ihren Anteil an
/// der Chancenrate.
/// </summary>
internal sealed class RuhenderZufall : Random
{
    protected override double Sample() => 0.25;
}
