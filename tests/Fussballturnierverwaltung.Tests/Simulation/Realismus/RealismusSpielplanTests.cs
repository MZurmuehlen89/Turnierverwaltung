using FluentAssertions;

namespace Fussballturnierverwaltung.Tests.Simulation.Realismus;

/// <summary>#359: der Spielplan der Messreihe mit dynamischer Teamstärke ist eine echte Doppelrunde.</summary>
public class RealismusSpielplanTests
{
    [Fact]
    public void Spielplan_IstEineDoppelrundeMitJeEinemSpielJeTeamUndSpieltag()
    {
        var teams = KalibrierungsTeams.Teams(KalibrierungsTeams.Bundesliga, 1);

        var plan = RealismusStichprobe.Spielplan(teams);

        plan.Should().HaveCount(2 * (teams.Count - 1));
        plan.Should().OnlyContain(tag => tag.Count == teams.Count / 2);
        plan.Should().OnlyContain(tag => tag.SelectMany(p => new[] { p.Heim.Id, p.Gast.Id }).Distinct().Count() == teams.Count);
        var paarungen = plan.SelectMany(t => t).Select(p => (p.Heim.Id, p.Gast.Id)).ToList();
        paarungen.Should().OnlyHaveUniqueItems("jede Ansetzung (Heim, Gast) kommt genau einmal vor");
        paarungen.Should().HaveCount(teams.Count * (teams.Count - 1));
    }
}
