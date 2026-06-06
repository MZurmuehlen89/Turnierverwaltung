using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Spielplan;

public class TopfLigaGeneratorTests
{
    private readonly TopfLigaGenerator _generator = new();

    private static TurnierRunde TestRunde() =>
        new() { Id = 1, TurnierInstanzId = 1, Name = "Ligaphase", Rundenart = Rundenart.TopfLiga };

    private static IReadOnlyList<Mannschaft> Teams(IEnumerable<int> ids) =>
        ids.Select(id => new Mannschaft { Id = id, Name = $"Team {id}" }).ToList();

    // 4 Teams, jeder gegen jeden (6 Paarungen) → 3 Spieltage
    [Fact]
    public void ViertTeams_SechsPaarungen_DreiSpieltage()
    {
        var paare = new List<(int, int)> { (1, 2), (3, 4), (1, 3), (2, 4), (1, 4), (2, 3) };
        var kontext = new SpielplanKontext(TestRunde(), Teams([1, 2, 3, 4]), 1, paare);

        var ergebnis = _generator.Generieren(kontext);

        ergebnis.Spiele.Should().HaveCount(6);
        var spieltage = ergebnis.Spiele.Select(s => s.Spieltag!.Value).Distinct().ToList();
        spieltage.Should().HaveCount(3);
    }

    // Kein Team darf am gleichen Spieltag zweimal antreten
    [Fact]
    public void KeinTeamZweimalAmGleichenSpieltag()
    {
        // UCL-ähnlich: 4 Töpfe à 3 Teams = 12 Teams, jeder spielt 2× pro Topf (vereinfacht)
        // Hier: 6 Teams, vollständige Paarungen (15 Spiele)
        var ids = Enumerable.Range(1, 6).ToList();
        var paare = ids
            .SelectMany(a => ids.Where(b => b > a).Select(b => (a, b)))
            .ToList<(int, int)>();

        var kontext = new SpielplanKontext(TestRunde(), Teams(ids), 1, paare);
        var ergebnis = _generator.Generieren(kontext);

        foreach (var spieltag in ergebnis.Spiele.GroupBy(s => s.Spieltag))
        {
            var teamsImSpieltag = spieltag
                .SelectMany(s => new[] { s.HeimMannschaftId, s.AuswaertsMannschaftId })
                .ToList();
            teamsImSpieltag.Should().OnlyHaveUniqueItems(
                $"Spieltag {spieltag.Key}: kein Team darf zweimal antreten");
        }
    }

    // Ohne Paarungen → Ausnahme
    [Fact]
    public void OhnePaarungen_WirftException()
    {
        var kontext = new SpielplanKontext(TestRunde(), Teams([1, 2]), 1, null);
        var act = () => _generator.Generieren(kontext);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Paarungen*");
    }

    // Spielnummern sind aufsteigend und lückenlos
    [Fact]
    public void Spielnummern_SindAufsteigend()
    {
        var paare = new List<(int, int)> { (1, 2), (3, 4), (1, 3), (2, 4) };
        var kontext = new SpielplanKontext(TestRunde(), Teams([1, 2, 3, 4]), 10, paare);

        var ergebnis = _generator.Generieren(kontext);
        var nummern  = ergebnis.Spiele.Select(s => s.Spielnummer).OrderBy(n => n).ToList();

        nummern.First().Should().Be(10);
        nummern.Should().BeInAscendingOrder();
        nummern.Zip(nummern.Skip(1)).Should().OnlyContain(pair => pair.Second - pair.First == 1);
    }
}
