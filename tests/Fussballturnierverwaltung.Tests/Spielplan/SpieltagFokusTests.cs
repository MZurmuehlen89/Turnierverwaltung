using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;

namespace Fussballturnierverwaltung.Tests.Spielplan;

public class SpieltagFokusTests
{
    [Fact]
    public void StartSpieltag_OffeneSpiele_IstDerErsteSpieltagMitOffenemSpiel() =>
        SpieltagFokus.StartSpieltag([(1, true), (1, true), (3, false), (2, true), (2, false)]).Should().Be(2);

    [Fact]
    public void StartSpieltag_AllesGespielt_IstDerLetzteSpieltag() =>
        SpieltagFokus.StartSpieltag([(1, true), (3, true), (2, true)]).Should().Be(3);

    [Fact]
    public void StartSpieltag_NochNichtsGespielt_IstDerErsteSpieltag() =>
        SpieltagFokus.StartSpieltag([(2, false), (1, false)]).Should().Be(1);

    [Fact]
    public void StartSpieltag_SpieleOhneSpieltag_ZaehlenNicht() =>
        SpieltagFokus.StartSpieltag([(null, false), (1, true), (2, true)]).Should().Be(2);

    [Fact]
    public void StartSpieltag_KeinSpielMitSpieltag_IstNull()
    {
        SpieltagFokus.StartSpieltag([]).Should().BeNull();
        SpieltagFokus.StartSpieltag([(null, false)]).Should().BeNull();
    }
}
