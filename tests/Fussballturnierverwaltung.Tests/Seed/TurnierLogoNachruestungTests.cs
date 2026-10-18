using FluentAssertions;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Seed;

public class TurnierLogoNachruestungTests
{
    private static readonly byte[] _eigenesLogo = [1, 2, 3];

    [Fact]
    public async Task InitialisierenAsync_LeereDatenbank_JedeWettbewerbsvorlageHatEinLogo()
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);

        await using var ctx = new TurnierverwaltungDbContext(optionen);
        var vorlagen = await ctx.TurnierInstanzen.Where(i => i.Status == TurnierStatus.Vorlage).ToListAsync();

        vorlagen.Where(v => v.Name is not ("Mini-Turnier" or "Freies Turnier"))
            .Should().OnlyContain(v => v.Logo != null && v.Logo.Length > 0);
        vorlagen.Where(v => v.Name is "Mini-Turnier" or "Freies Turnier").Should().OnlyContain(v => v.Logo == null);
    }

    [Fact]
    public async Task InitialisierenAsync_VorlageOhneLogo_GibtLogoEinmaligAnTurniereDesWettbewerbsWeiter()
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);
        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            (await ctx.TurnierInstanzen.SingleAsync(i => i.Name == "Bundesliga" && i.Status == TurnierStatus.Vorlage))
                .Logo = null;
            ctx.TurnierInstanzen.AddRange(
                Turnier("Bundesliga 2026"),
                Turnier("Bundesliga"),
                Turnier("2. Bundesliga 2026"),
                Turnier("Bundesligatest"),
                Turnier("Bundesliga 2027", _eigenesLogo));
            await ctx.SaveChangesAsync();
        }

        var protokoll = await SeedLaufAsync(optionen);

        protokoll.Select(e => (e.Anzahl, e.Text)).Should().Equal(
            (1, "Turniervorlagen um ihr Turnierlogo ergänzt."),
            (2, "Turniere um das Logo ihres Wettbewerbs ergänzt."));
        var logos = await LogosAsync(optionen);
        logos["Bundesliga 2026"].Should().NotBeNull();
        logos["Bundesliga"].Should().NotBeNull();
        logos["2. Bundesliga 2026"].Should().BeNull("die Vorlage der 2. Bundesliga hatte ihr Logo schon");
        logos["Bundesligatest"].Should().BeNull();
        logos["Bundesliga 2027"].Should().Equal(_eigenesLogo);

        // Ein am Turnier entferntes Logo kommt nicht zurück, solange die Vorlage ihres behält.
        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            (await ctx.TurnierInstanzen.SingleAsync(i => i.Name == "Bundesliga 2026")).Logo = null;
            await ctx.SaveChangesAsync();
        }
        (await SeedLaufAsync(optionen)).Should().BeEmpty();
        (await LogosAsync(optionen))["Bundesliga 2026"].Should().BeNull();
    }

    private static TurnierInstanz Turnier(string name, byte[]? logo = null) =>
        new() { Name = name, Status = TurnierStatus.Geplant, Logo = logo };

    private static async Task<Dictionary<string, byte[]?>> LogosAsync(DbContextOptions<TurnierverwaltungDbContext> optionen)
    {
        await using var ctx = new TurnierverwaltungDbContext(optionen);
        return await ctx.TurnierInstanzen.Where(i => i.Status != TurnierStatus.Vorlage)
            .ToDictionaryAsync(i => i.Name, i => i.Logo);
    }

    private static async Task<IReadOnlyList<SeedProtokollEintrag>> SeedLaufAsync(
        DbContextOptions<TurnierverwaltungDbContext> optionen)
    {
        await using var ctx = new TurnierverwaltungDbContext(optionen);
        return await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();
    }

    private static DbContextOptions<TurnierverwaltungDbContext> NeueDatenbank() =>
        new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
}
