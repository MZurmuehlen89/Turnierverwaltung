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
        SimulationsEngine.HeimrechtTauscht(neutral).Should().Be(erwartet);
    }

    /// <summary>Die Anzeige kennt nur die Runde und leitet den Tausch aus <c>NeutralerSpielort</c> ab; die Engine bekommt den
    /// Ortsvorteil aus <see cref="SimulationsEngine.SpielortvorteilErmitteln"/>. Beide müssen für jede Gastgeber-Lage
    /// dasselbe ergeben – sonst steht das Heimteam der Wiederholungskarte auf der falschen Seite.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Anzeige_UndEngine_TauschenFuerJedeGastgeberLageGleich(bool neutral)
    {
        int[][] gastgeberLagen = [[], [1], [2], [1, 2]];
        foreach (var gastgeber in gastgeberLagen)
        {
            var vorteil = SimulationsEngine.SpielortvorteilErmitteln(neutral, 1, 2, gastgeber);

            SimulationsEngine.HeimrechtTauscht(vorteil, neutral).Should().Be(
                SimulationsEngine.HeimrechtTauscht(neutral), "Gastgeber {0}, neutral {1}", string.Join(",", gastgeber), neutral);
        }
    }
}
