using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Application.Zufall;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Fussballturnierverwaltung.Tests.Spielplan;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Baut einen <see cref="SimulationsService"/> mit demselben Objektgraphen wie in der App auf einer
/// eigenen InMemory-Datenbank auf.</summary>
internal static class SimulationsDienste
{
    public static TurnierverwaltungDbContext NeuerContext() =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public static SimulationsService Service(TurnierverwaltungDbContext ctx, Zufallsquelle zufallsquelle)
    {
        var spielRepo = new SpielRepository(ctx);
        var instanzRepo = new TurnierInstanzRepository(ctx);
        var dienste = new SpielplanDienste(ctx);
        var ergebnisService = new SpielErgebnisService(
            spielRepo, new SpielErgebnisValidator(), dienste.Aufloesung, dienste.Kaskade, NullLogger<SpielErgebnisService>.Instance);
        var baumService = new TurnierBaumService(
            new TurnierBaumRepository(ctx), instanzRepo, spielRepo, dienste.Aufloesung, dienste.Quellverknuepfung,
            NullLogger<TurnierBaumService>.Instance);
        return new SimulationsService(
            spielRepo, instanzRepo, ergebnisService, baumService,
            new SimulationsLaufRepository(ctx), zufallsquelle, NullLogger<SimulationsService>.Instance);
    }
}
