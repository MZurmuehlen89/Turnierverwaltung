namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Merkt sich die Reihenfolge einer Liste, damit ein entnommenes Element an seine ursprüngliche Stelle
/// zurückkehrt statt ans Listenende. Kommt ein Schlüssel mehrfach vor, zählt sein erstes Vorkommen.</summary>
public sealed class Ausgangsreihenfolge<TSchluessel> where TSchluessel : notnull
{
    private readonly Dictionary<TSchluessel, int> _rang = [];

    public Ausgangsreihenfolge(IEnumerable<TSchluessel> schluessel)
    {
        int rang = 0;
        foreach (var s in schluessel)
            _rang.TryAdd(s, rang++);
    }

    /// <summary>Die Stelle, an der <paramref name="neu"/> in die verbliebenen Elemente gehört: hinter allen, die
    /// ursprünglich vor ihm standen. Ein Element, das nicht aus der Liste stammt, kommt ans Ende.</summary>
    public int EinfuegeIndex(IReadOnlyList<TSchluessel> vorhandene, TSchluessel neu)
    {
        int rang = RangVon(neu);
        int index = 0;
        while (index < vorhandene.Count && RangVon(vorhandene[index]) <= rang)
            index++;
        return index;
    }

    private int RangVon(TSchluessel schluessel) => _rang.GetValueOrDefault(schluessel, int.MaxValue);
}
