using FluentAssertions;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Tabelle;

/// <summary>#628: Hinweis des Regelwerk-Editors, wie der direkte Vergleich eines Regelsets rechnet.</summary>
public class DirekterVergleichTests
{
    private static RankingRegelSet Set(bool wiederholen, params RankingRegelArt[] arten) => new()
    {
        DirektenVergleichWiederholen = wiederholen,
        Eintraege = [.. arten.Select((art, i) => new RankingRegelSetEintrag
        {
            Reihenfolge = i + 1, RankingRegel = new RankingRegel { Regelart = art }
        })]
    };

    [Fact]
    public void Hinweis_OhneSetOderOhneDirektenVergleich_IstLeer()
    {
        DirekterVergleich.Hinweis(null).Should().BeNull();
        DirekterVergleich.Hinweis(Set(true, RankingRegelArt.Punkte, RankingRegelArt.GegnerPunkte)).Should().BeNull();
    }

    [Theory]
    [InlineData(true, "neu gerechnet (UEFA)")]
    [InlineData(false, "folgenden Kriterien (FIFA)")]
    public void Hinweis_NenntDasVerhaltenBeiTeilGleichstand(bool wiederholen, string erwartet)
    {
        var set = Set(wiederholen, RankingRegelArt.Punkte, RankingRegelArt.DirekterVergleichTordifferenz);

        DirekterVergleich.Hinweis(set).Should().StartWith("Direkter Vergleich: Mini-Tabelle").And.Contain(erwartet);
    }
}
