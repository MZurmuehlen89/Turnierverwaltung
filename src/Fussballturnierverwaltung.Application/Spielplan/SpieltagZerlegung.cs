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
    internal const int MAX_KNOTEN_JE_VERSUCH = 50_000;

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
                ? RundlaufZerlegung.SpieltageAusRundlaeufen(kanten, teams.Count, rundlaeufe, zufall)
                : SchrittweiseZerlegung.SpieltageSchrittweise(kanten, teams.Count, zufall);
            if (spieltage is null) continue;

            var ergebnis = new int[kanten.Length];
            for (int tag = 0; tag < spieltage.Count; tag++)
                foreach (int k in spieltage[tag])
                    ergebnis[k] = tag + 1;
            return ergebnis;
        }

        return null;
    }
}
