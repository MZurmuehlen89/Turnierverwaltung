using Fussballturnierverwaltung.Application.Spielplan;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>
/// Auslosung der UCL-Ligaphase (ab 2024/25) nach dem UEFA-Verfahren (Issue #347): Jedes Team erhält aus jedem
/// Topf genau einen Heim- und einen Auswärtsgegner, nie zweimal denselben Gegner, bei aktiven Länder-Regeln
/// keinen Gegner aus dem eigenen Land und höchstens <see cref="LigaphasenBelegung.MAX_GEGNER_JE_LAND"/> aus demselben Land.
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
    // Startprüfung: gründlich, da sie über "auslosbar ja/nein" entscheidet. Prüfung einzelner Kandidaten: knapp,
    // denn ein nicht bestätigter Kandidat wird nur übersprungen — die letzte gefundene Lösung garantiert immer
    // mindestens einen zulässigen Gegner. Ohne diese Begrenzung kostete jeder unzulässige Kandidat bis zu
    // 100.000 Suchschritte (Belastungstest: Ausreißer von über 2 Minuten).
    private const int MAX_KNOTEN_STARTPRUEFUNG = 30_000;
    private const int MAX_KNOTEN_KANDIDATENPRUEFUNG = 2_000;
    private const int MAX_VERSUCHE_STARTPRUEFUNG = 3;

    private readonly LigaphasenBelegung _belegung;
    private readonly Random _zufall;

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
        _belegung = new LigaphasenBelegung(topfJeTeam, landJeTeam, landRegeln);
        _zufall   = zufall;
    }

    /// <summary>Führt die Auslosung durch und liefert die Paarungen (Heim, Gast) als Team-Indizes in
    /// Ziehungsreihenfolge, oder <c>null</c>, wenn keine gültige Auslosung gefunden wurde.</summary>
    public List<(int Heim, int Gast)>? Ausloesen()
    {
        bool machbar = false;
        for (int versuch = 0; versuch < MAX_VERSUCHE_STARTPRUEFUNG && !machbar; versuch++)
            machbar = Vervollstaendigbar(MAX_KNOTEN_STARTPRUEFUNG);
        if (!machbar) return null;

        for (int topf = 0; topf < _belegung.TopfAnzahl; topf++)
        {
            var gezogeneTeams = _belegung.TeamsImTopf(topf).OrderBy(_ => _zufall.Next());
            foreach (int team in gezogeneTeams)
            {
                for (int gegnerTopf = 0; gegnerTopf < _belegung.TopfAnzahl; gegnerTopf++)
                {
                    if (_belegung.IstOffen(team, gegnerTopf, heim: true))  GegnerZiehen(team, gegnerTopf, heim: true);
                    if (_belegung.IstOffen(team, gegnerTopf, heim: false)) GegnerZiehen(team, gegnerTopf, heim: false);
                }
            }
        }

        return [.. _belegung.Paarungen];
    }

    private void GegnerZiehen(int team, int gegnerTopf, bool heim)
    {
        foreach (int gegner in _belegung.Kandidaten(team, gegnerTopf, heim).OrderBy(_ => _zufall.Next()))
        {
            var (h, g) = heim ? (team, gegner) : (gegner, team);
            if (_letzteLoesung.Contains((h, g)))
            {
                _belegung.Setzen(h, g);
                return;
            }

            _belegung.Setzen(h, g);
            if (Vervollstaendigbar(MAX_KNOTEN_KANDIDATENPRUEFUNG)) return;
            _belegung.Entfernen(h, g);
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

        for (int t = 0; t < _belegung.TeamAnzahl; t++)
        {
            for (int p = 0; p < _belegung.TopfAnzahl; p++)
            {
                for (int richtung = 0; richtung < 2; richtung++)
                {
                    bool heim = richtung == 0;
                    if (!_belegung.IstOffen(t, p, heim)) continue;
                    int anzahl = _belegung.KandidatenZaehlen(t, p, heim, wenigste);
                    if (anzahl == 0) return false;
                    if (anzahl < wenigste) (bestesTeam, besterTopf, besterHeim, wenigste) = (t, p, heim, anzahl);
                }
            }
        }

        if (bestesTeam < 0) return VollstaendigeLoesungPruefen();

        foreach (int gegner in _belegung.Kandidaten(bestesTeam, besterTopf, besterHeim).OrderBy(_ => _zufall.Next()))
        {
            var (h, g) = besterHeim ? (bestesTeam, gegner) : (gegner, bestesTeam);
            _belegung.Setzen(h, g);
            bool erfolg = Suchen();
            _belegung.Entfernen(h, g);
            if (erfolg) return true;
            if (_knoten > _maxKnoten) return false;
        }
        return false;
    }

    private bool VollstaendigeLoesungPruefen()
    {
        if (SpieltagZerlegung.Zerlegen(_belegung.Paarungen, _zufall, maxVersuche: 10) is null) return false;
        _letzteLoesung = [.. _belegung.Paarungen];
        return true;
    }
}
