using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class VerbandRepository(TurnierverwaltungDbContext context)
    : GenericRepository<Verband>(context), IVerbandRepository
{
    public override async Task<IReadOnlyList<Verband>> AlleFindenAsync(CancellationToken ct = default)
        => await Context.Verbaende
            .OrderBy(v => v.Name)
            .ToListAsync(ct);

    public async Task<VerbandBezuege> BezuegeZaehlenAsync(int verbandId, CancellationToken ct = default) => new(
        await Context.Laender.CountAsync(l => l.VerbandId == verbandId, ct),
        await Context.VerbandsQuoten.Where(q => q.VerbandId == verbandId)
            .Select(q => q.TurnierInstanzId).Distinct().CountAsync(ct));
}
