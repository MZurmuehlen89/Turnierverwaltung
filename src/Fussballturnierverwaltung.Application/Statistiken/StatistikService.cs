using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Statistiken;

public class StatistikService(ISpielRepository spielRepository)
{
    public async Task<DirektvergleichErgebnis?> DirektvergleichAsync(
        int turnierInstanzId, int mannschaftAId, int mannschaftBId, CancellationToken ct = default)
    {
        var spiele = await AbgeschlosseneLadenAsync(turnierInstanzId, ct);
        var teams  = Teams(spiele);

        if (!teams.TryGetValue(mannschaftAId, out var teamA) ||
            !teams.TryGetValue(mannschaftBId, out var teamB))
            return null;

        var direktSpiele = spiele
            .Where(s => (s.HeimMannschaftId == mannschaftAId && s.AuswaertsMannschaftId == mannschaftBId)
                     || (s.HeimMannschaftId == mannschaftBId && s.AuswaertsMannschaftId == mannschaftAId))
            .ToList();

        if (direktSpiele.Count == 0) return null;

        int siegeA = direktSpiele.Count(s => s.Ergebnis!.SiegerMannschaftId == mannschaftAId);
        int siegeB = direktSpiele.Count(s => s.Ergebnis!.SiegerMannschaftId == mannschaftBId);
        int unent  = direktSpiele.Count(s => s.Ergebnis!.SiegerMannschaftId is null);

        int toreA = direktSpiele.Where(s => s.HeimMannschaftId      == mannschaftAId).Sum(s => s.Ergebnis!.HeimToreGesamt)
                  + direktSpiele.Where(s => s.AuswaertsMannschaftId == mannschaftAId).Sum(s => s.Ergebnis!.AuswaertsToreGesamt);
        int toreB = direktSpiele.Where(s => s.HeimMannschaftId      == mannschaftBId).Sum(s => s.Ergebnis!.HeimToreGesamt)
                  + direktSpiele.Where(s => s.AuswaertsMannschaftId == mannschaftBId).Sum(s => s.Ergebnis!.AuswaertsToreGesamt);

        return new DirektvergleichErgebnis(teamA, teamB, siegeA, unent, siegeB, toreA, toreB, direktSpiele);
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────

    private async Task<List<Spiel>> AbgeschlosseneLadenAsync(int turnierInstanzId, CancellationToken ct)
    {
        var alle = await spielRepository.AlleNachInstanzAsync(turnierInstanzId, ct);
        return alle
            .Where(s => s.Status == SpielStatus.Abgeschlossen && s.Ergebnis is not null)
            .ToList();
    }

    private static Dictionary<int, Mannschaft> Teams(List<Spiel> spiele)
        => spiele
            .Where(s => s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue)
            .SelectMany(s => new[]
            {
                (Id: s.HeimMannschaftId!.Value,      Team: s.HeimMannschaft!),
                (Id: s.AuswaertsMannschaftId!.Value, Team: s.AuswaertsMannschaft!)
            })
            .DistinctBy(t => t.Id)
            .ToDictionary(t => t.Id, t => t.Team);
}
