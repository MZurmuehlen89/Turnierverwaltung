using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>#694, K3: der Heimfaktor der Torerwartung – Team mit Ortsvorteil ×1,05, Gegner ÷1,05, neutral keiner.</summary>
public class HeimvorteilTests
{
    [Theory]
    [InlineData(true, Spielortvorteil.Heim, 1.05)]
    [InlineData(false, Spielortvorteil.Heim, 1 / 1.05)]
    [InlineData(true, Spielortvorteil.Auswaerts, 1 / 1.05)]
    [InlineData(false, Spielortvorteil.Auswaerts, 1.05)]
    [InlineData(true, Spielortvorteil.Neutral, 1.0)]
    [InlineData(false, Spielortvorteil.Neutral, 1.0)]
    public void Heimvorteil_JeOrtsvorteil(bool istHeim, Spielortvorteil ortsvorteil, double erwartet) =>
        SimulationsEngine.Heimvorteil(istHeim, ortsvorteil).Should().BeApproximately(erwartet, 1e-12);

    [Fact]
    public void Heimvorteil_BeideTeamsHebenSichAufGleicheWeiseAuf()
    {
        double heim = SimulationsEngine.Heimvorteil(true, Spielortvorteil.Heim);
        double gast = SimulationsEngine.Heimvorteil(false, Spielortvorteil.Heim);
        (heim * gast).Should().BeApproximately(1.0, 1e-12);
    }
}
