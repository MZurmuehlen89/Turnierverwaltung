using System.Globalization;
using System.Text.RegularExpressions;
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

    /// <summary>Kein Team spielt mehr als <paramref name="hoechstensInFolge"/>-mal hintereinander zu Hause bzw.
    /// auswärts (#568); die Spieltage stehen als „ST n“ in der ersten Spalte.</summary>
    public static void HeimAuswaertsImWechsel(
        IReadOnlyList<SpielZeile> spiele, IReadOnlySet<string> teams, int hoechstensInFolge)
    {
        var nachSpieltag = spiele.OrderBy(s => int.Parse(s.Kennung["ST ".Length..], CultureInfo.InvariantCulture)).ToList();
        foreach (var team in teams)
        {
            string folge = string.Concat(nachSpieltag
                .Where(s => s.Heim == team || s.Auswaerts == team)
                .Select(s => s.Heim == team ? 'H' : 'A'));
            int laengsteSerie = Regex.Matches(folge, "H+|A+").Max(m => m.Length);
            laengsteSerie.Should().BeLessThanOrEqualTo(hoechstensInFolge, "{0} spielt {1}", team, folge);
        }
    }
}
