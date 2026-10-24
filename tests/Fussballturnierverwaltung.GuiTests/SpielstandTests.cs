using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Lesen der Anzeigetexte (#522) – ohne laufende App.</summary>
public class SpielstandTests
{
    private static SpielZeile Spiel(string ergebnis, string zusatz = "") => new("Heim", ergebnis, zusatz, "Gast", "Beendet");

    [Theory]
    [InlineData("2:1", "", "Heim")]
    [InlineData("1:2", "(1:1) n.V.", "Gast")]
    [InlineData("1:1", "i.E. 4:3", "Heim")]
    [InlineData("0:0", "(0:0) i.E. 2:4", "Gast")]
    public void Sieger_NachEndstandOderElfmeterschiessen(string ergebnis, string zusatz, string sieger)
    {
        Spiel(ergebnis, zusatz).Sieger.Should().Be(sieger);
    }

    [Theory]
    [InlineData("1:1", "(0:1)")]
    [InlineData("-:-", "")]
    public void Sieger_UnentschiedenOderUngespielt_KeinSieger(string ergebnis, string zusatz)
    {
        Spiel(ergebnis, zusatz).Sieger.Should().BeNull();
    }

    [Fact]
    public void Verlierer_IstDerAndere()
    {
        Spiel("0:3").Verlierer.Should().Be("Heim");
    }

    [Fact]
    public void IstGespielt_NurMitLesbaremEndstand()
    {
        Spiel("2:2").IstGespielt.Should().BeTrue();
        Spiel("-:-").IstGespielt.Should().BeFalse();
    }

    [Fact]
    public void Fortschritt_AusDerKopfzeile()
    {
        Spielstand.Fortschritt("Runde 2 von 4 · 12 / 16 Spiele gespielt").Should().Be((12, 16));
    }

    [Fact]
    public void Fortschritt_Unlesbar_Null()
    {
        Spielstand.Fortschritt("Kein Turnier").Should().BeNull();
    }
}
