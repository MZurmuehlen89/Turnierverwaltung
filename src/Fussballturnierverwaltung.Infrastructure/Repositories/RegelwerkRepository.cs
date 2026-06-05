using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class RegelwerkRepository(TurnierverwaltungDbContext context)
    : GenericRepository<Regelwerk>(context), IRegelwerkRepository
{
    public async Task<IReadOnlyList<Regelwerk>> AlleFindenMitRegelnAsync(CancellationToken ct = default)
        => await Context.Regelwerke
            .Include(r => r.PunkteRegeln)
            .Include(r => r.Entscheidungsverfahren.OrderBy(e => e.Reihenfolge))
            .Include(r => r.RankingRegelSet)
            .AsSplitQuery()
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

    public async Task<Regelwerk?> FindenNachIdMitAllenRegelnAsync(int id, CancellationToken ct = default)
        => await Context.Regelwerke
            .Include(r => r.PunkteRegeln)
            .Include(r => r.Entscheidungsverfahren.OrderBy(e => e.Reihenfolge))
            .Include(r => r.RankingRegelSet)
                .ThenInclude(s => s!.Eintraege.OrderBy(e => e.Reihenfolge))
                    .ThenInclude(e => e.RankingRegel)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id, ct);
}
