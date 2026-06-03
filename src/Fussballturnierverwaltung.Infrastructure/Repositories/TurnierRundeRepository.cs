using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class TurnierRundeRepository(TurnierverwaltungDbContext context)
    : GenericRepository<TurnierRunde>(context), ITurnierRundeRepository
{
    public async Task<TurnierRunde?> FindenNachIdMitRegelwerkAsync(int id, CancellationToken ct = default)
        => await Context.TurnierRunden
            .Include(r => r.Regelwerk)
                .ThenInclude(rw => rw.Entscheidungsverfahren)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<TurnierRunde>> AlleNachInstanzAsync(int turnierInstanzId, CancellationToken ct = default)
        => await Context.TurnierRunden
            .Include(r => r.Regelwerk)
            .Where(r => r.TurnierInstanzId == turnierInstanzId)
            .OrderBy(r => r.Reihenfolge)
            .ToListAsync(ct);
}
