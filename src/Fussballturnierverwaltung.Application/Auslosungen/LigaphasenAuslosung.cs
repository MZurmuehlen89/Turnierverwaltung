using Fussballturnierverwaltung.Application.Spielplan;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>
/// Auslosung der UCL-Ligaphase (ab 2024/25) nach dem UEFA-Verfahren (Issue #347): Jedes Team erhält aus jedem
/// Topf genau einen Heim- und einen Auswärtsgegner — in der UECL aus jedem Topf-Paar (#469, siehe
/// <see cref="LigaphasenBelegung"/>) —, nie zweimal denselben Gegner, bei aktiven Länder-Regeln keinen Gegner aus
/// dem eigenen Land und höchstens <see cref="LigaphasenBelegung.MAX_GEGNER_JE_LAND"/> aus demselben Land.
/// <para>Ablauf wie bei der offiziellen Auslosung (Software von AE Live): Topf für Topf wird ein Team nach dem
/// anderen gezogen, dann werden seine noch offenen Gegner Topf für Topf zufällig bestimmt. Vor jeder Festlegung
/// prüft eine Vorausschau (Backtracking-Suche), ob sich die Auslosung von dort aus noch vollständig und gültig
/// beenden lässt — einschließlich der Verteilbarkeit auf die Spieltage (<see cref="SpieltagZerlegung"/>; ohne
/// diese Prüfung gibt es regelkonforme Auslosungen, die sich nicht in 8 bzw. 6 Spieltagen spielen lassen, vgl.
/// Guyon et al., "Drawing and Scheduling the UEFA Champions League League Phase"). So kann sich die Auslosung nie
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
    /// <param name="gegnerJeTopf">1 oder 2, siehe <see cref="LigaphasenBelegung"/>.</param>
    public LigaphasenAuslosung(
        IReadOnlyList<int> topfJeTeam, IReadOnlyList<int?> landJeTeam, bool landRegeln, int gegnerJeTopf, Random zufall)
    {
        _belegung = new LigaphasenBelegung(topfJeTeam, landJeTeam, landRegeln, gegnerJeTopf);
        _zufall   = zufall;
    }

    /// <summary>Führt die Auslosung durch und liefert die Paarungen (Heim, Gast) als Team-Indizes in
    /// Ziehungsreihenfolge, oder <c>null</c>, wenn keine gültige Auslosung gefunden wurde.</summary>
    public List<(int Heim, int Gast)>? Ausloesen()
    {
        if (!StartPruefen()) return null;

        for (int topf = 0; topf < _belegung.TopfAnzahl; topf++)
            foreach (int team in Gemischt(_belegung.TeamsImTopf(topf)))
                TeamZiehen(team);

        return [.. _belegung.Paarungen];
    }

    private bool StartPruefen()
    {
        for (int versuch = 0; versuch < MAX_VERSUCHE_STARTPRUEFUNG; versuch++)
            if (Vervollstaendigbar(MAX_KNOTEN_STARTPRUEFUNG)) return true;
        return false;
    }

    // Die noch offenen Gegner des gezogenen Teams, Topf für Topf — Plätze, die schon beim Ziehen eines früheren
    // Teams besetzt wurden, entfallen.
    private void TeamZiehen(int team)
    {
        foreach (var platz in _belegung.PlaetzeVon(team))
            if (_belegung.IstOffen(platz)) GegnerZiehen(platz);
    }

    private void GegnerZiehen(Platz platz)
    {
        foreach (int gegner in Gemischt(_belegung.Kandidaten(platz)))
        {
            var paarung = platz.MitGegner(gegner);
            _belegung.Setzen(paarung);
            if (_letzteLoesung.Contains(paarung) || Vervollstaendigbar(MAX_KNOTEN_KANDIDATENPRUEFUNG)) return;
            _belegung.Entfernen(paarung);
        }

        // Unerreichbar: die letzte gefundene Lösung liefert für jeden offenen Platz einen zulässigen Gegner
        throw new InvalidOperationException("Interner Fehler in der Ligaphasen-Auslosung: kein zulässiger Gegner.");
    }

    private IEnumerable<int> Gemischt(IEnumerable<int> teams) => teams.OrderBy(_ => _zufall.Next());

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
        if (EngsterPlatz() is not var (platz, anzahl)) return VollstaendigeLoesungPruefen();
        if (anzahl == 0) return false;

        foreach (int gegner in Gemischt(_belegung.Kandidaten(platz)))
        {
            var paarung = platz.MitGegner(gegner);
            _belegung.Setzen(paarung);
            bool erfolg = Suchen();
            _belegung.Entfernen(paarung);
            if (erfolg) return true;
            if (_knoten > _maxKnoten) return false;
        }
        return false;
    }

    /// <summary>Der offene Platz mit den wenigsten zulässigen Gegnern — bei einer Sackgasse der erste Platz ohne
    /// Gegner (Anzahl 0), <c>null</c>, wenn alle Plätze besetzt sind.</summary>
    private (Platz Platz, int Anzahl)? EngsterPlatz()
    {
        (Platz Platz, int Anzahl)? engster = null;
        foreach (var platz in _belegung.OffenePlaetze())
        {
            int wenigste = engster?.Anzahl ?? int.MaxValue;
            int anzahl   = _belegung.KandidatenZaehlen(platz, wenigste);
            if (anzahl == 0) return (platz, 0);
            if (anzahl < wenigste) engster = (platz, anzahl);
        }
        return engster;
    }

    private bool VollstaendigeLoesungPruefen()
    {
        if (SpieltagZerlegung.Zerlegen(_belegung.Paarungen, _zufall, maxVersuche: 10) is null) return false;
        _letzteLoesung = [.. _belegung.Paarungen];
        return true;
    }
}
