namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Sonstige Pläne der <see cref="SpieltagZerlegung"/> (ungleiche Spielanzahl oder Heim-/Auswärtsbilanz):
/// Spieltag für Spieltag wird eine Paarungsmenge gesucht, die jedes Team abdeckt, dessen Restspiele sonst nicht mehr
/// in die verbleibenden Spieltage passen würden; ohne Rhythmus-Vorgabe. Eine Instanz je Versuch.</summary>
internal sealed class SchrittweiseZerlegung
{
    private readonly (int Heim, int Gast)[] _kanten;
    private readonly int[] _restGrad;           // noch nicht verteilte Spiele je Team
    private readonly List<int>[] _kantenJeTeam; // Kanten je Team in zufälliger Reihenfolge
    private readonly bool[] _offen;             // Kante noch keinem Spieltag zugeordnet

    // Der Spieltag, der gerade gebildet wird
    private List<int> _pflicht = [];
    private bool[] _belegt = [];
    private List<int> _gewaehlt = [];
    private int _knoten;

    private SchrittweiseZerlegung((int Heim, int Gast)[] kanten, int teamAnzahl, Random zufall)
    {
        _kanten   = kanten;
        _restGrad = new int[teamAnzahl];
        foreach (var (a, b) in kanten) { _restGrad[a]++; _restGrad[b]++; }

        _kantenJeTeam = [.. Enumerable.Range(0, teamAnzahl).Select(_ => new List<int>())];
        foreach (int k in Enumerable.Range(0, kanten.Length).OrderBy(_ => zufall.Next()))
        {
            _kantenJeTeam[kanten[k].Heim].Add(k);
            _kantenJeTeam[kanten[k].Gast].Add(k);
        }
        _offen = Enumerable.Repeat(true, kanten.Length).ToArray();
    }

    /// <summary>Die Spieltage als Listen von Kanten-Indizes, oder <c>null</c>, wenn ein Spieltag sich nicht
    /// bilden lässt.</summary>
    public static List<List<int>>? SpieltageSchrittweise((int Heim, int Gast)[] kanten, int teamAnzahl, Random zufall) =>
        new SchrittweiseZerlegung(kanten, teamAnzahl, zufall).Zerlegen();

    private List<List<int>>? Zerlegen()
    {
        int anzahlSpieltage = _restGrad.Max();
        var spieltage = new List<List<int>>();
        for (int tag = 0; tag < anzahlSpieltage; tag++)
        {
            if (SpieltagBilden(verbleibend: anzahlSpieltage - tag) is not { } spieltag) return null;
            Verteilen(spieltag);
            spieltage.Add(spieltag);
        }
        return spieltage;
    }

    // Pflicht: Teams, deren Restspiele genau die verbleibenden Spieltage füllen, müssen an diesem Spieltag spielen
    private List<int>? SpieltagBilden(int verbleibend)
    {
        _pflicht  = [.. Enumerable.Range(0, _restGrad.Length).Where(t => _restGrad[t] == verbleibend)];
        _belegt   = new bool[_restGrad.Length];
        _gewaehlt = [];
        _knoten   = 0;
        return PflichtTeamsPaaren() ? _gewaehlt : null;
    }

    private void Verteilen(List<int> spieltag)
    {
        foreach (int k in spieltag)
        {
            _offen[k] = false;
            _restGrad[_kanten[k].Heim]--;
            _restGrad[_kanten[k].Gast]--;
        }
    }

    // ── Suche je Spieltag ─────────────────────────────────────────────────────

    private bool PflichtTeamsPaaren()
    {
        if (++_knoten > SpieltagZerlegung.MAX_KNOTEN_JE_VERSUCH) return false;

        var (team, optionen) = EngstesPflichtTeam();
        if (team < 0) return true;
        if (optionen == 0) return false;

        foreach (int k in _kantenJeTeam[team])
            if (IstMoeglich(k, team) && KanteVersuchen(k, team)) return true;
        return false;
    }

    // Noch nicht belegtes Pflicht-Team mit den wenigsten möglichen Spielen zuerst (Fail-First-Heuristik);
    // Team -1, wenn alle Pflicht-Teams belegt sind.
    private (int Team, int Optionen) EngstesPflichtTeam()
    {
        int team = -1, wenigste = int.MaxValue;
        foreach (int t in _pflicht)
        {
            if (_belegt[t]) continue;
            int optionen = _kantenJeTeam[t].Count(k => IstMoeglich(k, t));
            if (optionen < wenigste) { wenigste = optionen; team = t; }
        }
        return (team, wenigste);
    }

    private bool IstMoeglich(int kante, int team) =>
        _offen[kante] && !_belegt[Gegner(_kanten[kante], team)];

    private bool KanteVersuchen(int kante, int team)
    {
        int gegner = Gegner(_kanten[kante], team);
        _belegt[team] = _belegt[gegner] = true;
        _gewaehlt.Add(kante);
        if (PflichtTeamsPaaren()) return true;
        _gewaehlt.RemoveAt(_gewaehlt.Count - 1);
        _belegt[team] = _belegt[gegner] = false;
        return false;
    }

    private static int Gegner((int Heim, int Gast) kante, int team) => kante.Heim == team ? kante.Gast : kante.Heim;
}
