namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>
/// Stand einer Ligaphasen-Auslosung (<see cref="LigaphasenAuslosung"/>): welche Plätze besetzt sind und welcher
/// Gegner einen offenen Platz noch füllen darf — aus jedem Topf genau ein Heim- und ein Auswärtsgegner, nie zweimal
/// derselbe, bei aktiven Länder-Regeln keiner aus dem eigenen Land und höchstens <see cref="MAX_GEGNER_JE_LAND"/> aus
/// demselben Land.
/// </summary>
internal sealed class LigaphasenBelegung
{
    public const int MAX_GEGNER_JE_LAND = 2;

    private readonly int _teamAnzahl;
    private readonly int _topfAnzahl;
    private readonly int[] _topf;
    private readonly int[][] _teamsJeTopf;
    private readonly int[] _landIndex;       // durchnummeriertes Land je Team, -1 = unbekannt
    private readonly bool _landRegeln;

    private readonly int[,] _heimGegner;   // [Team, Topf] → Gast, den das Team aus diesem Topf empfängt (-1 = offen)
    private readonly int[,] _gastgeber;    // [Team, Topf] → Gastgeber aus diesem Topf, bei dem das Team spielt (-1 = offen)
    private readonly bool[,] _gepaart;
    private readonly int[,] _gegnerJeLand;  // [Team, Land-Index] → Anzahl Gegner aus diesem Land
    private readonly List<(int Heim, int Gast)> _paarungen = [];

    /// <param name="topfJeTeam">Topf-Index (ab 0) je Team-Index.</param>
    /// <param name="landJeTeam">Land je Team-Index (null = unbekannt, z.B. Platzhalter).</param>
    public LigaphasenBelegung(IReadOnlyList<int> topfJeTeam, IReadOnlyList<int?> landJeTeam, bool landRegeln)
    {
        _teamAnzahl = topfJeTeam.Count;
        _topfAnzahl = topfJeTeam.Max() + 1;
        _topf       = [.. topfJeTeam];
        _teamsJeTopf = [.. Enumerable.Range(0, _topfAnzahl).Select(p => Enumerable.Range(0, _teamAnzahl).Where(t => _topf[t] == p).ToArray())];
        var laender = landJeTeam.OfType<int>().Distinct().Select((land, i) => (land, i)).ToDictionary(x => x.land, x => x.i);
        _landIndex  = [.. landJeTeam.Select(l => l is { } land ? laender[land] : -1)];
        _gepaart    = new bool[_teamAnzahl, _teamAnzahl];
        _gegnerJeLand = new int[_teamAnzahl, Math.Max(1, laender.Count)];
        _landRegeln = landRegeln;
        _heimGegner = new int[_teamAnzahl, _topfAnzahl];
        _gastgeber  = new int[_teamAnzahl, _topfAnzahl];
        for (int t = 0; t < _teamAnzahl; t++)
            for (int p = 0; p < _topfAnzahl; p++)
                _heimGegner[t, p] = _gastgeber[t, p] = -1;
    }

    public int TeamAnzahl => _teamAnzahl;

    public int TopfAnzahl => _topfAnzahl;

    public IReadOnlyList<int> TeamsImTopf(int topf) => _teamsJeTopf[topf];

    /// <summary>Die festgelegten Paarungen (Heim, Gast) in der Reihenfolge, in der sie gesetzt wurden.</summary>
    public IReadOnlyList<(int Heim, int Gast)> Paarungen => _paarungen;

    /// <summary>Ob das Team aus dem Topf noch einen Gast (<paramref name="heim"/>) bzw. Gastgeber braucht.</summary>
    public bool IstOffen(int team, int topf, bool heim) => (heim ? _heimGegner[team, topf] : _gastgeber[team, topf]) < 0;

    public List<int> Kandidaten(int team, int gegnerTopf, bool heim)
    {
        var kandidaten = new List<int>();
        foreach (int u in _teamsJeTopf[gegnerTopf])
            if (IstKandidat(team, u, heim)) kandidaten.Add(u);
        return kandidaten;
    }

    // Zählt nur bis zur bisher kleinsten Anzahl — mehr interessiert die Auswahl "wenigste Möglichkeiten" nicht.
    public int KandidatenZaehlen(int team, int gegnerTopf, bool heim, int abbruchBei)
    {
        int anzahl = 0;
        foreach (int u in _teamsJeTopf[gegnerTopf])
            if (IstKandidat(team, u, heim) && ++anzahl >= abbruchBei) break;
        return anzahl;
    }

    private bool IstKandidat(int team, int u, bool heim)
    {
        if (u == team || _gepaart[team, u]) return false;
        // Gegenstück muss frei sein: empfängt das Team u, spielt u bei ihm auswärts (und umgekehrt)
        int eigenerTopf = _topf[team];
        if ((heim ? _gastgeber[u, eigenerTopf] : _heimGegner[u, eigenerTopf]) >= 0) return false;
        return LaenderErlaubt(team, u);
    }

    private bool LaenderErlaubt(int a, int b)
    {
        if (!_landRegeln) return true;
        int landA = _landIndex[a], landB = _landIndex[b];
        if (landA >= 0 && landA == landB) return false;
        return (landB < 0 || _gegnerJeLand[a, landB] < MAX_GEGNER_JE_LAND)
            && (landA < 0 || _gegnerJeLand[b, landA] < MAX_GEGNER_JE_LAND);
    }

    public void Setzen(int heim, int gast)
    {
        _heimGegner[heim, _topf[gast]] = gast;
        _gastgeber[gast, _topf[heim]]  = heim;
        _gepaart[heim, gast] = _gepaart[gast, heim] = true;
        _paarungen.Add((heim, gast));
        LandZaehlen(heim, gast, +1);
    }

    public void Entfernen(int heim, int gast)
    {
        _heimGegner[heim, _topf[gast]] = -1;
        _gastgeber[gast, _topf[heim]]  = -1;
        _gepaart[heim, gast] = _gepaart[gast, heim] = false;
        _paarungen.RemoveAt(_paarungen.LastIndexOf((heim, gast)));
        LandZaehlen(heim, gast, -1);
    }

    private void LandZaehlen(int a, int b, int delta)
    {
        if (!_landRegeln) return;
        if (_landIndex[b] >= 0) _gegnerJeLand[a, _landIndex[b]] += delta;
        if (_landIndex[a] >= 0) _gegnerJeLand[b, _landIndex[a]] += delta;
    }
}
