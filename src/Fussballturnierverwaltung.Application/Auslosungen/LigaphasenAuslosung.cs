using Fussballturnierverwaltung.Application.Spielplan;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>
/// Auslosung der UCL-Ligaphase (ab 2024/25) nach dem UEFA-Verfahren (Issue #347): Jedes Team erhält aus jedem
/// Topf genau einen Heim- und einen Auswärtsgegner, nie zweimal denselben Gegner, bei aktiven Länder-Regeln
/// keinen Gegner aus dem eigenen Land und höchstens <see cref="MAX_GEGNER_JE_LAND"/> aus demselben Land.
/// <para>Ablauf wie bei der offiziellen Auslosung (Software von AE Live): Topf für Topf wird ein Team nach dem
/// anderen gezogen, dann werden seine noch offenen Gegner Topf für Topf zufällig bestimmt. Vor jeder Festlegung
/// prüft eine Vorausschau (Backtracking-Suche), ob sich die Auslosung von dort aus noch vollständig und gültig
/// beenden lässt — einschließlich der Verteilbarkeit auf die Spieltage (<see cref="SpieltagZerlegung"/>; ohne
/// diese Prüfung gibt es regelkonforme Auslosungen, die sich nicht in 8 Spieltagen spielen lassen, vgl. Guyon et
/// al., "Drawing and Scheduling the UEFA Champions League League Phase"). So kann sich die Auslosung nie
/// festfahren.</para>
/// </summary>
internal sealed class LigaphasenAuslosung
{
    public const int MAX_GEGNER_JE_LAND = 2;
    // Startprüfung: gründlich, da sie über "auslosbar ja/nein" entscheidet. Prüfung einzelner Kandidaten: knapp,
    // denn ein nicht bestätigter Kandidat wird nur übersprungen — die letzte gefundene Lösung garantiert immer
    // mindestens einen zulässigen Gegner. Ohne diese Begrenzung kostete jeder unzulässige Kandidat bis zu
    // 100.000 Suchschritte (Belastungstest: Ausreißer von über 2 Minuten).
    private const int MAX_KNOTEN_STARTPRUEFUNG = 30_000;
    private const int MAX_KNOTEN_KANDIDATENPRUEFUNG = 2_000;
    private const int MAX_VERSUCHE_STARTPRUEFUNG = 3;

    private readonly int _teamAnzahl;
    private readonly int _topfAnzahl;
    private readonly int[] _topf;
    private readonly int[][] _teamsJeTopf;
    private readonly int[] _landIndex;       // durchnummeriertes Land je Team, -1 = unbekannt
    private readonly bool _landRegeln;
    private readonly Random _zufall;

    private readonly int[,] _heimGegner;   // [Team, Topf] → Gast, den das Team aus diesem Topf empfängt (-1 = offen)
    private readonly int[,] _gastgeber;    // [Team, Topf] → Gastgeber aus diesem Topf, bei dem das Team spielt (-1 = offen)
    private readonly bool[,] _gepaart;
    private readonly int[,] _gegnerJeLand;  // [Team, Land-Index] → Anzahl Gegner aus diesem Land
    private readonly List<(int Heim, int Gast)> _paarungen = [];

    // Zuletzt von der Vorausschau gefundene vollständige Auslosung — enthält alle bisher festgelegten Paarungen.
    // Ein gezogener Gegner, der darin vorkommt, ist ohne neue Suche zulässig; zugleich garantiert sie, dass für
    // jeden offenen Platz mindestens ein zulässiger Gegner existiert.
    private HashSet<(int, int)> _letzteLoesung = [];
    private int _knoten;
    private int _maxKnoten;

