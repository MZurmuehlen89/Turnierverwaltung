namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Sonstige Pläne der <see cref="SpieltagZerlegung"/>: Spieltag für Spieltag.</summary>
internal static class SchrittweiseZerlegung
{
    public static List<List<int>>? SpieltageSchrittweise((int Heim, int Gast)[] kanten, int teamAnzahl, Random zufall)
    {
        var restGrad = new int[teamAnzahl];
        foreach (var (a, b) in kanten) { restGrad[a]++; restGrad[b]++; }
        int anzahlSpieltage = restGrad.Max();

        var kantenJeTeam = Enumerable.Range(0, teamAnzahl).Select(_ => new List<int>()).ToArray();
        foreach (int k in Enumerable.Range(0, kanten.Length).OrderBy(_ => zufall.Next()))
        {
            kantenJeTeam[kanten[k].Heim].Add(k);
            kantenJeTeam[kanten[k].Gast].Add(k);
        }

        var offen = new bool[kanten.Length];
        Array.Fill(offen, true);
        var spieltage = new List<List<int>>();

        for (int tag = 0; tag < anzahlSpieltage; tag++)
        {
            int verbleibend = anzahlSpieltage - tag;
            // Pflicht: Teams, deren Restspiele genau die verbleibenden Spieltage füllen, müssen heute spielen
            var pflicht  = Enumerable.Range(0, teamAnzahl).Where(t => restGrad[t] == verbleibend).ToList();
            var belegt   = new bool[teamAnzahl];
            var gewaehlt = new List<int>();
            int knoten   = 0;

            if (!PflichtTeamsPaaren()) return null;

            foreach (int k in gewaehlt)
            {
                offen[k] = false;
                restGrad[kanten[k].Heim]--;
                restGrad[kanten[k].Gast]--;
            }
            spieltage.Add(gewaehlt);

            bool PflichtTeamsPaaren()
            {
                if (++knoten > SpieltagZerlegung.MAX_KNOTEN_JE_VERSUCH) return false;

                int team = -1, wenigste = int.MaxValue;
                foreach (int t in pflicht)
                {
                    if (belegt[t]) continue;
                    int optionen = kantenJeTeam[t].Count(k => offen[k] && !belegt[Gegner(kanten[k], t)]);
                    if (optionen < wenigste) { wenigste = optionen; team = t; }
                }
                if (team < 0) return true;
                if (wenigste == 0) return false;

                foreach (int k in kantenJeTeam[team])
                {
                    int gegner = Gegner(kanten[k], team);
                    if (!offen[k] || belegt[gegner]) continue;

                    belegt[team] = belegt[gegner] = true;
                    gewaehlt.Add(k);
                    if (PflichtTeamsPaaren()) return true;
                    gewaehlt.RemoveAt(gewaehlt.Count - 1);
                    belegt[team] = belegt[gegner] = false;
                }
                return false;
            }
        }

        return spieltage;
    }

    private static int Gegner((int Heim, int Gast) kante, int team) => kante.Heim == team ? kante.Gast : kante.Heim;
}
