using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>Spielklassen aus einer Ligapyramide und das Heimrecht des Unterklassigen (Issue #393).</summary>
public class SpielklassenTests
{
    // Klassen: 1 = Bundesliga, 2 = 2. Bundesliga, 3 = 3. Liga, 4 = Regionalliga.
    private static Spielklassen Pyramide(int? profigrenze) =>
        new(new Dictionary<int, int> { [1] = 1, [2] = 2, [3] = 3, [4] = 4, [5] = 4 }, profigrenze);

    [Theory]
    [InlineData(3, 1, true)]   // Drittligist gegen Bundesligist
    [InlineData(4, 2, true)]   // Regionalligist gegen Zweitligist
    [InlineData(2, 1, false)]  // zwei Profiklubs: Los
    [InlineData(4, 3, false)]  // zwei Amateurklubs: Los
    [InlineData(1, 3, false)]  // der Höherklassige hat nie Heimrecht
    [InlineData(4, 5, false)]  // gleiche Klasse (Staffeln)
    public void MitProfigrenze_NurAmateurGegenProfi(int mannschaft, int gegner, bool erwartet) =>
        Pyramide(profigrenze: 2).HatHeimrechtGegen(mannschaft, gegner).Should().Be(erwartet);

    [Theory]
    [InlineData(2, 1, true)]
    [InlineData(4, 3, true)]   // auch zwischen zwei unteren Ligen (Copa del Rey)
    [InlineData(4, 5, false)]
    [InlineData(1, 2, false)]
    public void OhneProfigrenze_StriktKlasseGegenKlasse(int mannschaft, int gegner, bool erwartet) =>
        Pyramide(profigrenze: null).HatHeimrechtGegen(mannschaft, gegner).Should().Be(erwartet);

    [Theory]
    [InlineData(99, 1)]
    [InlineData(1, 99)]
    [InlineData(null, 1)]
    public void OhneKlasse_KeinHeimrecht(int? mannschaft, int? gegner)
    {
        Pyramide(profigrenze: null).HatHeimrechtGegen(mannschaft, gegner).Should().BeFalse();
        Pyramide(profigrenze: null).HatHeimrechtGegen(gegner, mannschaft).Should().BeFalse();
    }

    [Fact]
    public void Aus_OrdnetPlaetzeDenAbschnittenZu()
    {
        var rangliste = new Rangliste
        {
            ProfigrenzeSpielklasse = 2,
            Abschnitte =
            [
                new RanglistenAbschnitt { Name = "2. Liga", Spielklasse = 2, BisPlatz = 4 },
                new RanglistenAbschnitt { Name = "1. Liga", Spielklasse = 1, BisPlatz = 2 },
                new RanglistenAbschnitt { Name = "Staffel Nord", Spielklasse = 3, BisPlatz = 5 },
                new RanglistenAbschnitt { Name = "Staffel Süd", Spielklasse = 3, BisPlatz = 6 },
            ],
            Eintraege =
            [
                new RanglistenEintrag { MannschaftId = 10, Rang = 1 },
                new RanglistenEintrag { MannschaftId = 20, Rang = 2 },
                new RanglistenEintrag { MannschaftId = 30, Rang = 3 },
                new RanglistenEintrag { MannschaftId = 50, Rang = 5 },
                new RanglistenEintrag { MannschaftId = 60, Rang = 6 },
                new RanglistenEintrag { MannschaftId = 70, Rang = 7 },  // hinter dem letzten Abschnitt
                new RanglistenEintrag { MannschaftId = 30, Rang = 8 },  // doppelt: der bessere Platz zählt
            ]
        };

        var klassen = Spielklassen.Aus(rangliste);

        klassen.Profigrenze.Should().Be(2);
        klassen.JeMannschaft.Should().BeEquivalentTo(new Dictionary<int, int>
        {
            [10] = 1, [20] = 1, [30] = 2, [50] = 3, [60] = 3
        });
    }

    [Fact]
    public void Aus_OhneAbschnitte_KenntKeineKlassen() =>
        Spielklassen.Aus(new Rangliste { Eintraege = [new RanglistenEintrag { MannschaftId = 1, Rang = 1 }] })
            .JeMannschaft.Should().BeEmpty();
}
