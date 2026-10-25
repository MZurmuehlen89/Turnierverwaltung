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
            .Include(r => r.Abschnitte)
            .AsSplitQuery()
            .OrderByDescending(r => r.Datum)
            .ToListAsync(ct);

    public async Task<Rangliste?> FindenNachIdMitEintraegenAsync(int id, CancellationToken ct = default)
        => await Context.Ranglisten
            .Include(r => r.Eintraege.OrderBy(e => e.Rang))
                .ThenInclude(e => e.Mannschaft)
                    .ThenInclude(m => m.Land)
            .Include(r => r.Abschnitte)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyDictionary<int, int>> PlaetzeAsync(int ranglisteId, CancellationToken ct = default)
    {
        var eintraege = await Context.RanglistenEintraege
            .Where(e => e.RanglisteId == ranglisteId)
            .Select(e => new { e.MannschaftId, e.Rang })
            .ToListAsync(ct);

        // Steht eine Mannschaft doppelt in der Liste, gilt ihr bester Platz.
        return eintraege
            .GroupBy(e => e.MannschaftId)
            .ToDictionary(g => g.Key, g => g.Min(e => e.Rang));
    }

    public Task<Rangliste?> MitAbschnittenAsync(int ranglisteId, CancellationToken ct = default) =>
        Context.Ranglisten
            .AsNoTracking()
            .Include(r => r.Eintraege)
            .Include(r => r.Abschnitte)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == ranglisteId, ct);

    public async Task<IReadOnlyList<Rangliste>> LigapyramidenAsync(CancellationToken ct = default)
        => await Context.Ranglisten
            .AsNoTracking()
            .Where(r => r.Abschnitte.Any())
            .Include(r => r.Eintraege)
            .Include(r => r.Abschnitte)
            .AsSplitQuery()
            .ToListAsync(ct);

    public Task SpeichernAenderungenAsync(CancellationToken ct = default)
        => Context.SaveChangesAsync(ct);
}
