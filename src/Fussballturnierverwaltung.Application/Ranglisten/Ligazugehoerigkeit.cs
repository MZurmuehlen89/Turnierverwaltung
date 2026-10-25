using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

namespace Fussballturnierverwaltung.Application.Ranglisten;

/// <summary>Liga je Verein aus den Abschnitten der Ligapyramiden (Issue #384) — der Name des Abschnitts, in den der
/// Ranglistenplatz fällt, z. B. „2. Bundesliga“. Zustandslos.</summary>
public sealed class Ligazugehoerigkeit
{
    private readonly IReadOnlyDictionary<int, string> _jeMannschaft;

    private Ligazugehoerigkeit(IReadOnlyDictionary<int, string> jeMannschaft) => _jeMannschaft = jeMannschaft;

    public static Ligazugehoerigkeit Keine { get; } = new(new Dictionary<int, string>());

    /// <summary>Liga je Mannschaft aus allen Ranglisten mit Abschnitten. Eine Rangliste hat nur einen Stand (#492, ohne
    /// Saison-Logik); steht ein Verein dennoch in mehreren Pyramiden, gilt die neueste nach Datum. Innerhalb einer
    /// Pyramide gilt der beste Platz, Plätze hinter dem letzten Abschnitt bleiben ohne Liga.</summary>
    public static Ligazugehoerigkeit Aus(IEnumerable<Rangliste> ranglisten)
    {
        var jeMannschaft = new Dictionary<int, string>();
        foreach (var pyramide in ranglisten.Where(r => r.Abschnitte.Count > 0).OrderByDescending(r => r.Datum))
            foreach (var eintrag in pyramide.Eintraege.GroupBy(e => e.MannschaftId).Select(g => g.MinBy(e => e.Rang)!))
                if (pyramide.AbschnittFuer(eintrag.Rang) is { } abschnitt)
                    jeMannschaft.TryAdd(eintrag.MannschaftId, abschnitt.Name);
        return new Ligazugehoerigkeit(jeMannschaft);
    }

    /// <summary>Name der Liga von <paramref name="mannschaftId"/>; <c>null</c>, wenn der Verein in keiner Pyramide steht.</summary>
    public string? LigaVon(int mannschaftId) => _jeMannschaft.GetValueOrDefault(mannschaftId);
}
