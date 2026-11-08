namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>
/// Ziehung von Hand für K.-o.-Runden (#482): erst ein Team, dann sein Gegner — Topfpaar für Topfpaar, danach der
/// Überhang untereinander (<see cref="TopfPaarStruktur"/>). Bei ungerader Teamzahl bekommt das zuletzt übrige Team das
/// Freilos.
/// <para>Heimrecht: das zuerst gezogene Team spielt zu Hause, außer „Heimrecht für Unterklassige“, „für den unteren
/// Topf“ oder „nach Setzliste“ sagen anderes (<see cref="Losverfahren.HeimrechtFestlegen"/>).</para>
/// <para>Wählbar sind nur Gegner, mit denen sich die übrigen Teams noch regelkonform paaren lassen
/// (<see cref="PaarungsSuche"/>).</para>
/// </summary>
internal sealed class PaarungsZiehung : IZiehungsAblauf
{
    private readonly ZiehungsKontext _kontext;
    private readonly TopfPaarStruktur _struktur;
    private readonly bool[] _gezogen;
    private readonly bool[,] _duerfen; // [x, y]: x und y dürfen sich treffen (Topfpaar und Regeln)
    private readonly List<(int Erster, int? Zweiter, bool IstFreilos)> _paare = [];
    private int? _offen;

    public PaarungsZiehung(ZiehungsKontext kontext)
    {
        _kontext  = kontext;
        _struktur = new TopfPaarStruktur(
            [.. kontext.Toepfe.Select(t => t.Teilnehmer.Count)], [.. kontext.Teilnehmer.Select(t => t.TopfIndex)]);
        _gezogen  = new bool[kontext.Teilnehmer.Count];
        _duerfen  = TreffenTabelleAufbauen();

        TopfPaarStruktur.UngeradeAnzahlPruefen(kontext.Teilnehmer.Count, kontext.Regeln);
        FreilosVergeben();
    }

    // ── Stand ─────────────────────────────────────────────────────────────────

    public IReadOnlyList<ZiehungsGruppe> Gruppen => [];

    public int? AktuellesTeam => _offen;

    public IReadOnlyList<ZiehungsPaarung> Paarungen =>
    [
        .. _paare.Select((p, i) => p.Zweiter is { } zweiter
            ? HeimrechtFestlegen(i + 1, p.Erster, zweiter)
            : new ZiehungsPaarung(i + 1, p.Erster, null, p.IstFreilos, IstOffen: !p.IstFreilos))
    ];

    public IReadOnlyList<ManuellePaarungEingabe> Ergebnis() =>
    [
        .. Paarungen.Select(p => ManuellePaarungEingabe.FuerPaarung(
            p.Nr, _kontext.Seite(p.Heim), p.Auswaerts is { } gast ? _kontext.Seite(gast) : null))
    ];

    public bool? IstLoesbar() => RestLoesbar();

    // ── Ansage ────────────────────────────────────────────────────────────────

    public ZiehungsAnsage AnsageErmitteln()
    {
        if (_offen is { } team) return GegnerAnsage(team);
        if (!_gezogen.Contains(false)) return ZiehungsAnsage.Fertig;

        return AktuellesTopfPaar() is var (a, b)
            ? TeamAnsage($"Ziehe ein Team aus {_kontext.Toepfe[a].Name}", Offene(a),
                pruefen: OffeneAnzahl(a) > OffeneAnzahl(b))
            : TeamAnsage(_struktur.TopfPaare.Count == 0
                    ? $"Ziehe ein Team aus {_kontext.Toepfe[0].Name}"
                    : "Die übrigen Teams werden untereinander gezogen – ziehe ein Team",
                Offene(), pruefen: Offene().Count() % 2 == 1);
    }

    // Jedes Team findet einen Gegner, solange die Ziehung lösbar ist — außer es bleiben mehr Teams als Gegner (größerer
    // Topf des Paars, ungerade Zahl im Überhang). Nur dann lohnt die Prüfung je Team.
    private ZiehungsAnsage TeamAnsage(string text, IEnumerable<int> teams, bool pruefen) =>
        new(ZiehungsSchrittArt.Team, text,
            [.. teams.Select(t => new ZiehungsOption(t, !pruefen || HatGegner(t)
                ? null
                : "Für dieses Team bleibt jetzt kein regelkonformer Gegner – es kommt später an die Reihe."))]);

    private ZiehungsAnsage GegnerAnsage(int team)
    {
        var paar = AktuellesTopfPaar();
        string text = paar is var (_, b)
            ? $"{Name(team)} – ziehe den Gegner aus {_kontext.Toepfe[b].Name}"
            : $"{Name(team)} – ziehe den Gegner";
        var kandidaten = paar is var (_, gegnerTopf) ? Offene(gegnerTopf) : Offene().Where(t => t != team);
        return new ZiehungsAnsage(ZiehungsSchrittArt.Gegner, text,
            ZielOptionen.Freigeben([.. kandidaten.Select(g => new ZiehungsOption(g, GegnerGrund(team, g)))]));
    }

