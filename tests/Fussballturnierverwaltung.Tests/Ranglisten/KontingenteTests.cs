using FluentAssertions;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

namespace Fussballturnierverwaltung.Tests.Ranglisten;

/// <summary>Kontingente (#572): Spielklasse + ab Platz + Anzahl → Ranglistenplätze.</summary>
public class KontingenteTests
{
    // Wie die deutsche Pyramide im Kleinen: Bundesliga 1–4, 2. Bundesliga 5–8, zwei Regionalliga-Staffeln 9–10 und
    // 11–12. Abschnitte bewusst ungeordnet.
    private static Rangliste Pyramide() => new()
    {
        Abschnitte =
        [
            new RanglistenAbschnitt { Name = "Regionalliga West", Spielklasse = 3, BisPlatz = 12 },
            new RanglistenAbschnitt { Name = "Bundesliga", Spielklasse = 1, BisPlatz = 4 },
            new RanglistenAbschnitt { Name = "Regionalliga Nordost", Spielklasse = 3, BisPlatz = 10 },
            new RanglistenAbschnitt { Name = "2. Bundesliga", Spielklasse = 2, BisPlatz = 8 },
        ],
    };

    [Fact]
    public void Spielklassen_FasstStaffelnZusammenUndOrdnetNachKlasse() =>
        Kontingente.Spielklassen(Pyramide()).Should().Equal(
            new SpielklassenInfo(1, "Bundesliga", 1, 4),
            new SpielklassenInfo(2, "2. Bundesliga", 5, 8),
            new SpielklassenInfo(3, "Regionalliga", 9, 12));

    [Fact]
    public void Spielklassen_OhneGemeinsamenWortanfang_HeisstNachNummer()
    {
        var rangliste = new Rangliste
        {
            Abschnitte =
            [
                new RanglistenAbschnitt { Name = "Nord", Spielklasse = 1, BisPlatz = 2 },
                new RanglistenAbschnitt { Name = "Süd", Spielklasse = 1, BisPlatz = 4 },
            ],
        };

        Kontingente.Spielklassen(rangliste).Single().Name.Should().Be("Spielklasse 1");
    }

    [Fact]
    public void Spielklassen_OhneAbschnitte_Leer() =>
        Kontingente.Spielklassen(new Rangliste()).Should().BeEmpty();

    [Fact]
    public void Bereich_AbPlatzInnerhalbDerSpielklasse()
    {
        var bereich = Kontingente.Bereich(Pyramide(), spielklasse: 2, abPlatz: 2, anzahl: 2)!;

        bereich.Anzeige.Should().Be("2. Bundesliga, Plätze 2–3");
        (bereich.ErsterRang, bereich.LetzterRang).Should().Be((6, 7));
    }

    [Fact]
    public void Bereich_UeberParalleleStaffelnHinweg()
    {
        var bereich = Kontingente.Bereich(Pyramide(), spielklasse: 3, abPlatz: 2, anzahl: 2)!;

        (bereich.ErsterRang, bereich.LetzterRang).Should().Be((10, 11));
    }

    [Fact]
    public void Bereich_EinPlatz_Einzahl() =>
        Kontingente.Bereich(Pyramide(), 1, 1, 1)!.Anzeige.Should().Be("Bundesliga, Platz 1");

    [Fact]
    public void Bereich_GanzeSpielklasse_Passt() =>
        Kontingente.Bereich(Pyramide(), 1, 1, 4).Should().NotBeNull();

    [Theory]
    [InlineData(1, 1, 5)]   // reicht über das Ende der Spielklasse
    [InlineData(1, 4, 2)]
    [InlineData(1, 0, 1)]   // Platz 0 gibt es nicht
    [InlineData(1, 1, 0)]   // leeres Kontingent
    [InlineData(4, 1, 1)]   // Spielklasse fehlt
    public void Bereich_PasstNicht_Null(int spielklasse, int abPlatz, int anzahl) =>
        Kontingente.Bereich(Pyramide(), spielklasse, abPlatz, anzahl).Should().BeNull();
}