    /// <param name="topfJeTeam">Topf-Index (ab 0) je Team-Index.</param>
    /// <param name="landJeTeam">Land je Team-Index (null = unbekannt, z.B. Platzhalter).</param>
    public LigaphasenAuslosung(IReadOnlyList<int> topfJeTeam, IReadOnlyList<int?> landJeTeam, bool landRegeln, Random zufall)
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
        _zufall     = zufall;
        _heimGegner = new int[_teamAnzahl, _topfAnzahl];
        _gastgeber  = new int[_teamAnzahl, _topfAnzahl];
        for (int t = 0; t < _teamAnzahl; t++)
            for (int p = 0; p < _topfAnzahl; p++)
                _heimGegner[t, p] = _gastgeber[t, p] = -1;
    }

    /// <summary>Führt die Auslosung durch und liefert die Paarungen (Heim, Gast) als Team-Indizes in
    /// Ziehungsreihenfolge, oder <c>null</c>, wenn keine gültige Auslosung gefunden wurde.</summary>
    public List<(int Heim, int Gast)>? Ausloesen()
    {
        bool machbar = false;
        for (int versuch = 0; versuch < MAX_VERSUCHE_STARTPRUEFUNG && !machbar; versuch++)
            machbar = Vervollstaendigbar(MAX_KNOTEN_STARTPRUEFUNG);
        if (!machbar) return null;

        for (int topf = 0; topf < _topfAnzahl; topf++)
        {
            var gezogeneTeams = _teamsJeTopf[topf].OrderBy(_ => _zufall.Next());
            foreach (int team in gezogeneTeams)
            {
                for (int gegnerTopf = 0; gegnerTopf < _topfAnzahl; gegnerTopf++)
                {
                    if (_heimGegner[team, gegnerTopf] < 0) GegnerZiehen(team, gegnerTopf, heim: true);
                    if (_gastgeber[team, gegnerTopf] < 0)  GegnerZiehen(team, gegnerTopf, heim: false);
                }
            }
        }

        return [.. _paarungen];
    }

    private void GegnerZiehen(int team, int gegnerTopf, bool heim)
    {
        foreach (int gegner in Kandidaten(team, gegnerTopf, heim).OrderBy(_ => _zufall.Next()))
        {
            var (h, g) = heim ? (team, gegner) : (gegner, team);
            if (_letzteLoesung.Contains((h, g)))
            {
                Setzen(h, g);
                return;
            }

            Setzen(h, g);
            if (Vervollstaendigbar(MAX_KNOTEN_KANDIDATENPRUEFUNG)) return;
            Entfernen(h, g);
        }

        // Unerreichbar: die letzte gefundene Lösung liefert für jeden offenen Platz einen zulässigen Gegner
        throw new InvalidOperationException("Interner Fehler in der Ligaphasen-Auslosung: kein zulässiger Gegner.");
    }

    // ── Vorausschau ─────────────────────────────────────────────────────────

    private bool Vervollstaendigbar(int maxKnoten)
    {
        _knoten    = 0;
        _maxKnoten = maxKnoten;
        return Suchen();
    }

    // Backtracking mit "wenigste Möglichkeiten zuerst": der offene Platz mit den wenigsten zulässigen Gegnern
    // wird als Nächstes besetzt; hat irgendein Platz keinen mehr, ist dieser Zweig eine Sackgasse.
    private bool Suchen()
    {
        if (++_knoten > _maxKnoten) return false;

        int bestesTeam = -1, besterTopf = -1, wenigste = int.MaxValue;
        bool besterHeim = false;

        for (int t = 0; t < _teamAnzahl; t++)
        {
            for (int p = 0; p < _topfAnzahl; p++)
            {
                for (int richtung = 0; richtung < 2; richtung++)
                {
                    bool heim = richtung == 0;
                    if ((heim ? _heimGegner[t, p] : _gastgeber[t, p]) >= 0) continue;
                    int anzahl = KandidatenZaehlen(t, p, heim, wenigste);
                    if (anzahl == 0) return false;
                    if (anzahl < wenigste) (bestesTeam, besterTopf, besterHeim, wenigste) = (t, p, heim, anzahl);
                }
            }
        }

        if (bestesTeam < 0) return VollstaendigeLoesungPruefen();

        foreach (int gegner in Kandidaten(bestesTeam, besterTopf, besterHeim).OrderBy(_ => _zufall.Next()))
        {
            var (h, g) = besterHeim ? (bestesTeam, gegner) : (gegner, bestesTeam);
            Setzen(h, g);
            bool erfolg = Suchen();
            Entfernen(h, g);
            if (erfolg) return true;
            if (_knoten > _maxKnoten) return false;
        }
        return false;
    }

    private bool VollstaendigeLoesungPruefen()
    {
        if (SpieltagZerlegung.Zerlegen(_paarungen, _zufall, maxVersuche: 10) is null) return false;
        _letzteLoesung = [.. _paarungen];
        return true;
    }

    // ── Zustand ─────────────────────────────────────────────────────────────

    private List<int> Kandidaten(int team, int gegnerTopf, bool heim)
    {
        var kandidaten = new List<int>();
        foreach (int u in _teamsJeTopf[gegnerTopf])
            if (IstKandidat(team, u, heim)) kandidaten.Add(u);
        return kandidaten;
    }

    // Zählt nur bis zur bisher kleinsten Anzahl — mehr interessiert die Auswahl "wenigste Möglichkeiten" nicht.
    private int KandidatenZaehlen(int team, int gegnerTopf, bool heim, int abbruchBei)
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

    private void Setzen(int heim, int gast)
    {
        _heimGegner[heim, _topf[gast]] = gast;
        _gastgeber[gast, _topf[heim]]  = heim;
        _gepaart[heim, gast] = _gepaart[gast, heim] = true;
        _paarungen.Add((heim, gast));
        LandZaehlen(heim, gast, +1);
    }

    private void Entfernen(int heim, int gast)
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
