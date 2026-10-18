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

    public async Task<IReadOnlyDictionary<int, int>> PlaetzeDerNeuestenZurQuelleAsync(int quelleId, CancellationToken ct = default)
    {
        var ranglisteId = await Context.Ranglisten
            .Where(r => r.RanglistenQuelleId == quelleId)
            .OrderByDescending(r => r.Datum)
            .ThenByDescending(r => r.Id)
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync(ct);
        if (ranglisteId is null) return new Dictionary<int, int>();

        var eintraege = await Context.RanglistenEintraege
            .Where(e => e.RanglisteId == ranglisteId)
            .Select(e => new { e.MannschaftId, e.Rang })
            .ToListAsync(ct);

        // Steht eine Mannschaft doppelt in der Liste, gilt ihr bester Platz.
        return eintraege
            .GroupBy(e => e.MannschaftId)
            .ToDictionary(g => g.Key, g => g.Min(e => e.Rang));
    }

    public Task SpeichernAenderungenAsync(CancellationToken ct = default)
        => Context.SaveChangesAsync(ct);
}
