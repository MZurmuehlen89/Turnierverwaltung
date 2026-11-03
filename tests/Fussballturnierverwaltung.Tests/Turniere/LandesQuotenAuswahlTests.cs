using FluentAssertions;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Tests.Turniere;

/// <summary>Was eine Landes-Quoten-Zeile neben den übrigen anbieten darf (#572).</summary>
public class LandesQuotenAuswahlTests
{
    private static readonly Land _deutschland = new()
    {
        Id = 1, Name = "Deutschland",
        Rangliste = new Rangliste { Abschnitte = [new RanglistenAbschnitt { Spielklasse = 1, BisPlatz = 18 }] },
    };

    private static readonly Land _malta = new() { Id = 2, Name = "Malta" };

    [Fact]
    public void LandWaehlbar_OhneWeitereZeile() =>
        LandesQuotenAuswahl.LandWaehlbar(_malta, [new LandesQuoteZeileEingabe(1, 4)]).Should().BeTrue();

    [Fact]
    public void LandWaehlbar_NebenKontingentenMitSpielklasse() =>
        LandesQuotenAuswahl.LandWaehlbar(_deutschland, [new LandesQuoteZeileEingabe(1, 4, Spielklasse: 1)])
            .Should().BeTrue();

    [Fact]
    public void LandWaehlbar_NichtNebenZeileOhneSpielklasse() =>
        LandesQuotenAuswahl.LandWaehlbar(_deutschland, [new LandesQuoteZeileEingabe(1, 4)]).Should().BeFalse();

    [Fact]
    public void LandWaehlbar_OhnePyramide_NurEinmal() =>
        LandesQuotenAuswahl.LandWaehlbar(_malta, [new LandesQuoteZeileEingabe(2, 1)]).Should().BeFalse();

    [Fact]
    public void LandWaehlbar_NebenPokalsieger() =>
        LandesQuotenAuswahl.LandWaehlbar(_deutschland, [new LandesQuoteZeileEingabe(1, 1, Pokalsieger: true)])
            .Should().BeTrue();

    [Fact]
    public void PokalsiegerWaehlbar_NurEinmalJeLand()
    {
        LandesQuotenAuswahl.PokalsiegerWaehlbar(1, [new LandesQuoteZeileEingabe(1, 4, Spielklasse: 1)]).Should().BeTrue();
        LandesQuotenAuswahl.PokalsiegerWaehlbar(1, [new LandesQuoteZeileEingabe(2, 1, Pokalsieger: true)]).Should().BeTrue();
        LandesQuotenAuswahl.PokalsiegerWaehlbar(1, [new LandesQuoteZeileEingabe(1, 1, Pokalsieger: true)]).Should().BeFalse();
    }

    [Fact]
    public void NaechsterFreierPlatz_PokalsiegerBelegtKeinenPlatz() =>
        LandesQuotenAuswahl.NaechsterFreierPlatz(1, 1, [new LandesQuoteZeileEingabe(1, 1, AbPlatz: 6, Pokalsieger: true)])
            .Should().Be(1);

    [Fact]
    public void GanzesLandWaehlbar_NurOhneWeitereZeileDesLandes()
    {
        LandesQuotenAuswahl.GanzesLandWaehlbar(1, [new LandesQuoteZeileEingabe(2, 1)]).Should().BeTrue();
        LandesQuotenAuswahl.GanzesLandWaehlbar(1, [new LandesQuoteZeileEingabe(1, 4, Spielklasse: 1)]).Should().BeFalse();
    }

    [Fact]
    public void NaechsterFreierPlatz_HinterDenKontingentenDerSpielklasse() =>
        LandesQuotenAuswahl.NaechsterFreierPlatz(1, 1,
            [
                new LandesQuoteZeileEingabe(1, 4, Spielklasse: 1),
                new LandesQuoteZeileEingabe(1, 2, Spielklasse: 1, AbPlatz: 5),
                new LandesQuoteZeileEingabe(1, 18, Spielklasse: 2),
                new LandesQuoteZeileEingabe(3, 9, Spielklasse: 1),
            ])
            .Should().Be(7);

    [Fact]
    public void NaechsterFreierPlatz_OhneKontingentInDerSpielklasse_Platz1() =>
        LandesQuotenAuswahl.NaechsterFreierPlatz(1, 2, [new LandesQuoteZeileEingabe(1, 4, Spielklasse: 1)]).Should().Be(1);
}
