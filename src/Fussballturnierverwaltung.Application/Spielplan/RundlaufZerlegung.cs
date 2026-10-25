namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Ausgeglichener Plan der <see cref="SpieltagZerlegung"/> (jedes Team gleich viele Heim- wie Auswärtsspiele):
/// zerlegt die Paarungen per Backtracking in Rundläufe — Mengen, in denen jedes Team genau einmal Gastgeber und genau
/// einmal Gast ist — mit nur geraden Kreisen; jeder Rundlauf ergibt zwei Spieltage. Eine Instanz je Versuch.</summary>
internal sealed class RundlaufZerlegung
{
    private readonly (int Heim, int Gast)[] _kanten;
    private readonly int _teamAnzahl;
    private readonly int _rundlaeufe;
    private readonly Random _zufall;

    private readonly List<int>[] _heimKanten;    // Heimspiel-Kanten je Team in zufälliger Reihenfolge
    private readonly bool[] _offen;              // Kante noch keinem Rundlauf zugeordnet
    private readonly List<int[]> _gefunden = []; // fertige Rundläufe, je Gastgeber seine Kante
    private int _knoten;

    /// <summary>Der Rundlauf, der gerade entsteht: Kante je Gastgeber (-1 = offen) und welche Teams schon Gast sind.</summary>
    private sealed class Entwurf(int teamAnzahl)
    {
        public int[] KanteJeGastgeber { get; } = Enumerable.Repeat(-1, teamAnzahl).ToArray();
        public bool[] GastBelegt { get; } = new bool[teamAnzahl];
    }

    private RundlaufZerlegung((int Heim, int Gast)[] kanten, int teamAnzahl, int rundlaeufe, Random zufall)
    {
        _kanten     = kanten;
        _teamAnzahl = teamAnzahl;
        _rundlaeufe = rundlaeufe;
        _zufall     = zufall;

        // Jeder Versuch durchsucht den Raum in anderer Reihenfolge
        _heimKanten = [.. Enumerable.Range(0, teamAnzahl).Select(_ => new List<int>())];
        foreach (int k in Enumerable.Range(0, kanten.Length).OrderBy(_ => zufall.Next()))
            _heimKanten[kanten[k].Heim].Add(k);
        _offen = Enumerable.Repeat(true, kanten.Length).ToArray();
    }

    /// <summary>Die Spieltage als Listen von Kanten-Indizes, oder <c>null</c>, wenn die Suche innerhalb des
    /// Knotenlimits keine Zerlegung findet.</summary>
    public static List<List<int>>? SpieltageAusRundlaeufen(
        (int Heim, int Gast)[] kanten, int teamAnzahl, int rundlaeufe, Random zufall)
    {
        var zerlegung = new RundlaufZerlegung(kanten, teamAnzahl, rundlaeufe, zufall);
        return zerlegung.RundlaufSuchen() ? zerlegung.InSpieltageAufteilen() : null;
    }

    // Rundläufe in zufälliger Reihenfolge, je Rundlauf zufällig, welche Hälfte zuerst gespielt wird
    private List<List<int>> InSpieltageAufteilen()
    {
        var spieltage = new List<List<int>>();
        foreach (var rundlauf in _gefunden.OrderBy(_ => _zufall.Next()))
        {
            var (erste, zweite) = InZweiSpieltageTeilen(rundlauf);
            if (_zufall.Next(2) == 0) (erste, zweite) = (zweite, erste);
            spieltage.Add(erste);
            spieltage.Add(zweite);
        }
        return spieltage;
    }

    // ── Suche ─────────────────────────────────────────────────────────────────

    private bool RundlaufSuchen() =>
        _gefunden.Count == _rundlaeufe || Zuweisen(new Entwurf(_teamAnzahl), zugewiesen: 0);

