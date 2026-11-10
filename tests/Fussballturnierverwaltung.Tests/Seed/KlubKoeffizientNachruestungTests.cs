using FluentAssertions;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Seed;

/// <summary>Seed des UEFA-Klub-Koeffizienten (Issue #381): eine Rangliste, mindestens auf dem Seed-Stand.</summary>
public class KlubKoeffizientNachruestungTests
{
    private const string RANGLISTE = "UEFA-Klub-Koeffizient (Kassies)";
    private static readonly DateOnly SEED_STAND = new(2026, 11, 1);
    private const int EINTRAEGE = 211;

    // Lückenlose Ränge 1…211 belegen zugleich: jeder Eintrag der Seed-Daten trifft einen Verein des Seeds.
    [Fact]
    public async Task LeereDatenbank_AlleEintraegeOhneLueckeUndUefaVerweistDarauf()
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);

        await using var ctx = new TurnierverwaltungDbContext(optionen);
        var rangliste = await RanglisteLadenAsync(ctx);
        rangliste.Datum.Should().Be(SEED_STAND);
        rangliste.Eintraege.Select(e => e.Rang).Should().BeEquivalentTo(Enumerable.Range(1, EINTRAEGE));
        (await RangUndPunkteAsync(ctx, rangliste, "FCB", "GER")).Should().Be((1, 148));
        (await RangUndPunkteAsync(ctx, rangliste, "SLP", "CZE")).Should().Be((59, 44), "Slavia Prag, neu mit #381");
        var verweise = await ctx.Verbaende.ToDictionaryAsync(v => v.Kurzname, v => v.VereinsRanglisteId);
        verweise["UEFA"].Should().Be(rangliste.Id);
        verweise["FIFA"].Should().BeNull("die übrigen Kontinentalverbände haben seit #581 eigene Listen");
    }

    [Fact]
    public async Task LeereDatenbank_LigaUndGruppenphasenDerEuropapokalVorlagenLosenNachDemKoeffizienten()
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);

        await using var ctx = new TurnierverwaltungDbContext(optionen);
        var id = (await RanglisteLadenAsync(ctx)).Id;
        var ersteRunden = await ctx.TurnierRunden
            .Where(r => r.TurnierInstanz.Status == TurnierStatus.Vorlage && r.Reihenfolge == 1
                        && (r.TurnierInstanz.Name.StartsWith("UEFA Champions League")
                            || r.TurnierInstanz.Name.StartsWith("UEFA Europa League")
                            || r.TurnierInstanz.Name.StartsWith("UEFA Conference League")))
            .Select(r => r.StandardRanglisteId)
            .ToListAsync();

        ersteRunden.Should().HaveCount(5).And.OnlyContain(r => r == id);
    }

    [Fact]
    public async Task ZweiterLauf_ErgaenztNichts()
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);

        var protokoll = await SeedLaufAsync(optionen);

        protokoll.Should().BeEmpty();
    }

    // Stand einer bestehenden Datenbank: Rangliste älter als der Seed und lückenhaft (fehlende Vereine).
    [Fact]
    public async Task AeltereRangliste_WirdAufDenSeedStandGebracht()
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);
        int id = await AendernAsync(optionen, r =>
        {
            r.Datum = new DateOnly(2026, 9, 28);
            foreach (var e in r.Eintraege.Where(e => e.Rang > 44).ToList()) r.Eintraege.Remove(e);
        });

        await SeedLaufAsync(optionen);

        await using var ctx = new TurnierverwaltungDbContext(optionen);
        var rangliste = await RanglisteLadenAsync(ctx);
        rangliste.Id.Should().Be(id);
        rangliste.Datum.Should().Be(SEED_STAND);
        rangliste.Eintraege.Should().HaveCount(EINTRAEGE);
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
        TurnierverwaltungDbContext ctx, Rangliste rangliste, string kurzname, string land)
    {
        int id = await ctx.Mannschaften
            .Where(m => m.Mannschaftsart == Mannschaftsart.Verein && m.Kurzname == kurzname && m.Land.FifaCode == land)
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
