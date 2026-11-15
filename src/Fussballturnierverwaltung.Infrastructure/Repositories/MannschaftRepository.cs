using System.Globalization;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class MannschaftRepository(TurnierverwaltungDbContext context)
    : GenericRepository<Mannschaft>(context), IMannschaftRepository
{
    public async Task<IReadOnlyList<Mannschaft>> AlleFindenMitLandAsync(CancellationToken ct = default)
        => await Context.Mannschaften
            .Include(m => m.Land)
            .OrderBy(m => m.Mannschaftsart)
            .ThenBy(m => m.Name)
            .ToListAsync(ct);

    // Unsortiert und ohne Bildspalten (#419): Auswahllisten sortieren selbst, Wappen und Flaggen zeigen sie nicht.
    public async Task<IReadOnlyList<MannschaftAuswahl>> AlleAlsAuswahlFindenAsync(CancellationToken ct = default)
        => await Context.Mannschaften
            .Select(m => new MannschaftAuswahl(m.Id, m.Name, m.Mannschaftsart, m.LandId, m.Land.VerbandId))
            .ToListAsync(ct);

    public async Task<Mannschaft?> FindenNachIdMitLandAsync(int id, CancellationToken ct = default)
        => await Context.Mannschaften
            .Include(m => m.Land)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    // PostgreSQL (ICU) schreibt „İ“ klein als „i“ + kombinierenden Punkt (U+0307), .NET als schlichtes „i“ – ohne den
    // Punkt fand der Import „İstanbul Başakşehir“ nicht wieder und die Suche nach „istanbul“ nichts (#49). Beide Seiten
    // werfen ihn deshalb nach dem Kleinschreiben weg; EF übersetzt Replace in die Abfrage, InMemory rechnet es in .NET.
    private const string KOMBINIERENDER_PUNKT = "\u0307";

    // Ausdrücklich de-DE wie die Kollation der Datenbank, nicht die Kultur des Rechners (unter tr-TR würde „I“ zu „ı“).
    // Nicht invariant: ToLowerInvariant lässt „İ“ stehen, dann fehlen die Treffer wieder (belegt per Test, #49).
    private static readonly CultureInfo _deutsch = CultureInfo.GetCultureInfo("de-DE");

    private static string Vergleichsform(string text) => text.Trim().ToLower(_deutsch).Replace(KOMBINIERENDER_PUNKT, "");

    public async Task<IReadOnlyList<Mannschaft>> SuchenAsync(string suchbegriff, CancellationToken ct = default)
    {
        var term = Vergleichsform(suchbegriff);
        return await Context.Mannschaften
            .Include(m => m.Land)
            .Where(m => m.Name.ToLower().Replace(KOMBINIERENDER_PUNKT, "").Contains(term)
                     || m.Kurzname.ToLower().Replace(KOMBINIERENDER_PUNKT, "").Contains(term)
                     || m.Land.Name.ToLower().Replace(KOMBINIERENDER_PUNKT, "").Contains(term)
                     || m.Land.FifaCode.ToLower().Contains(term))
            .OrderBy(m => m.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Mannschaft>> AlleNachArtFindenAsync(Mannschaftsart art, CancellationToken ct = default)
        => await Context.Mannschaften
            .Include(m => m.Land)
            .Where(m => m.Mannschaftsart == art)
            .OrderBy(m => m.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Mannschaft>> FindenNachIdsAsync(IReadOnlyList<int> ids, CancellationToken ct = default)
        => await Context.Mannschaften
            .Include(m => m.Land)
            .Where(m => ids.Contains(m.Id))
            .ToListAsync(ct);

    public async Task<Mannschaft?> FindenNachNameAsync(string name, CancellationToken ct = default)
    {
        var vergleichsname = Vergleichsform(name);
        return await Context.Mannschaften
            .FirstOrDefaultAsync(m => m.Name.ToLower().Replace(KOMBINIERENDER_PUNKT, "") == vergleichsname, ct);
    }

    public async Task<IReadOnlyList<Mannschaft>> AlleNachTurnierfilterAsync(
        int? verbandId, int? landId, MannschaftsartFilter filter, CancellationToken ct = default)
    {
        var query = Context.Mannschaften.Include(m => m.Land).AsQueryable();

        // FIFA ist die Weltebene über den sechs Kontinentalverbänden (siehe VerbaendeSeedDaten) — kein Land
        // ist ihr direkt zugeordnet (Land.VerbandId zeigt immer auf einen Kontinentalverband). Als Filter
        // bedeutet FIFA daher "weltweit", nicht "kein Land passt" (Issue #211).
        if (verbandId.HasValue)
        {
            var istFifa = await Context.Verbaende.AnyAsync(v => v.Id == verbandId.Value && v.Kurzname == "FIFA", ct);
            if (!istFifa)
                query = query.Where(m => m.Land.VerbandId == verbandId.Value);
        }

        if (landId.HasValue)
            query = query.Where(m => m.LandId == landId.Value);

        query = filter switch
        {
            MannschaftsartFilter.Verein              => query.Where(m => m.Mannschaftsart == Mannschaftsart.Verein),
            MannschaftsartFilter.Nationalmannschaft  => query.Where(m => m.Mannschaftsart == Mannschaftsart.Nationalmannschaft),
            _                                        => query
        };

        return await query.OrderBy(m => m.Name).ToListAsync(ct);
    }
}
