using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Tabelle;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Schweizer System ab Spieltag 2: die nächste Runde aus dem aktuellen Tabellenstand.</summary>
public class SchweizerRundenfolge(
    IAuslosungRepository auslosungRepository,
    TabellenService tabellenService,
    SpielplanService spielplanService)
{
    /// <summary>Generiert die nächste Schweizer Runde (Spieltag) direkt aus dem aktuellen Tabellenstand — die
    /// GUI-Aufrufstelle für eine bereits laufende Schweizer-Rundenfolge ab Spieltag 2 (Issue #329 Teil E;
    /// <see cref="SpielplanService.SchweizerRundeGenerierenAsync"/> bleibt die davon genutzte, unveränderte Kernlogik). Die
    /// vollständige Teamliste kommt bewusst aus der abgeschlossenen Auslosung statt ausschließlich aus
    /// <see cref="TabellenService"/>: ein Team mit Freilos in Spieltag 1 hat zu diesem Zeitpunkt noch kein
    /// gespieltes Spiel und fehlt daher in der aus gespielten Spielen aufgebauten Tabelle — ohne diesen
    /// Rückgriff würde es nach seinem ersten Freilos nie wieder eingeplant.</summary>
    public async Task<SpielplanErgebnis> SchweizerNaechsteRundeGenerierenAsync(int turnierRundeId, CancellationToken ct = default)
    {
        var runde = await spielplanService.RundeLadenAsync(turnierRundeId, ct);

        var auslosung = await auslosungRepository.FindenAbgeschlosseneNachRundeAsync(runde.TurnierInstanzId, turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Für Runde '{runde.Name}' wurde noch keine Auslosung abgeschlossen.");

        var rosterIds = auslosung.Ergebnisse
            .SelectMany(e => new[] { e.HeimMannschaftId, e.AuswaertsMannschaftId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var tabelle = await tabellenService.TabelleBerechnenAsync(turnierRundeId, ct: ct);
        var rangNachTeamId = tabelle.ToDictionary(e => e.Mannschaft.Id, e => e.Rang);

        var sortierteIds = rosterIds
            .OrderBy(id => rangNachTeamId.TryGetValue(id, out var rang) ? rang : int.MaxValue)
            .ToList();

        return await spielplanService.SchweizerRundeGenerierenAsync(turnierRundeId, sortierteIds, ct);
    }
}
