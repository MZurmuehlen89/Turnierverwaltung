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

    /// <summary>Synchronisiert per Schlüssel-Diff statt Löschen+Neuanlegen, siehe VerbandsQuoteRepository.QuotenErsetzenAsync.
    /// Schlüssel ist das Kontingent (Land, Spielklasse, ab Platz, Pokalsieger), da ein Land mehrere Quoten haben kann
    /// (#572, #583).</summary>
    public async Task<IReadOnlyList<LandesQuote>> QuotenErsetzenAsync(int turnierInstanzId, IReadOnlyList<LandesQuote> neueQuoten, CancellationToken ct = default)
    {
        var alte = await Context.LandesQuoten.Where(q => q.TurnierInstanzId == turnierInstanzId).ToListAsync(ct);
        var alteNachSchluessel = alte.ToDictionary(Schluessel);

        var ergebnis = new List<LandesQuote>(neueQuoten.Count);
        var neueSchluessel = new HashSet<(int, int?, int?, bool)>();

        foreach (var neu in neueQuoten)
        {
            neueSchluessel.Add(Schluessel(neu));
            if (alteNachSchluessel.TryGetValue(Schluessel(neu), out var bestehend))
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

        Context.LandesQuoten.RemoveRange(alte.Where(q => !neueSchluessel.Contains(Schluessel(q))));

        await Context.SaveChangesAsync(ct);
        return ergebnis;
    }

    private static (int, int?, int?, bool) Schluessel(LandesQuote quote) =>
        (quote.LandId, quote.Spielklasse, quote.AbPlatz, quote.Pokalsieger);
}