    // ── Züge ──────────────────────────────────────────────────────────────────

    public void Anwenden(int wert)
    {
        _gezogen[wert] = true;
        if (_offen is { } erster)
        {
            _paare[^1] = (erster, wert, false);
            _offen = null;
            FreilosVergeben();
        }
        else
        {
            _paare.Add((wert, null, false));
            _offen = wert;
        }
    }

    // Bleibt ein einziges Team übrig, bekommt es das Freilos — ohne eigenen Zug.
    private void FreilosVergeben()
    {
        if (Offene().ToList() is not [var letzter]) return;
        _gezogen[letzter] = true;
        _paare.Add((letzter, null, true));
    }

    private ZiehungsPaarung HeimrechtFestlegen(int nr, int erster, int zweiter)
    {
        var (heim, _) = Losverfahren.HeimrechtFestlegen(
            _kontext.Slots[erster], _kontext.Slots[zweiter], _kontext.Regeln, zufall: null);
        return ReferenceEquals(heim, _kontext.Slots[erster])
            ? new ZiehungsPaarung(nr, erster, zweiter, false, false)
            : new ZiehungsPaarung(nr, zweiter, erster, false, false);
    }

    // ── Regeln und Vorausschau ────────────────────────────────────────────────

    private bool HatGegner(int team)
    {
        var kandidaten = AktuellesTopfPaar() is var (_, b) ? Offene(b) : Offene().Where(t => t != team);
        return kandidaten.Any(g => _kontext.RegelVerstoss(team, g) is null && RestLoesbar(team, g) != false);
    }

    private string? GegnerGrund(int team, int gegner)
    {
        if (_kontext.RegelVerstoss(team, gegner) is { } regel)
            return $"{regel}: {Name(team)} darf nicht auf {Name(gegner)} treffen.";
        return RestLoesbar(team, gegner) == false ? ManuelleZiehung.NICHT_LOESBAR : null;
    }

    // Lassen sich die noch offenen Teams (ohne das probeweise gepaarte Paar) vollständig paaren — mit höchstens einem
    // Freilos bei ungerader Zahl?
    private bool? RestLoesbar(int? a = null, int? b = null)
    {
        var rest = Offene().Where(t => t != a && t != b && t != _offen).ToList();
        int freilose = rest.Count % 2 == 1 && _kontext.Regeln.FreilosMoeglich ? 1 : 0;
        return PaarungsSuche.Finden(rest, DuerfenSichTreffen, freilose, _struktur.ImUeberhang).Loesbar;
    }

    private bool DuerfenSichTreffen(int x, int y) => _duerfen[x, y];

    // Wer auf wen treffen darf, ändert sich während der Ziehung nicht. Die Vorausschau fragt es aber je Ansage
    // millionenfach ab (bei 64 Teams rund 900 Suchen) — einmal vorab berechnet, läuft die Ziehung 2,6-mal so schnell (#549).
    private bool[,] TreffenTabelleAufbauen()
    {
        int anzahl  = _gezogen.Length;
        var duerfen = new bool[anzahl, anzahl];
        for (int x = 0; x < anzahl; x++)
            for (int y = 0; y < anzahl; y++)
                duerfen[x, y] = x != y && _struktur.DuerfenSichTreffen(x, y) && _kontext.RegelVerstoss(x, y) is null;
        return duerfen;
    }

    // ── Hilfen ────────────────────────────────────────────────────────────────

    /// <summary>Das erste Topfpaar, in dem beide Töpfe noch Teams haben (das gerade gezogene Team zählt in seinem
    /// Topf mit) — <c>null</c> in der Schlussphase.</summary>
    private (int A, int B)? AktuellesTopfPaar()
    {
        foreach (var (a, b) in _struktur.TopfPaare)
            if (OffeneAnzahl(b) > 0 && (OffeneAnzahl(a) > 0 || _offen is { } t && _kontext.Teilnehmer[t].TopfIndex == a))
                return (a, b);
        return null;
    }

    private IEnumerable<int> Offene() => Enumerable.Range(0, _gezogen.Length).Where(t => !_gezogen[t]);

    private IEnumerable<int> Offene(int topf) => _kontext.Toepfe[topf].Teilnehmer.Where(t => !_gezogen[t]);

    private int OffeneAnzahl(int topf) => Offene(topf).Count();

    private string Name(int team) => _kontext.Teilnehmer[team].Name;
}
