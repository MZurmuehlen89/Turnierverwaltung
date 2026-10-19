using FluentAssertions;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

namespace Fussballturnierverwaltung.Tests.Ranglisten;

public class AbschnittsUebersichtTests
{
    [Fact]
    public void Fuer_SortiertNachPlatz_UndSchliesstLueckenlosAn()
    {
        var rangliste = new Rangliste
        {
            Abschnitte =
            [
                new RanglistenAbschnitt { Name = "2. Bundesliga", Spielklasse = 2, BisPlatz = 36 },
                new RanglistenAbschnitt { Name = "Bundesliga", Spielklasse = 1, BisPlatz = 18 },
            ]
        };

        AbschnittsUebersicht.Fuer(rangliste).Select(z => z.Anzeige).Should().Equal(
            "Platz 1–18 · Bundesliga (Spielklasse 1)",
            "Platz 19–36 · 2. Bundesliga (Spielklasse 2)");
    }

    [Fact]
    public void Fuer_OhneAbschnitte_IstLeer() =>
        AbschnittsUebersicht.Fuer(new Rangliste()).Should().BeEmpty();
}
