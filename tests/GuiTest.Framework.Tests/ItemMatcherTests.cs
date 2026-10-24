using FluentAssertions;

namespace GuiTest.Framework.Tests;

public class ItemMatcherTests
{
    private const string TYPNAME = "Fussballturnierverwaltung.Domain.Entitaeten.Turniere.TurnierInstanz";

    [Fact]
    public void Matches_AlleTexteExaktAngezeigt_Trifft()
    {
        ItemMatcher.Matches(TYPNAME, ["Mini-Turnier", "8 Teams"], ["Mini-Turnier", "8 Teams"]).Should().BeTrue();
    }

    [Fact]
    public void Matches_TeilstringReichtNicht()
    {
        ItemMatcher.Matches(TYPNAME, ["FIFA WM", "48 Teams"], ["8 Teams"]).Should().BeFalse();
    }

    [Fact]
    public void Matches_EinTextFehlt_TrifftNicht()
    {
        ItemMatcher.Matches(TYPNAME, ["Mini-Turnier", "4 Teams"], ["Mini-Turnier", "8 Teams"]).Should().BeFalse();
    }

    [Fact]
    public void Matches_EinzelnerTextGleichDemNamen_Trifft()
    {
        ItemMatcher.Matches("Halbfinale", [], ["Halbfinale"]).Should().BeTrue();
    }

    [Fact]
    public void Matches_AngezeigterTextMitLeerzeichenAmEnde_Trifft()
    {
        ItemMatcher.Matches(TYPNAME, ["Bayern München "], ["Bayern München"]).Should().BeTrue();
    }

    [Fact]
    public void Matches_OhneGesuchteTexte_TrifftNie()
    {
        ItemMatcher.Matches("", [], []).Should().BeFalse();
    }
}
