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

    /// <summary>Synchronisiert per Schlüssel-Diff statt Löschen+Neuanlegen (Nutzer-Vorgabe: grundsätzlich
    /// Insert/Update statt Löschen+Neuanlegen, Delete nur für tatsächlich weggefallene Zeilen — analog
    /// TurnierVorlageRepository.RundenErsetzenAsync). VerbandId identifiziert die Zeile eindeutig je
    /// TurnierInstanz, Anzahl ist das einzige veränderliche Feld.</summary>
    public async Task<IReadOnlyList<VerbandsQuote>> QuotenErsetzenAsync(int turnierInstanzId, IReadOnlyList<VerbandsQuote> neueQuoten, CancellationToken ct = default)
    {
        var alte = await Context.VerbandsQuoten.Where(q => q.TurnierInstanzId == turnierInstanzId).ToListAsync(ct);
        var alteNachVerband = alte.ToDictionary(q => q.VerbandId);

        var ergebnis = new List<VerbandsQuote>(neueQuoten.Count);
        var neueVerbandIds = new HashSet<int>();

        foreach (var neu in neueQuoten)
        {
            neueVerbandIds.Add(neu.VerbandId);
            if (alteNachVerband.TryGetValue(neu.VerbandId, out var bestehend))
            {
                bestehend.Anzahl = neu.Anzahl;
                ergebnis.Add(bestehend);
            }
            else
            {
                Context.VerbandsQuoten.Add(neu);
                ergebnis.Add(neu);
            }
        }

        var entfernte = alte.Where(q => !neueVerbandIds.Contains(q.VerbandId)).ToList();
        Context.VerbandsQuoten.RemoveRange(entfernte);

        await Context.SaveChangesAsync(ct);
        return ergebnis;
    }
}
