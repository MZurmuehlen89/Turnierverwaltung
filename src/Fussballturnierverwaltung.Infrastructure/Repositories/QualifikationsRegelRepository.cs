using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class QualifikationsRegelRepository(TurnierverwaltungDbContext context)
    : GenericRepository<QualifikationsRegel>(context), IQualifikationsRegelRepository
{
    public async Task<IReadOnlyList<QualifikationsRegel>> AlleNachQuellRundeIdAsync(int quellRundeId, CancellationToken ct = default)
        => await Context.QualifikationsRegeln
            .Include(r => r.ZielRunde)
            .Where(r => r.QuellRundeId == quellRundeId)
            .OrderBy(r => r.GruppeIndex)
                .ThenBy(r => r.PlatzierungVon)
            .ToListAsync(ct);
}
