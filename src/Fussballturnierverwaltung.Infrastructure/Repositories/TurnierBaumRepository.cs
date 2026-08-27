using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class TurnierBaumRepository(TurnierverwaltungDbContext context)
    : GenericRepository<TurnierBaum>(context), ITurnierBaumRepository
{
    public async Task<TurnierBaum?> FindenNachInstanzAsync(int turnierInstanzId, CancellationToken ct = default)
        => await Context.TurnierBaeume
            .FirstOrDefaultAsync(b => b.TurnierInstanzId == turnierInstanzId, ct);

    public async Task<TurnierBaum?> FindenNachInstanzMitKnotenAsync(int turnierInstanzId, CancellationToken ct = default)
        => await Context.TurnierBaeume
            .Include(b => b.Knoten.OrderBy(k => k.Runde))
                .ThenInclude(k => k.Spiel)
                    .ThenInclude(s => s!.HeimMannschaft)
                        .ThenInclude(m => m!.Land)
            .Include(b => b.Knoten)
                .ThenInclude(k => k.Spiel)
                    .ThenInclude(s => s!.AuswaertsMannschaft)
                        .ThenInclude(m => m!.Land)
            .Include(b => b.Knoten)
                .ThenInclude(k => k.Spiel)
                    .ThenInclude(s => s!.Ergebnis)
            .FirstOrDefaultAsync(b => b.TurnierInstanzId == turnierInstanzId, ct);

    public async Task KnotenSpeichernAsync(IEnumerable<TurnierBaumKnoten> knoten, CancellationToken ct = default)
    {
        await Context.TurnierBaumKnotenMenge.AddRangeAsync(knoten, ct);
        await Context.SaveChangesAsync(ct);
    }

    public async Task VerknuepfungenAktualisierenAsync(
        TurnierBaum baum, IEnumerable<TurnierBaumKnoten> knoten, CancellationToken ct = default)
    {
        // Knoten-Verknüpfungen aktualisieren
        Context.TurnierBaumKnotenMenge.UpdateRange(knoten);
        // Wurzelknoten am Baum setzen
        Context.TurnierBaeume.Update(baum);
        await Context.SaveChangesAsync(ct);
    }

    public async Task SpielIdsNullSetzenAsync(int turnierRundeId, CancellationToken ct = default)
    {
        // Bewusst kein ExecuteUpdateAsync (umgeht den Change-Tracker) - falls ein betroffener
        // TurnierBaumKnoten im selben Scope bereits geladen/getrackt ist (z.B. TurnierBaumAktualisierenAsync
        // im selben Request), würde die In-Memory-Instanz sonst nicht aktualisiert (Stale-Bug, siehe #103).
        var knoten = await Context.TurnierBaumKnotenMenge
            .Where(k => k.SpielId != null && k.Spiel!.TurnierRundeId == turnierRundeId)
            .ToListAsync(ct);

        foreach (var k in knoten)
            k.SpielId = null;

        await Context.SaveChangesAsync(ct);
    }

    public async Task LoeschenMitKnotenAsync(int turnierBaumId, CancellationToken ct = default)
    {
        var baum = await Context.TurnierBaeume
            .Include(b => b.Knoten)
            .FirstOrDefaultAsync(b => b.Id == turnierBaumId, ct);

        if (baum is null) return;

        // Schritt 1: Zirkuläre Referenzen auflösen
        baum.WurzelKnotenId = null;
        foreach (var knoten in baum.Knoten)
        {
            knoten.ElternKnotenId        = null;
            knoten.LinkerKindKnotenId    = null;
            knoten.RechterKindKnotenId   = null;
        }
        await Context.SaveChangesAsync(ct);

        // Schritt 2: Knoten löschen
        Context.TurnierBaumKnotenMenge.RemoveRange(baum.Knoten);
        await Context.SaveChangesAsync(ct);

        // Schritt 3: Baum löschen
        Context.TurnierBaeume.Remove(baum);
        await Context.SaveChangesAsync(ct);
    }
}
