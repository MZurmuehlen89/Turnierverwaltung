using FluentAssertions;
using Fussballturnierverwaltung.Application.Turnierbaum;

namespace Fussballturnierverwaltung.Tests.Turnierbaum;

/// <summary>Verbindungslinien des Turnierbaums (#506 aus der Ansicht herausgelöst).</summary>
public class VerbindungslinienTests
{
    // ── Gesamtübersicht: zwischen zwei Karten ────────────────────────────

    [Fact]
    public void ZwischenKarten_KindLinks_VonRechterKindkanteZurLinkenElternkante()
    {
        var punkte = Verbindungslinien.ZwischenKarten(kindX: 16, kindMitteY: 100, elternX: 250, elternMitteY: 140,
            kartenBreite: 190);

        // x1 = 16 + 190 = 206, x2 = 250, Mitte 228.
        punkte.Should().Equal(new Punkt(206, 100), new Punkt(228, 100), new Punkt(228, 140), new Punkt(250, 140));
    }

    [Fact]
    public void ZwischenKarten_KindRechts_Gespiegelt()
    {
        var punkte = Verbindungslinien.ZwischenKarten(kindX: 500, kindMitteY: 100, elternX: 266, elternMitteY: 140,
            kartenBreite: 190);

        // x1 = 500 (linke Kindkante), x2 = 266 + 190 = 456 (rechte Elternkante), Mitte 478.
        punkte.Should().Equal(new Punkt(500, 100), new Punkt(478, 100), new Punkt(478, 140), new Punkt(456, 140));
    }

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
