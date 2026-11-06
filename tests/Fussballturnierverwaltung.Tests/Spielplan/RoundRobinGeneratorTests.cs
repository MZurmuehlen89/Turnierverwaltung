using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Spielplan;

public class RoundRobinGeneratorTests
{
    private readonly RoundRobinGenerator _generator = new();

    private static TurnierRunde TestRunde(int turnierInstanzId = 1) =>
        new() { Id = 1, TurnierInstanzId = turnierInstanzId, Name = "Liga", Rundenart = Rundenart.Liga, RegelwerkId = 1 };

    private static TurnierRunde TestRundeMitRueckspiel(int turnierInstanzId = 1) =>
        new() { Id = 1, TurnierInstanzId = turnierInstanzId, Name = "Liga", Rundenart = Rundenart.Liga, RegelwerkId = 1, MitRueckspiel = true };

    private static IReadOnlyList<Mannschaft> Teams(int anzahl) =>
        Enumerable.Range(1, anzahl)
            .Select(i => new Mannschaft { Id = i, Name = $"Team {i}" })
            .ToList();

    // n Teams → n*(n-1)/2 Spiele
    [Theory]
    [InlineData(4, 6)]
    [InlineData(6, 15)]
    [InlineData(8, 28)]
    [InlineData(3, 3)]  // ungerade
    [InlineData(5, 10)] // ungerade
    public void AnzahlSpiele_IsKorrekt(int teamAnzahl, int erwarteteSpiele)
    {
        var kontext = new SpielplanKontext(TestRunde(), Teams(teamAnzahl), 1);
        var ergebnis = _generator.Generieren(kontext);
        ergebnis.Spiele.Should().HaveCount(erwarteteSpiele);
    }

    [Fact]
    public void JedesTeamSpielt_GegenJedesAndere_GenauEinmal()
    {
        int n = 6;
        var teams = Teams(n);
        var kontext = new SpielplanKontext(TestRunde(), teams, 1);
        var ergebnis = _generator.Generieren(kontext);

        // Für jedes Paar (i,j) prüfen, dass genau ein Spiel existiert
        for (int i = 1; i <= n; i++)
        {
            for (int j = i + 1; j <= n; j++)
            {
                int ti = i, tj = j;
                ergebnis.Spiele
                    .Where(s => (s.HeimMannschaftId == ti && s.AuswaertsMannschaftId == tj) ||
                                (s.HeimMannschaftId == tj && s.AuswaertsMannschaftId == ti))
                    .Should().HaveCount(1, $"Team {i} und Team {j} sollen genau einmal gegeneinander spielen.");
            }
        }
    }

    [Fact]
    public void JedesTeam_KommmtGleichHaeufigVor()
    {
        int n = 4;
        var teams = Teams(n);
        var kontext = new SpielplanKontext(TestRunde(), teams, 1);
        var ergebnis = _generator.Generieren(kontext);

        foreach (var team in teams)
        {
            int auftritte = ergebnis.Spiele
                .Count(s => s.HeimMannschaftId == team.Id || s.AuswaertsMannschaftId == team.Id);
            auftritte.Should().Be(n - 1, $"Team {team.Id} soll {n - 1} Spiele haben.");
        }
    }

    [Fact]
    public void Spieltage_WerdenKorrektNummeriert()
    {
        var kontext = new SpielplanKontext(TestRunde(), Teams(4), 1);
        var ergebnis = _generator.Generieren(kontext);

        var spieltage = ergebnis.Spiele.Select(s => s.Spieltag!.Value).Distinct().OrderBy(x => x).ToList();
        spieltage.Should().BeEquivalentTo([1, 2, 3]); // 4 Teams → 3 Spieltage
    }

    [Fact]
    public void StartSpielNummer_WirdKorrektAngewendet()
    {
        var kontext = new SpielplanKontext(TestRunde(), Teams(4), StartSpielNummer: 10);
        var ergebnis = _generator.Generieren(kontext);

        ergebnis.Spiele.First().Spielnummer.Should().Be(10);
        ergebnis.Spiele.Should().BeInAscendingOrder(s => s.Spielnummer);
    }

