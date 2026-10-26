namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>
/// Stand einer Ligaphasen-Auslosung (<see cref="LigaphasenAuslosung"/>): welche Plätze besetzt sind und welcher
/// Gegner einen offenen Platz noch füllen darf — aus jeder Topf-Gruppe genau ein Heim- und ein Auswärtsgegner, aus
/// jedem Topf höchstens <c>gegnerJeTopf</c>, nie zweimal derselbe, bei aktiven Länder-Regeln keiner aus dem eigenen
/// Land und höchstens <see cref="MAX_GEGNER_JE_LAND"/> aus demselben Land.
/// <para>Topf-Gruppen (#469): Bei 2 Gegnern je Topf ist jeder Topf eine eigene Gruppe (UCL/UEL: aus jedem Topf ein
/// Heim- und ein Auswärtsgegner). Bei 1 Gegner je Topf bilden die Töpfe 1/2, 3/4, … je eine Gruppe (UECL: aus jedem
/// Topf-Paar ein Heim- und ein Auswärtsgegner, die aus verschiedenen Töpfen kommen).</para>
/// </summary>
internal sealed class LigaphasenBelegung
{
    public const int MAX_GEGNER_JE_LAND = 2;

    private readonly int _teamAnzahl;
    private readonly int _topfAnzahl;
    private readonly int _gegnerJeTopf;
    private readonly int[] _topf;
    private readonly int[] _gruppeVonTopf;
    private readonly int[][] _teamsJeTopf;
    private readonly int[][] _teamsJeGruppe;
    private readonly int[] _landIndex;       // durchnummeriertes Land je Team, -1 = unbekannt
    private readonly bool _landRegeln;

    private readonly int[,] _heimGegner;    // [Team, Gruppe] → Gast, den das Team aus dieser Gruppe empfängt (-1 = offen)
    private readonly int[,] _gastgeber;     // [Team, Gruppe] → Gastgeber aus dieser Gruppe, bei dem das Team spielt (-1 = offen)
    private readonly int[,] _gegnerAusTopf; // [Team, Topf] → Anzahl Gegner aus diesem Topf
    private readonly bool[,] _gepaart;
    private readonly int[,] _gegnerJeLand;  // [Team, Land-Index] → Anzahl Gegner aus diesem Land
    private readonly List<(int Heim, int Gast)> _paarungen = [];

    /// <param name="topfJeTeam">Topf-Index (ab 0) je Team-Index.</param>
    /// <param name="landJeTeam">Land je Team-Index (null = unbekannt, z.B. Platzhalter).</param>
    /// <param name="gegnerJeTopf">2 = je Topf ein Heim- und ein Auswärtsgegner, 1 = je Topf ein Gegner, Töpfe
    /// paarweise (braucht eine gerade Topfanzahl).</param>
    public LigaphasenBelegung(IReadOnlyList<int> topfJeTeam, IReadOnlyList<int?> landJeTeam, bool landRegeln, int gegnerJeTopf)
    {
        _teamAnzahl   = topfJeTeam.Count;
        _topfAnzahl   = topfJeTeam.Max() + 1;
        _gegnerJeTopf = gegnerJeTopf;
        if (gegnerJeTopf is not (1 or 2))
            throw new ArgumentOutOfRangeException(nameof(gegnerJeTopf), gegnerJeTopf, "Erlaubt sind 1 oder 2 Gegner je Topf.");
        if (gegnerJeTopf == 1 && _topfAnzahl % 2 != 0)
            throw new ArgumentException("Bei einem Gegner je Topf braucht die Ligaphase eine gerade Anzahl Töpfe.", nameof(topfJeTeam));

        _topf          = [.. topfJeTeam];
        _gruppeVonTopf = [.. Enumerable.Range(0, _topfAnzahl).Select(p => p * gegnerJeTopf / 2)];
        int gruppenAnzahl = _topfAnzahl * gegnerJeTopf / 2;
        _teamsJeTopf   = [.. Enumerable.Range(0, _topfAnzahl).Select(p => TeamsWo(t => _topf[t] == p))];
        _teamsJeGruppe = [.. Enumerable.Range(0, gruppenAnzahl).Select(g => TeamsWo(t => _gruppeVonTopf[_topf[t]] == g))];
        var laender = landJeTeam.OfType<int>().Distinct().Select((land, i) => (land, i)).ToDictionary(x => x.land, x => x.i);
        _landIndex     = [.. landJeTeam.Select(l => l is { } land ? laender[land] : -1)];
        _gepaart       = new bool[_teamAnzahl, _teamAnzahl];
        _gegnerJeLand  = new int[_teamAnzahl, Math.Max(1, laender.Count)];
        _gegnerAusTopf = new int[_teamAnzahl, _topfAnzahl];
        _landRegeln    = landRegeln;
        _heimGegner    = new int[_teamAnzahl, gruppenAnzahl];
        _gastgeber     = new int[_teamAnzahl, gruppenAnzahl];
        for (int t = 0; t < _teamAnzahl; t++)
            for (int g = 0; g < gruppenAnzahl; g++)
                _heimGegner[t, g] = _gastgeber[t, g] = -1;
    }

