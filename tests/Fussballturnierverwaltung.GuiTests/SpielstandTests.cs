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

    // ── Hin- und Rückspiel ────────────────────────────────────────────────────

    private static SpielZeile Hin(string ergebnis) => new("A", ergebnis, "", "B", "Beendet", "HF 1 H");

    private static SpielZeile Rueck(string ergebnis, string zusatz = "") => new("B", ergebnis, zusatz, "A", "Beendet", "HF 1 R");

    [Theory]
    [InlineData("2:0", "1:0", false, "A")]   // Gesamt 2:1
    [InlineData("1:1", "2:2", true, "A")]    // Gesamt 3:3, auswärts A 2, B 1
    [InlineData("2:1", "1:0", true, "B")]    // Gesamt 2:2, auswärts A 0, B 1
    [InlineData("1:0", "1:0", true, "A")]    // Gesamt 1:1, auswärts je 0 → Elfmeter 3:4 aus Sicht von B
    public void Weiterkommender_NachGesamtAuswaertstorenUndElfmeter(string hin, string rueck, bool regel, string weiter)
    {
        Spielstand.Weiterkommender(Hin(hin), Rueck(rueck, "(1:0) i.E. 3:4"), regel).Should().Be(weiter);
    }

    [Fact]
    public void Weiterkommender_OhneAuswaertstorregel_EntscheidetDasElfmeterschiessen()
    {
        Spielstand.Weiterkommender(Hin("1:1"), Rueck("2:2", "(2:2) i.E. 5:4"), auswaertstorregel: false).Should().Be("B");
    }

    [Fact]
    public void Weiterkommender_UnentschiedenOhneElfmeter_Null()
    {
        Spielstand.Weiterkommender(Hin("1:1"), Rueck("1:1"), auswaertstorregel: true).Should().BeNull();
    }

    [Fact]
    public void Weiterkommender_FalschesGegenstueck_Fehler()
    {
        var fremd = new SpielZeile("C", "0:0", "", "A", "Beendet", "HF 1 R");
        FluentActions.Invoking(() => Spielstand.Weiterkommender(Hin("1:0"), fremd, false)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void HinUndRueckspiele_PaartNachGetauschtemHeimrecht()
    {
        var c = new SpielZeile("C", "0:1", "", "D", "Beendet", "HF 2 H");
        var d = new SpielZeile("D", "2:0", "", "C", "Beendet", "HF 2 R");
        var paare = Spielstand.HinUndRueckspiele([Hin("1:0"), Rueck("0:0"), c, d]);
        paare.Should().HaveCount(2);
        paare.Should().Contain(p => p.Hin.Heim == "C" && p.Rueck.Heim == "D");
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