    private bool Zuweisen(Entwurf entwurf, int zugewiesen)
    {
        if (++_knoten > SpieltagZerlegung.MAX_KNOTEN_JE_VERSUCH) return false;
        if (zugewiesen == _teamAnzahl) return RundlaufUebernehmen(entwurf.KanteJeGastgeber);

        var (gastgeber, optionen) = EngsterGastgeber(entwurf);
        if (optionen == 0) return false;

        foreach (int k in _heimKanten[gastgeber])
        {
            if (!IstMoeglich(k, entwurf)) continue;
            if (KanteVersuchen(entwurf, gastgeber, k, zugewiesen)) return true;
            if (_knoten > SpieltagZerlegung.MAX_KNOTEN_JE_VERSUCH) return false;
        }
        return false;
    }

    // Gastgeber mit den wenigsten noch möglichen Gästen zuerst (Fail-First-Heuristik)
    private (int Gastgeber, int Optionen) EngsterGastgeber(Entwurf entwurf)
    {
        int gastgeber = -1, wenigste = int.MaxValue;
        for (int t = 0; t < _teamAnzahl; t++)
        {
            if (entwurf.KanteJeGastgeber[t] >= 0) continue;
            int optionen = _heimKanten[t].Count(k => IstMoeglich(k, entwurf));
            if (optionen < wenigste) { wenigste = optionen; gastgeber = t; }
        }
        return (gastgeber, wenigste);
    }

    private bool IstMoeglich(int kante, Entwurf entwurf) =>
        _offen[kante] && !entwurf.GastBelegt[_kanten[kante].Gast];

    private bool KanteVersuchen(Entwurf entwurf, int gastgeber, int kante, int zugewiesen)
    {
        int gast = _kanten[kante].Gast;
        entwurf.KanteJeGastgeber[gastgeber] = kante;
        entwurf.GastBelegt[gast] = true;
        if (Zuweisen(entwurf, zugewiesen + 1)) return true;
        entwurf.KanteJeGastgeber[gastgeber] = -1;
        entwurf.GastBelegt[gast] = false;
        return false;
    }

    // Ein vollständiger Rundlauf zählt nur mit geraden Kreisen; mit ihm geht die Suche über die übrigen Kanten weiter.
    private bool RundlaufUebernehmen(int[] kanteJeGastgeber)
    {
        if (!AlleKreiseGerade(kanteJeGastgeber)) return false;

        var rundlauf = (int[])kanteJeGastgeber.Clone();
        OffenSetzen(rundlauf, offen: false);
        _gefunden.Add(rundlauf);
        if (RundlaufSuchen()) return true;
        _gefunden.RemoveAt(_gefunden.Count - 1);
        OffenSetzen(rundlauf, offen: true);
        return false;
    }

    private void OffenSetzen(int[] rundlauf, bool offen)
    {
        foreach (int k in rundlauf) _offen[k] = offen;
    }

    // ── Kreise ────────────────────────────────────────────────────────────────

    private bool AlleKreiseGerade(int[] kanteJeGastgeber)
    {
        var besucht = new bool[kanteJeGastgeber.Length];
        for (int start = 0; start < kanteJeGastgeber.Length; start++)
        {
            if (besucht[start]) continue;
            int laenge = 0;
            for (int t = start; !besucht[t]; t = _kanten[kanteJeGastgeber[t]].Gast)
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
    private (List<int> Erste, List<int> Zweite) InZweiSpieltageTeilen(int[] kanteJeGastgeber)
    {
        var erste   = new List<int>();
        var zweite  = new List<int>();
        var besucht = new bool[kanteJeGastgeber.Length];
        for (int start = 0; start < kanteJeGastgeber.Length; start++)
        {
            bool gerade = true;
            for (int t = start; !besucht[t]; t = _kanten[kanteJeGastgeber[t]].Gast)
            {
                besucht[t] = true;
                (gerade ? erste : zweite).Add(kanteJeGastgeber[t]);
                gerade = !gerade;
            }
        }
        return (erste, zweite);
    }
}
