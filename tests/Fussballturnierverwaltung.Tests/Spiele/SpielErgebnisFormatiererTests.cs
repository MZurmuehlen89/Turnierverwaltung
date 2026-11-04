using FluentAssertions;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Spiele;

public class SpielErgebnisFormatiererTests
{
    private static SpielErgebnis Ergebnis(int heim, int auswaerts, EntscheidungArt art = EntscheidungArt.RegulaereSpielzeit,
        int? heimVerl = null, int? auswVerl = null, int? heimElf = null, int? auswElf = null) => new()
    {
        HeimToreRegulaer = heim, AuswaertsToreRegulaer = auswaerts,
        HeimToreVerlaengerung = heimVerl, AuswaertsToreVerlaengerung = auswVerl,
        HeimElfmetertore = heimElf, AuswaertsElfmetertore = auswElf,
        EntscheidungArt = art
    };

    // ── DetailText ────────────────────────────────────────────────────────

    [Fact]
    public void DetailText_RegulaereSpielzeit_IstLeer()
    {
        var text = SpielErgebnisFormatierer.DetailText(Ergebnis(2, 1));
        text.Should().BeEmpty();
    }

    [Fact]
    public void DetailText_Auswaertstorregel_ZeigtKuerzel()
    {
        var text = SpielErgebnisFormatierer.DetailText(Ergebnis(1, 2, EntscheidungArt.Auswaertstorregel));
        text.Should().Be("A.T.");
    }

    [Theory]
    [InlineData(EntscheidungArt.Verlaengerung, "(1:1) n.V.")]
    [InlineData(EntscheidungArt.GoldenGoal, "(1:1) G.G.")]
    [InlineData(EntscheidungArt.SilverGoal, "(1:1) S.G.")]
    public void DetailText_VerlaengerungMitToren_ZeigtRegulaerzeitKlammerUndKuerzel(EntscheidungArt art, string erwartet)
    {
        var text = SpielErgebnisFormatierer.DetailText(Ergebnis(1, 1, art, heimVerl: 1, auswVerl: 0));
        text.Should().Be(erwartet);
    }

    [Fact]
    public void DetailText_VerlaengerungOhneTore_NurKuerzel()
    {
        var text = SpielErgebnisFormatierer.DetailText(
            Ergebnis(1, 1, EntscheidungArt.Verlaengerung, heimVerl: 0, auswVerl: 0));
        text.Should().Be("n.V.");
    }

    [Fact]
    public void DetailText_ElfmeterschiessenNachTorloserVerlaengerung_OhneRegulaerzeitKlammer()
    {
        // Elfmeterschießen nach torloser Verlängerung: Endstand = Regulärzeit-Stand, die Klammer wäre redundant.
        var text = SpielErgebnisFormatierer.DetailText(
            Ergebnis(2, 2, EntscheidungArt.Elfmeterschiessen, heimVerl: 0, auswVerl: 0, heimElf: 5, auswElf: 4));
        text.Should().Be("i.E. 5:4");
    }

    [Fact]
    public void DetailText_ElfmeterschiessenNachVerlaengerungstoren_ZeigtKlammerUndElfmeterstand()
    {
        var text = SpielErgebnisFormatierer.DetailText(
            Ergebnis(1, 1, EntscheidungArt.Elfmeterschiessen, heimVerl: 1, auswVerl: 1, heimElf: 3, auswElf: 4));
        text.Should().Be("(1:1) i.E. 3:4");
    }

    [Fact]
    public void DetailText_AusAuswaertsSicht_SpiegeltBeideStaende()
    {
        // Mannschaftsseite des Auswärtsteams: Regulärzeit 2:1 und Elfmeter 3:4 aus dessen Sicht.
        var text = SpielErgebnisFormatierer.DetailText(
            Ergebnis(1, 2, EntscheidungArt.Elfmeterschiessen, heimVerl: 1, auswVerl: 0, heimElf: 3, auswElf: 4),
            ausHeimSicht: false);
        text.Should().Be("(2:1) i.E. 4:3");
    }

    // ── EntscheidungKuerzel (#438) ───────────────────────────────────────────

    [Theory]
    [InlineData(EntscheidungArt.RegulaereSpielzeit, "")]
    [InlineData(EntscheidungArt.Verlaengerung, "n.V.")]
    [InlineData(EntscheidungArt.Elfmeterschiessen, "i.E.")]
    [InlineData(EntscheidungArt.Auswaertstorregel, "A.T.")]
    [InlineData(EntscheidungArt.GoldenGoal, "G.G.")]
    [InlineData(EntscheidungArt.SilverGoal, "S.G.")]
    public void EntscheidungKuerzel_JeEntscheidungsart(EntscheidungArt art, string erwartet)
    {
        SpielErgebnisFormatierer.EntscheidungKuerzel(art).Should().Be(erwartet);
    }

