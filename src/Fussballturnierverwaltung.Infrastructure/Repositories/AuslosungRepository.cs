using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
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
                    .ThenInclude(m => m!.Land)
            .Include(a => a.Ergebnisse)
                .ThenInclude(e => e.AuswaertsMannschaft)
                    .ThenInclude(m => m!.Land)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<GruppenkopfKandidaten> GruppenkopfKandidatenAsync(int turnierInstanzId, CancellationToken ct = default)
    {
        var gastgeberIds = await Context.TurnierGastgeber
            .Where(g => g.TurnierInstanzId == turnierInstanzId)
            .OrderBy(g => g.Id)
            .Select(g => g.MannschaftId)
            .ToListAsync(ct);
        var titelverteidigerId = await Context.TurnierInstanzen
            .Where(i => i.Id == turnierInstanzId)
            .Select(i => i.TitelverteidigerId)
            .FirstOrDefaultAsync(ct);
        var ids = titelverteidigerId is { } tv ? [.. gastgeberIds, tv] : gastgeberIds;
        var namen = await Context.Mannschaften
            .Where(m => ids.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.Name, ct);
        return new GruppenkopfKandidaten(gastgeberIds, titelverteidigerId, namen);
    }

    public async Task<Auslosung?> FindenAbgeschlosseneNachRundeAsync(
        int turnierInstanzId, int turnierRundeId, CancellationToken ct = default)
    {
        var trefferId = await Context.Auslosungen
            .Where(a => a.TurnierInstanzId == turnierInstanzId
                     && a.TurnierRundeId == turnierRundeId
                     && a.Status == AuslosungsStatus.Abgeschlossen)
            .OrderByDescending(a => a.Id)
            .Select(a => (int?)a.Id)
            .FirstOrDefaultAsync(ct);

        return trefferId is null ? null : await FindenNachIdMitDetailsAsync(trefferId.Value, ct);
    }

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