    [Fact]
    public void KeinHinRueckspielPaare_BeiLiga()
    {
        var kontext = new SpielplanKontext(TestRunde(), Teams(4), 1);
        var ergebnis = _generator.Generieren(kontext);
        ergebnis.HatHinUndRueckspiele.Should().BeFalse();
    }

    [Fact]
    public void ZweiTeams_GenerierenEinSpiel()
    {
        var kontext = new SpielplanKontext(TestRunde(), Teams(2), 1);
        var ergebnis = _generator.Generieren(kontext);
        ergebnis.Spiele.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(3)]
    [InlineData(5)]
    public void JedesTeam_SpieltProSpieltag_HoechstensEinmal(int teamAnzahl)
    {
        var kontext = new SpielplanKontext(TestRunde(), Teams(teamAnzahl), 1);
        var ergebnis = _generator.Generieren(kontext);

        foreach (var spieltag in ergebnis.Spiele.Select(s => s.Spieltag).Distinct())
        {
            var spieleAmTag = ergebnis.Spiele.Where(s => s.Spieltag == spieltag).ToList();
            var alleTeams   = spieleAmTag
                .SelectMany(s => new[] { s.HeimMannschaftId, s.AuswaertsMannschaftId })
                .ToList();

            alleTeams.Should().OnlyHaveUniqueItems(
                $"Spieltag {spieltag}: jedes Team darf nur einmal spielen (n={teamAnzahl})");
        }
    }

    // ── MitRueckspiel (Regressionstest fuer Issue #96) ─────────────────────

    [Fact]
    public void MitRueckspiel_ErzeugtDoppelteAnzahlSpiele()
    {
        var kontext = new SpielplanKontext(TestRundeMitRueckspiel(), Teams(4), 1);
        var ergebnis = _generator.Generieren(kontext);

        ergebnis.Spiele.Should().HaveCount(12, "4 Teams → 6 Hinspiele + 6 Rückspiele");
    }

    [Fact]
    public void MitRueckspiel_SetztHinRueckspielPaareUndIstRueckspielKorrekt()
    {
        var kontext = new SpielplanKontext(TestRundeMitRueckspiel(), Teams(4), 1);
        var ergebnis = _generator.Generieren(kontext);

        ergebnis.HatHinUndRueckspiele.Should().BeTrue();
        ergebnis.HinRueckspielPaare.Should().HaveCount(6);

        foreach (var (hinIdx, rueckIdx) in ergebnis.HinRueckspielPaare)
        {
            var hin   = ergebnis.Spiele[hinIdx];
            var rueck = ergebnis.Spiele[rueckIdx];

            hin.IstRueckspiel.Should().BeFalse();
            rueck.IstRueckspiel.Should().BeTrue();
            rueck.HeimMannschaftId.Should().Be(hin.AuswaertsMannschaftId,
                "im Rückspiel wird Heim/Auswärts gegenüber dem Hinspiel getauscht");
            rueck.AuswaertsMannschaftId.Should().Be(hin.HeimMannschaftId);
        }
    }

    [Fact]
    public void MitRueckspiel_RueckspieleHabenSpaetereSpieltage()
    {
        int n = 4;
        int spieltageHinrunde = n - 1;
        var kontext = new SpielplanKontext(TestRundeMitRueckspiel(), Teams(n), 1);
        var ergebnis = _generator.Generieren(kontext);

        var rueckspiele = ergebnis.HinRueckspielPaare
            .Select(p => ergebnis.Spiele[p.RueckspielIndex])
            .ToList();

        rueckspiele.Should().AllSatisfy(r =>
            r.Spieltag.Should().BeGreaterThan(spieltageHinrunde,
                "Rückrunden-Spieltage müssen um die Anzahl der Hinrunden-Spieltage versetzt sein"));
    }

    // ── Exakte Spielfolge (#504) ────────────────────────────────────────────

    /// <summary>Spielfolge in Spielnummer-Reihenfolge als "Spieltag:Heim-Gast" — hält die Berger-Tabelle samt Heimrecht
    /// und Rückrunde exakt fest, damit ein Umbau keine Paarung, kein Heimrecht und keinen Spieltag verschiebt.</summary>
    internal static string Kurzform(IEnumerable<(int? Spieltag, int? Heim, int? Gast)> spiele) =>
        string.Join(" ", spiele.Select(s => $"{s.Spieltag}:{s.Heim}-{s.Gast}"));

