using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class LandRepository(TurnierverwaltungDbContext context)
    : GenericRepository<Land>(context), ILandRepository
{
    // Rangliste samt Abschnitten (#572): Turnierkopf und Quotenprüfung brauchen die Spielklassen der Ligapyramide.
    public async Task<IReadOnlyList<Land>> AlleFindenMitVerbandAsync(CancellationToken ct = default)
        => await Context.Laender
            .Include(l => l.Verband)
            .Include(l => l.Rangliste!).ThenInclude(r => r.Abschnitte)
            .OrderBy(l => l.Kontinent)
            .ThenBy(l => l.Name)
            .ToListAsync(ct);

    public override async Task<IReadOnlyList<Land>> AlleFindenAsync(CancellationToken ct = default)
        => await Context.Laender
            .Include(l => l.Verband)
            .OrderBy(l => l.Name)
            .ToListAsync(ct);
}
