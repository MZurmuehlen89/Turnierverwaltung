namespace Fussballturnierverwaltung.Application.Ranglisten;

/// <summary>Umsortieren der Eintragsliste einer Rangliste mit mehreren markierten Einträgen (#477). Liefert jeweils die
/// neue Reihenfolge; die markierten Einträge behalten untereinander ihre Reihenfolge.</summary>
public static class EintragsReihenfolge
{
    /// <summary>Jeder markierte Eintrag rückt einen Platz nach oben. Ein markierter Block am Listenanfang bleibt stehen,
    /// die übrigen rücken trotzdem auf.</summary>
    public static IReadOnlyList<T> Hoch<T>(IReadOnlyList<T> liste, IReadOnlySet<T> markiert)
    {
        var ergebnis = liste.ToList();
        for (int i = 1; i < ergebnis.Count; i++)
            if (markiert.Contains(ergebnis[i]) && !markiert.Contains(ergebnis[i - 1]))
                (ergebnis[i - 1], ergebnis[i]) = (ergebnis[i], ergebnis[i - 1]);
        return ergebnis;
    }

    /// <summary>Gegenstück zu <see cref="Hoch{T}"/>: jeder markierte Eintrag rückt einen Platz nach unten.</summary>
    public static IReadOnlyList<T> Runter<T>(IReadOnlyList<T> liste, IReadOnlySet<T> markiert)
    {
        var ergebnis = liste.ToList();
        for (int i = ergebnis.Count - 2; i >= 0; i--)
            if (markiert.Contains(ergebnis[i]) && !markiert.Contains(ergebnis[i + 1]))
                (ergebnis[i], ergebnis[i + 1]) = (ergebnis[i + 1], ergebnis[i]);
        return ergebnis;
    }

    /// <summary>Die markierten Einträge rücken als geschlossener Block so, dass der erste auf Rang
    /// <paramref name="zielRang"/> steht; ein Rang hinter dem Listenende setzt den Block ans Ende.</summary>
    public static IReadOnlyList<T> AufRang<T>(IReadOnlyList<T> liste, IReadOnlySet<T> markiert, int zielRang)
    {
        var block = liste.Where(markiert.Contains).ToList();
        var rest = liste.Where(e => !markiert.Contains(e)).ToList();
        var position = Math.Clamp(zielRang - 1, 0, rest.Count);
        return [.. rest.Take(position), .. block, .. rest.Skip(position)];
    }

    /// <summary>Fügt <paramref name="neu"/> hinter dem letzten markierten Eintrag ein, ohne Markierung am Ende.</summary>
    public static IReadOnlyList<T> EinfuegenNach<T>(IReadOnlyList<T> liste, IReadOnlySet<T> markiert, T neu)
    {
        var letzter = -1;
        for (int i = 0; i < liste.Count; i++)
            if (markiert.Contains(liste[i])) letzter = i;
        var position = letzter < 0 ? liste.Count : letzter + 1;
        return [.. liste.Take(position), neu, .. liste.Skip(position)];
    }

    /// <summary>Index des nächsten Treffers ab <paramref name="startIndex"/> (einschließlich), am Ende weiter von vorn;
    /// <c>null</c> ohne Treffer.</summary>
    public static int? NaechsterTreffer<T>(IReadOnlyList<T> liste, Func<T, bool> passt, int startIndex)
    {
        for (int schritt = 0; schritt < liste.Count; schritt++)
        {
            var index = ((startIndex + schritt) % liste.Count + liste.Count) % liste.Count;
            if (passt(liste[index])) return index;
        }
        return null;
    }
}
