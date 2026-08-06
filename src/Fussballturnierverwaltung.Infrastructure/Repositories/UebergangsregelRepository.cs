using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class UebergangsregelRepository(TurnierverwaltungDbContext context)
    : GenericRepository<Uebergangsregel>(context), IUebergangsregelRepository
{
    public async Task<IReadOnlyList<Uebergangsregel>> AlleNachQuellRundeIdAsync(int quellRundeId, CancellationToken ct = default)
        => await Context.Uebergangsregeln
            .Include(r => r.ZielRunde)
            .Where(r => r.QuellRundeId == quellRundeId)
            .OrderBy(r => r.GruppeIndex)
                .ThenBy(r => r.PlatzierungVon)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Uebergangsregel>> AlleNachZielRundeIdAsync(int zielRundeId, CancellationToken ct = default)
        => await Context.Uebergangsregeln
            .Include(r => r.QuellRunde)
            .Where(r => r.ZielRundeId == zielRundeId)
            .OrderBy(r => r.GruppeIndex)
                .ThenBy(r => r.PlatzierungVon)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Uebergangsregel>> AlleNachTurnierInstanzIdAsync(int turnierInstanzId, CancellationToken ct = default)
        => await Context.Uebergangsregeln
            .Include(r => r.QuellRunde)
            .Include(r => r.ZielRunde)
            .Where(r => r.QuellRunde.TurnierInstanzId == turnierInstanzId)
            .ToListAsync(ct);

    public async Task<Uebergangsregel?> FindenNachIdMitRundenAsync(int id, CancellationToken ct = default)
        => await Context.Uebergangsregeln
            .Include(r => r.QuellRunde)
            .Include(r => r.ZielRunde)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
}
