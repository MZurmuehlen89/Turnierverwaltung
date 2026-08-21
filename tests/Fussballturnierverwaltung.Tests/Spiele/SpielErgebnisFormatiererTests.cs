using FluentAssertions;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Spiele;

public class SpielErgebnisFormatiererTests
{
    // ── DetailText ────────────────────────────────────────────────────────

    [Fact]
    public void DetailText_RegulaereSpielzeit_IstLeer()
    {
        var text = SpielErgebnisFormatierer.DetailText(
            EntscheidungArt.RegulaereSpielzeit, 2, 1, null, null, null, null);
        text.Should().BeEmpty();
    }

    [Fact]
    public void DetailText_Auswaertstorregel_ZeigtKuerzel()
    {
        var text = SpielErgebnisFormatierer.DetailText(
            EntscheidungArt.Auswaertstorregel, 1, 2, null, null, null, null);
        text.Should().Be("A.T.");
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
}
