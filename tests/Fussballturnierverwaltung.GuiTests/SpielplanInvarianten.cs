using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Invarianten eines Spielplans mit Spieltagen (Liga, Ligaphase; #560).</summary>
internal static class SpielplanInvarianten
{
    /// <summary><paramref name="spieltage"/> Spieltage („ST n“ in der ersten Spalte), an jedem spielt jedes der
    /// <paramref name="teams"/> genau einmal.</summary>
    public static void JedesTeamEinmalJeSpieltag(
        IReadOnlyList<SpielZeile> spiele, IReadOnlySet<string> teams, int spieltage)
    {
        var jeSpieltag = spiele.GroupBy(s => s.Kennung).ToList();
        jeSpieltag.Select(g => g.Key).Should().OnlyContain(k => k.StartsWith("ST ", StringComparison.Ordinal));
        jeSpieltag.Should().HaveCount(spieltage);
        foreach (var spieltag in jeSpieltag)
            spieltag.SelectMany(s => new[] { s.Heim, s.Auswaerts }).Should().BeEquivalentTo(teams,
                "an {0} spielt jedes Team genau einmal", spieltag.Key);
    }
}
