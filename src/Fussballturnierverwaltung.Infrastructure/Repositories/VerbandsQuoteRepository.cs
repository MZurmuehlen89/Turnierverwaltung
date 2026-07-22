using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class VerbandsQuoteRepository(TurnierverwaltungDbContext context)
    : GenericRepository<VerbandsQuote>(context), IVerbandsQuoteRepository
{
    public async Task<IReadOnlyList<VerbandsQuote>> AlleNachTurnierInstanzIdAsync(int turnierInstanzId, CancellationToken ct = default)
        => await Context.VerbandsQuoten
            .Include(q => q.Verband)
            .Where(q => q.TurnierInstanzId == turnierInstanzId)
            .OrderBy(q => q.Verband.Name)
            .ToListAsync(ct);
}
