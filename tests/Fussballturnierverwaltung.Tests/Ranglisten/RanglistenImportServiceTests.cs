using FluentAssertions;
using Fussballturnierverwaltung.Application.Ranglisten.Import;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Ranglisten;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Ranglisten;

public class RanglistenImportServiceTests
{
    private const string Html = """
        <table>
        <tr><th>Rang</th><th>Team</th><th>Punkte</th></tr>
        <tr><td>1</td><td>Argentina[a]</td><td>1,832</td></tr>
        <tr><td>2</td><td>Unbekanntes Team</td><td>1,795</td></tr>
        <tr><td>3</td><td>france</td><td>1,700</td></tr>
        </table>
        """;

    private static readonly RanglistenQuelle Quelle = new()
    {
        Name = "Test-Quelle",
        Url = "https://example.com/ranking",
        TabellenIndex = 0,
        MannschaftSpalte = 1,
        PunkteSpalte = 2,
        KopfzeileUeberspringen = true
    };

    private static TurnierverwaltungDbContext NeuerContext() =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<RanglistenImportService> ServiceMitMannschaftenAsync(
        TurnierverwaltungDbContext ctx, params string[] mannschaftsNamen)
    {
        foreach (var (name, index) in mannschaftsNamen.Select((n, i) => (n, i)))
        {
            ctx.Mannschaften.Add(new Mannschaft
            {
                Id = index + 1,
                Name = name,
                Kurzname = name[..Math.Min(3, name.Length)],
                LandId = 1
            });
        }
        await ctx.SaveChangesAsync();

        var mannschaftService = new MannschaftService(
            new MannschaftRepository(ctx), new MannschaftValidator(), NullLogger<MannschaftService>.Instance);

        return new RanglistenImportService(
            new FakeHtmlHerunterlader(Html),
            new RanglistenHtmlAuswerter(),
            mannschaftService,
            NullLogger<RanglistenImportService>.Instance);
    }

    [Fact]
    public async Task ExakterName_WirdAufgeloest()
    {
        using var ctx = NeuerContext();
        var service = await ServiceMitMannschaftenAsync(ctx, "Argentina");

        var vorschau = await service.VorschauAbrufenAsync(Quelle);

        vorschau.Aufgeloest.Should().ContainSingle(z => z.Mannschaft.Name == "Argentina");
    }

    [Fact]
    public async Task GrossKleinschreibungUndWhitespace_WerdenToleriert()
    {
        using var ctx = NeuerContext();
        var service = await ServiceMitMannschaftenAsync(ctx, " France ");

        var vorschau = await service.VorschauAbrufenAsync(Quelle);

        vorschau.Aufgeloest.Should().Contain(z => z.Mannschaft.Name == " France ");
    }

    [Fact]
    public async Task NichtGefundenerName_LandetInUnaufgeloest()
    {
        using var ctx = NeuerContext();
        var service = await ServiceMitMannschaftenAsync(ctx, "Argentina", "France");

        var vorschau = await service.VorschauAbrufenAsync(Quelle);

        vorschau.Unaufgeloest.Should().ContainSingle(z => z.RohName == "Unbekanntes Team");
    }

    [Fact]
    public async Task RangWirdAusPositionNachUeberspringenAbgeleitet()
    {
        using var ctx = NeuerContext();
        var service = await ServiceMitMannschaftenAsync(ctx, "Argentina", "France");
        var vorschau = await service.VorschauAbrufenAsync(Quelle);

        // Simuliert: "Unbekanntes Team" wird vom Nutzer übersprungen, die beiden
        // aufgelösten Zeilen bleiben in ihrer Quellreihenfolge.
        var zuordnung = vorschau.Aufgeloest
            .Select(z => (z.Mannschaft, z.Quelle.Punkte))
            .ToList();

        var eintraege = service.EintraegeAusAufloesungErstellen(zuordnung);

        eintraege.Should().HaveCount(2);
        eintraege[0].Rang.Should().Be(1);
        eintraege[1].Rang.Should().Be(2);
    }
}