    [Theory]
    [InlineData(2, false, "1:1-2")]
    [InlineData(4, false, "1:1-4 1:3-2 2:3-1 2:2-4 3:1-2 3:4-3")]
    [InlineData(5, false, "1:5-2 1:3-4 2:5-1 2:2-3 3:1-4 3:3-5 4:3-1 4:2-4 5:1-2 5:4-5")]
    [InlineData(6, false, "1:1-6 1:5-2 1:3-4 2:5-1 2:4-6 2:2-3 3:1-4 3:3-5 3:6-2 4:3-1 4:2-4 4:5-6 5:1-2 5:6-3 5:4-5")]
    [InlineData(3, true, "1:3-2 2:3-1 3:1-2 4:2-3 5:1-3 6:2-1")]
    [InlineData(4, true, "1:1-4 1:3-2 2:3-1 2:2-4 3:1-2 3:4-3 4:4-1 4:2-3 5:1-3 5:4-2 6:2-1 6:3-4")]
    public void Spielfolge_IstExaktFestgehalten(int teamAnzahl, bool mitRueckspiel, string erwartet)
    {
        var runde    = mitRueckspiel ? TestRundeMitRueckspiel() : TestRunde();
        var ergebnis = _generator.Generieren(new SpielplanKontext(runde, Teams(teamAnzahl), 1));

        Kurzform(ergebnis.Spiele.OrderBy(s => s.Spielnummer).Select(s => (s.Spieltag, s.HeimMannschaftId, s.AuswaertsMannschaftId)))
            .Should().Be(erwartet);
    }

    // ── Heim/Auswärts im Wechsel (#568) ─────────────────────────────────────

    /// <summary>Heim/Auswärts-Folge je Team über alle Spieltage: H, A oder „-“ (Freilos).</summary>
    private static Dictionary<int, string> HeimAuswaertsFolge(SpielplanErgebnis ergebnis, int teamAnzahl)
    {
        int spieltage = ergebnis.Spiele.Max(s => s.Spieltag!.Value);
        return Enumerable.Range(1, teamAnzahl).ToDictionary(team => team, team => string.Concat(
            Enumerable.Range(1, spieltage).Select(tag =>
                ergebnis.Spiele.FirstOrDefault(s => s.Spieltag == tag && (s.HeimMannschaftId == team || s.AuswaertsMannschaftId == team)) switch
                {
                    null => '-',
                    var s => s.HeimMannschaftId == team ? 'H' : 'A',
                })));
    }

    private static int LaengsteSerie(string folge) =>
        System.Text.RegularExpressions.Regex.Matches(folge, "H+|A+").Select(m => m.Length).DefaultIfEmpty(0).Max();

    [Theory]
    [MemberData(nameof(TeamAnzahlen))]
    public void Hinrunde_KeinTeamSpieltMehrAlsZweimalInFolgeHeimOderAuswaerts(int teamAnzahl)
    {
        var ergebnis = _generator.Generieren(new SpielplanKontext(TestRunde(), Teams(teamAnzahl), 1));

        HeimAuswaertsFolge(ergebnis, teamAnzahl).Should().AllSatisfy(x =>
            LaengsteSerie(x.Value).Should().BeLessThanOrEqualTo(2, $"Team {x.Key} spielt {x.Value}"));
    }

    /// <summary>Die Rückrunde spiegelt die Hinrunde in gleicher Reihenfolge (wie die Bundesliga) — eine dritte Partie
    /// in Folge auf derselben Seite gibt es nur am Übergang.</summary>
    [Theory]
    [MemberData(nameof(TeamAnzahlen))]
    public void HinUndRueckrunde_KeinTeamSpieltMehrAlsDreimalInFolgeHeimOderAuswaerts(int teamAnzahl)
    {
        var ergebnis = _generator.Generieren(new SpielplanKontext(TestRundeMitRueckspiel(), Teams(teamAnzahl), 1));

        HeimAuswaertsFolge(ergebnis, teamAnzahl).Should().AllSatisfy(x =>
            LaengsteSerie(x.Value).Should().BeLessThanOrEqualTo(3, $"Team {x.Key} spielt {x.Value}"));
    }

    public static TheoryData<int> TeamAnzahlen() => [.. Enumerable.Range(3, 18)];
}