    // ── HalbzeitKlammer (Issue #104) ─────────────────────────────────────────

    [Fact]
    public void HalbzeitKlammer_RegulaereSpielzeitMitAbweichendemHalbzeitstand_ZeigtKlammer()
    {
        var klammer = SpielErgebnisFormatierer.HalbzeitKlammer(
            EntscheidungArt.RegulaereSpielzeit, 2, 1, 1, 0);
        klammer.Should().Be("(1:0)");
    }

    [Fact]
    public void HalbzeitKlammer_OhneHalbzeitstand_IstLeer()
    {
        var klammer = SpielErgebnisFormatierer.HalbzeitKlammer(
            EntscheidungArt.RegulaereSpielzeit, 2, 1, null, null);
        klammer.Should().BeEmpty();
    }

    [Fact]
    public void HalbzeitKlammer_HalbzeitstandGleichEndstand_IstLeer()
    {
        // Kein 2. Halbzeit-Tor gefallen — Klammer wäre redundant zum bereits sichtbaren Endstand.
        var klammer = SpielErgebnisFormatierer.HalbzeitKlammer(
            EntscheidungArt.RegulaereSpielzeit, 1, 0, 1, 0);
        klammer.Should().BeEmpty();
    }

    [Theory]
    [InlineData(EntscheidungArt.Verlaengerung)]
    [InlineData(EntscheidungArt.Elfmeterschiessen)]
    [InlineData(EntscheidungArt.Auswaertstorregel)]
    public void HalbzeitKlammer_KeineReineRegulaerzeit_IstLeer(EntscheidungArt entscheidungArt)
    {
        // Bei n.V./i.E./A.T. steht an derselben Anzeige-Position bereits DetailText — kein Platz
        // für eine zusätzliche Halbzeit-Klammer (Issue #104-Abgrenzung).
        var klammer = SpielErgebnisFormatierer.HalbzeitKlammer(
            entscheidungArt, 1, 1, 1, 0);
        klammer.Should().BeEmpty();
    }

    // ── Hin-/Rückspiel-Paarung (Issue #340) ───────────────────────────────────

    [Fact]
    public void RueckspielGesamtText_ZeigtGesamtstandAusSichtDesRueckspielHeimteams()
    {
        // Hinspiel A–B 1:0, Rückspiel B–A 3:1 → Gesamt aus Sicht B: 3:2.
        var text = SpielErgebnisFormatierer.RueckspielGesamtText(Ergebnis(1, 0), Ergebnis(3, 1));
        text.Should().Be("Gesamt 3:2");
    }

    [Fact]
    public void RueckspielGesamtText_Elfmeterschiessen_HaengtKuerzelAn()
    {
        // Hinspiel A–B 1:0, Rückspiel B–A 2:1 → 2:2 gesamt, keine Auswärtstorregel, i.E. 2:4.
        var text = SpielErgebnisFormatierer.RueckspielGesamtText(
            Ergebnis(1, 0), Ergebnis(2, 1, EntscheidungArt.Elfmeterschiessen, 0, 0, 2, 4));
        text.Should().Be("Gesamt 2:2 · i.E. 2:4");
    }

    [Fact]
    public void RueckspielGesamtText_Verlaengerung_ZaehltVerlaengerungstoreMit()
    {
        // Hinspiel A–B 5:2, Rückspiel B–A 3:0 reg. + 0:2 n.V. → Gesamt aus Sicht B: 5:7.
        var text = SpielErgebnisFormatierer.RueckspielGesamtText(
            Ergebnis(5, 2), Ergebnis(3, 0, EntscheidungArt.Verlaengerung, 0, 2));
        text.Should().Be("Gesamt 5:7 · n.V.");
    }

    [Fact]
    public void PaarungsEinzelergebnisse_ZeigtBeideEinzelergebnisse()
    {
        var text = SpielErgebnisFormatierer.PaarungsEinzelergebnisse(
            Ergebnis(1, 0), Ergebnis(2, 1, EntscheidungArt.Elfmeterschiessen, 0, 0, 4, 2));
        text.Should().Be("Hin 1:0 · Rück 2:1");
    }

    [Fact]
    public void PaarungsEntscheidung_Elfmeterschiessen_ZeigtElfmeterstand()
    {
        SpielErgebnisFormatierer.PaarungsEntscheidung(
                Ergebnis(2, 1, EntscheidungArt.Elfmeterschiessen, 0, 0, 4, 2))
            .Should().Be("i.E. 4:2");
    }

    [Fact]
    public void PaarungsEinzelergebnisse_OhneRueckspiel_ZeigtHinweis()
    {
        SpielErgebnisFormatierer.PaarungsEinzelergebnisse(Ergebnis(1, 0), null).Should().Be("nach Hinspiel");
    }
}
