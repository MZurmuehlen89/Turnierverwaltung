using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class TurnierRundeRepository(TurnierverwaltungDbContext context)
    : GenericRepository<TurnierRunde>(context), ITurnierRundeRepository
{
    public async Task<TurnierRunde?> FindenNachIdMitRegelwerkAsync(int id, CancellationToken ct = default)
        => await Context.TurnierRunden
            .Include(r => r.Regelwerk)
                .ThenInclude(rw => rw!.Entscheidungsverfahren)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<int?> StandardRegelwerkIdDerInstanzAsync(int turnierInstanzId, CancellationToken ct = default)
        => await Context.TurnierInstanzen
            .Where(i => i.Id == turnierInstanzId)
            .Select(i => i.StandardRegelwerkId)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<TurnierRunde>> KoRundenMitWirksamemRegelwerkAsync(
        int regelwerkId, CancellationToken ct = default)
        => await Context.TurnierRunden
            .Include(r => r.TurnierInstanz)
            .Where(r => (r.Rundenart == Rundenart.KoRunde || r.Rundenart == Rundenart.Finale
                         || r.Rundenart == Rundenart.SpielUmPlatzDrei)
                        && (r.RegelwerkId == regelwerkId
                            || (r.RegelwerkId == null && r.TurnierInstanz.StandardRegelwerkId == regelwerkId)))
            .OrderBy(r => r.TurnierInstanzId).ThenBy(r => r.Reihenfolge)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TurnierRunde>> AlleNachInstanzAsync(int turnierInstanzId, CancellationToken ct = default)
        => await Context.TurnierRunden
            .Include(r => r.Regelwerk)
            .Where(r => r.TurnierInstanzId == turnierInstanzId)
            .OrderBy(r => r.Reihenfolge)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TabellenBereich>> TabellenBereicheAsync(int turnierRundeId, CancellationToken ct = default)
        => await Context.TabellenBereiche
            .AsNoTracking()
            .Where(b => b.TurnierRundeId == turnierRundeId)
            .OrderBy(b => b.PlatzVon)
            .ToListAsync(ct);
}
