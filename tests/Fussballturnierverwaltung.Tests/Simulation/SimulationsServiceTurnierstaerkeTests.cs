using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Zufall;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Turnierstärke im <see cref="SimulationsService"/> (#679, K1, K3–K5, K9): Der Stärke-Seed gehört dem Turnier,
/// wird mit der ersten Simulation gesichert und bleibt, solange es Ergebnisse gibt.</summary>
public class SimulationsServiceTurnierstaerkeTests
{
    private const int FESTER_SEED = 123;

    /// <summary>Liga mit vier Teams, Spiel i gegen i+1 (Id = Spielnummer = i).</summary>
    private static TurnierverwaltungDbContext Turnier(int? staerkeSeed = null)
    {
        var ctx = SimulationsDienste.NeuerContext();
        SimulationsServiceTests.GrunddatenAnlegen(ctx, mannschaften: 4);
        ctx.TurnierInstanzen.Local.Single().StaerkeSeed = staerkeSeed;
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Liga", Reihenfolge = 1, Rundenart = Rundenart.Liga, RegelwerkId = 1
        });
        for (int i = 1; i <= 3; i++)
            ctx.Spiele.Add(new Spiel
            {
                Id = i, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = i, Spieltag = i,
                HeimMannschaftId = i, AuswaertsMannschaftId = i + 1, Status = SpielStatus.Geplant
            });
        ctx.SaveChanges();
        return ctx;
    }

    private static SimulationsService Service(TurnierverwaltungDbContext ctx) =>
        SimulationsDienste.Service(ctx, new Zufallsquelle(FESTER_SEED));

    private static void ErgebnisAnlegen(TurnierverwaltungDbContext ctx, int spielId)
    {
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { SpielId = spielId, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });
        ctx.Spiele.Single(s => s.Id == spielId).Status = SpielStatus.Abgeschlossen;
        ctx.SaveChanges();
    }

    private static int? Seed(TurnierverwaltungDbContext ctx) =>
        ctx.TurnierInstanzen.AsNoTracking().Single().StaerkeSeed;

    /// <summary>Tore und Minuten der Ereignisse eines gespeicherten Spiels.</summary>
    private static string Gespeichert(TurnierverwaltungDbContext ctx, int spielId)
    {
        var spiel = ctx.Spiele.AsNoTracking().Include(s => s.Ergebnis).Include(s => s.Ereignisse).Single(s => s.Id == spielId);
        return Abdruck(spiel.Ergebnis!.HeimToreRegulaer, spiel.Ergebnis.AuswaertsToreRegulaer,
            spiel.Ereignisse.Select(e => e.Minute));
    }

    /// <summary>Was die Engine mit der Turnierform des Seeds für dasselbe Spiel und denselben Zufall liefert.</summary>
    private static string Erwartet(TurnierverwaltungDbContext ctx, int spielId, Random rng, int? staerkeSeed)
    {
        var spiel = ctx.Spiele.AsNoTracking().Include(s => s.HeimMannschaft).Include(s => s.AuswaertsMannschaft)
            .Single(s => s.Id == spielId);
        Turnierform? form = staerkeSeed is int seed
            ? Turnierstaerke.Form(seed, spiel.HeimMannschaft!.Id, spiel.AuswaertsMannschaft!.Id)
            : null;
        var e = SimulationsEngine.Simulieren(
            spiel.HeimMannschaft!, spiel.AuswaertsMannschaft!, [], ausfuehrlich: true, rng, turnierform: form);
        return Abdruck(e.HeimToreRegulaer, e.AuswaertsToreRegulaer, e.Ereignisse.Select(x => x.Minute));
    }

    private static string Abdruck(int heim, int gast, IEnumerable<int> minuten) =>
        $"{heim}:{gast}/{string.Join(',', minuten.Order())}";

    // ── K1 Eine Stärke je Turnier ─────────────────────────────────────────

    [Fact]
    public async Task EinzelneSpiele_RechnenMitDemselbenStaerkeSeedDesTurniers()
    {
        await using var ctx = Turnier();
        var service = Service(ctx);

        await service.SpielSimulierenAsync(1, new Random(1));
        int seed = Seed(ctx)!.Value;
        await service.SpielSimulierenAsync(2, new Random(2));

        Seed(ctx).Should().Be(seed);
        Gespeichert(ctx, 1).Should().Be(Erwartet(ctx, 1, new Random(1), seed));
        Gespeichert(ctx, 2).Should().Be(Erwartet(ctx, 2, new Random(2), seed));
    }

    [Fact]
    public async Task SpieleEinesBlocks_RechnenMitDemselbenStaerkeSeed()
    {
        await using var ctx = Turnier();
        var service = Service(ctx);

        await service.SpieleSimulierenAsync([1, 2, 3], seed: 9);

        int seed = Seed(ctx)!.Value;
        var rng = new Random(9);
        foreach (int id in new[] { 1, 2, 3 })
            Gespeichert(ctx, id).Should().Be(Erwartet(ctx, id, rng, seed));
    }

    [Fact]
    public async Task Form_VerandertDasErgebnisGegenueberOhneForm()
    {
        // Gegenprobe zu den Tests oben: ohne die Form kämen andere Minuten heraus, sie belegen die Verdrahtung also wirklich.
        await using var ctx = Turnier(staerkeSeed: 555);
        await Service(ctx).SpieleSimulierenAsync([1, 2, 3], seed: 9);

        var rngOhne = new Random(9);
        var ohneForm = new[] { 1, 2, 3 }.Select(id => Erwartet(ctx, id, rngOhne, staerkeSeed: null)).ToList();
        var gespeichert = new[] { 1, 2, 3 }.Select(id => Gespeichert(ctx, id)).ToList();

        gespeichert.Should().NotEqual(ohneForm);
    }

    // ── K3 Zustand davor ──────────────────────────────────────────────────

    [Fact]
    public async Task OhneSeed_ErsteSimulationZiehtUndSichertIhn()
    {
        await using var ctx = Turnier();
        Seed(ctx).Should().BeNull();

        await Service(ctx).SpielSimulierenAsync(1, new Random(1));

        Seed(ctx).Should().Be(FESTER_SEED);
    }

    [Fact]
    public async Task Altbestand_MitErgebnissenAberOhneSeed_BekommtEinenBeiDerErstenSimulation()
    {
        await using var ctx = Turnier();
        ErgebnisAnlegen(ctx, 1);

        await Service(ctx).SpielSimulierenAsync(2, new Random(2));

        Seed(ctx).Should().Be(FESTER_SEED);
    }

    // ── K4 Teilweise gespielt ─────────────────────────────────────────────

    [Fact]
    public async Task MindestensEinErgebnis_SeedBleibt_AuchNachLoeschenEinzelnerErgebnisse()
    {
        await using var ctx = Turnier(staerkeSeed: 555);
        ErgebnisAnlegen(ctx, 1);
        ErgebnisAnlegen(ctx, 2);
        ctx.SpielErgebnisse.Remove(ctx.SpielErgebnisse.Single(e => e.SpielId == 2));
        ctx.Spiele.Single(s => s.Id == 2).Status = SpielStatus.Geplant;
        ctx.SaveChanges();

        await Service(ctx).SpielSimulierenAsync(2, new Random(2));

        Seed(ctx).Should().Be(555);
    }

    // ── K5 Neuer Durchlauf ────────────────────────────────────────────────

    [Fact]
    public async Task OhneErgebnisse_ZiehtDieNaechsteSimulationEinenNeuenSeed()
    {
        await using var ctx = Turnier(staerkeSeed: 555);

        await Service(ctx).SpielSimulierenAsync(1, new Random(1));

        Seed(ctx).Should().Be(FESTER_SEED);
    }

    // ── K9 Stammdaten unberührt ───────────────────────────────────────────

    [Fact]
    public async Task Simulation_AendertDieRatingsDerMannschaftenNicht()
    {
        await using var ctx = Turnier();
        string Ratings() => string.Join(';', ctx.Mannschaften.AsNoTracking().OrderBy(m => m.Id)
            .Select(m => $"{m.OffensivRating}/{m.MittelfeldRating}/{m.DefensivRating}/{m.Heimstaerke}/{m.Auswaertsstaerke}"));
        var vorher = Ratings();

        await Service(ctx).SpieleSimulierenAsync([1, 2, 3], seed: 9);

        Ratings().Should().Be(vorher);
    }
}
