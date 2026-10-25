namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Berger-Tabelle (Kreismethode) für Round-Robin-Spielpläne: Platz 0 steht fest, die übrigen rücken je
/// Spieltag einen Platz weiter; der feste Platz wechselt je Spieltag das Heimrecht. Bei ungerader Teilnehmerzahl setzt
/// je Spieltag einer aus (Freilos). Gemeinsame Grundlage von <see cref="RoundRobinGenerator"/> (feste Teams) und
/// <see cref="GruppenSpielplan"/> (Teilnehmerplätze mit Platzhaltern), damit beide dieselben Paarungen bilden.</summary>
internal static class BergerTabelle
{
    /// <summary>Die Hinrunde für <paramref name="teilnehmer"/> Teilnehmer (mindestens 2) als Indizes, Spieltag für
    /// Spieltag; innerhalb eines Spieltags zuerst die Partie des festen Platzes.</summary>
    public static IEnumerable<(int Spieltag, int Heim, int Gast)> Paarungen(int teilnehmer)
    {
        int gesamt = Spieltage(teilnehmer) + 1; // bei ungerader Zahl ist Index "teilnehmer" das Freilos
        var kreis  = Enumerable.Range(1, gesamt - 1).ToArray();

        for (int spieltag = 1; spieltag < gesamt; spieltag++)
        {
            foreach (var (heim, gast) in SpieltagPaarungen(kreis, spieltag))
                if (heim < teilnehmer && gast < teilnehmer) yield return (spieltag, heim, gast);

            kreis = [kreis[^1], .. kreis[..^1]];
        }
    }

    /// <summary>Spieltage einer Hinrunde — bei ungerader Teilnehmerzahl einer mehr wegen des Freiloses.</summary>
    public static int Spieltage(int teilnehmer) => teilnehmer % 2 == 0 ? teilnehmer - 1 : teilnehmer;

    private static IEnumerable<(int Heim, int Gast)> SpieltagPaarungen(int[] kreis, int spieltag)
    {
        yield return spieltag % 2 == 1 ? (0, kreis[^1]) : (kreis[^1], 0);

        int gesamt = kreis.Length + 1;
        for (int i = 0; i < (gesamt - 2) / 2; i++)
            yield return (kreis[i], kreis[gesamt - 3 - i]);
    }
}
