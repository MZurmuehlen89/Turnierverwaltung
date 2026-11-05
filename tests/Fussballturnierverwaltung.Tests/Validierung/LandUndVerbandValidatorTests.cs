using FluentAssertions;
using Fussballturnierverwaltung.Application.Stammdaten.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Validierung;

/// <summary>Land- und Verbandsprüfung mit Feldbezug (#602): Feld = Name der Eigenschaft in der Eingabe.</summary>
public class LandUndVerbandValidatorTests
{
    private static readonly byte[] _zuGrossesBild = new byte[2 * 1024 * 1024 + 1];

    [Fact]
    public void Land_GueltigeEingabe_IstGueltig() =>
        new LandValidator().Validieren(new LandErstellenEingabe("Deutschland", "DEU", "GER", Kontinent.Europa, 1))
            .IstGueltig.Should().BeTrue();

    [Fact]
    public void Land_FehlerTragenDenNamenDerEigenschaft()
    {
        var eingabe = new LandErstellenEingabe(" ", "", "GERM", Kontinent.Europa, 0, _zuGrossesBild);

        new LandValidator().Validieren(eingabe).Meldungen.Select(m => m.Feld).Should().Equal(
            nameof(eingabe.Name), nameof(eingabe.Kurzname), nameof(eingabe.FifaCode),
            nameof(eingabe.VerbandId), nameof(eingabe.Flagge));
    }

    [Fact]
    public void Land_AktualisierenPrueftWieErstellen()
    {
        var eingabe = new LandAktualisierenEingabe("", "DEU", "", Kontinent.Europa, 1);

        new LandValidator().Validieren(eingabe).Meldungen.Select(m => m.Feld).Should().Equal(
            nameof(eingabe.Name), nameof(eingabe.FifaCode));
    }

    [Fact]
    public void Verband_FehlerTragenDenNamenDerEigenschaft()
    {
        var eingabe = new VerbandErstellenEingabe("", " ", _zuGrossesBild);

        new VerbandValidator().Validieren(eingabe).Meldungen.Select(m => m.Feld).Should().Equal(
            nameof(eingabe.Name), nameof(eingabe.Kurzname), nameof(eingabe.Logo));
    }

    [Fact]
    public void Verband_AktualisierenPrueftAuchDieLaenge() =>
        new VerbandValidator().Validieren(new VerbandAktualisierenEingabe(new string('x', 201), "X"))
            .Meldungen.Should().ContainSingle().Which.Feld.Should().Be(nameof(VerbandAktualisierenEingabe.Name));

    [Fact]
    public void Verband_GueltigeEingabe_IstGueltig() =>
        new VerbandValidator().Validieren(new VerbandAktualisierenEingabe("UEFA", "UEFA"))
            .IstGueltig.Should().BeTrue();
}
