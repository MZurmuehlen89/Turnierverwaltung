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
            .Include(s => s.AuswaertsMannschaft)
            .Include(s => s.Ergebnis)
            .Where(s => s.TurnierRundeId == turnierRundeId)
            .OrderBy(s => s.Spieltag)
            .ThenBy(s => s.Spielnummer)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Spiel>> AlleNachInstanzAsync(int turnierInstanzId, CancellationToken ct = default)
        => await Context.Spiele
            .Include(s => s.HeimMannschaft)
            .Include(s => s.AuswaertsMannschaft)
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

    public async Task HinzufuegenMehrfachAsync(IEnumerable<Spiel> spiele, CancellationToken ct = default)
    {
        await Context.Spiele.AddRangeAsync(spiele, ct);
        await Context.SaveChangesAsync(ct);
    }

    public async Task AlleNachRundeLoeschenAsync(int turnierRundeId, CancellationToken ct = default)
        => await Context.Spiele
            .Where(s => s.TurnierRundeId == turnierRundeId)
            .ExecuteDeleteAsync(ct);

    public async Task<Spiel?> FindenNachIdMitErgebnisAsync(int id, CancellationToken ct = default)
        => await Context.Spiele
            .Include(s => s.HeimMannschaft)
            .Include(s => s.AuswaertsMannschaft)
            .Include(s => s.Ergebnis)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task ErgebnisSetzenAsync(int spielId, SpielErgebnis ergebnis, CancellationToken ct = default)
    {
        var spiel = await Context.Spiele
            .Include(s => s.Ergebnis)
            .FirstOrDefaultAsync(s => s.Id == spielId, ct);
        if (spiel is null) return;

        if (spiel.Ergebnis is not null)
            Context.SpielErgebnisse.Remove(spiel.Ergebnis);

        ergebnis.SpielId = spielId;
        await Context.SpielErgebnisse.AddAsync(ergebnis, ct);
        spiel.Status = SpielStatus.Abgeschlossen;
        await Context.SaveChangesAsync(ct);
    }

    public async Task ErgebnisLoeschenAsync(int spielId, CancellationToken ct = default)
    {
        var spiel = await Context.Spiele
            .Include(s => s.Ergebnis)
            .FirstOrDefaultAsync(s => s.Id == spielId, ct);
        if (spiel?.Ergebnis is null) return;

        Context.SpielErgebnisse.Remove(spiel.Ergebnis);
        spiel.Status = SpielStatus.Geplant;
        await Context.SaveChangesAsync(ct);
    }

    public async Task<Spiel?> FindenNachIdFuerSimulationAsync(int id, CancellationToken ct = default)
        => await Context.Spiele
            .Include(s => s.HeimMannschaft)
            .Include(s => s.AuswaertsMannschaft)
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
}
