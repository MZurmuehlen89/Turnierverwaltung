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
            .OrderByDescending(ti => ti.SaisonOderJahr)
            .ThenBy(ti => ti.Name)
            .ToListAsync(ct);

    public async Task<TurnierInstanz?> FindenNachIdMitRundenAsync(int id, CancellationToken ct = default)
        => await Context.TurnierInstanzen
            .Include(ti => ti.Runden.OrderBy(r => r.Reihenfolge))
                .ThenInclude(r => r.Regelwerk)
            .Include(ti => ti.StandardRegelwerk)
            .FirstOrDefaultAsync(ti => ti.Id == id, ct);

    public async Task<IReadOnlyList<TurnierInstanz>> AlleNachStatusFindenAsync(TurnierStatus status, CancellationToken ct = default)
        => await Context.TurnierInstanzen
            .Where(ti => ti.Status == status)
            .OrderByDescending(ti => ti.SaisonOderJahr)
            .ToListAsync(ct);

    /// <summary>Issue #294: vollständiger Objektgraph für <see cref="Application.Turniere.TurnierKlonService"/>
    /// — Runden inkl. ausgehender Übergangsregeln sowie Verbands-/Landesquoten (nie Spiele/Auslosungen/SimulationsLaeufe).</summary>
    public async Task<TurnierInstanz?> FindenNachIdMitVollstaendigenDatenAsync(int id, CancellationToken ct = default)
        => await Context.TurnierInstanzen
            .Include(ti => ti.Runden.OrderBy(r => r.Reihenfolge))
                .ThenInclude(r => r.Uebergangsregeln)
            .Include(ti => ti.VerbandsQuoten)
            .Include(ti => ti.LandesQuoten)
            .FirstOrDefaultAsync(ti => ti.Id == id, ct);

    public async Task<IReadOnlyList<TurnierRunde>> RundenErsetzenAsync(int instanzId, IReadOnlyList<TurnierRunde> neueRunden, CancellationToken ct = default)
    {
        var alte = await Context.TurnierRunden
            .Where(r => r.TurnierInstanzId == instanzId)
            .OrderBy(r => r.Reihenfolge)
            .ToListAsync(ct);

        // Update existing rounds in-place to preserve IDs referenced by Auslosungen
        int gemeinsam = Math.Min(alte.Count, neueRunden.Count);
        for (int i = 0; i < gemeinsam; i++)
        {
            var alt = alte[i];
            var neu = neueRunden[i];
            alt.Name                      = neu.Name;
            alt.Reihenfolge               = neu.Reihenfolge;
            alt.Rundenart                 = neu.Rundenart;
            alt.EingangsMannschaftsanzahl = neu.EingangsMannschaftsanzahl;
            alt.AusgangsMannschaftsanzahl = neu.AusgangsMannschaftsanzahl;
            alt.GruppenAnzahl             = neu.GruppenAnzahl;
            alt.RegelwerkId               = neu.RegelwerkId;
            alt.MitRueckspiel             = neu.MitRueckspiel;
            alt.AuswaertstorRegelAktiv    = neu.AuswaertstorRegelAktiv;
            alt.KonfigurationJson         = neu.KonfigurationJson;
            alt.EingangUeberUebergangsregeln = neu.EingangUeberUebergangsregeln;
            alt.PaarungFestVorgegeben     = neu.PaarungFestVorgegeben;
        }

        // Add rounds if the new list is longer
        for (int i = alte.Count; i < neueRunden.Count; i++)
            Context.TurnierRunden.Add(neueRunden[i]);

        // Delete rounds that no longer exist (FK violation here = correct — round has active Auslosung)
        if (alte.Count > neueRunden.Count)
        {
            var entfernte = alte.Skip(neueRunden.Count).ToList();
            var entfernteIds = entfernte.Select(r => r.Id).ToHashSet();

            // Uebergangsregeln, die eine entfernte Runde als Zielrunde referenzieren, muessen explizit
            // mitgeloescht werden: die Quellrunden-Beziehung cascadet bereits ueber die DB-Konfiguration
            // (OnDelete Cascade), die Zielrunden-Beziehung ist bewusst Restrict und wird von EF nicht
            // automatisch mitgezogen. Ohne diesen Cleanup wirft SaveChangesAsync eine "association ...
            // severed but the relationship is required"-Exception fuer jede in diesem DbContext bereits
            // getrackte Uebergangsregel, die noch auf die entfernte Zielrunde zeigt — z.B. weil der
            // "Ausgehende Übergangsregeln"-Bereich (Issue #186) sie beim Betrachten der Quellrunde geladen hat.
            var betroffeneZielregeln = await Context.Uebergangsregeln
                .Where(u => entfernteIds.Contains(u.ZielRundeId))
                .ToListAsync(ct);
            Context.Uebergangsregeln.RemoveRange(betroffeneZielregeln);

            Context.TurnierRunden.RemoveRange(entfernte);
        }

        await Context.SaveChangesAsync(ct);

        // Positionsgleich zu neueRunden zurückgeben, jetzt mit echten Ids (bestehende Runden über
        // alte[i], neu hinzugefügte Runden über neueRunden[i] — EF befüllt deren Id nach SaveChanges).
        var ergebnis = new List<TurnierRunde>(neueRunden.Count);
        for (int i = 0; i < neueRunden.Count; i++)
            ergebnis.Add(i < alte.Count ? alte[i] : neueRunden[i]);
        return ergebnis;
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
