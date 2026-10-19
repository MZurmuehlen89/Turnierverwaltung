using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Application.Stammdaten;

/// <summary>Suche in einer Mannschaftsauswahl beim Tippen (#479): Name oder Kurzname enthält die Eingabe, ohne
/// Groß-/Kleinschreibung; Treffer am Namensanfang stehen vorn, sonst bleibt die gelieferte Reihenfolge.</summary>
public static class MannschaftsSuche
{
    public static IReadOnlyList<Mannschaft> Treffer(IEnumerable<Mannschaft> mannschaften, string? eingabe)
    {
        var text = eingabe?.Trim() ?? "";
        if (text.Length == 0) return [.. mannschaften];

        return
        [
            .. mannschaften
                .Where(m => Enthaelt(m.Name, text) || Enthaelt(m.Kurzname, text))
                .OrderBy(m => m.Name.StartsWith(text, StringComparison.CurrentCultureIgnoreCase) ? 0 : 1)
        ];
    }

    private static bool Enthaelt(string wert, string text) =>
        wert.Contains(text, StringComparison.CurrentCultureIgnoreCase);
}
