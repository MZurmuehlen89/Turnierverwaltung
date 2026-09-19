using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class LandesQuoteRepository(TurnierverwaltungDbContext context)
    : GenericRepository<LandesQuote>(context), ILandesQuoteRepository
{
    public async Task<IReadOnlyList<LandesQuote>> AlleNachTurnierInstanzIdAsync(int turnierInstanzId, CancellationToken ct = default)
        => await Context.LandesQuoten
            .Include(q => q.Land)
            .Where(q => q.TurnierInstanzId == turnierInstanzId)
            .OrderBy(q => q.Land.Name)
            .ToListAsync(ct);

    /// <summary>Synchronisiert per Schlüssel-Diff statt Löschen+Neuanlegen, siehe VerbandsQuoteRepository.QuotenErsetzenAsync.</summary>
    public async Task<IReadOnlyList<LandesQuote>> QuotenErsetzenAsync(int turnierInstanzId, IReadOnlyList<LandesQuote> neueQuoten, CancellationToken ct = default)
    {
        var alte = await Context.LandesQuoten.Where(q => q.TurnierInstanzId == turnierInstanzId).ToListAsync(ct);
        var alteNachLand = alte.ToDictionary(q => q.LandId);

        var ergebnis = new List<LandesQuote>(neueQuoten.Count);
        var neueLandIds = new HashSet<int>();

        foreach (var neu in neueQuoten)
        {
            neueLandIds.Add(neu.LandId);
            if (alteNachLand.TryGetValue(neu.LandId, out var bestehend))
            {
                bestehend.Anzahl = neu.Anzahl;
                ergebnis.Add(bestehend);
            }
            else
            {
                Context.LandesQuoten.Add(neu);
                ergebnis.Add(neu);
            }
        }

        var entfernte = alte.Where(q => !neueLandIds.Contains(q.LandId)).ToList();
        Context.LandesQuoten.RemoveRange(entfernte);

        await Context.SaveChangesAsync(ct);
        return ergebnis;
    }
}
