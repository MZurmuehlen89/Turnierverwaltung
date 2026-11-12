using FluentAssertions;
using Fussballturnierverwaltung.Application.Turnierbaum;

namespace Fussballturnierverwaltung.Tests.Turnierbaum;

/// <summary>Verbindungslinien des Turnierbaums (#506 aus der Ansicht herausgelöst).</summary>
public class VerbindungslinienTests
{
    // ── Rundenweise Ansicht: zwischen den beiden Spalten ─────────────────

    [Fact]
    public void ZwischenSpalten_ZweiKinder_GabelMitSenkrechtemStueck()
    {
        var segmente = Verbindungslinien.ZwischenSpalten(new Punkt(100, 50), new Punkt(100, 150), new Punkt(200, 100));

        segmente.Should().Equal(
            new Segment(new Punkt(100, 50), new Punkt(150, 50)),
            new Segment(new Punkt(100, 150), new Punkt(150, 150)),
            new Segment(new Punkt(150, 50), new Punkt(150, 150)),
            new Segment(new Punkt(150, 100), new Punkt(200, 100)));
    }

    [Fact]
    public void ZwischenSpalten_KinderAufGleicherHoehe_OhneSenkrechtesStueck()
    {
        // Abstand 0,5 gilt noch als gleiche Höhe.
        var segmente = Verbindungslinien.ZwischenSpalten(new Punkt(100, 50), new Punkt(100, 50.5), new Punkt(200, 50));

        segmente.Should().HaveCount(3).And.NotContain(s => s.Von.X == 150 && s.Nach.X == 150);
    }

    [Fact]
    public void ZwischenSpalten_NurRechtesKind_MitteAusDessenKante()
    {
        var segmente = Verbindungslinien.ZwischenSpalten(null, new Punkt(120, 80), new Punkt(200, 80));

        segmente.Should().Equal(
            new Segment(new Punkt(120, 80), new Punkt(160, 80)),
            new Segment(new Punkt(160, 80), new Punkt(200, 80)));
    }

    [Fact]
    public void ZwischenSpalten_NurLinkesKind()
    {
        var segmente = Verbindungslinien.ZwischenSpalten(new Punkt(100, 40), null, new Punkt(200, 60));

        segmente.Should().Equal(
            new Segment(new Punkt(100, 40), new Punkt(150, 40)),
            new Segment(new Punkt(150, 60), new Punkt(200, 60)));
    }

    [Fact]
    public void ZwischenSpalten_OhneKinder_KeineLinie()
    {
        Verbindungslinien.ZwischenSpalten(null, null, new Punkt(200, 60)).Should().BeEmpty();
    }
}
