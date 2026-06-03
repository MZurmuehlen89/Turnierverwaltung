using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class RankingRegelRepository(TurnierverwaltungDbContext context)
    : GenericRepository<RankingRegel>(context), IRankingRegelRepository
{
    public override async Task<IReadOnlyList<RankingRegel>> AlleFindenAsync(CancellationToken ct = default)
        => await Context.RankingRegeln.OrderBy(r => r.Regelart).ToListAsync(ct);
}
