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
        var suche   = new Suche(zufall is null ? teams : [.. teams.OrderBy(_ => zufall.Next())], duerfenSichTreffen, darfFreilos);
        var loesbar = suche.Suchen(freilose);
        return loesbar == true ? new(true, suche.Paare, suche.Freilose) : new(loesbar, [], []);
    }

    // Stand einer Suche: offene Teams, die bisher gebildeten Paare und Freilose. Jeder Versuch nimmt seine Belegung
    // zurück, wenn der Rest nicht aufgeht — nur eine gefundene Lösung bleibt stehen.
    private sealed class Suche(
        IReadOnlyList<int> reihenfolge, Func<int, int, bool> duerfenSichTreffen, Func<int, bool> darfFreilos)
    {
        private readonly HashSet<int> _offen = [.. reihenfolge];
        private readonly List<(int Team, int Gegner)> _paare = [];
        private readonly List<int> _ohneGegner = [];
        private int _schritte;

        public IReadOnlyList<(int Team, int Gegner)> Paare => _paare;

        public IReadOnlyList<int> Freilose => _ohneGegner;

        public bool? Suchen(int freiloseUebrig)
        {
            if (_offen.Count == 0) return true;
            if (++_schritte > MAX_SUCHSCHRITTE) return null;

            var (team, gegner) = EngstesTeam();
            _offen.Remove(team);
            var ergebnis = MitGegnerVersuchen(team, gegner, freiloseUebrig);
            if (ergebnis != true && freiloseUebrig > 0 && darfFreilos(team))
                ergebnis = Zusammenfassen(ergebnis, MitFreilosVersuchen(team, freiloseUebrig));
            if (ergebnis != true) _offen.Add(team);
            return ergebnis;
        }

        // Das Team mit den wenigsten möglichen Gegnern zuerst — Sackgassen zeigen sich so am frühesten.
        private (int Team, List<int> Gegner) EngstesTeam()
        {
            var offeneTeams = reihenfolge.Where(_offen.Contains).ToList();
            int team = offeneTeams.MinBy(t => offeneTeams.Count(g => g != t && duerfenSichTreffen(t, g)));
            return (team, [.. offeneTeams.Where(g => g != team && duerfenSichTreffen(team, g))]);
        }

        // true beim ersten Gegner, mit dem der Rest aufgeht; null, wenn keiner aufging, aber eine Teilsuche an der
        // Grenze abbrach.
        private bool? MitGegnerVersuchen(int team, List<int> gegner, int freiloseUebrig)
        {
            bool unklar = false;
            foreach (int g in gegner)
            {
                _offen.Remove(g);
                _paare.Add((team, g));
                var ergebnis = Suchen(freiloseUebrig);
                if (ergebnis == true) return true;
                _paare.RemoveAt(_paare.Count - 1);
                _offen.Add(g);
                unklar |= ergebnis is null;
            }
            return unklar ? null : false;
        }

        private bool? MitFreilosVersuchen(int team, int freiloseUebrig)
        {
            _ohneGegner.Add(team);
            var ergebnis = Suchen(freiloseUebrig - 1);
            if (ergebnis != true) _ohneGegner.RemoveAt(_ohneGegner.Count - 1);
            return ergebnis;
        }

        // Gelöst, wenn ein Weg aufgeht; sonst unklar, wenn einer an der Grenze abbrach.
        private static bool? Zusammenfassen(bool? a, bool? b) => (a, b) switch
        {
            (true, _) or (_, true) => true,
            (null, _) or (_, null) => null,
            _                      => false
        };
    }
}