    private int[] TeamsWo(Func<int, bool> bedingung) => [.. Enumerable.Range(0, _teamAnzahl).Where(bedingung)];

    /// <summary>Die festgelegten Paarungen (Heim, Gast) in der Reihenfolge, in der sie gesetzt wurden.</summary>
    public IReadOnlyList<(int Heim, int Gast)> Paarungen => _paarungen;

    public int TopfAnzahl => _topfAnzahl;

    public IReadOnlyList<int> TeamsImTopf(int topf) => _teamsJeTopf[topf];

    /// <summary>Die Plätze eines Teams: je Topf-Gruppe erst der Gast, den es empfängt, dann der Gastgeber, bei dem
    /// es spielt.</summary>
    public IEnumerable<Platz> PlaetzeVon(int team)
    {
        for (int gruppe = 0; gruppe < _teamsJeGruppe.Length; gruppe++)
        {
            yield return new Platz(team, gruppe, Heim: true);
            yield return new Platz(team, gruppe, Heim: false);
        }
    }

    /// <summary>Alle noch offenen Plätze, Team für Team in der Reihenfolge von <see cref="PlaetzeVon"/>.</summary>
    public IEnumerable<Platz> OffenePlaetze() =>
        Enumerable.Range(0, _teamAnzahl).SelectMany(PlaetzeVon).Where(IstOffen);

    public bool IstOffen(Platz platz) =>
        (platz.Heim ? _heimGegner[platz.Team, platz.Gruppe] : _gastgeber[platz.Team, platz.Gruppe]) < 0;

    public List<int> Kandidaten(Platz platz) =>
        [.. _teamsJeGruppe[platz.Gruppe].Where(u => IstKandidat(platz.Team, u, platz.Heim))];

    // Zählt nur bis zur bisher kleinsten Anzahl — mehr interessiert die Auswahl "wenigste Möglichkeiten" nicht.
    public int KandidatenZaehlen(Platz platz, int abbruchBei)
    {
        int anzahl = 0;
        foreach (int u in _teamsJeGruppe[platz.Gruppe])
            if (IstKandidat(platz.Team, u, platz.Heim) && ++anzahl >= abbruchBei) break;
        return anzahl;
    }

    private bool IstKandidat(int team, int u, bool heim)
    {
        if (u == team || _gepaart[team, u]) return false;
        // Gegenstück muss frei sein: empfängt das Team u, spielt u bei ihm auswärts (und umgekehrt)
        int eigeneGruppe = _gruppeVonTopf[_topf[team]];
        if ((heim ? _gastgeber[u, eigeneGruppe] : _heimGegner[u, eigeneGruppe]) >= 0) return false;
        return TopfErlaubt(team, u) && LaenderErlaubt(team, u);
    }

    // Bei 2 Gegnern je Topf (eine Gruppe = ein Topf) begrenzen schon die zwei Plätze je Gruppe — wirksam wird die
    // Prüfung bei Topf-Paaren: dort kommen Heim- und Auswärtsgegner aus verschiedenen Töpfen, für beide Seiten.
    private bool TopfErlaubt(int a, int b) =>
        _gegnerAusTopf[a, _topf[b]] < _gegnerJeTopf && _gegnerAusTopf[b, _topf[a]] < _gegnerJeTopf;

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
        Belegen(paarung, setzen: true);
        _paarungen.Add(paarung);
    }

    public void Entfernen((int Heim, int Gast) paarung)
    {
        Belegen(paarung, setzen: false);
        _paarungen.RemoveAt(_paarungen.LastIndexOf(paarung));
    }

    private void Belegen((int Heim, int Gast) paarung, bool setzen)
    {
        var (heim, gast) = paarung;
        int delta = setzen ? +1 : -1;
        _heimGegner[heim, _gruppeVonTopf[_topf[gast]]] = setzen ? gast : -1;
        _gastgeber[gast, _gruppeVonTopf[_topf[heim]]]  = setzen ? heim : -1;
        _gepaart[heim, gast] = _gepaart[gast, heim] = setzen;
        _gegnerAusTopf[heim, _topf[gast]] += delta;
        _gegnerAusTopf[gast, _topf[heim]] += delta;
        LandZaehlen(heim, gast, delta);
    }

    private void LandZaehlen(int a, int b, int delta)
    {
        if (!_landRegeln) return;
        if (_landIndex[b] >= 0) _gegnerJeLand[a, _landIndex[b]] += delta;
        if (_landIndex[a] >= 0) _gegnerJeLand[b, _landIndex[a]] += delta;
    }
}

/// <summary>Ein Platz in der Ligaphase: der Gegner aus der Topf-Gruppe <paramref name="Gruppe"/>, den
/// <paramref name="Team"/> empfängt (<paramref name="Heim"/>) bzw. bei dem es auswärts spielt.</summary>
internal readonly record struct Platz(int Team, int Gruppe, bool Heim)
{
    /// <summary>Die Paarung (Heim, Gast), die entsteht, wenn <paramref name="gegner"/> diesen Platz füllt.</summary>
    public (int Heim, int Gast) MitGegner(int gegner) => Heim ? (Team, gegner) : (gegner, Team);
}
