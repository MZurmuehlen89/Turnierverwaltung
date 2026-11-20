using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>#247/#547: Die Regel „tauscht das Heimrecht im Wiederholungsspiel?“ ist eine öffentliche reine Funktion,
/// damit Engine und Anzeige dieselbe Regel nutzen.</summary>
public class HeimrechtTauschTests
{
    [Theory]
    [InlineData(Spielortvorteil.Heim, false, true)]
    [InlineData(Spielortvorteil.Heim, true, false)]
    [InlineData(Spielortvorteil.Neutral, false, false)]
    [InlineData(Spielortvorteil.Auswaerts, false, false)]
    public void HeimrechtTauscht_NurMitOrtsvorteilDesHeimsUndOhneNeutralenPlatz(Spielortvorteil vorteil, bool neutral, bool erwartet)
    {
        SimulationsEngine.HeimrechtTauscht(vorteil, neutral).Should().Be(erwartet);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Anzeige_AusRundeAbgeleitet_TauschtGenauAusserAufNeutralemPlatz(bool neutral, bool erwartet)
    {
        // Die Anzeige kennt nur die Runde: außerhalb neutraler Runden hat das Heimteam immer den Ortsvorteil.
        var vorteil = SimulationsEngine.SpielortvorteilErmitteln(neutral, 1, 2, []);

        SimulationsEngine.HeimrechtTauscht(vorteil, neutral).Should().Be(erwartet);
    }
}
