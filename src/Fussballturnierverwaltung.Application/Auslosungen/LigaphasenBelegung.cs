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

    /// <summary>Die festgelegten Paarungen (Heim, Gast) in der Reihenfolge, in der sie gesetzt wurden.</summary>
    public IReadOnlyList<(int Heim, int Gast)> Paarungen => _paarungen;

    public int TopfAnzahl => _topfAnzahl;

    public IReadOnlyList<int> TeamsImTopf(int topf) => _teamsJeTopf[topf];

    /// <summary>Die Plätze eines Teams: je Topf erst der Gast, den es empfängt, dann der Gastgeber, bei dem es spielt.</summary>
    public IEnumerable<Platz> PlaetzeVon(int team)
    {
        for (int topf = 0; topf < _topfAnzahl; topf++)
        {
            yield return new Platz(team, topf, Heim: true);
            yield return new Platz(team, topf, Heim: false);
        }
    }

    /// <summary>Alle noch offenen Plätze, Team für Team in der Reihenfolge von <see cref="PlaetzeVon"/>.</summary>
    public IEnumerable<Platz> OffenePlaetze() =>
        Enumerable.Range(0, _teamAnzahl).SelectMany(PlaetzeVon).Where(IstOffen);

    public bool IstOffen(Platz platz) =>
        (platz.Heim ? _heimGegner[platz.Team, platz.Topf] : _gastgeber[platz.Team, platz.Topf]) < 0;

    public List<int> Kandidaten(Platz platz) =>
        [.. _teamsJeTopf[platz.Topf].Where(u => IstKandidat(platz.Team, u, platz.Heim))];

    // Zählt nur bis zur bisher kleinsten Anzahl — mehr interessiert die Auswahl "wenigste Möglichkeiten" nicht.
    public int KandidatenZaehlen(Platz platz, int abbruchBei)
    {
        int anzahl = 0;
        foreach (int u in _teamsJeTopf[platz.Topf])
            if (IstKandidat(platz.Team, u, platz.Heim) && ++anzahl >= abbruchBei) break;
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

    public void Setzen((int Heim, int Gast) paarung)
    {
        var (heim, gast) = paarung;
        _heimGegner[heim, _topf[gast]] = gast;
        _gastgeber[gast, _topf[heim]]  = heim;
        _gepaart[heim, gast] = _gepaart[gast, heim] = true;
        _paarungen.Add((heim, gast));
        LandZaehlen(heim, gast, +1);
    }

    public void Entfernen((int Heim, int Gast) paarung)
    {
        var (heim, gast) = paarung;
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

/// <summary>Ein Platz in der Ligaphase: der Gegner aus <paramref name="Topf"/>, den <paramref name="Team"/> empfängt
/// (<paramref name="Heim"/>) bzw. bei dem es auswärts spielt.</summary>
internal readonly record struct Platz(int Team, int Topf, bool Heim)
{
    /// <summary>Die Paarung (Heim, Gast), die entsteht, wenn <paramref name="gegner"/> diesen Platz füllt.</summary>
    public (int Heim, int Gast) MitGegner(int gegner) => Heim ? (Team, gegner) : (gegner, Team);
}
