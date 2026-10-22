namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>
/// Ziehung von Hand für K.-o.-Runden (#482): erst ein Team, dann sein Gegner. Topf 1 wird gegen Topf 2 gezogen, Topf 3
/// gegen Topf 4 …, bis einer der beiden leer ist; die übrigen Teams der größeren Töpfe und eines überzähligen letzten
/// Topfs werden danach untereinander gezogen — wie im DFB-Pokal ab der 2. Runde, wenn Amateur- und Profitopf
/// ungleich groß sind. Mit nur einem Topf ist das die ganze Ziehung. Bei ungerader Teamzahl bekommt das zuletzt
/// übrige Team das Freilos.
/// <para>Heimrecht: das zuerst gezogene Team spielt zu Hause, außer „Heimrecht für Unterklassige“ oder „nach
/// Setzliste“ sagen anderes (<see cref="Losverfahren.HeimrechtFestlegen"/>).</para>
/// <para>Wählbar sind nur Gegner, mit denen sich die übrigen Teams noch regelkonform paaren lassen
/// (<see cref="PaarungsSuche"/>).</para>
/// </summary>
internal sealed class PaarungsZiehung : IZiehungsAblauf
{
    private readonly ZiehungsKontext _kontext;
    private readonly IReadOnlyList<(int A, int B)> _topfPaare;
    private readonly bool[] _imUeberhang;   // darf in der Schlussphase untereinander gezogen werden
    private readonly bool[] _gezogen;
    private readonly List<(int Erster, int? Zweiter, bool IstFreilos)> _paare = [];
    private int? _offen;

    public PaarungsZiehung(ZiehungsKontext kontext)
    {
        _kontext   = kontext;
        int anzahl = kontext.Toepfe.Count;
        _topfPaare = [.. Enumerable.Range(0, anzahl / 2).Select(i => (2 * i, 2 * i + 1))];
        _gezogen   = new bool[kontext.Teilnehmer.Count];

        // Im Überhang: der größere Topf eines Paars und ein überzähliger letzter Topf (bei nur einem Topf: alle).
        var ueberhangToepfe = new HashSet<int>();
        foreach (var (a, b) in _topfPaare)
        {
            int na = kontext.Toepfe[a].Teilnehmer.Count, nb = kontext.Toepfe[b].Teilnehmer.Count;
            if (na > nb) ueberhangToepfe.Add(a);
            if (nb > na) ueberhangToepfe.Add(b);
        }
        if (anzahl % 2 == 1) ueberhangToepfe.Add(anzahl - 1);
        _imUeberhang = [.. kontext.Teilnehmer.Select(t => ueberhangToepfe.Contains(t.TopfIndex))];

        if (kontext.Teilnehmer.Count % 2 == 1 && !kontext.Regeln.FreilosMoeglich)
            throw new InvalidOperationException(
                $"Die Lostöpfe enthalten {kontext.Teilnehmer.Count} Teams — bei ungerader Anzahl bliebe ein Team ohne " +
                "Gegner. Bitte die Regel „Freilos möglich“ aktivieren oder die Teams anpassen.");
        FreilosVergeben();
    }

    // ── Stand ─────────────────────────────────────────────────────────────────

    public IReadOnlyList<ZiehungsGruppe> Gruppen => [];

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
            : TeamAnsage(_topfPaare.Count == 0
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
        return PaarungsSuche.Loesbar(rest, DuerfenSichTreffen, freilose, t => _imUeberhang[t]);
    }

    // Aus demselben Topfpaar nur über Kreuz, untereinander nur im Überhang — und keine Regel dagegen.
    private bool DuerfenSichTreffen(int x, int y)
    {
        int tx = _kontext.Teilnehmer[x].TopfIndex, ty = _kontext.Teilnehmer[y].TopfIndex;
        bool ueberKreuz = tx != ty && tx / 2 == ty / 2 && tx / 2 < _topfPaare.Count;
        return (ueberKreuz || _imUeberhang[x] && _imUeberhang[y]) && _kontext.RegelVerstoss(x, y) is null;
    }

    // ── Hilfen ────────────────────────────────────────────────────────────────

    /// <summary>Das erste Topfpaar, in dem beide Töpfe noch Teams haben (das gerade gezogene Team zählt in seinem
    /// Topf mit) — <c>null</c> in der Schlussphase.</summary>
    private (int A, int B)? AktuellesTopfPaar()
    {
        foreach (var (a, b) in _topfPaare)
            if (OffeneAnzahl(b) > 0 && (OffeneAnzahl(a) > 0 || _offen is { } t && _kontext.Teilnehmer[t].TopfIndex == a))
                return (a, b);
        return null;
    }

    private IEnumerable<int> Offene() => Enumerable.Range(0, _gezogen.Length).Where(t => !_gezogen[t]);

    private IEnumerable<int> Offene(int topf) => _kontext.Toepfe[topf].Teilnehmer.Where(t => !_gezogen[t]);

    private int OffeneAnzahl(int topf) => Offene(topf).Count();

    private string Name(int team) => _kontext.Teilnehmer[team].Name;
}

/// <summary>Sucht eine vollständige Paarung der Teams (Tiefensuche, das Team mit den wenigsten Gegnern zuerst) —
/// höchstens <c>freilose</c> Teams dürfen ohne Gegner bleiben, und nur solche, die <c>darfFreilos</c> zulässt.</summary>
internal static class PaarungsSuche
{
    // Obergrenze wie im Losverfahren: eine unerfüllbare Kombination bricht so zuverlässig ab. Endet die Suche an der
    // Grenze, ist nichts bewiesen (null) — die Ziehung wertet das als lösbar.
    private const int MAX_SUCHSCHRITTE = 200_000;

    public static bool? Loesbar(
        IReadOnlyList<int> teams, Func<int, int, bool> duerfenSichTreffen, int freilose, Func<int, bool> darfFreilos)
    {
        var offen = new HashSet<int>(teams);
        int schritte = 0;

        bool? Suchen(int freiloseUebrig)
        {
            if (offen.Count == 0) return true;
            if (++schritte > MAX_SUCHSCHRITTE) return null;

            // Das Team mit den wenigsten möglichen Gegnern zuerst — Sackgassen zeigen sich so am frühesten.
            int team = offen.MinBy(t => offen.Count(g => g != t && duerfenSichTreffen(t, g)));
            var gegner = offen.Where(g => g != team && duerfenSichTreffen(team, g)).ToList();
            bool unklar = false;

            offen.Remove(team);
            foreach (int g in gegner)
            {
                offen.Remove(g);
                var ergebnis = Suchen(freiloseUebrig);
                offen.Add(g);
                if (ergebnis == true) { offen.Add(team); return true; }
                unklar |= ergebnis is null;
            }
            if (freiloseUebrig > 0 && darfFreilos(team))
            {
                var ergebnis = Suchen(freiloseUebrig - 1);
                if (ergebnis == true) { offen.Add(team); return true; }
                unklar |= ergebnis is null;
            }
            offen.Add(team);
            return unklar ? null : false;
        }

        return Suchen(freilose);
    }
}
