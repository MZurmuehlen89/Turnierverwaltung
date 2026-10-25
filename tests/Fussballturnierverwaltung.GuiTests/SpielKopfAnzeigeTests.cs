using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Lesen der Spielköpfe und Gesamtwertung von Hin- und Rückspiel (#536) – ohne laufende App.</summary>
public class SpielKopfAnzeigeTests
{
    private const string ELFMETER = "im Elfmeterschießen";

    [Theory]
    [InlineData("2:1", "", "HZ 1:0", 2, 1, 2, 1)]
    [InlineData("3:2", "nach Verlängerung", "HZ 0:1 · 90' 1:1 · n.V. 3:2", 1, 1, 3, 2)]
    [InlineData("4:3", ELFMETER, "HZ 0:0 · 90' 1:1 · n.V. 2:2", 1, 1, 2, 2)]
    [InlineData("5:4", ELFMETER, "90' 0:0", 0, 0, 0, 0)]
    [InlineData("1:1", "", "", 1, 1, 1, 1)]
    public void Tore_NachNeunzigMinutenUndOhneElfmeter(
        string score, string entscheidung, string zwischenstaende, int heim90, int auswaerts90, int heim, int auswaerts)
    {
        var kopf = new SpielKopfAnzeige("A", "B", score, entscheidung, zwischenstaende);

        kopf.ToreNach90.Should().Be((heim90, auswaerts90));
        kopf.Tore.Should().Be((heim, auswaerts));
    }

    [Fact]
    public void Elfmeter_NurNachElfmeterschiessen()
    {
        new SpielKopfAnzeige("A", "B", "4:3", ELFMETER, "90' 1:1").Elfmeter.Should().Be((4, 3));
        new SpielKopfAnzeige("A", "B", "2:1", "nach Verlängerung", "90' 1:1 · n.V. 2:1").Elfmeter.Should().BeNull();
    }

    [Theory]
    // Rückspiel aus Sicht von B (Heim im Rückspiel)
    [InlineData("2:0", "1:1", "", "", "3:1", "Sieger: A")]
    [InlineData("0:1", "2:0", "", "", "0:3", "Sieger: B")]
    [InlineData("1:0", "1:0", "", "", "1:1", HinUndRueckspiel.UNENTSCHIEDEN)]
    [InlineData("1:0", "2:1", "nach Verlängerung", "90' 1:0 · n.V. 2:1", "2:2", HinUndRueckspiel.UNENTSCHIEDEN)]
    [InlineData("1:0", "3:5", ELFMETER, "90' 1:0 · n.V. 1:0", "1:1", "Sieger: A")]
    [InlineData("0:0", "4:2", ELFMETER, "90' 0:0", "0:0", "Sieger: B")]
    public void Erwartet_GesamtstandUndSieger(
        string hinScore, string rueckScore, string rueckEntscheidung, string rueckZwischenstaende,
        string gesamt, string siegerzeile)
    {
        var hinspiel = new SpielKopfAnzeige("A", "B", hinScore);
        var rueckspiel = new SpielKopfAnzeige("B", "A", rueckScore, rueckEntscheidung, rueckZwischenstaende);

        HinUndRueckspiel.Erwartet(hinspiel, rueckspiel).Should().Be((gesamt, siegerzeile));
    }

    [Theory]
    [InlineData("1:0", "1:0", "", true)]
    [InlineData("1:0", "2:1", "90' 1:0 · n.V. 2:1", true)]
    [InlineData("2:1", "0:1", "", false)]
    public void GleichstandNach180_ZaehltNurDieRegulaereSpielzeit(
        string hinScore, string rueckScore, string rueckZwischenstaende, bool gleichstand)
    {
        var hinspiel = new SpielKopfAnzeige("A", "B", hinScore);
        var rueckspiel = new SpielKopfAnzeige("B", "A", rueckScore, "", rueckZwischenstaende);

        HinUndRueckspiel.GleichstandNach180(hinspiel, rueckspiel).Should().Be(gleichstand);
    }
}
