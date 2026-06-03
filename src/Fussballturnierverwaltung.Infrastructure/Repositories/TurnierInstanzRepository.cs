using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class TurnierInstanzRepository(TurnierverwaltungDbContext context)
    : GenericRepository<TurnierInstanz>(context), ITurnierInstanzRepository
{
    public async Task<IReadOnlyList<TurnierInstanz>> AlleFindenMitVorlageAsync(CancellationToken ct = default)
        => await Context.TurnierInstanzen
            .Include(ti => ti.TurnierVorlage)
            .OrderByDescending(ti => ti.SaisonOderJahr)
            .ThenBy(ti => ti.Name)
            .ToListAsync(ct);

    public async Task<TurnierInstanz?> FindenNachIdMitRundenAsync(int id, CancellationToken ct = default)
        => await Context.TurnierInstanzen
            .Include(ti => ti.TurnierVorlage)
            .Include(ti => ti.Runden.OrderBy(r => r.Reihenfolge))
                .ThenInclude(r => r.Regelwerk)
            .FirstOrDefaultAsync(ti => ti.Id == id, ct);

    public async Task<IReadOnlyList<TurnierInstanz>> AlleNachStatusFindenAsync(TurnierStatus status, CancellationToken ct = default)
        => await Context.TurnierInstanzen
            .Include(ti => ti.TurnierVorlage)
            .Where(ti => ti.Status == status)
            .OrderByDescending(ti => ti.SaisonOderJahr)
            .ToListAsync(ct);
}
