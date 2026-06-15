using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class MannschaftRepository(TurnierverwaltungDbContext context)
    : GenericRepository<Mannschaft>(context), IMannschaftRepository
{
    public async Task<IReadOnlyList<Mannschaft>> AlleFindenMitLandAsync(CancellationToken ct = default)
        => await Context.Mannschaften
            .Include(m => m.Land)
            .OrderBy(m => m.Mannschaftsart)
            .ThenBy(m => m.Name)
            .ToListAsync(ct);

    public async Task<Mannschaft?> FindenNachIdMitLandAsync(int id, CancellationToken ct = default)
        => await Context.Mannschaften
            .Include(m => m.Land)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<IReadOnlyList<Mannschaft>> SuchenAsync(string suchbegriff, CancellationToken ct = default)
    {
        var term = suchbegriff.Trim().ToLower();
        return await Context.Mannschaften
            .Include(m => m.Land)
            .Where(m => m.Name.ToLower().Contains(term)
                     || m.Kurzname.ToLower().Contains(term)
                     || m.Land.Name.ToLower().Contains(term)
                     || m.Land.FifaCode.ToLower().Contains(term))
            .OrderBy(m => m.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Mannschaft>> AlleNachArtFindenAsync(Mannschaftsart art, CancellationToken ct = default)
        => await Context.Mannschaften
            .Include(m => m.Land)
            .Where(m => m.Mannschaftsart == art)
            .OrderBy(m => m.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Mannschaft>> FindenNachIdsAsync(IReadOnlyList<int> ids, CancellationToken ct = default)
        => await Context.Mannschaften
            .Include(m => m.Land)
            .Where(m => ids.Contains(m.Id))
            .ToListAsync(ct);

    public async Task<Mannschaft?> FindenNachNameAsync(string name, CancellationToken ct = default)
    {
        var nameLower = name.Trim().ToLower();
        return await Context.Mannschaften
            .FirstOrDefaultAsync(m => m.Name.ToLower() == nameLower, ct);
    }

    public async Task<IReadOnlyList<Mannschaft>> AlleNachTurnierfilterAsync(
        int? verbandId, MannschaftsartFilter filter, CancellationToken ct = default)
    {
        var query = Context.Mannschaften.Include(m => m.Land).AsQueryable();

        if (verbandId.HasValue)
            query = query.Where(m => m.Land.VerbandId == verbandId.Value);

        query = filter switch
        {
            MannschaftsartFilter.Verein              => query.Where(m => m.Mannschaftsart == Mannschaftsart.Verein),
            MannschaftsartFilter.Nationalmannschaft  => query.Where(m => m.Mannschaftsart == Mannschaftsart.Nationalmannschaft),
            _                                        => query
        };

        return await query.OrderBy(m => m.Name).ToListAsync(ct);
    }
}
