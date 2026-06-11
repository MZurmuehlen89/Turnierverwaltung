using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Statistiken;

public class StatistikService(ISpielRepository spielRepository)
{
    public async Task<IReadOnlyList<TorStatistikEintrag>> TorStatistikAsync(
        int turnierInstanzId, CancellationToken ct = default)
    {
        var spiele = await AbgeschlosseneLadenAsync(turnierInstanzId, ct);
        return [.. Teams(spiele).Values
            .Select(team =>
            {
                int erzielt  = spiele.Where(s => s.HeimMannschaftId      == team.Id).Sum(s => s.Ergebnis!.HeimToreGesamt)
                             + spiele.Where(s => s.AuswaertsMannschaftId == team.Id).Sum(s => s.Ergebnis!.AuswaertsToreGesamt);
                int kassiert = spiele.Where(s => s.HeimMannschaftId      == team.Id).Sum(s => s.Ergebnis!.AuswaertsToreGesamt)
                             + spiele.Where(s => s.AuswaertsMannschaftId == team.Id).Sum(s => s.Ergebnis!.HeimToreGesamt);
                return new TorStatistikEintrag(team, erzielt, kassiert);
            })
            .OrderByDescending(e => e.ToreErzielt)
            .ThenByDescending(e => e.Tordifferenz)];
    }

    public async Task<IReadOnlyList<FormEintrag>> FormtabelleAsync(
        int turnierInstanzId, int letzte = 5, CancellationToken ct = default)
    {
        var spiele = await AbgeschlosseneLadenAsync(turnierInstanzId, ct);
        return [.. Teams(spiele).Values
            .Select(team =>
            {
                var ergebnisse = spiele
                    .Where(s => s.HeimMannschaftId == team.Id || s.AuswaertsMannschaftId == team.Id)
                    .TakeLast(letzte)
                    .Select(s =>
                    {
                        int? sieger = s.Ergebnis!.SiegerMannschaftId;
                        if (sieger == team.Id) return 'S';
                        if (sieger is null)    return 'U';
                        return 'N';
                    })
                    .ToList();
                int punkte = ergebnisse.Sum(c => c switch { 'S' => 3, 'U' => 1, _ => 0 });
                return new FormEintrag(team, ergebnisse, punkte);
            })
            .OrderByDescending(e => e.Punkte)
            .ThenByDescending(e => e.LetzteErgebnisse.Count(c => c == 'S'))];
    }

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

    public async Task<IReadOnlyList<HeimAuswaertsEintrag>> HeimAuswaertsStatistikAsync(
        int turnierInstanzId, CancellationToken ct = default)
    {
        var spiele = await AbgeschlosseneLadenAsync(turnierInstanzId, ct);
        return [.. Teams(spiele).Values
            .Select(team =>
            {
                var heim = spiele.Where(s => s.HeimMannschaftId      == team.Id).ToList();
                var ausw = spiele.Where(s => s.AuswaertsMannschaftId == team.Id).ToList();
                return new HeimAuswaertsEintrag(
                    team,
                    heim.Count,
                    heim.Count(s => s.Ergebnis!.SiegerMannschaftId == team.Id),
                    heim.Count(s => s.Ergebnis!.SiegerMannschaftId is null),
                    heim.Count(s => s.Ergebnis!.SiegerMannschaftId is not null && s.Ergebnis!.SiegerMannschaftId != team.Id),
                    heim.Sum(s => s.Ergebnis!.HeimToreGesamt),
                    heim.Sum(s => s.Ergebnis!.AuswaertsToreGesamt),
                    ausw.Count,
                    ausw.Count(s => s.Ergebnis!.SiegerMannschaftId == team.Id),
                    ausw.Count(s => s.Ergebnis!.SiegerMannschaftId is null),
                    ausw.Count(s => s.Ergebnis!.SiegerMannschaftId is not null && s.Ergebnis!.SiegerMannschaftId != team.Id),
                    ausw.Sum(s => s.Ergebnis!.AuswaertsToreGesamt),
                    ausw.Sum(s => s.Ergebnis!.HeimToreGesamt));
            })
            .OrderByDescending(e => e.HeimSiegquote + e.AuswaertsSiegquote)];
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
            .SelectMany(s => new[]
            {
                (Id: s.HeimMannschaftId,      Team: s.HeimMannschaft),
                (Id: s.AuswaertsMannschaftId, Team: s.AuswaertsMannschaft)
            })
            .DistinctBy(t => t.Id)
            .ToDictionary(t => t.Id, t => t.Team);
}
