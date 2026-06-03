using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;

namespace Fussballturnierverwaltung.Application.Validierung;

public class RankingRegelSetValidator
{
    public ValidationErgebnis Validieren(RankingRegelSet regelSet)
    {
        var fehler = new List<string>();

        if (string.IsNullOrWhiteSpace(regelSet.Name))
            fehler.Add("Ranking-Regelset muss einen Namen haben.");

        if (!regelSet.Eintraege.Any())
            fehler.Add($"Ranking-Regelset '{regelSet.Name}' muss mindestens eine Regel enthalten.");

        // Keine doppelten Regelarten
        var doppelteArten = regelSet.Eintraege
            .GroupBy(e => e.RankingRegel?.Regelart)
            .Where(g => g.Count() > 1 && g.Key.HasValue)
            .Select(g => g.Key!.Value.ToString());
        foreach (var art in doppelteArten)
            fehler.Add($"Ranking-Regelart '{art}' ist mehrfach im Regelset '{regelSet.Name}' vorhanden.");

        // Keine doppelten Reihenfolgen
        var doppelteReihenfolgen = regelSet.Eintraege
            .GroupBy(e => e.Reihenfolge)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key.ToString());
        foreach (var r in doppelteReihenfolgen)
            fehler.Add($"Reihenfolge {r} ist mehrfach im Regelset '{regelSet.Name}' vorhanden.");

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }
}
