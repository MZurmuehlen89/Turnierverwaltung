using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class RanglisteRepository(TurnierverwaltungDbContext context)
    : GenericRepository<Rangliste>(context), IRanglisteRepository
{
    public async Task<IReadOnlyList<Rangliste>> AlleFindenMitEintraegenAsync(CancellationToken ct = default)
        => await Context.Ranglisten
            .Include(r => r.Eintraege.OrderBy(e => e.Rang))
                .ThenInclude(e => e.Mannschaft)
                    .ThenInclude(m => m.Land)
            .AsSplitQuery()
            .OrderByDescending(r => r.Datum)
            .ToListAsync(ct);

    public async Task<Rangliste?> FindenNachIdMitEintraegenAsync(int id, CancellationToken ct = default)
        => await Context.Ranglisten
            .Include(r => r.Eintraege.OrderBy(e => e.Rang))
                .ThenInclude(e => e.Mannschaft)
                    .ThenInclude(m => m.Land)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task SpeichernAenderungenAsync(CancellationToken ct = default)
        => Context.SaveChangesAsync(ct);
}
