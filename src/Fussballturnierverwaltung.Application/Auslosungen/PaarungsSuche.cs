namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Sucht eine vollständige Paarung der Teams (Tiefensuche, das Team mit den wenigsten Gegnern zuerst) —
/// höchstens <c>freilose</c> Teams dürfen ohne Gegner bleiben, und nur solche, die <c>darfFreilos</c> zulässt. Mit
/// <c>zufall</c> werden Teams und Gegner vorab gemischt, die gefundene Paarung ist dann eine Auslosung (#495).</summary>
internal static class PaarungsSuche
{
    // Obergrenze wie im Losverfahren: eine unerfüllbare Kombination bricht so zuverlässig ab. Endet die Suche an der
    // Grenze, ist nichts bewiesen (Loesbar = null) — die Ziehung von Hand wertet das als lösbar.
    private const int MAX_SUCHSCHRITTE = 200_000;

    /// <param name="Loesbar"><c>true</c> mit Paaren und Freilos-Teams, <c>false</c> bewiesen unlösbar, <c>null</c>
    /// an der Suchgrenze abgebrochen.</param>
    public sealed record Ergebnis(bool? Loesbar, IReadOnlyList<(int Team, int Gegner)> Paare, IReadOnlyList<int> Freilose);

    public static Ergebnis Finden(
        IReadOnlyList<int> teams, Func<int, int, bool> duerfenSichTreffen, int freilose, Func<int, bool> darfFreilos,
        Random? zufall = null)
    {
        var reihenfolge = zufall is null ? teams : [.. teams.OrderBy(_ => zufall.Next())];
        var offen       = new HashSet<int>(teams);
        var paare       = new List<(int, int)>();
        var ohneGegner  = new List<int>();
        int schritte    = 0;

        bool? Suchen(int freiloseUebrig)
        {
            if (offen.Count == 0) return true;
            if (++schritte > MAX_SUCHSCHRITTE) return null;

            // Das Team mit den wenigsten möglichen Gegnern zuerst — Sackgassen zeigen sich so am frühesten.
            var offeneTeams = reihenfolge.Where(offen.Contains).ToList();
            int team   = offeneTeams.MinBy(t => offeneTeams.Count(g => g != t && duerfenSichTreffen(t, g)));
            var gegner = offeneTeams.Where(g => g != team && duerfenSichTreffen(team, g)).ToList();
            bool unklar = false;

            offen.Remove(team);
            foreach (int g in gegner)
            {
                offen.Remove(g);
                paare.Add((team, g));
                var ergebnis = Suchen(freiloseUebrig);
                if (ergebnis == true) return true;
                paare.RemoveAt(paare.Count - 1);
                offen.Add(g);
                unklar |= ergebnis is null;
            }
            if (freiloseUebrig > 0 && darfFreilos(team))
            {
                ohneGegner.Add(team);
                var ergebnis = Suchen(freiloseUebrig - 1);
                if (ergebnis == true) return true;
                ohneGegner.RemoveAt(ohneGegner.Count - 1);
                unklar |= ergebnis is null;
            }
            offen.Add(team);
            return unklar ? null : false;
        }

        var loesbar = Suchen(freilose);
        return loesbar == true ? new(true, paare, ohneGegner) : new(loesbar, [], []);
    }
}
