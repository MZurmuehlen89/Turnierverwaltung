using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
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
        // Bewusst kein ExecuteDeleteAsync (umgeht den Change-Tracker) - falls betroffene Spiele im selben
        // Scope bereits geladen/getrackt sind, würden sie sonst als "noch vorhanden" im Speicher bleiben
        // (Stale-Bug, siehe #103).
        var spiele = await Context.Spiele
            .Where(s => s.TurnierRundeId == turnierRundeId)
            .ToListAsync(ct);

        Context.Spiele.RemoveRange(spiele);
        await Context.SaveChangesAsync(ct);
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

    public async Task ErgebnisLoeschenAsync(int spielId, CancellationToken ct = default)
    {
        var spiel = await Context.Spiele
            .Include(s => s.Ergebnis)
            .Include(s => s.Ereignisse)
            .FirstOrDefaultAsync(s => s.Id == spielId, ct);
        if (spiel?.Ergebnis is null) return;

        Context.SpielErgebnisse.Remove(spiel.Ergebnis);
        if (spiel.Ereignisse.Count > 0)
            Context.SpielEreignisse.RemoveRange(spiel.Ereignisse);
        spiel.Status = SpielStatus.Geplant;
        await Context.SaveChangesAsync(ct);
    }

    public async Task<Spiel?> FindenNachIdFuerSimulationAsync(int id, CancellationToken ct = default)
        => await Context.Spiele
            .Include(s => s.HeimMannschaft)
                .ThenInclude(m => m!.Land)
            .Include(s => s.AuswaertsMannschaft)
                .ThenInclude(m => m!.Land)
            .Include(s => s.Ergebnis)
            .Include(s => s.TurnierRunde)
                .ThenInclude(r => r.Regelwerk)
                    .ThenInclude(rw => rw.Entscheidungsverfahren.OrderBy(e => e.Reihenfolge))
            .FirstOrDefaultAsync(s => s.Id == id, ct);

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

    public async Task<Spiel?> FindenNachIdMitEreignissenAsync(int id, CancellationToken ct = default)
        => await Context.Spiele
            .Include(s => s.HeimMannschaft)
                .ThenInclude(m => m!.Land)
            .Include(s => s.AuswaertsMannschaft)
                .ThenInclude(m => m!.Land)
            .Include(s => s.Ergebnis)
            .Include(s => s.Ereignisse.OrderBy(e => e.Minute))
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task PlatzhalterKorrigierenAsync(
        int spielId,
        int? heimMannschaftId, string? heimPlatzhalterName,
        int? auswaertsMannschaftId, string? auswaertsPlatzhalterName,
        CancellationToken ct = default)
    {
        // Kein ExecuteUpdateAsync: das umgeht den Change-Tracker, wodurch bereits im selben
        // (scope-langlebigen) DbContext geladene Spiel-Instanzen veraltet bleiben und bei
        // erneutem Laden per Identity-Resolution zurueckgegeben werden (sichtbar erst nach
        // Neustart der App).
        var spiel = await Context.Spiele.FirstOrDefaultAsync(s => s.Id == spielId, ct);
        if (spiel is null) return;

        spiel.HeimMannschaftId         = heimMannschaftId;
        spiel.HeimPlatzhalterName      = heimMannschaftId is null ? heimPlatzhalterName : null;
        spiel.AuswaertsMannschaftId    = auswaertsMannschaftId;
        spiel.AuswaertsPlatzhalterName = auswaertsMannschaftId is null ? auswaertsPlatzhalterName : null;
        await Context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<int>> TurnierInstanzIdsAsync(IReadOnlyList<int> spielIds, CancellationToken ct = default)
        => await Context.Spiele
            .Where(s => spielIds.Contains(s.Id))
            .Select(s => s.TurnierRunde.TurnierInstanzId)
            .Distinct()
            .ToListAsync(ct);
}
