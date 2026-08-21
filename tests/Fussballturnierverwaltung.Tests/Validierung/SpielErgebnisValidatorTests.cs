using FluentAssertions;
using Fussballturnierverwaltung.Application.Spiele.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Validierung;

public class SpielErgebnisValidatorTests
{
    private readonly SpielErgebnisValidator _validator = new();

    private static SpielErgebnisEingabe GueltigeEingabe(
        int heim = 2, int auswaerts = 1,
        int? halbzeitHeim = null, int? halbzeitAuswaerts = null) =>
        new(1, 2, heim, auswaerts, null, null, null, null,
            EntscheidungArt.RegulaereSpielzeit, 1, halbzeitHeim, halbzeitAuswaerts);

    [Fact]
    public void OhneHalbzeitstand_IstGueltig()
    {
        var ergebnis = _validator.Validieren(GueltigeEingabe());
        ergebnis.IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void MitPassendemHalbzeitstand_IstGueltig()
    {
        var ergebnis = _validator.Validieren(GueltigeEingabe(halbzeitHeim: 1, halbzeitAuswaerts: 0));
        ergebnis.IstGueltig.Should().BeTrue();
    }

    [Theory]
    [InlineData(1, null)]
    [InlineData(null, 0)]
    public void NurEineSeiteAngegeben_IstUngueltig(int? halbzeitHeim, int? halbzeitAuswaerts)
    {
        var ergebnis = _validator.Validieren(GueltigeEingabe(halbzeitHeim: halbzeitHeim, halbzeitAuswaerts: halbzeitAuswaerts));
        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*für beide Mannschaften*");
    }

    [Fact]
    public void NegativerHalbzeitstand_IstUngueltig()
    {
        var ergebnis = _validator.Validieren(GueltigeEingabe(halbzeitHeim: -1, halbzeitAuswaerts: 0));
        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*nicht negativ*");
    }

    [Fact]
    public void HalbzeitstandUeberschreitetEndstand_IstUngueltig()
    {
        // 2 Tore zur Halbzeit, aber nur 1 Tor im Endstand — physikalisch nicht möglich.
        var ergebnis = _validator.Validieren(GueltigeEingabe(heim: 1, auswaerts: 1, halbzeitHeim: 2, halbzeitAuswaerts: 0));
        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*darf den Stand der regulären Spielzeit nicht überschreiten*");
    }

    [Fact]
    public void HalbzeitstandGleichEndstand_IstGueltig()
    {
        var ergebnis = _validator.Validieren(GueltigeEingabe(heim: 1, auswaerts: 0, halbzeitHeim: 1, halbzeitAuswaerts: 0));
        ergebnis.IstGueltig.Should().BeTrue();
    }
}
