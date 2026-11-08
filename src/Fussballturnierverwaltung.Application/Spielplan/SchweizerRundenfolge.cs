using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Tabelle;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Schweizer System ab Spieltag 2 (Issue #329 Teil E): die nächste Runde aus dem aktuellen Tabellenstand —
/// die GUI-Aufrufstelle für eine laufende Rundenfolge. Spieltag 1 entsteht aus der Auslosung
/// (<see cref="SpielplanService.GenerierenAusAuslosungAsync"/>); beide nutzen
/// <see cref="SpielplanService.SchweizerRundeGenerierenAsync"/>.</summary>
public class SchweizerRundenfolge(
    IAuslosungRepository auslosungRepository,
    TabellenService tabellenService,
    SpielplanService spielplanService)
{
    public async Task<SpielplanErgebnis> NaechsteRundeGenerierenAsync(int turnierRundeId, CancellationToken ct = default)
    {
        var runde = await spielplanService.RundeLadenAsync(turnierRundeId, ct);

        // Die Auslosung ist Voraussetzung: aus ihr kennt die Tabelle auch ein Team, das an Spieltag 1 ein Freilos
        // hatte und deshalb noch in keinem Spiel steht (#608) — ohne es würde es nie wieder eingeplant.
        _ = await auslosungRepository.AusgeloesteMannschaftenAsync(runde.TurnierInstanzId, turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Für Runde '{runde.Name}' wurde noch keine Auslosung abgeschlossen.");
        var tabelle = await tabellenService.TabelleBerechnenAsync(turnierRundeId, ct: ct);

        List<int> sortierteIds = [.. tabelle.Select(e => e.Mannschaft.Id)]; // nach Rang sortiert
        return await spielplanService.SchweizerRundeGenerierenAsync(runde, sortierteIds, ct);
    }
}
