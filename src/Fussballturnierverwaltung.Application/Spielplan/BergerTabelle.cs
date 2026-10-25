namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Berger-Tabelle (Kreismethode) für Round-Robin-Spielpläne.</summary>
internal static class BergerTabelle
{
    /// <summary>Berger-Tabelle: Platz 0 steht fest, die übrigen rücken je Spieltag einen Platz weiter; der feste
    /// Platz wechselt je Spieltag das Heimrecht. Paarungen mit dem Freilos (<c>null</c>) entfallen.</summary>
    internal static IEnumerable<(int Spieltag, GruppenSlot Heim, GruppenSlot Auswaerts)> BergerPaarungen(
        IReadOnlyList<GruppenSlot?> plaetze)
    {
        int gesamt = plaetze.Count;
        var kreis  = Enumerable.Range(1, gesamt - 1).ToArray();

        for (int spieltag = 1; spieltag < gesamt; spieltag++)
        {
            var paare = new List<(GruppenSlot? Heim, GruppenSlot? Auswaerts)>
            {
                spieltag % 2 == 1 ? (plaetze[0], plaetze[kreis[^1]]) : (plaetze[kreis[^1]], plaetze[0]),
            };
            for (int i = 0; i < (gesamt - 2) / 2; i++)
                paare.Add((plaetze[kreis[i]], plaetze[kreis[gesamt - 3 - i]]));

            foreach (var (heim, auswaerts) in paare)
                if (heim is { } h && auswaerts is { } a) yield return (spieltag, h, a);

            kreis = [kreis[^1], .. kreis[..^1]];
        }
    }
}
