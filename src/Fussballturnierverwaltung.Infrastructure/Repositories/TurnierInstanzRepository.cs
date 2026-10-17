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
            .Include(ti => ti.Runden)
                .ThenInclude(r => r.TabellenBereiche)
            .Include(ti => ti.StandardRegelwerk)
            .Include(ti => ti.Gastgeber)
            .AsSplitQuery()
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
            .Include(ti => ti.Runden)
                .ThenInclude(r => r.TabellenBereiche)
            .Include(ti => ti.VerbandsQuoten)
            .Include(ti => ti.LandesQuoten)
            .Include(ti => ti.Auslosungen)
                .ThenInclude(a => a.Ergebnisse)
            .AsSplitQuery()
            .FirstOrDefaultAsync(ti => ti.Id == id, ct);

    public async Task<IReadOnlyList<int>> GastgeberIdsAsync(int instanzId, CancellationToken ct = default)
        => await Context.TurnierGastgeber
            .Where(g => g.TurnierInstanzId == instanzId)
            .Select(g => g.MannschaftId)
            .ToListAsync(ct);

    /// <summary>Issue #352: gleicht die Gastgeber per Schlüssel-Diff ab (nur Hinzufügen/Entfernen, kein
    /// Löschen und Neuanlegen unveränderter Einträge).</summary>
    public async Task GastgeberErsetzenAsync(int instanzId, IReadOnlyCollection<int> mannschaftIds, CancellationToken ct = default)
    {
        var soll = mannschaftIds.ToHashSet();
        var alte = await Context.TurnierGastgeber
            .Where(g => g.TurnierInstanzId == instanzId)
            .ToListAsync(ct);

        Context.TurnierGastgeber.RemoveRange(alte.Where(g => !soll.Contains(g.MannschaftId)));
        var vorhanden = alte.Select(g => g.MannschaftId).ToHashSet();
        foreach (var id in soll.Where(id => !vorhanden.Contains(id)))
            Context.TurnierGastgeber.Add(new TurnierGastgeber { TurnierInstanzId = instanzId, MannschaftId = id });

        await Context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TurnierRunde>> RundenErsetzenAsync(int instanzId, IReadOnlyList<TurnierRunde> neueRunden, CancellationToken ct = default)
    {
        var alte = await Context.TurnierRunden
            .Include(r => r.TabellenBereiche)
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
            alt.NeutralerSpielort         = neu.NeutralerSpielort;
            alt.Ueberraschungsfaktor      = neu.Ueberraschungsfaktor;
            alt.KonfigurationJson         = neu.KonfigurationJson;
            alt.EingangUeberUebergangsregeln = neu.EingangUeberUebergangsregeln;
            alt.PaarungFestVorgegeben     = neu.PaarungFestVorgegeben;
            alt.VorgegebeneAuslosungsart  = neu.VorgegebeneAuslosungsart;
            alt.VorgegebeneAuslosungsregeln = [.. neu.VorgegebeneAuslosungsregeln];
            alt.KombinationstabelleJson   = neu.KombinationstabelleJson;
            alt.StandardRanglistenQuelleId = neu.StandardRanglistenQuelleId;
            alt.TitelverteidigerInTopf1   = neu.TitelverteidigerInTopf1;
            TabellenBereicheAbgleichen(alt, neu);
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

    // Issue #420: positionsgleich wie die Runden selbst — bestehende Bereiche werden überschrieben, überzählige
    // entfernt (Kaskade über die Pflicht-Beziehung), fehlende ergänzt.
    private void TabellenBereicheAbgleichen(TurnierRunde alt, TurnierRunde neu)
    {
        var alteBereiche = alt.TabellenBereiche.OrderBy(b => b.PlatzVon).ThenBy(b => b.Id).ToList();
        var neueBereiche = neu.TabellenBereiche.ToList();

        for (int i = 0; i < Math.Min(alteBereiche.Count, neueBereiche.Count); i++)
        {
            alteBereiche[i].PlatzVon    = neueBereiche[i].PlatzVon;
            alteBereiche[i].PlatzBis    = neueBereiche[i].PlatzBis;
            alteBereiche[i].Art         = neueBereiche[i].Art;
            alteBereiche[i].Bezeichnung = neueBereiche[i].Bezeichnung;
        }

        Context.TabellenBereiche.RemoveRange(alteBereiche.Skip(neueBereiche.Count));
        foreach (var bereich in neueBereiche.Skip(alteBereiche.Count))
            alt.TabellenBereiche.Add(bereich);
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
