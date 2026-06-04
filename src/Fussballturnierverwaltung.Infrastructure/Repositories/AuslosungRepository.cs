using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class AuslosungRepository(TurnierverwaltungDbContext context)
    : GenericRepository<Auslosung>(context), IAuslosungRepository
{
    public async Task<IReadOnlyList<Auslosung>> AlleNachInstanzAsync(int turnierInstanzId, CancellationToken ct = default)
        => await Context.Auslosungen
            .Include(a => a.TurnierRunde)
            .Where(a => a.TurnierInstanzId == turnierInstanzId)
            .OrderBy(a => a.TurnierRunde.Reihenfolge)
            .ThenBy(a => a.Name)
            .ToListAsync(ct);

    public async Task<Auslosung?> FindenNachIdMitDetailsAsync(int id, CancellationToken ct = default)
        => await Context.Auslosungen
            .Include(a => a.TurnierRunde)
            .Include(a => a.Lostoepfe.OrderBy(l => l.Reihenfolge))
                .ThenInclude(l => l.Eintraege)
                    .ThenInclude(e => e.Mannschaft)
                        .ThenInclude(m => m.Land)
            .Include(a => a.Regeln)
            .Include(a => a.Ergebnisse.OrderBy(e => e.PaarungsNummer))
                .ThenInclude(e => e.HeimMannschaft)
            .Include(a => a.Ergebnisse)
                .ThenInclude(e => e.AuswaertsMannschaft)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task ErgebnisseSetzenUndAbschliessenAsync(
        int auslosungId,
        IReadOnlyList<AuslosungsErgebnis> ergebnisse,
        CancellationToken ct = default)
    {
        var auslosung = await Context.Auslosungen
            .Include(a => a.Ergebnisse)
            .FirstOrDefaultAsync(a => a.Id == auslosungId, ct);

        if (auslosung is null) return;

        Context.AuslosungsErgebnisse.RemoveRange(auslosung.Ergebnisse);

        foreach (var e in ergebnisse)
        {
            e.AuslosungId = auslosungId;
            await Context.AuslosungsErgebnisse.AddAsync(e, ct);
        }

        auslosung.Status = AuslosungsStatus.Abgeschlossen;
        await Context.SaveChangesAsync(ct);
    }

    public async Task ErgebnisseLoeschenUndZuruecksetzenAsync(int auslosungId, CancellationToken ct = default)
    {
        var auslosung = await Context.Auslosungen
            .Include(a => a.Ergebnisse)
            .FirstOrDefaultAsync(a => a.Id == auslosungId, ct);

        if (auslosung is null) return;

        Context.AuslosungsErgebnisse.RemoveRange(auslosung.Ergebnisse);
        auslosung.Status = AuslosungsStatus.Vorbereitung;
        await Context.SaveChangesAsync(ct);
    }
}
