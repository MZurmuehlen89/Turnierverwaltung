namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Ausgeglichener Plan der <see cref="SpieltagZerlegung"/>: Rundläufe mit geraden Kreisen.</summary>
internal static class RundlaufZerlegung
{
    public static List<List<int>>? SpieltageAusRundlaeufen((int Heim, int Gast)[] kanten, int teamAnzahl, int rundlaeufe, Random zufall)
    {
        // Heimspiel-Kanten je Team in zufälliger Reihenfolge — jeder Versuch durchsucht den Raum anders
        var heimKanten = Enumerable.Range(0, teamAnzahl).Select(_ => new List<int>()).ToArray();
        foreach (int k in Enumerable.Range(0, kanten.Length).OrderBy(_ => zufall.Next()))
            heimKanten[kanten[k].Heim].Add(k);

        var offen = new bool[kanten.Length];
        Array.Fill(offen, true);
        var gefunden = new List<int[]>();
        int knoten = 0;

        if (!RundlaufSuchen()) return null;

        // Rundläufe in zufälliger Reihenfolge, je Rundlauf zufällig, welche Hälfte zuerst gespielt wird
        var spieltage = new List<List<int>>();
        foreach (var rundlauf in gefunden.OrderBy(_ => zufall.Next()))
        {
            var (erste, zweite) = InZweiSpieltageTeilen(rundlauf, kanten);
            if (zufall.Next(2) == 0) (erste, zweite) = (zweite, erste);
            spieltage.Add(erste);
            spieltage.Add(zweite);
        }
        return spieltage;

        bool RundlaufSuchen()
        {
            if (gefunden.Count == rundlaeufe) return true;

            var kanteJeGastgeber = new int[teamAnzahl];
            Array.Fill(kanteJeGastgeber, -1);
            var gastBelegt = new bool[teamAnzahl];
            return Zuweisen(0);

            bool Zuweisen(int zugewiesen)
            {
                if (++knoten > SpieltagZerlegung.MAX_KNOTEN_JE_VERSUCH) return false;

                if (zugewiesen == teamAnzahl)
                {
                    if (!AlleKreiseGerade(kanteJeGastgeber, kanten)) return false;
                    var rundlauf = (int[])kanteJeGastgeber.Clone();
                    foreach (int k in rundlauf) offen[k] = false;
                    gefunden.Add(rundlauf);
                    if (RundlaufSuchen()) return true;
                    gefunden.RemoveAt(gefunden.Count - 1);
                    foreach (int k in rundlauf) offen[k] = true;
                    return false;
                }

                // Gastgeber mit den wenigsten noch möglichen Gästen zuerst (Fail-First-Heuristik)
                int gastgeber = -1, wenigste = int.MaxValue;
                for (int t = 0; t < teamAnzahl; t++)
                {
                    if (kanteJeGastgeber[t] >= 0) continue;
                    int optionen = heimKanten[t].Count(k => offen[k] && !gastBelegt[kanten[k].Gast]);
                    if (optionen < wenigste) { wenigste = optionen; gastgeber = t; }
                }
                if (wenigste == 0) return false;

                foreach (int k in heimKanten[gastgeber])
                {
                    int gast = kanten[k].Gast;
                    if (!offen[k] || gastBelegt[gast]) continue;

                    kanteJeGastgeber[gastgeber] = k;
                    gastBelegt[gast] = true;
                    if (Zuweisen(zugewiesen + 1)) return true;
                    kanteJeGastgeber[gastgeber] = -1;
                    gastBelegt[gast] = false;
                    if (knoten > SpieltagZerlegung.MAX_KNOTEN_JE_VERSUCH) return false;
                }
                return false;
            }
        }
    }

    private static bool AlleKreiseGerade(int[] kanteJeGastgeber, (int Heim, int Gast)[] kanten)
    {
        var besucht = new bool[kanteJeGastgeber.Length];
        for (int start = 0; start < kanteJeGastgeber.Length; start++)
        {
            if (besucht[start]) continue;
            int laenge = 0;
            for (int t = start; !besucht[t]; t = kanten[kanteJeGastgeber[t]].Gast)
            {
                besucht[t] = true;
                laenge++;
            }
            if (laenge % 2 != 0) return false;
        }
        return true;
    }

    // Kreis t0→t1→t2→…→t0 (ti empfängt ti+1): Kanten abwechselnd auf zwei Spieltage — jedes Team hat seine
    // Heim-Kante (ti→ti+1) und seine Gast-Kante (ti-1→ti) dann an verschiedenen Spieltagen.
    private static (List<int> Erste, List<int> Zweite) InZweiSpieltageTeilen(int[] kanteJeGastgeber, (int Heim, int Gast)[] kanten)
    {
        var erste   = new List<int>();
        var zweite  = new List<int>();
        var besucht = new bool[kanteJeGastgeber.Length];
        for (int start = 0; start < kanteJeGastgeber.Length; start++)
        {
            bool gerade = true;
            for (int t = start; !besucht[t]; t = kanten[kanteJeGastgeber[t]].Gast)
            {
                besucht[t] = true;
                (gerade ? erste : zweite).Add(kanteJeGastgeber[t]);
                gerade = !gerade;
            }
        }
        return (erste, zweite);
    }
}
