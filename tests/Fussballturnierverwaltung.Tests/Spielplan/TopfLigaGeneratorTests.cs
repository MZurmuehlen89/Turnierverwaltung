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

    // ── UCL-Ligaphase (Issue #347) ──────────────────────────────────────────

    /// <summary>UCL-artige Paarungen wie aus der Topf-Zuordnung: 4 Töpfe à 9 Teams, im Topf ein Kreis (je 1 Heim-
    /// und 1 Auswärtsgegner), zwischen zwei Töpfen zwei Permutationen (je 1 Heim- und 1 Auswärtsgegner) → 144 Spiele,
    /// jedes Team 8 verschiedene Gegner, 4 Heim-/4 Auswärtsspiele.</summary>
    private static List<(int, int)> UclPaarungen(int seed)
    {
        var zufall = new Random(seed);
        var toepfe = Enumerable.Range(0, 4).Select(t => Enumerable.Range(t * 9 + 1, 9).ToList()).ToList();
        var paare  = new List<(int, int)>();
        for (int i = 0; i < 4; i++)
        {
            var kreis = toepfe[i].OrderBy(_ => zufall.Next()).ToList();
            for (int k = 0; k < 9; k++) paare.Add((kreis[k], kreis[(k + 1) % 9]));

            for (int j = i + 1; j < 4; j++)
            {
                List<int> perm1, perm2;
                do
                {
                    perm1 = toepfe[j].OrderBy(_ => zufall.Next()).ToList();
                    perm2 = toepfe[j].OrderBy(_ => zufall.Next()).ToList();
                } while (perm1.Zip(perm2).Any(x => x.First == x.Second));

                for (int k = 0; k < 9; k++)
                {
                    paare.Add((toepfe[i][k], perm1[k]));
                    paare.Add((perm2[k], toepfe[i][k]));
                }
            }
        }
        return paare;
    }

    public static TheoryData<int> Seeds() => [.. Enumerable.Range(1, 30)];

    [Theory]
    [MemberData(nameof(Seeds))]
    public void UclLigaphase_GenauAchtSpieltageMitJe18Spielen(int seed)
    {
        var paare   = UclPaarungen(seed);
        var kontext = new SpielplanKontext(TestRunde(), Teams(Enumerable.Range(1, 36)), 1, paare);

        var ergebnis = _generator.Generieren(kontext);

        ergebnis.Spiele.Should().HaveCount(144);
        var spieltage = ergebnis.Spiele.GroupBy(s => s.Spieltag!.Value).ToList();
        spieltage.Select(g => g.Key).Should().BeEquivalentTo(Enumerable.Range(1, 8));
        spieltage.Should().OnlyContain(g => g.Count() == 18);
        foreach (var tag in spieltage)
            tag.SelectMany(s => new[] { s.HeimMannschaftId, s.AuswaertsMannschaftId })
                .Should().OnlyHaveUniqueItems($"Spieltag {tag.Key}: kein Team darf zweimal antreten");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void UclLigaphase_HeimAuswaertsRhythmusAusgeglichen(int seed)
    {
        var kontext  = new SpielplanKontext(TestRunde(), Teams(Enumerable.Range(1, 36)), 1, UclPaarungen(seed));
        var ergebnis = _generator.Generieren(kontext);

        foreach (int team in Enumerable.Range(1, 36))
        {
            var folge = ergebnis.Spiele
                .Where(s => s.HeimMannschaftId == team || s.AuswaertsMannschaftId == team)
                .OrderBy(s => s.Spieltag)
                .Select(s => s.HeimMannschaftId == team ? 'H' : 'A')
                .ToArray();
            var text = new string(folge);

            folge.Should().HaveCount(8);
            text.Should().NotContain("HHH").And.NotContain("AAA", $"Team {team}: {text}");
            folge[0].Should().NotBe(folge[1], $"Team {team} soll an Spieltag 1/2 gemischt spielen: {text}");
            folge[6].Should().NotBe(folge[7], $"Team {team} soll an Spieltag 7/8 gemischt spielen: {text}");
        }
    }

    [Fact]
    public void UclLigaphase_SpielnummernFolgenDenSpieltagen()
    {
        var kontext  = new SpielplanKontext(TestRunde(), Teams(Enumerable.Range(1, 36)), 1, UclPaarungen(7));
        var ergebnis = _generator.Generieren(kontext);

        ergebnis.Spiele.OrderBy(s => s.Spielnummer).Select(s => s.Spieltag)
            .Should().BeInAscendingOrder("Spieltag 1 trägt die ersten Spielnummern");
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
