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

    public async Task<IReadOnlyList<QualifikationsRegel>> AlleNachZielRundeIdAsync(int zielRundeId, CancellationToken ct = default)
        => await Context.QualifikationsRegeln
            .Include(r => r.QuellRunde)
            .Where(r => r.ZielRundeId == zielRundeId)
            .OrderBy(r => r.GruppeIndex)
                .ThenBy(r => r.PlatzierungVon)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<QualifikationsRegel>> AlleNachTurnierInstanzIdAsync(int turnierInstanzId, CancellationToken ct = default)
        => await Context.QualifikationsRegeln
            .Include(r => r.QuellRunde)
            .Include(r => r.ZielRunde)
            .Where(r => r.QuellRunde.TurnierInstanzId == turnierInstanzId)
            .ToListAsync(ct);

    public async Task<QualifikationsRegel?> FindenNachIdMitRundenAsync(int id, CancellationToken ct = default)
        => await Context.QualifikationsRegeln
            .Include(r => r.QuellRunde)
            .Include(r => r.ZielRunde)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
}
