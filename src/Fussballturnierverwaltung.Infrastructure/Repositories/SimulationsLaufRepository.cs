using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Simulation;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class SimulationsLaufRepository(TurnierverwaltungDbContext context)
    : GenericRepository<SimulationsLauf>(context), ISimulationsLaufRepository
{
    public async Task<IReadOnlyList<SimulationsLauf>> AlleNachInstanzAsync(
        int turnierInstanzId, CancellationToken ct = default)
        => await Context.SimulationsLaeufe
            .Where(s => s.TurnierInstanzId == turnierInstanzId)
            .OrderByDescending(s => s.StartZeitpunkt)
            .ToListAsync(ct);
}
