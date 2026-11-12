using FluentAssertions;
using Fussballturnierverwaltung.Application.Turnierbaum;

namespace Fussballturnierverwaltung.Tests.Turnierbaum;

/// <summary>Höhen der Karten im Zwei-Spalten-Fenster der rundenweisen Ansicht (#567, aus dem ViewModel
/// herausgelöst).</summary>
public class RundenFensterTests
{
    private static readonly KartenVorgaenger _ohne = new(null, null);

    [Fact]
    public void LinkeSpalte_StehtFortlaufend()
    {
        var zeilen = RundenFenster.Zeilen([1, 2, 3], [], new Dictionary<int, KartenVorgaenger>());

        zeilen.Should().Equal(new Dictionary<int, double> { [1] = 0.5, [2] = 1.5, [3] = 2.5 });
    }

    [Fact]
    public void RechteKarte_StehtMittigZwischenIhrenVorgaengern()
    {
        // AK3: Platz-3-Karte mittig zwischen den beiden Halbfinals.
        var zeilen = RundenFenster.Zeilen([1, 2], [3],
            new Dictionary<int, KartenVorgaenger> { [3] = new(1, 2) });

        zeilen[3].Should().Be(1.0);
    }

    [Fact]
    public void RechteKarte_MitEinemVorgaenger_StehtAufDessenHoehe()
    {
        // AK4: Platz 3 und Finale auf derselben Höhe.
        var zeilen = RundenFenster.Zeilen([3], [4],
            new Dictionary<int, KartenVorgaenger> { [3] = _ohne, [4] = new(3, null) });

        zeilen[4].Should().Be(zeilen[3]);
    }

    [Fact]
    public void RechteKarte_MitNurRechtemVorgaenger_StehtAufDessenHoehe()
    {
        var zeilen = RundenFenster.Zeilen([1, 2], [3],
            new Dictionary<int, KartenVorgaenger> { [3] = new(null, 2) });

        zeilen[3].Should().Be(1.5);
    }

    [Fact]
    public void RechteKarte_OhneVorgaengerImFenster_StehtNachIhrerPosition()
    {
        // Vorgänger außerhalb des Fensters (oder gar keiner) zählen nicht.
        var zeilen = RundenFenster.Zeilen([1, 2], [3, 4],
            new Dictionary<int, KartenVorgaenger> { [3] = new(99, null), [4] = _ohne });

        zeilen[3].Should().Be(0.5);
        zeilen[4].Should().Be(1.5);
    }
}
