using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class SpielRepository(TurnierverwaltungDbContext context)
    : GenericRepository<Spiel>(context), ISpielRepository
{
    public async Task<IReadOnlyList<Spiel>> AlleNachRundeAsync(int turnierRundeId, CancellationToken ct = default)
        => await Context.Spiele
            .Include(s => s.HeimMannschaft)
            .Include(s => s.AuswaertsMannschaft)
            .Include(s => s.Ergebnis)
            .Where(s => s.TurnierRundeId == turnierRundeId)
            .OrderBy(s => s.Spieltag)
            .ThenBy(s => s.Spielnummer)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Spiel>> AlleNachInstanzAsync(int turnierInstanzId, CancellationToken ct = default)
        => await Context.Spiele
            .Include(s => s.HeimMannschaft)
            .Include(s => s.AuswaertsMannschaft)
            .Include(s => s.Ergebnis)
            .Include(s => s.TurnierRunde)
            .Where(s => s.TurnierInstanzId == turnierInstanzId)
            .OrderBy(s => s.TurnierRunde.Reihenfolge)
            .ThenBy(s => s.Spieltag)
            .ThenBy(s => s.Spielnummer)
            .ToListAsync(ct);

    public async Task<int> HoechsteSpielNummerAsync(int turnierInstanzId, CancellationToken ct = default)
    {
        var max = await Context.Spiele
            .Where(s => s.TurnierInstanzId == turnierInstanzId)
            .MaxAsync(s => (int?)s.Spielnummer, ct);
        return max ?? 0;
    }

    public async Task HinzufuegenMehrfachAsync(IEnumerable<Spiel> spiele, CancellationToken ct = default)
    {
        await Context.Spiele.AddRangeAsync(spiele, ct);
        await Context.SaveChangesAsync(ct);
    }
}
