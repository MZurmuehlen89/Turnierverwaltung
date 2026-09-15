using FluentAssertions;
using Fussballturnierverwaltung.Application.Turniere;

namespace Fussballturnierverwaltung.Tests.Turniere;

public class RundenKonfigurationTests
{
    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(8, 3)]
    [InlineData(9, 4)]
    [InlineData(16, 4)]
    public void VorschlagAnzahlSpieltage_IstAufgerundeterLogarithmusZurBasis2(int mannschaftsanzahl, int erwartet) =>
        RundenKonfiguration.VorschlagAnzahlSpieltage(mannschaftsanzahl).Should().Be(erwartet);

    [Fact]
    public void ZuJson_UndZurueck_ErgibtGleichenWert()
    {
        var konfiguration = new RundenKonfiguration(AnzahlSpieltage: 5);

        var json = konfiguration.ZuJson();
        var zurueck = RundenKonfiguration.AusJson(json);

        zurueck.AnzahlSpieltage.Should().Be(5);
    }

    [Fact]
    public void ZuJson_OhneAnzahlSpieltage_GibtNull() =>
        new RundenKonfiguration().ZuJson().Should().BeNull();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kein-json")]
    public void AusJson_UngueltigOderLeer_GibtLeereKonfiguration(string? json) =>
        RundenKonfiguration.AusJson(json).AnzahlSpieltage.Should().BeNull();

    [Fact]
    public void GruppenAnzahlVorschlaege_Bei24Mannschaften_EnthaeltAlleTeilerPaare() =>
        RundenKonfiguration.GruppenAnzahlVorschlaege(24).Should().BeEquivalentTo(
        [
            (2, 12), (3, 8), (4, 6), (6, 4), (8, 3), (12, 2)
        ]);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GruppenAnzahlVorschlaege_UnterVierMannschaften_IstLeer(int mannschaftsanzahl) =>
        RundenKonfiguration.GruppenAnzahlVorschlaege(mannschaftsanzahl).Should().BeEmpty();

    [Fact]
    public void GruppenAnzahlVorschlaege_BeiPrimzahl_IstLeer() =>
        RundenKonfiguration.GruppenAnzahlVorschlaege(7).Should().BeEmpty();
}
