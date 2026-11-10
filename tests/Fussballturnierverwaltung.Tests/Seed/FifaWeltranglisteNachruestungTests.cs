using FluentAssertions;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Seed;

/// <summary>Seed der vollständigen FIFA-Weltrangliste (Issue #579): eine Rangliste, mindestens auf dem Seed-Stand.</summary>
public class FifaWeltranglisteNachruestungTests
{
    private const string RANGLISTE = "FIFA-Weltrangliste (Herren)";
    private static readonly DateOnly SEED_STAND = new(2026, 10, 7);

    [Fact]
    public async Task LeereDatenbank_AlleNationalmannschaftenOhneLueckeUndAlleVerbaendeVerweisenDarauf()
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);

        await using var ctx = new TurnierverwaltungDbContext(optionen);
        var rangliste = await RanglisteLadenAsync(ctx);
        rangliste.Datum.Should().Be(SEED_STAND);
        rangliste.Eintraege.Should().HaveCount(211, "jedes FIFA-Mitglied ist als Nationalmannschaft im Seed");
        rangliste.Eintraege.Select(e => e.Rang).Should().BeEquivalentTo(Enumerable.Range(1, 211));
        (await RangUndPunkteAsync(ctx, rangliste, "ESP")).Should().Be((1, 2012));
        (await RangUndPunkteAsync(ctx, rangliste, "CUW")).Should().Be((80, 1305));
        (await RangUndPunkteAsync(ctx, rangliste, "KVX")).Should().Be((76, 1328), "FIFA-Kürzel KOS");
        (await ctx.Verbaende.Select(v => v.RanglisteId).ToListAsync()).Should().OnlyContain(id => id == rangliste.Id);
    }

    [Fact]
    public async Task LeereDatenbank_GruppenphasenDerNationalmannschaftsVorlagenLosenNachDerFifaListe()
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);

        await using var ctx = new TurnierverwaltungDbContext(optionen);
        var fifaId = (await RanglisteLadenAsync(ctx)).Id;
        var gruppenphasen = await ctx.TurnierRunden
            .Where(r => r.TurnierInstanz.Status == TurnierStatus.Vorlage && r.Rundenart == Rundenart.Gruppenphase && r.Reihenfolge == 1)
            .Select(r => new { r.TurnierInstanz.Name, r.TurnierInstanz.MannschaftsartFilter, r.StandardRanglisteId })
            .ToListAsync();

        gruppenphasen.Where(r => r.MannschaftsartFilter == MannschaftsartFilter.Nationalmannschaft)
            .Should().NotBeEmpty().And.OnlyContain(r => r.StandardRanglisteId == fifaId);
        gruppenphasen.Where(r => r.Name == "FIFA Klub-WM").Should().OnlyContain(r => r.StandardRanglisteId != fifaId);
    }

    [Fact]
    public async Task ZweiterLauf_ErgaenztNichts()
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);

        var protokoll = await SeedLaufAsync(optionen);

        protokoll.Should().BeEmpty();
    }

    // Stand nach der Migration FifaWeltranglisteVonQuelleLoesen: dieselbe Rangliste, Einträge geleert.
    [Fact]
    public async Task LeereVorhandeneRangliste_BekommtDenSeedStandUnterIhrerId()
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);
        int id = await AendernAsync(optionen, r => r.Eintraege.Clear());

        await SeedLaufAsync(optionen);

        await using var ctx = new TurnierverwaltungDbContext(optionen);
        var rangliste = await RanglisteLadenAsync(ctx);
        rangliste.Id.Should().Be(id);
        rangliste.Eintraege.Should().HaveCount(211);
        (await ctx.Ranglisten.CountAsync(r => r.Name == RANGLISTE)).Should().Be(1);
    }

    [Fact]
    public async Task AeltereRangliste_WirdAufDenSeedStandGebracht()
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);
        int id = await AendernAsync(optionen, r =>
        {
            r.Datum = SEED_STAND.AddMonths(-3);
            r.Quelle = "Wikipedia";
            foreach (var e in r.Eintraege.Where(e => e.Rang > 25).ToList()) r.Eintraege.Remove(e);
            foreach (var e in r.Eintraege) { e.Rang = 26 - e.Rang; e.Punkte = 1; }
        });

        await SeedLaufAsync(optionen);

        await using var ctx = new TurnierverwaltungDbContext(optionen);
        var rangliste = await RanglisteLadenAsync(ctx);
        rangliste.Id.Should().Be(id);
        rangliste.Datum.Should().Be(SEED_STAND);
        rangliste.Eintraege.Should().HaveCount(211);
        (await RangUndPunkteAsync(ctx, rangliste, "ESP")).Should().Be((1, 2012));
    }

    [Fact]
    public async Task NeuereRangliste_BleibtUnangetastet()
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);
        await AendernAsync(optionen, r =>
        {
            r.Datum = SEED_STAND.AddDays(42);
            foreach (var e in r.Eintraege.Where(e => e.Rang > 20).ToList()) r.Eintraege.Remove(e);
        });

        var protokoll = await SeedLaufAsync(optionen);

        protokoll.Should().BeEmpty();
        await using var ctx = new TurnierverwaltungDbContext(optionen);
        (await RanglisteLadenAsync(ctx)).Eintraege.Should().HaveCount(20);
    }

    private static Task<Rangliste> RanglisteLadenAsync(TurnierverwaltungDbContext ctx) =>
        ctx.Ranglisten.Include(r => r.Eintraege).SingleAsync(r => r.Name == RANGLISTE);

    private static async Task<(int Rang, int? Punkte)> RangUndPunkteAsync(
        TurnierverwaltungDbContext ctx, Rangliste rangliste, string fifaCode)
    {
        int id = await ctx.Mannschaften
            .Where(m => m.Mannschaftsart == Mannschaftsart.Nationalmannschaft && m.Land.FifaCode == fifaCode)
            .Select(m => m.Id).SingleAsync();
        var eintrag = rangliste.Eintraege.Single(e => e.MannschaftId == id);
        return (eintrag.Rang, eintrag.Punkte);
    }

    private static async Task<int> AendernAsync(DbContextOptions<TurnierverwaltungDbContext> optionen, Action<Rangliste> aendern)
    {
        await using var ctx = new TurnierverwaltungDbContext(optionen);
        var rangliste = await RanglisteLadenAsync(ctx);
        aendern(rangliste);
        await ctx.SaveChangesAsync();
        return rangliste.Id;
    }

    private static async Task<IReadOnlyList<SeedProtokollEintrag>> SeedLaufAsync(
        DbContextOptions<TurnierverwaltungDbContext> optionen)
    {
        await using var ctx = new TurnierverwaltungDbContext(optionen);
        return await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();
    }

    private static DbContextOptions<TurnierverwaltungDbContext> NeueDatenbank() =>
        new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
}
