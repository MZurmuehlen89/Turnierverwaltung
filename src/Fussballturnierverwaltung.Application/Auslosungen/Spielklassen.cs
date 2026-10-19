using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Spielklasse je Mannschaft aus den Abschnitten einer Ligapyramide samt deren Profigrenze — Grundlage für
/// „Heimrecht für Unterklassige“ (Issue #393). Zustandslos, 1 = höchste Klasse.</summary>
public sealed record Spielklassen(IReadOnlyDictionary<int, int> JeMannschaft, int? Profigrenze)
{
    public static Spielklassen Keine { get; } = new(new Dictionary<int, int>(), null);

    /// <summary>Spielklassen aus den Abschnitten der Rangliste. Mannschaften hinter dem letzten Abschnitt bleiben ohne
    /// Klasse; steht eine Mannschaft doppelt in der Liste, gilt ihr bester Platz.</summary>
    public static Spielklassen Aus(Rangliste rangliste)
    {
        var jeMannschaft = new Dictionary<int, int>();
        foreach (var eintrag in rangliste.Eintraege.GroupBy(e => e.MannschaftId).Select(g => g.MinBy(e => e.Rang)!))
            if (rangliste.AbschnittFuer(eintrag.Rang) is { } abschnitt)
                jeMannschaft[eintrag.MannschaftId] = abschnitt.Spielklasse;
        return new Spielklassen(jeMannschaft, rangliste.ProfigrenzeSpielklasse);
    }

    /// <summary>Ob <paramref name="mannschaftId"/> gegen <paramref name="gegnerId"/> als Unterklassiger Heimrecht hat:
    /// tiefere Klasse und — mit Profigrenze — der Gegner spielt bis zur Grenze, die Mannschaft darunter (DFB-Pokal).
    /// Ohne Profigrenze entscheidet strikt die Klasse (Copa del Rey). Ohne Klasse für eines der Teams: nein.</summary>
    public bool HatHeimrechtGegen(int? mannschaftId, int? gegnerId)
    {
        if (Klasse(mannschaftId) is not int eigene || Klasse(gegnerId) is not int gegner) return false;
        if (eigene <= gegner) return false;
        return Profigrenze is not int grenze || (gegner <= grenze && eigene > grenze);
    }

    private int? Klasse(int? mannschaftId) =>
        mannschaftId is int id && JeMannschaft.TryGetValue(id, out var klasse) ? klasse : null;
}
