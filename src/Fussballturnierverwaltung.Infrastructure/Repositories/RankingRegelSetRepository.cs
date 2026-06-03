using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class RankingRegelSetRepository(TurnierverwaltungDbContext context)
    : GenericRepository<RankingRegelSet>(context), IRankingRegelSetRepository
{
    public async Task<RankingRegelSet?> FindenNachIdMitEintraegenAsync(int id, CancellationToken ct = default)
        => await Context.RankingRegelSets
            .Include(s => s.Eintraege.OrderBy(e => e.Reihenfolge))
                .ThenInclude(e => e.RankingRegel)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<RankingRegelSet>> AlleFindenMitEintraegenAsync(CancellationToken ct = default)
        => await Context.RankingRegelSets
            .Include(s => s.Eintraege.OrderBy(e => e.Reihenfolge))
                .ThenInclude(e => e.RankingRegel)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);
}
