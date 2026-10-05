namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>
/// Verteilt Teilnehmer auf Gruppen wie bei einer offiziellen FIFA-/UEFA-Ziehung (Issue #356): Topf für Topf wird
/// ein Team nach dem anderen gezogen und kommt in die erste Gruppe in alphabetischer Reihenfolge, in die es darf.
/// Eine Gruppe wird übersprungen, wenn das Team dort gegen eine Regel verstieße oder sich die Auslosung danach
/// nicht mehr regelkonform beenden ließe (Vorausschau) — so kann sich die Ziehung nie festfahren.
/// <para>Ersetzt das frühere Mischen mit bis zu 500 Versuchen je Topf: das nahm frühere Töpfe nie zurück und
/// meldete „nicht erfüllbar“, obwohl eine andere Verteilung eines früheren Topfs aufgegangen wäre.</para>
/// <para>Technisch eine Tiefensuche in Ziehungsreihenfolge mit aufsteigender Gruppenwahl: ihre erste Lösung ist
/// genau das Ergebnis des beschriebenen Verfahrens.</para>
/// </summary>
internal sealed class GruppenAuslosung
{
    // Obergrenze für die Suchschritte je Ziehung — eine unerfüllbare Kombination bricht so zuverlässig ab. Endet
    // die Suche an dieser Grenze, ist damit nichts bewiesen: dann wird neu gezogen (andere Reihenfolge). Eine
    // gewöhnliche Ziehung braucht wenige hundert Schritte; mit knapper Grenze und mehreren Ziehungen blieb im
    // Belastungstest (WM-Feld mit 48 Teams, zufällige Topfeinteilung, 12.000 Läufe) jede Auslosung unter 0,1 s —
    // mit 200.000 Schritten in einer einzigen Ziehung gab es Ausreißer von fast 2 s.
    private const int MAX_SUCHSCHRITTE = 5_000;
    private const int MAX_ZIEHUNGEN = 20;

    private readonly int _gruppenAnzahl;
    private readonly Func<int, int, bool> _verbietet;
    private readonly Random _zufall;

    private readonly int[] _verband;     // durchnummerierter Verband je Teilnehmer, -1 = unbekannt oder Regel aus
    private readonly int[] _min;         // je Verband: Mindestanzahl je Gruppe
    private readonly int[] _max;         // je Verband: Höchstanzahl je Gruppe
    private readonly int[] _offen;       // je Verband: noch nicht zugeordnete Teilnehmer
    private readonly int[,] _anzahl;     // [Gruppe, Verband] → zugeordnete Teilnehmer
    private readonly List<int>[] _mitglieder;
    private readonly int[] _gruppe;      // je Teilnehmer, -1 = noch nicht zugeordnet

    private int[][] _ziehungen = [];
    private bool[][] _belegt = [];       // [Ziehung][Gruppe] → hat aus dieser Ziehung schon ein Team
    private int _schritte;

    /// <param name="verbandJeTeilnehmer">Kontinentalverband je Teilnehmer-Index (null = unbekannt, z. B. Platzhalter).</param>
    /// <param name="konfoederationsverteilung">Ob die Regel „Konföderationsverteilung“ gilt (<see cref="KonfoederationsGrenzen"/>).</param>
    /// <param name="verbietet">Paarweise Regeln: dürfen diese beiden Teilnehmer nicht in dieselbe Gruppe?</param>
    public GruppenAuslosung(
        int gruppenAnzahl, IReadOnlyList<int?> verbandJeTeilnehmer, bool konfoederationsverteilung,
        Func<int, int, bool> verbietet, Random zufall)
    {
        _gruppenAnzahl = gruppenAnzahl;
        _verbietet     = verbietet;
        _zufall        = zufall;

        List<int> verbaende = konfoederationsverteilung ? [.. verbandJeTeilnehmer.OfType<int>().Distinct()] : [];
        var grenzen = new KonfoederationsGrenzen(verbandJeTeilnehmer, gruppenAnzahl);
        _verband = [.. verbandJeTeilnehmer.Select(v => v is { } verband ? verbaende.IndexOf(verband) : -1)];
        _min     = [.. verbaende.Select(grenzen.Min)];
        _max     = [.. verbaende.Select(grenzen.Max)];
        _offen   = [.. verbaende.Select((_, i) => _verband.Count(v => v == i))];
        _anzahl  = new int[gruppenAnzahl, Math.Max(1, verbaende.Count)];
        _mitglieder = [.. Enumerable.Range(0, gruppenAnzahl).Select(_ => new List<int>())];
        _gruppe  = [.. verbandJeTeilnehmer.Select(_ => -1)];
    }

    /// <summary>Setzt einen Teilnehmer vorab fest in eine Gruppe (Gruppenkopf laut Setzliste) — ohne Regelprüfung,
    /// die Setzliste gibt der Nutzer vor.</summary>
    public void Setzen(int teilnehmer, int gruppe) => Zuordnen(teilnehmer, gruppe);

    /// <summary>Lost die Töpfe nacheinander aus; aus einem Topf erhält jede Gruppe höchstens ein Team je Durchgang
    /// (ein Topf mit mehr Teams als Gruppen wird in mehreren Durchgängen verteilt). Liefert die Gruppe (ab 0) je
    /// Teilnehmer-Index oder <c>null</c>, wenn die Regeln mit diesen Teilnehmern nicht erfüllbar sind.</summary>
    public int[]? Verteilen(IEnumerable<IReadOnlyList<int>> toepfe)
    {
        var topfListe = toepfe.ToList();
        for (int ziehung = 0; ziehung < MAX_ZIEHUNGEN; ziehung++)
        {
            _ziehungen = [.. topfListe.SelectMany(topf => topf.OrderBy(_ => _zufall.Next()).Chunk(_gruppenAnzahl))];
            _belegt    = [.. _ziehungen.Select(_ => new bool[_gruppenAnzahl])];
            _schritte  = 0;
            if (Suchen(0, 0)) return [.. _gruppe];
            if (_schritte <= MAX_SUCHSCHRITTE) return null; // vollständig durchsucht — wirklich nicht erfüllbar
        }
        return null;
    }

