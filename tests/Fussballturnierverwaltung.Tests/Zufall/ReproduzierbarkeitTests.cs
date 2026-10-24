using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Application.Zufall;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Tests.Auslosungen;
using Fussballturnierverwaltung.Tests.Simulation;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Zufall;

/// <summary>Mit festem Seed liefern Auslosung, Spielplan und Simulation bei jedem Lauf dasselbe Ergebnis (#519) —
/// Voraussetzung, um einen fehlgeschlagenen GUI-Test zu reproduzieren (#494).</summary>
public class ReproduzierbarkeitTests
{
    private const int SEED = 4711;

    // ── Auslosung ─────────────────────────────────────────────────────────────

    private static async Task<List<(int? Heim, int? Auswaerts)>> KoAuslosungMitSeed(int seed)
    {
        var repo = new FakeAuslosungRepository();
        var service = new AuslosungService(
            repo, new FakeVerbandsQuoteRepository(), new FakeLandesQuoteRepository(), new FakeTurnierRundeRepository(),
            new AuslosungsValidator(), new FakePlatzhalterKaskadeService(), new FakeRanglisteRepository(),
            new Zufallsquelle(seed), NullLogger<AuslosungService>.Instance);

        var auslosung = new Auslosung
        {
            Id = 1,
            TurnierRunde = new TurnierRunde { Id = 1, Name = "Achtelfinale", Rundenart = Rundenart.KoRunde, RegelwerkId = 1 },
            Auslosungsart = Auslosungsart.Zufall,
            Status = AuslosungsStatus.Vorbereitung
        };
        var topf = new Lostopf { Id = 1, Reihenfolge = 1, Name = "Topf 1" };
        foreach (var id in Enumerable.Range(1, 16))
        {
            var team = new Mannschaft { Id = id, Name = $"Team {id}", LandId = id };
            topf.Eintraege.Add(new LostopfEintrag { Id = id, MannschaftId = id, Mannschaft = team });
        }
        auslosung.Lostoepfe.Add(topf);
        repo.Speichern(auslosung);

        var ergebnisse = await service.AusfuehrenAsync(auslosung.Id);
        return [.. ergebnisse.Select(e => (e.HeimMannschaftId, e.AuswaertsMannschaftId))];
    }

    [Fact]
    public async Task Auslosung_GleicherSeed_GleichePaarungen()
    {
        var erste = await KoAuslosungMitSeed(SEED);
        var zweite = await KoAuslosungMitSeed(SEED);

        zweite.Should().Equal(erste);
    }

    // ── Spielplan ─────────────────────────────────────────────────────────────

    private static List<(int Heim, int Auswaerts, int? Spieltag)> TopfLigaSpielplanMitSeed(int seed)
    {
        var ids = Enumerable.Range(1, 8).ToList();
        var paare = ids.SelectMany(a => ids.Where(b => b > a).Select(b => (a, b))).ToList<(int, int)>();
        var runde = new TurnierRunde { Id = 1, TurnierInstanzId = 1, Name = "Ligaphase", Rundenart = Rundenart.TopfLiga };
        var teams = ids.Select(id => new Mannschaft { Id = id, Name = $"Team {id}" }).ToList();

        var ergebnis = new TopfLigaGenerator(new Zufallsquelle(seed)).Generieren(new SpielplanKontext(runde, teams, 1, paare));
        return [.. ergebnis.Spiele.Select(s => (s.HeimMannschaftId!.Value, s.AuswaertsMannschaftId!.Value, s.Spieltag))];
    }

    [Fact]
    public void Spielplan_GleicherSeed_GleicheSpieltage()
    {
        TopfLigaSpielplanMitSeed(SEED).Should().Equal(TopfLigaSpielplanMitSeed(SEED));
    }

    // ── Simulation ────────────────────────────────────────────────────────────

    [Fact]
    public void Einzelspiel_GleicherSeed_GleichesErgebnis()
    {
        using var ctx = SimulationsDienste.NeuerContext();
        var service = SimulationsDienste.Service(ctx, new Zufallsquelle(SEED));
        var heim = new Mannschaft { Id = 1, Name = "Heim", OffensivRating = 80, MittelfeldRating = 80, DefensivRating = 80 };
        var gast = new Mannschaft { Id = 2, Name = "Gast", OffensivRating = 75, MittelfeldRating = 75, DefensivRating = 75 };

        var erstes = service.EinzelspielSimulieren(heim, gast, [], SimulationsModus.AusfuehrlicheSimulation);
        var zweites = service.EinzelspielSimulieren(heim, gast, [], SimulationsModus.AusfuehrlicheSimulation);

        (zweites.HeimToreRegulaer, zweites.AuswaertsToreRegulaer).Should()
            .Be((erstes.HeimToreRegulaer, erstes.AuswaertsToreRegulaer));
        zweites.Ereignisse.Select(e => e.Minute).Should().Equal(erstes.Ereignisse.Select(e => e.Minute));
    }

    [Fact]
    public async Task Turniersimulation_OhneExplizitenSeed_NimmtDenFestenSeedDerQuelle()
    {
        using var ctx = SimulationsDienste.NeuerContext();
        ctx.TurnierInstanzen.Add(new TurnierInstanz { Id = 1, Name = "Leeres Turnier" });
        await ctx.SaveChangesAsync();

        var lauf = await SimulationsDienste.Service(ctx, new Zufallsquelle(SEED))
            .TurnierSimulierenAsync(1, SimulationsModus.SchnellSimulation);

        lauf.ZufallsSeed.Should().Be(SEED);
    }
}
