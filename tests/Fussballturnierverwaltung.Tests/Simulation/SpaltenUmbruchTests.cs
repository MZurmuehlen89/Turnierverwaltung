using FluentAssertions;
using Fussballturnierverwaltung.WinUi.Views;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>#247: Umbruchentscheidung des SpaltenPanel (reine Funktion, ins Testprojekt verknüpft).</summary>
public class SpaltenUmbruchTests
{
    private const double MIN = 380;

    [Theory]
    [InlineData(1000, 1, false, false)]   // eine Karte: nie untereinander
    [InlineData(772, 2, false, false)]    // 2 × 380 + 12: gerade genug
    [InlineData(771, 2, false, true)]     // einen Pixel zu schmal
    [InlineData(1163, 3, false, true)]    // drei Spalten brauchen 3 × 380 + 24 = 1164
    [InlineData(1164, 3, false, false)]
    public void Untereinander_UnterDerMindestbreiteJeSpalte(double breite, int anzahl, bool bisher, bool erwartet)
    {
        SpaltenUmbruch.Untereinander(breite, anzahl, MIN, bisher).Should().Be(erwartet);
    }

    [Fact]
    public void Untereinander_ZurueckNebeneinanderErstMitReserveFuerDenBalken()
    {
        // Untereinander blendet der Bildlauf einen Balken ein (Breite sinkt um bis zu HYSTERESE): bei 780 px (Balken weg:
        // 772 reichen) bliebe das Layout sonst nicht stabil.
        SpaltenUmbruch.Untereinander(780, 2, MIN, bisherUntereinander: true).Should().BeTrue();
        SpaltenUmbruch.Untereinander(772 + 2 * SpaltenUmbruch.HYSTERESE, 2, MIN, bisherUntereinander: true).Should().BeFalse(); // Reserve je Spalte
        SpaltenUmbruch.Untereinander(771 + 2 * SpaltenUmbruch.HYSTERESE, 2, MIN, bisherUntereinander: true).Should().BeTrue();
        SpaltenUmbruch.Untereinander(780, 2, MIN, bisherUntereinander: false).Should().BeFalse();
    }

    [Fact]
    public void Spaltenbreite_ZiehtDieAbstaendeAb()
    {
        SpaltenUmbruch.Spaltenbreite(772, 2).Should().Be(380);
    }
}
