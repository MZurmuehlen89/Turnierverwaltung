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
}
