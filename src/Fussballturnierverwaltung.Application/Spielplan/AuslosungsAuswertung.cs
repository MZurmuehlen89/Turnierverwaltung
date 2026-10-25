using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Liest aus den Ergebnissen einer abgeschlossenen Auslosung, was die Spielplan-Generierung je Rundenart
/// braucht — reine Funktionen, ohne Datenbank. Die Gruppen liest <see cref="GruppenSpielplan.AusAuslosung"/>.</summary>
public static class AuslosungsAuswertung
{
    /// <summary>TopfLiga: die ausgelosten Paarungen (Heim, Gast) in Paarungsreihenfolge; Einträge ohne zwei feste
    /// Teams entfallen.</summary>
    public static List<(int HeimId, int AuswaertsId)> TopfLigaPaarungen(IEnumerable<AuslosungsErgebnis> ergebnisse) =>
        [.. ergebnisse
            .Where(e => e.HeimMannschaftId.HasValue && e.AuswaertsMannschaftId.HasValue)
            .OrderBy(e => e.PaarungsNummer)
            .Select(e => (e.HeimMannschaftId!.Value, e.AuswaertsMannschaftId!.Value))];

    /// <summary>Alle festen Teams in Paarungsreihenfolge, je Paarung Heim vor Gast, jedes Team einmal — die
    /// Reihenfolge, in der Generator bzw. Schweizer System sie paaren.</summary>
    public static List<int> MannschaftenInPaarungsreihenfolge(IEnumerable<AuslosungsErgebnis> ergebnisse) =>
        [.. ergebnisse
            .OrderBy(e => e.PaarungsNummer)
            .SelectMany(e => new[] { e.HeimMannschaftId, e.AuswaertsMannschaftId })
            .OfType<int>()
            .Distinct()];

    /// <summary>Ob die Auslosung nur Platzhalter enthält (keine einzige feste Mannschaft).</summary>
    public static bool NurPlatzhalter(IReadOnlyCollection<AuslosungsErgebnis> ergebnisse) =>
        ergebnisse.Count > 0 && ergebnisse.All(e => !e.HeimMannschaftId.HasValue && !e.AuswaertsMannschaftId.HasValue);

    /// <summary>Die Platzhalter-Paarungen in Paarungsreihenfolge, ohne Freilose; ein fehlender Name wird "?".</summary>
    public static List<(string Heim, string Auswaerts)> PlatzhalterPaarungen(IEnumerable<AuslosungsErgebnis> ergebnisse) =>
        [.. ergebnisse
            .Where(e => !e.IstFreilos)
            .OrderBy(e => e.PaarungsNummer)
            .Select(e => (e.HeimPlatzhalterName ?? "?", e.AuswaertsPlatzhalterName ?? "?"))];
}
