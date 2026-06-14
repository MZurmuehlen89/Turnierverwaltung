using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class TurnierInstanzRepository(TurnierverwaltungDbContext context)
    : GenericRepository<TurnierInstanz>(context), ITurnierInstanzRepository
{
    public async Task<IReadOnlyList<TurnierInstanz>> AlleFindenMitVorlageAsync(CancellationToken ct = default)
        => await Context.TurnierInstanzen
            .Include(ti => ti.TurnierVorlage)
            .OrderByDescending(ti => ti.SaisonOderJahr)
            .ThenBy(ti => ti.Name)
            .ToListAsync(ct);

    public async Task<TurnierInstanz?> FindenNachIdMitRundenAsync(int id, CancellationToken ct = default)
        => await Context.TurnierInstanzen
            .Include(ti => ti.TurnierVorlage)
                .ThenInclude(v => v!.VorlagenRunden.OrderBy(r => r.Reihenfolge))
                    .ThenInclude(r => r.Regelwerk)
            .Include(ti => ti.Runden.OrderBy(r => r.Reihenfolge))
                .ThenInclude(r => r.Regelwerk)
            .FirstOrDefaultAsync(ti => ti.Id == id, ct);

    public async Task<IReadOnlyList<TurnierInstanz>> AlleNachStatusFindenAsync(TurnierStatus status, CancellationToken ct = default)
        => await Context.TurnierInstanzen
            .Include(ti => ti.TurnierVorlage)
            .Where(ti => ti.Status == status)
            .OrderByDescending(ti => ti.SaisonOderJahr)
            .ToListAsync(ct);

    public async Task RundenErsetzenAsync(int instanzId, IReadOnlyList<TurnierRunde> neueRunden, CancellationToken ct = default)
    {
        var alte = await Context.TurnierRunden
            .Where(r => r.TurnierInstanzId == instanzId)
            .ToListAsync(ct);
        Context.TurnierRunden.RemoveRange(alte);
        await Context.SaveChangesAsync(ct);

        if (neueRunden.Count > 0)
        {
            Context.TurnierRunden.AddRange(neueRunden);
            await Context.SaveChangesAsync(ct);
        }
    }

    // Löscht in der richtigen Reihenfolge:
    //   1. TurnierBaum (zirkuläre Knoten-Refs → Knoten → Baum)
    //   2. Spiele (non-nullable TurnierInstanzId mit NoAction; SQL Server erlaubt keinen
    //      zweiten Cascade-Pfad neben TurnierInstanz→TurnierRunde→Spiel)
    //   3. TurnierInstanz (cascadet TurnierRunden)
    public async Task LoeschenMitSpielen(int id, CancellationToken ct = default)
    {
        var baum = await Context.TurnierBaeume
            .Include(b => b.Knoten)
            .FirstOrDefaultAsync(b => b.TurnierInstanzId == id, ct);

        if (baum is not null)
        {
            baum.WurzelKnotenId = null;
            foreach (var k in baum.Knoten)
            {
                k.ElternKnotenId      = null;
                k.LinkerKindKnotenId  = null;
                k.RechterKindKnotenId = null;
            }
            await Context.SaveChangesAsync(ct);
            Context.TurnierBaumKnotenMenge.RemoveRange(baum.Knoten);
            await Context.SaveChangesAsync(ct);
            Context.TurnierBaeume.Remove(baum);
            await Context.SaveChangesAsync(ct);
        }

        await Context.Spiele
            .Where(s => s.TurnierInstanzId == id)
            .ExecuteDeleteAsync(ct);

        var instanz = await Context.TurnierInstanzen.FindAsync([id], ct);
        if (instanz is null) return;
        Context.TurnierInstanzen.Remove(instanz);
        await Context.SaveChangesAsync(ct);
    }
}
