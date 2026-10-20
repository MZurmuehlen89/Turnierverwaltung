using FluentAssertions;
using Fussballturnierverwaltung.Application.Ranglisten;

namespace Fussballturnierverwaltung.Tests.Ranglisten;

public class EintragsReihenfolgeTests
{
    private static readonly string[] _liste = ["A", "B", "C", "D", "E"];

    private static HashSet<string> Markiert(params string[] eintraege) => [.. eintraege];

    [Fact]
    public void Hoch_EinzelnerEintrag_TauschtMitVorgaenger()
    {
        EintragsReihenfolge.Hoch(_liste, Markiert("C")).Should().Equal("A", "C", "B", "D", "E");
    }

    [Fact]
    public void Hoch_Block_RueckGeschlossenAuf()
    {
        EintragsReihenfolge.Hoch(_liste, Markiert("C", "D")).Should().Equal("A", "C", "D", "B", "E");
    }

    [Fact]
    public void Hoch_BlockAmAnfang_BleibtStehen_UebrigeRueckenAuf()
    {
        EintragsReihenfolge.Hoch(_liste, Markiert("A", "B", "D")).Should().Equal("A", "B", "D", "C", "E");
    }

    [Fact]
    public void Runter_VerstreuteEintraege_RueckenJeEinenPlatz()
    {
        EintragsReihenfolge.Runter(_liste, Markiert("A", "C")).Should().Equal("B", "A", "D", "C", "E");
    }

    [Fact]
    public void Runter_BlockAmEnde_BleibtStehen()
    {
        EintragsReihenfolge.Runter(_liste, Markiert("D", "E")).Should().Equal(_liste);
    }

    [Fact]
    public void AufRang_VerstreuteEintraege_WerdenAlsBlockAbZielrangEingefuegt()
    {
        EintragsReihenfolge.AufRang(_liste, Markiert("B", "E"), 1).Should().Equal("B", "E", "A", "C", "D");
    }

    [Fact]
    public void AufRang_ZielInDerMitte_ErsterMarkierterStehtAufZielrang()
    {
        EintragsReihenfolge.AufRang(_liste, Markiert("A"), 3).Should().Equal("B", "C", "A", "D", "E");
    }

    [Fact]
    public void AufRang_HinterDemEnde_SetztBlockAnsEnde()
    {
        EintragsReihenfolge.AufRang(_liste, Markiert("A", "B"), 99).Should().Equal("C", "D", "E", "A", "B");
    }

    [Fact]
    public void EinfuegenNach_HinterLetztemMarkierten()
    {
        EintragsReihenfolge.EinfuegenNach(_liste, Markiert("B", "C"), "X").Should().Equal("A", "B", "C", "X", "D", "E");
    }

    [Fact]
    public void EinfuegenNach_OhneMarkierung_AmEnde()
    {
        EintragsReihenfolge.EinfuegenNach(_liste, Markiert(), "X").Should().Equal("A", "B", "C", "D", "E", "X");
    }

    [Fact]
    public void NaechsterTreffer_SuchtAbStart_UndBeginntAmEndeVonVorn()
    {
        string[] liste = ["Bayern", "Dortmund", "Bayer Leverkusen", "Mainz"];
        bool Passt(string name) => name.StartsWith("Bay");

        EintragsReihenfolge.NaechsterTreffer(liste, Passt, 1).Should().Be(2);
        EintragsReihenfolge.NaechsterTreffer(liste, Passt, 3).Should().Be(0);
        EintragsReihenfolge.NaechsterTreffer(liste, n => n == "Köln", 0).Should().BeNull();
    }
}
