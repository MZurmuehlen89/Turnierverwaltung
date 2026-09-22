using FluentAssertions;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Seed;

public class SeedDatenServiceTests
{
    private const string CL_VORLAGE = "UEFA Champions League (36 Teams, ab 2024/25)";

    [Fact]
    public async Task InitialisierenAsync_KorrigiertAlteSchweizerLigaphaseInVorlageAufTopfLiga()
    {
        var optionen = new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();
            // Stand vor #328 simulieren: Ligaphase fälschlich als Schweizer System gespeichert,
            // plus eine bewusst manuell auf Liga umgestellte Runde, die unangetastet bleiben muss.
            var cl = await ctx.TurnierInstanzen.Include(i => i.Runden)
                .SingleAsync(i => i.Name == CL_VORLAGE && i.Status == TurnierStatus.Vorlage);
            cl.Runden.Single(r => r.Name == "Ligaphase").Rundenart = Rundenart.SchweizerSystem;
            cl.Runden.Single(r => r.Name == "Ligaphase").KonfigurationJson = "{\"anzahlSpieltage\":6}";
            var el = await ctx.TurnierInstanzen.Include(i => i.Runden)
                .SingleAsync(i => i.Name == "UEFA Europa League (36 Teams, ab 2024/25)" && i.Status == TurnierStatus.Vorlage);
            el.Runden.Single(r => r.Name == "Ligaphase").Rundenart = Rundenart.Liga;
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();

            var runden = await ctx.TurnierRunden
                .Where(r => r.Name == "Ligaphase" && r.TurnierInstanz.Status == TurnierStatus.Vorlage)
                .Select(r => new { Vorlage = r.TurnierInstanz.Name, r.Rundenart, r.KonfigurationJson })
                .ToListAsync();
            runden.Single(r => r.Vorlage == CL_VORLAGE).Rundenart.Should().Be(Rundenart.TopfLiga);
            runden.Single(r => r.Vorlage == CL_VORLAGE).KonfigurationJson.Should().BeNull();
            runden.Single(r => r.Vorlage == "UEFA Europa League (36 Teams, ab 2024/25)").Rundenart
                .Should().Be(Rundenart.Liga, "manuelle Anpassungen außerhalb des bekannten Altwerts bleiben erhalten");
        }
    }
}
