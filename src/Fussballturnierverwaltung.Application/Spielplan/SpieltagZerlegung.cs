namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>
/// Verteilt vorgegebene Paarungen exakt auf so viele Spieltage, wie das Team mit den meisten Spielen
/// Spiele hat (Issue #347) — bei der UCL-Ligaphase (36 Teams à 8 Spiele) also genau 8 Spieltage à 18 Spiele.
/// Die frühere Greedy-Färbung durfte auf einen zusätzlichen Spieltag ausweichen und landete bei 8 Spielen
/// pro Team regelmäßig bei 9 ungleich vollen Spieltagen.
/// <para><b>Ausgeglichener Plan</b> (jedes Team gleich viele Heim- wie Auswärtsspiele, z.B. UCL 4/4): Die
/// Paarungen werden per Backtracking in "Rundläufe" zerlegt — Mengen, in denen jedes Team genau einmal
/// Gastgeber und genau einmal Gast ist (die Gastgeber→Gast-Kanten bilden Kreise). Ein Rundlauf, dessen Kreise
/// alle gerade Länge haben, lässt sich abwechselnd auf zwei Spieltage aufteilen, an denen jedes Team genau
/// einmal zu Hause und einmal auswärts spielt. Werden diese Spieltag-Paare hintereinander angesetzt, ergibt
/// sich der gewünschte Heim-/Auswärtsrhythmus automatisch: nie mehr als 2 Heim- bzw. Auswärtsspiele in Folge,
/// an den ersten beiden und den letzten beiden Spieltagen je ein Heim- und ein Auswärtsspiel.</para>
/// <para><b>Sonstige Pläne</b> (ungleiche Spielanzahl oder Heim-/Auswärtsbilanz): Spieltag für Spieltag wird
/// eine Paarungsmenge gesucht, die jedes Team abdeckt, dessen Restspiele sonst nicht mehr in die
/// verbleibenden Spieltage passen würden; ohne Rhythmus-Vorgabe.</para>
/// </summary>
public static class SpieltagZerlegung
{
    private const int MAX_KNOTEN_JE_VERSUCH = 50_000;

    /// <summary>Liefert je Paarung (gleicher Index wie <paramref name="paarungen"/>) den Spieltag ab 1, oder
    /// <c>null</c>, wenn innerhalb von <paramref name="maxVersuche"/> Versuchen keine Verteilung gefunden wurde.
    /// Heim ist jeweils das erste Element des Tupels.</summary>
    public static int[]? Zerlegen(
        IReadOnlyList<(int HeimId, int AuswaertsId)> paarungen, Random zufall, int maxVersuche = 200)
    {
        if (paarungen.Count == 0) return [];

        var teams  = paarungen.SelectMany(p => new[] { p.HeimId, p.AuswaertsId }).Distinct().ToList();
        var index  = teams.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        var kanten = paarungen.Select(p => (Heim: index[p.HeimId], Gast: index[p.AuswaertsId])).ToArray();

        var heimSpiele = new int[teams.Count];
        var gastSpiele = new int[teams.Count];
        foreach (var (h, g) in kanten) { heimSpiele[h]++; gastSpiele[g]++; }
        int rundlaeufe = heimSpiele[0];
        bool ausgeglichen = heimSpiele.All(x => x == rundlaeufe) && gastSpiele.All(x => x == rundlaeufe);

        for (int versuch = 0; versuch < maxVersuche; versuch++)
        {
            // Rundläufe mit nur geraden Kreisen gibt es nicht bei jeder Paarungsmenge (z.B. ungerade Teamzahl) —
            // dann in der zweiten Hälfte der Versuche ohne Rhythmus-Garantie Spieltag für Spieltag verteilen.
            var spieltage = ausgeglichen && versuch < maxVersuche / 2
                ? SpieltageAusRundlaeufen(kanten, teams.Count, rundlaeufe, zufall)
                : SpieltageSchrittweise(kanten, teams.Count, zufall);
            if (spieltage is null) continue;

            var ergebnis = new int[kanten.Length];
            for (int tag = 0; tag < spieltage.Count; tag++)
                foreach (int k in spieltage[tag])
                    ergebnis[k] = tag + 1;
            return ergebnis;
        }

        return null;
    }

    // ── Ausgeglichener Plan: Rundläufe mit geraden Kreisen ──────────────────

    private static List<List<int>>? SpieltageAusRundlaeufen((int Heim, int Gast)[] kanten, int teamAnzahl, int rundlaeufe, Random zufall)
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
                if (++knoten > MAX_KNOTEN_JE_VERSUCH) return false;

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
                    if (knoten > MAX_KNOTEN_JE_VERSUCH) return false;
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

    // ── Sonstige Pläne: Spieltag für Spieltag ───────────────────────────────

    private static List<List<int>>? SpieltageSchrittweise((int Heim, int Gast)[] kanten, int teamAnzahl, Random zufall)
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
                if (++knoten > MAX_KNOTEN_JE_VERSUCH) return false;

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
