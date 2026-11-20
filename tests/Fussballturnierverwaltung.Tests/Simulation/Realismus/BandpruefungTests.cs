using FluentAssertions;

namespace Fussballturnierverwaltung.Tests.Simulation.Realismus;

/// <summary>#683, K3: die Bandprüfung mit Rauschzuschlag (z = 2 Standardfehler).</summary>
public class BandpruefungTests
{
    private static bool Gruen(double wert, double se, double von, double bis) =>
        Bandpruefung.Pruefen("Test", wert, se, von, bis, "Ziel").Gruen;

    [Theory]
    [InlineData(0.50, true)]   // im Band
    [InlineData(0.40, true)]   // genau auf der Untergrenze
    [InlineData(0.60, true)]   // genau auf der Obergrenze
    [InlineData(0.619, true)]  // über der Obergrenze, aber innerhalb von 2 SE
    [InlineData(0.621, false)] // weiter als 2 SE über der Obergrenze
    [InlineData(0.381, true)]  // unter der Untergrenze, aber innerhalb von 2 SE
    [InlineData(0.379, false)] // weiter als 2 SE unter der Untergrenze
    public void ZweiseitigesBand_RotErstJenseitsVonZweiStandardfehlern(double wert, bool erwartet) =>
        Gruen(wert, 0.01, 0.40, 0.60).Should().Be(erwartet);

    [Theory]
    [InlineData(0.80, true)]
    [InlineData(0.915, true)]  // über der Obergrenze, innerhalb von 2 SE
    [InlineData(0.925, false)] // weiter als 2 SE darüber
    [InlineData(-5.0, true)]   // nach unten gibt es keine Grenze
    public void NurObergrenze_NachUntenOffen(double wert, bool erwartet) =>
        Gruen(wert, 0.01, double.NegativeInfinity, 0.90).Should().Be(erwartet);

    [Theory]
    [InlineData(1.20, true)]
    [InlineData(1.085, true)]  // unter der Untergrenze, innerhalb von 2 SE
    [InlineData(1.075, false)] // weiter als 2 SE darunter
    [InlineData(50.0, true)]   // nach oben gibt es keine Grenze
    public void NurUntergrenze_NachObenOffen(double wert, bool erwartet) =>
        Gruen(wert, 0.01, 1.10, double.PositiveInfinity).Should().Be(erwartet);

    [Fact]
    public void OhneRauschen_GiltDasBandGenau()
    {
        Gruen(0.6001, 0, 0.40, 0.60).Should().BeFalse();
        Gruen(0.5999, 0, 0.40, 0.60).Should().BeTrue();
    }

    [Theory]
    [InlineData(0.50, false)] // im Band: grün ohne Schätzung
    [InlineData(0.61, true)]  // außerhalb: der Zuschlag braucht den Standardfehler
    public void KennzahlImBand_SchaetztKeinRauschen(double wert, bool geschaetzt)
    {
        var kennzahl = new Realismuskennzahl("Test", 0.40, 0.60, "Ziel", "P1", _ => 0);
        bool aufgerufen = false;

        var ergebnis = Bandpruefung.Pruefen(kennzahl, wert, () => { aufgerufen = true; return 0.01; });

        aufgerufen.Should().Be(geschaetzt);
        ergebnis.Gruen.Should().BeTrue();
    }

    [Fact]
    public void KennzahlOhneRauschzuschlag_RotSchonKnappAusserhalbDesBandes()
    {
        var kennzahl = new Realismuskennzahl("Maximum", 0, 0.030, "Ziel", "P1", _ => 0, MitRauschzuschlag: false);

        var ergebnis = Bandpruefung.Pruefen(kennzahl, 0.031, 0.01);

        ergebnis.Gruen.Should().BeFalse("ohne Rauschzuschlag zählt nur das Band, nicht der Standardfehler");
        ergebnis.Zeile.Should().Contain("ohne Rauschzuschlag");
    }

    [Fact]
    public void Berichtszeile_NenntNameWertRauschenUndBand()
    {
        var ergebnis = Bandpruefung.Pruefen("Remis", 0.262, 0.002, 0.220, 0.275, "BL 24,7 %");
        ergebnis.Zeile.Should().Contain("Remis").And.Contain("26,2").And.Contain("22,0").And.Contain("27,5").And.Contain("BL 24,7 %");
    }

    [Fact]
    public void Berichtszeile_EinseitigesBandZeigtUnendlich()
    {
        Bandpruefung.Pruefen("DC", 0.89, 0.01, double.NegativeInfinity, 0.90, "Ziel", "F2").Zeile.Should().Contain("∞");
    }
}
