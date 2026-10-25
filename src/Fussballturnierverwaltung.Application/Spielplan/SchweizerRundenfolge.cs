using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;

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

        var auslosung = await auslosungRepository.FindenAbgeschlosseneNachRundeAsync(runde.TurnierInstanzId, turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Für Runde '{runde.Name}' wurde noch keine Auslosung abgeschlossen.");
        var tabelle = await tabellenService.TabelleBerechnenAsync(turnierRundeId, ct: ct);

        var sortierteIds = NachRang(Teilnehmer(auslosung), tabelle.ToDictionary(e => e.Mannschaft.Id, e => e.Rang));
        return await spielplanService.SchweizerRundeGenerierenAsync(turnierRundeId, sortierteIds, ct);
    }

    // Die vollständige Teamliste kommt bewusst aus der abgeschlossenen Auslosung statt aus der Tabelle: ein Team mit
    // Freilos an Spieltag 1 hat noch kein gespieltes Spiel und fehlt deshalb in der aus Spielen aufgebauten Tabelle —
    // ohne diesen Rückgriff würde es nach seinem ersten Freilos nie wieder eingeplant.
    private static IEnumerable<int> Teilnehmer(Auslosung auslosung) =>
        auslosung.Ergebnisse
            .SelectMany(e => new[] { e.HeimMannschaftId, e.AuswaertsMannschaftId })
            .OfType<int>()
            .Distinct();

    // Teams ohne Tabellenplatz (noch kein Spiel) ans Ende
    private static List<int> NachRang(IEnumerable<int> teilnehmer, Dictionary<int, int> rangJeTeam) =>
        [.. teilnehmer.OrderBy(id => rangJeTeam.TryGetValue(id, out var rang) ? rang : int.MaxValue)];
}