    private bool Suchen(int ziehung, int position)
    {
        if (ziehung == _ziehungen.Length) return true;
        if (position == _ziehungen[ziehung].Length) return Suchen(ziehung + 1, 0);
        if (++_schritte > MAX_SUCHSCHRITTE) return false;

        int teilnehmer = _ziehungen[ziehung][position];
        for (int g = 0; g < _gruppenAnzahl; g++)
        {
            if (_belegt[ziehung][g] || !Zulaessig(teilnehmer, g)) continue;

            Zuordnen(teilnehmer, g);
            _belegt[ziehung][g] = true;
            if (NochLoesbar(ziehung, position + 1) && Suchen(ziehung, position + 1)) return true;
            _belegt[ziehung][g] = false;
            Aufheben(teilnehmer, g);
        }
        return false;
    }

    private bool Zulaessig(int teilnehmer, int gruppe)
    {
        int verband = _verband[teilnehmer];
        if (verband >= 0 && _anzahl[gruppe, verband] >= _max[verband]) return false;
        foreach (int mitglied in _mitglieder[gruppe])
            if (_verbietet(mitglied, teilnehmer)) return false;
        return true;
    }

    private void Zuordnen(int teilnehmer, int gruppe)
    {
        _gruppe[teilnehmer] = gruppe;
        _mitglieder[gruppe].Add(teilnehmer);
        if (_verband[teilnehmer] is >= 0 and var verband)
        {
            _anzahl[gruppe, verband]++;
            _offen[verband]--;
        }
    }

    private void Aufheben(int teilnehmer, int gruppe)
    {
        _gruppe[teilnehmer] = -1;
        _mitglieder[gruppe].RemoveAt(_mitglieder[gruppe].Count - 1);
        if (_verband[teilnehmer] is >= 0 and var verband)
        {
            _anzahl[gruppe, verband]--;
            _offen[verband]++;
        }
    }

    // ── Vorausschau ──────────────────────────────────────────────────────────
    // Notwendige Bedingungen, die eine Sackgasse früh erkennen — ohne sie probierte die Suche erst alle
    // Anordnungen der späteren Töpfe durch, bevor sie eine frühe Fehlentscheidung zurücknimmt.

    private bool NochLoesbar(int ziehung, int position)
    {
        if (!MindestanzahlErreichbar(ziehung, position)) return false;

        // Jedes noch nicht gezogene Team braucht eine eigene zulässige Gruppe — im laufenden Topf unter den dort
        // noch freien Gruppen, in jedem späteren Topf unter allen (spätere Zuordnungen machen es nur enger).
        if (!Zuordenbar(_ziehungen[ziehung].AsSpan(position), _belegt[ziehung])) return false;
        for (int z = ziehung + 1; z < _ziehungen.Length; z++)
            if (!Zuordenbar(_ziehungen[z], belegt: null)) return false;
        return true;
    }

    // Fehlen einer Gruppe noch Teams eines Verbands bis zur Mindestanzahl, müssen dafür genug Teams dieses Verbands
    // und genug freie Plätze in der Gruppe übrig sein.
    private bool MindestanzahlErreichbar(int ziehung, int position)
    {
        if (_min.Length == 0) return true;

        int spaetereZiehungen = _ziehungen.Length - ziehung - 1;
        bool laufendeHatNochTeams = position < _ziehungen[ziehung].Length;
        var fehlendJeVerband = new int[_min.Length];

        for (int g = 0; g < _gruppenAnzahl; g++)
        {
            int fehlendInGruppe = 0;
            for (int v = 0; v < _min.Length; v++)
            {
                int fehlend = Math.Max(0, _min[v] - _anzahl[g, v]);
                fehlendInGruppe     += fehlend;
                fehlendJeVerband[v] += fehlend;
            }
            int freiePlaetze = spaetereZiehungen + (laufendeHatNochTeams && !_belegt[ziehung][g] ? 1 : 0);
            if (fehlendInGruppe > freiePlaetze) return false;
        }

        for (int v = 0; v < _min.Length; v++)
            if (fehlendJeVerband[v] > _offen[v]) return false;
        return true;
    }

    // Bipartite Zuordnung Teams → Gruppen (Erweiterungspfade): gibt es für jedes Team eine eigene zulässige Gruppe?
    private bool Zuordenbar(ReadOnlySpan<int> teams, bool[]? belegt)
    {
        if (teams.Length == 0) return true;

        var teamJeGruppe = new int[_gruppenAnzahl];
        Array.Fill(teamJeGruppe, -1);
        var besucht = new bool[_gruppenAnzahl];
        var kandidaten = teams.ToArray();

        bool Erweitern(int team)
        {
            for (int g = 0; g < _gruppenAnzahl; g++)
            {
                if (besucht[g] || belegt?[g] == true || !Zulaessig(team, g)) continue;
                besucht[g] = true;
                if (teamJeGruppe[g] < 0 || Erweitern(teamJeGruppe[g]))
                {
                    teamJeGruppe[g] = team;
                    return true;
                }
            }
            return false;
        }

        foreach (int team in kandidaten)
        {
            Array.Clear(besucht);
            if (!Erweitern(team)) return false;
        }
        return true;
    }
}
