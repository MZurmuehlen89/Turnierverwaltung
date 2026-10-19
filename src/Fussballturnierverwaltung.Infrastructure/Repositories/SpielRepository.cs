using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class SpielRepository(TurnierverwaltungDbContext context)
    : GenericRepository<Spiel>(context), ISpielRepository
{
    public async Task<IReadOnlyList<Spiel>> AlleNachRundeAsync(int turnierRundeId, CancellationToken ct = default)
        => await Context.Spiele
            .Include(s => s.HeimMannschaft)
                .ThenInclude(m => m!.Land)
            .Include(s => s.AuswaertsMannschaft)
                .ThenInclude(m => m!.Land)
            .Include(s => s.Ergebnis)
            .Where(s => s.TurnierRundeId == turnierRundeId)
            .OrderBy(s => s.Spieltag)
            .ThenBy(s => s.Spielnummer)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Spiel>> AlleNachInstanzAsync(int turnierInstanzId, CancellationToken ct = default)
        => await Context.Spiele
            .Include(s => s.HeimMannschaft)
                .ThenInclude(m => m!.Land)
            .Include(s => s.AuswaertsMannschaft)
                .ThenInclude(m => m!.Land)
            .Include(s => s.Ergebnis)
            .Include(s => s.TurnierRunde)
            .Where(s => s.TurnierInstanzId == turnierInstanzId)
            .OrderBy(s => s.TurnierRunde.Reihenfolge)
            .ThenBy(s => s.Spieltag)
            .ThenBy(s => s.Spielnummer)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Spiel>> AlleNachMannschaftAsync(
        int turnierInstanzId, int mannschaftId, CancellationToken ct = default)
        => await Context.Spiele
            .Include(s => s.HeimMannschaft)
                .ThenInclude(m => m!.Land)
            .Include(s => s.AuswaertsMannschaft)
                .ThenInclude(m => m!.Land)
            .Include(s => s.Ergebnis)
            .Include(s => s.TurnierRunde)
            .Where(s => s.TurnierInstanzId == turnierInstanzId
                        && (s.HeimMannschaftId == mannschaftId || s.AuswaertsMannschaftId == mannschaftId))
            .OrderBy(s => s.TurnierRunde.Reihenfolge)
            .ThenBy(s => s.Spieltag)
            .ThenBy(s => s.Spielnummer)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Mannschaft>> MannschaftenNachInstanzAsync(
        int turnierInstanzId, CancellationToken ct = default)
        => await Context.Mannschaften
            .Include(m => m.Land)
            .Where(m => Context.Spiele.Any(s => s.TurnierInstanzId == turnierInstanzId
                                                && (s.HeimMannschaftId == m.Id || s.AuswaertsMannschaftId == m.Id)))
            .OrderBy(m => m.Name)
            .ToListAsync(ct);

    public async Task<int> HoechsteSpielNummerAsync(int turnierInstanzId, CancellationToken ct = default)
    {
        var max = await Context.Spiele
            .Where(s => s.TurnierInstanzId == turnierInstanzId)
            .MaxAsync(s => (int?)s.Spielnummer, ct);
        return max ?? 0;
    }

    public async Task<int> NaechsteSpielNummerAsync(int turnierInstanzId, int turnierRundeId, int reihenfolge, CancellationToken ct = default)
    {
        var vorgaenger = await Context.Spiele
            .Where(s => s.TurnierInstanzId == turnierInstanzId
                     && s.TurnierRunde.Reihenfolge < reihenfolge)
            .CountAsync(ct);

        var dieseRunde = await Context.Spiele
            .CountAsync(s => s.TurnierRundeId == turnierRundeId, ct);

        return vorgaenger + dieseRunde + 1;
    }

    public async Task HinzufuegenMehrfachAsync(IEnumerable<Spiel> spiele, CancellationToken ct = default)
    {
        await Context.Spiele.AddRangeAsync(spiele, ct);
        await Context.SaveChangesAsync(ct);
    }

    public async Task AlleNachRundeLoeschenAsync(int turnierRundeId, CancellationToken ct = default)
    {
        var spielIds = (await Context.Spiele
            .Where(s => s.TurnierRundeId == turnierRundeId)
            .Select(s => s.Id)
            .ToListAsync(ct)).ToHashSet();
        if (spielIds.Count == 0) return;

        // Issue #320-Folgefund: Folgerunden-Spiele, die per Übergangsregel über HeimQuellSpielId/
        // AuswaertsQuellSpielId (Self-Referencing FK, NoAction) auf eines dieser Spiele verweisen, müssen
        // die Referenz zuerst verlieren — sonst schlägt das folgende Löschen mit einer SQL-FK-Verletzung
        // fehl (verifiziert an einem Alt-Turnier mit bereits generiertem, dieselben Spiele referenzierendem
        // Turnierbaum/Folgespielen; TurnierBaumKnoten.SpielId wird bereits separat von
        // SpielplanService.RundenSpielplanOhneKaskadeLoeschenAsync vor diesem Aufruf genullt).
        var abhaengigeSpiele = await Context.Spiele
            .Where(s => (s.HeimQuellSpielId != null && spielIds.Contains(s.HeimQuellSpielId.Value)) ||
                        (s.AuswaertsQuellSpielId != null && spielIds.Contains(s.AuswaertsQuellSpielId.Value)))
            .ToListAsync(ct);

        foreach (var abhaengiges in abhaengigeSpiele)
        {
            if (abhaengiges.HeimQuellSpielId.HasValue && spielIds.Contains(abhaengiges.HeimQuellSpielId.Value))
                abhaengiges.HeimQuellSpielId = null;
            if (abhaengiges.AuswaertsQuellSpielId.HasValue && spielIds.Contains(abhaengiges.AuswaertsQuellSpielId.Value))
                abhaengiges.AuswaertsQuellSpielId = null;
        }

        if (abhaengigeSpiele.Count > 0)
            await Context.SaveChangesAsync(ct);

        if (!Context.Database.IsRelational())
        {
            // Der InMemory-Provider der Tests kennt kein ExecuteDelete.
            Context.Spiele.RemoveRange(await Context.Spiele.Where(s => s.TurnierRundeId == turnierRundeId).ToListAsync(ct));
            await Context.SaveChangesAsync(ct);
            return;
        }

        // Eine Anweisung für die ganze Runde (#414): der SQL Server kompiliert jedes einzelne
        // "DELETE … WHERE Id = @p" auf dieser Tabelle bei jeder Ausführung neu (gemessen 6–8 ms je Spiel bei
        // unter 1 ms Ausführung, 144 Spiele der Ligaphase über 1 s). Ergebnisse und Ereignisse löscht die
        // Datenbank per Kaskade mit. ExecuteDelete läuft am Change-Tracker vorbei — die gelöschten Zeilen
        // müssen deshalb ausdrücklich aus dem Kontext (Stale-Bug, siehe #103).
        await Context.Spiele.Where(s => s.TurnierRundeId == turnierRundeId).ExecuteDeleteAsync(ct);
        AusKontextNehmen(spielIds);
    }

    /// <summary>Nimmt in der Datenbank bereits gelöschte Spiele samt Ergebnissen und Ereignissen aus dem
    /// Kontext. Ein erneutes Laden genügt dafür nicht: eine Abfrage liefert die gelöschten Zeilen nur nicht
    /// mehr, die verfolgten Objekte blieben aber im Kontext — als Treffer für <c>FindAsync</c>, in den
    /// Spiele-Sammlungen von Runde und Turnier und als Fehlerquelle beim nächsten Speichern.</summary>
    public void AusKontextNehmen(IReadOnlySet<int> spielIds)
    {
        // Entries() löst sonst je Aufruf eine Änderungssuche über den ganzen Kontext aus.
        var tracker = Context.ChangeTracker;
        bool automatisch = tracker.AutoDetectChangesEnabled;
        tracker.AutoDetectChangesEnabled = false;
        try
        {
            foreach (var eintrag in tracker.Entries<SpielErgebnis>().Where(e => spielIds.Contains(e.Entity.SpielId)).ToList())
                eintrag.State = EntityState.Detached;
            foreach (var eintrag in tracker.Entries<SpielEreignis>().Where(e => spielIds.Contains(e.Entity.SpielId)).ToList())
                eintrag.State = EntityState.Detached;

            foreach (var eintrag in tracker.Entries<Spiel>().Where(e => spielIds.Contains(e.Entity.Id)).ToList())
            {
                // Beim Herausnehmen räumt EF die Sammlungen der verbleibenden Objekte nicht selbst auf.
                var spiel = eintrag.Entity;
                spiel.TurnierRunde?.Spiele.Remove(spiel);
                spiel.TurnierInstanz?.Spiele.Remove(spiel);
                eintrag.State = EntityState.Detached;
            }
        }
        finally { tracker.AutoDetectChangesEnabled = automatisch; }
    }

    public async Task<Spiel?> FindenNachIdMitErgebnisAsync(int id, CancellationToken ct = default)
        => await Context.Spiele
            .Include(s => s.HeimMannschaft)
                .ThenInclude(m => m!.Land)
            .Include(s => s.AuswaertsMannschaft)
                .ThenInclude(m => m!.Land)
            .Include(s => s.Ergebnis)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task ErgebnisSetzenAsync(int spielId, SpielErgebnis ergebnis, CancellationToken ct = default)
    {
        var spiel = await Context.Spiele
            .Include(s => s.Ergebnis)
            .Include(s => s.Ereignisse)
            .FirstOrDefaultAsync(s => s.Id == spielId, ct);
        if (spiel is null) return;

        if (spiel.Ergebnis is not null)
            Context.SpielErgebnisse.Remove(spiel.Ergebnis);

        if (spiel.Ereignisse.Count > 0)
            Context.SpielEreignisse.RemoveRange(spiel.Ereignisse);

        ergebnis.SpielId = spielId;
        await Context.SpielErgebnisse.AddAsync(ergebnis, ct);
        spiel.Status = SpielStatus.Abgeschlossen;
        await Context.SaveChangesAsync(ct);
    }

    public Task ErgebnisLoeschenAsync(int spielId, CancellationToken ct = default)
        => ErgebnisseLoeschenAsync([spielId], ct);

    public async Task ErgebnisseLoeschenAsync(IReadOnlyCollection<int> spielIds, CancellationToken ct = default)
    {
        if (spielIds.Count == 0) return;

        var spiele = await Context.Spiele
            .Include(s => s.Ergebnis)
            .Include(s => s.Ereignisse)
            .Where(s => spielIds.Contains(s.Id) && s.Ergebnis != null)
            .ToListAsync(ct);
        if (spiele.Count == 0) return;

        foreach (var spiel in spiele)
        {
            Context.SpielErgebnisse.Remove(spiel.Ergebnis!);
            if (spiel.Ereignisse.Count > 0)
                Context.SpielEreignisse.RemoveRange(spiel.Ereignisse);
            spiel.Status = SpielStatus.Geplant;
        }
        await Context.SaveChangesAsync(ct);
    }

    public async Task<Spiel?> FindenNachIdFuerSimulationAsync(int id, CancellationToken ct = default)
    {
        var spiel = await Context.Spiele
            .Include(s => s.HeimMannschaft)
                .ThenInclude(m => m!.Land)
            .Include(s => s.AuswaertsMannschaft)
                .ThenInclude(m => m!.Land)
            .Include(s => s.Ergebnis)
            .Include(s => s.TurnierRunde)
                .ThenInclude(r => r.Regelwerk)
                    .ThenInclude(rw => rw!.Entscheidungsverfahren.OrderBy(e => e.Reihenfolge))
            .Include(s => s.Hinspiel)
                .ThenInclude(h => h!.Ergebnis)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        // AD-026: Runde ohne eigenes Regelwerk fällt auf das Standard-Regelwerk der Instanz zurück — nur dann
        // nachladen (ein Include wäre ein Inner Join und würde Spiele ohne geladene Instanz verschlucken).
        if (spiel?.TurnierRunde is { RegelwerkId: null } runde)
            await Context.Entry(runde).Reference(r => r.TurnierInstanz).Query()
                .Include(i => i.StandardRegelwerk)
                    .ThenInclude(rw => rw!.Entscheidungsverfahren)
                .LoadAsync(ct);

        return spiel;
    }

    public async Task EreignisseHinzufuegenAsync(
        int spielId, IEnumerable<SpielEreignis> ereignisse, CancellationToken ct = default)
    {
        foreach (var e in ereignisse)
            e.SpielId = spielId;
        await Context.SpielEreignisse.AddRangeAsync(ereignisse, ct);
        await Context.SaveChangesAsync(ct);
    }

    public async Task<Spiel?> FindenNachIdMitRundeUndHinspielAsync(int id, CancellationToken ct = default)
        => await Context.Spiele
            .Include(s => s.HeimMannschaft)
                .ThenInclude(m => m!.Land)
            .Include(s => s.AuswaertsMannschaft)
                .ThenInclude(m => m!.Land)
            .Include(s => s.Ergebnis)
            .Include(s => s.TurnierRunde)
            .Include(s => s.Hinspiel)
                .ThenInclude(h => h!.Ergebnis)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Spiel>> AlleNachRundeMitEreignissenAsync(int turnierRundeId, CancellationToken ct = default)
        => await Context.Spiele
            .Include(s => s.HeimMannschaft)
                .ThenInclude(m => m!.Land)
            .Include(s => s.AuswaertsMannschaft)
                .ThenInclude(m => m!.Land)
            .Include(s => s.Ergebnis)
            .Include(s => s.Ereignisse)
            .Where(s => s.TurnierRundeId == turnierRundeId)
            .OrderBy(s => s.Spieltag)
            .ThenBy(s => s.Spielnummer)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Spiel>> AlleNachInstanzMitEreignissenAsync(int turnierInstanzId, CancellationToken ct = default)
        => await Context.Spiele
            .Include(s => s.TurnierRunde)
            .Include(s => s.HeimMannschaft)
                .ThenInclude(m => m!.Land)
                    .ThenInclude(l => l.Verband)
            .Include(s => s.AuswaertsMannschaft)
                .ThenInclude(m => m!.Land)
                    .ThenInclude(l => l.Verband)
            .Include(s => s.Ergebnis)
            .Include(s => s.Ereignisse)
            .Where(s => s.TurnierInstanzId == turnierInstanzId)
            .OrderBy(s => s.TurnierRunde.Reihenfolge)
            .ThenBy(s => s.Spieltag)
            .ThenBy(s => s.Spielnummer)
            .AsSplitQuery()
            .ToListAsync(ct);

    public async Task<Spiel?> FindenNachIdMitEreignissenAsync(int id, CancellationToken ct = default)
        => await Context.Spiele
            // Issue #341: Turnier/Runde für die Kontextzeile, Verband für den Untertitel im Spielkopf
            .Include(s => s.TurnierInstanz)
            .Include(s => s.TurnierRunde)
            .Include(s => s.HeimMannschaft)
                .ThenInclude(m => m!.Land)
                    .ThenInclude(l => l.Verband)
            .Include(s => s.AuswaertsMannschaft)
                .ThenInclude(m => m!.Land)
                    .ThenInclude(l => l.Verband)
            .Include(s => s.Ergebnis)
            .Include(s => s.Ereignisse.OrderBy(e => e.Minute))
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task PlatzhalterKorrigierenAsync(IReadOnlyCollection<PlatzhalterKorrektur> korrekturen, CancellationToken ct = default)
    {
        if (korrekturen.Count == 0) return;

        // Kein ExecuteUpdateAsync: das umgeht den Change-Tracker, wodurch bereits im selben
        // (scope-langlebigen) DbContext geladene Spiel-Instanzen veraltet bleiben und bei
        // erneutem Laden per Identity-Resolution zurueckgegeben werden (sichtbar erst nach
        // Neustart der App).
        var spielIds = korrekturen.Select(k => k.SpielId).ToHashSet();
        var spiele = await Context.Spiele
            .Where(s => spielIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        foreach (var korrektur in korrekturen)
        {
            if (!spiele.TryGetValue(korrektur.SpielId, out var spiel)) continue;

            spiel.HeimMannschaftId         = korrektur.HeimMannschaftId;
            spiel.HeimPlatzhalterName      = korrektur.HeimMannschaftId is null ? korrektur.HeimPlatzhalterName : null;
            spiel.AuswaertsMannschaftId    = korrektur.AuswaertsMannschaftId;
            spiel.AuswaertsPlatzhalterName = korrektur.AuswaertsMannschaftId is null ? korrektur.AuswaertsPlatzhalterName : null;
        }
        await Context.SaveChangesAsync(ct);
    }

    public async Task AktualisierenMehrfachAsync(IReadOnlyCollection<Spiel> spiele, CancellationToken ct = default)
    {
        if (spiele.Count == 0) return;

        // Bereits verfolgte Spiele nicht per Update() anfassen: das schriebe alle Spalten statt nur der geänderten
        // (gemessen: 16 Spiele 137 ms statt rund 40 ms).
        foreach (var spiel in spiele)
            if (Context.Entry(spiel).State == EntityState.Detached)
                Context.Spiele.Update(spiel);
        await Context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<int>> TurnierInstanzIdsAsync(IReadOnlyList<int> spielIds, CancellationToken ct = default)
        => await Context.Spiele
            .Where(s => spielIds.Contains(s.Id))
            .Select(s => s.TurnierRunde.TurnierInstanzId)
            .Distinct()
            .ToListAsync(ct);
}
