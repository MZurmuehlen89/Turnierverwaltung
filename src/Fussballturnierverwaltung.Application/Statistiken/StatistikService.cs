using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Statistiken;

public class StatistikService(
    ISpielRepository spielRepository,
    ITurnierRundeRepository rundeRepository,
    IRegelwerkRepository regelwerkRepository)
{
    /// <summary>Alle abgeschlossenen Spiele des Turniers aus Sicht beider Mannschaften — die Eingabe für die
    /// Ranglisten in <see cref="StatistikBerechnung"/>.</summary>
    public async Task<IReadOnlyList<MannschaftsSpiel>> MannschaftsSpieleLadenAsync(
        int turnierInstanzId, CancellationToken ct = default)
    {
        var punkteRegelJeRunde = await PunkteRegelJeRundeLadenAsync(turnierInstanzId, ct);
        var spiele = await spielRepository.AlleNachInstanzMitEreignissenAsync(turnierInstanzId, ct);
        return StatistikBerechnung.AusMannschaftsSicht(spiele, punkteRegelJeRunde);
    }

    // AD-026: Rundenregelwerk überschreibt das Standard-Regelwerk der Instanz, fehlt es, gilt dieses. Ohne
    // jedes Regelwerk gilt 3/1/0 — die Statistik soll daran nicht scheitern.
    private async Task<Dictionary<int, PunkteRegel>> PunkteRegelJeRundeLadenAsync(int turnierInstanzId, CancellationToken ct)
    {
        var runden = await rundeRepository.AlleNachInstanzAsync(turnierInstanzId, ct);
        var standardRegelwerkId = await rundeRepository.StandardRegelwerkIdDerInstanzAsync(turnierInstanzId, ct);

        var punkteRegelJeRegelwerk = new Dictionary<int, PunkteRegel?>();
        var punkteRegelJeRunde = new Dictionary<int, PunkteRegel>();
        foreach (var runde in runden)
        {
            PunkteRegel? punkteRegel = null;
            if ((runde.RegelwerkId ?? standardRegelwerkId) is { } regelwerkId
                && !punkteRegelJeRegelwerk.TryGetValue(regelwerkId, out punkteRegel))
            {
                var regelwerk = await regelwerkRepository.FindenNachIdMitAllenRegelnAsync(regelwerkId, ct);
                punkteRegel = regelwerk?.PunkteRegeln.FirstOrDefault();
                punkteRegelJeRegelwerk[regelwerkId] = punkteRegel;
            }
            punkteRegelJeRunde[runde.Id] = punkteRegel ?? _standardPunkteRegel;
        }
        return punkteRegelJeRunde;
    }

    private static readonly PunkteRegel _standardPunkteRegel
        = new() { PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 };

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
