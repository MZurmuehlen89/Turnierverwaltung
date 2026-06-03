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
}
