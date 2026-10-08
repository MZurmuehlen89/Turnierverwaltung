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

    public async Task<TurnierBaum?> FindenNachInstanzMitAufbauAsync(int turnierInstanzId, CancellationToken ct = default)
        => await Context.TurnierBaeume
            .Include(b => b.Knoten)
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
        // TurnierBaumKnoten im selben Scope bereits geladen/getrackt ist (z.B. durch die Turnierbaum-Aktualisierung
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
        await KnotenLoeschenAsync(baum, ct);

        // Schritt 3: Baum löschen
        Context.TurnierBaeume.Remove(baum);
        await Context.SaveChangesAsync(ct);
    }

    private async Task KnotenLoeschenAsync(TurnierBaum baum, CancellationToken ct)
    {
        if (!Context.Database.IsRelational())
        {
            // Der InMemory-Provider der Tests kennt kein ExecuteDelete.
            Context.TurnierBaumKnotenMenge.RemoveRange(baum.Knoten);
            await Context.SaveChangesAsync(ct);
            return;
        }

        // Eine Anweisung für alle Knoten (#390): der SQL Server kompiliert jedes einzelne
        // "DELETE … WHERE Id = @p" auf dieser Tabelle bei jeder Ausführung neu (gemessen rund 95–125 ms für
        // 23 Knoten gegenüber 3–9 ms für ebenso viele Einfügungen, dasselbe Muster wie bei Spielen in #414).
        // ExecuteDelete läuft am Change-Tracker vorbei — die gelöschten Knoten müssen deshalb ausdrücklich aus
        // dem Kontext und aus der Sammlung des Baums (Stale-Bug, siehe #103).
        await Context.TurnierBaumKnotenMenge.Where(k => k.TurnierBaumId == baum.Id).ExecuteDeleteAsync(ct);
        foreach (var knoten in baum.Knoten)
            Context.Entry(knoten).State = EntityState.Detached;
        baum.Knoten.Clear();
    }
}
