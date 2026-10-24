using FluentAssertions;
using Fussballturnierverwaltung.Application.Stammdaten.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Validierung;

public class MannschaftValidatorTests
{
    private readonly MannschaftValidator _validator = new();

    private static MannschaftErstellenEingabe GueltigeEingabe(
        int off = 80, int mid = 80, int def = 80, int heim = 80, int ausw = 75) =>
        new("Deutschland", "GER", Mannschaftsart.Nationalmannschaft, 1, off, mid, def, heim, ausw);

    [Fact]
    public void GueltigeEingabe_IstGueltig()
    {
        var ergebnis = _validator.Validieren(GueltigeEingabe());
        ergebnis.IstGueltig.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]   // zu niedrig
    [InlineData(101)] // zu hoch
    [InlineData(-5)]  // negativ
    public void RatingAusserhalb_IstUngueltig(int rating)
    {
        var ergebnis = _validator.Validieren(GueltigeEingabe(off: rating));
        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*zwischen 1 und 100*");
    }

    [Theory]
    [InlineData(1)]   // Minimalwert gültig
    [InlineData(100)] // Maximalwert gültig
    public void RatingGrenzwerte_SindGueltig(int rating)
    {
        var ergebnis = _validator.Validieren(GueltigeEingabe(off: rating, mid: rating, def: rating, heim: rating, ausw: rating));
        ergebnis.IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void LeererName_IstUngueltig()
    {
        var eingabe = new MannschaftErstellenEingabe("", "GER", Mannschaftsart.Nationalmannschaft, 1, 80, 80, 80, 80, 75);
        var ergebnis = _validator.Validieren(eingabe);
        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().Contain(f => f.Contains("name", StringComparison.OrdinalIgnoreCase) && f.Contains("leer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MehrereFehler_WerdenGesammelt()
    {
        var eingabe = new MannschaftErstellenEingabe("", "", Mannschaftsart.Nationalmannschaft, 0, 0, 200, 80, 80, 75);
        var ergebnis = _validator.Validieren(eingabe);
        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Count.Should().BeGreaterThan(1);
    }
}
