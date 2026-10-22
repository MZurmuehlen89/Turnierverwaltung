using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>
/// Ziehung von Hand für die Gruppenphase (#482), Ablauf wie bei FIFA und UEFA: Topf für Topf wird ein Team gezogen,
/// dann seine Gruppe — je Durchgang eines Topfs erhält jede Gruppe höchstens ein Team (ein Topf mit mehr Teams als
/// Gruppen hat mehrere Durchgänge). Gastgeber und Titelverteidiger stehen vorab in ihrer Gruppe (#487) und zählen im
/// ersten Durchgang ihres Topfs mit.
/// <para>Wählbar sind nur Gruppen, mit denen sich die Auslosung noch regelkonform beenden lässt: die Vorausschau
/// rechnet über <see cref="GruppenAuslosung"/> vom Zwischenstand aus weiter.</para>
/// <para>Position in der Gruppe: Gruppenköpfe stehen auf Position 1, bei „Nach Topf“ (und Topf 1 bei „Topf 1 fest“)
/// folgt sie aus der Reihenfolge der Töpfe, sonst zieht der Anwender sie nach der Gruppe. Das geht nur, wenn jeder
/// Topf ein Vielfaches der Gruppenanzahl enthält — nur dann sind alle Gruppen gleich groß.</para>
/// </summary>
internal sealed class GruppenZiehung : IZiehungsAblauf
{
    private readonly ZiehungsKontext _kontext;
    private readonly int _gruppenAnzahl;
    private readonly int _plaetzeJeGruppe;
    private readonly bool _positionZiehen;
    private readonly Gruppenposition _variante;
    private readonly HashSet<int> _koepfe = [];
    private readonly KonfoederationsGrenzen _grenzen;

    private readonly int[] _gruppe;           // je Teilnehmer, -1 = noch keine
    private readonly int?[] _position;        // je Teilnehmer, ab 1
    private readonly List<int>[] _gezogenJeTopf;
    private int? _offen;                      // gezogen, Gruppe oder Position steht noch aus

    public GruppenZiehung(
        ZiehungsKontext kontext, int gruppenAnzahl, Gruppenposition variante, IReadOnlyDictionary<int, int> gruppenkoepfe)
    {
        _kontext         = kontext;
        _gruppenAnzahl   = gruppenAnzahl;
        _variante        = variante;
        _plaetzeJeGruppe = (kontext.Teilnehmer.Count + gruppenAnzahl - 1) / gruppenAnzahl;
        _positionZiehen  = variante != Gruppenposition.NachTopf &&
                           kontext.Toepfe.All(t => t.Teilnehmer.Count % gruppenAnzahl == 0);
        _grenzen         = new KonfoederationsGrenzen(kontext.Slots.Select(s => s.VerbandId), gruppenAnzahl);
        _gruppe          = [.. kontext.Teilnehmer.Select(_ => -1)];
        _position        = new int?[kontext.Teilnehmer.Count];
        _gezogenJeTopf   = [.. kontext.Toepfe.Select(_ => new List<int>())];

        foreach (var t in kontext.Teilnehmer)
        {
            if (kontext.Slots[t.Index].SpielendeMannschaftId is not { } id ||
                !gruppenkoepfe.TryGetValue(id, out int gruppe) || gruppe >= gruppenAnzahl) continue;
            _koepfe.Add(t.Index);
            _gezogenJeTopf[t.TopfIndex].Add(t.Index);
            Platzieren(t.Index, gruppe);
        }
    }

    // ── Stand ─────────────────────────────────────────────────────────────────

    public IReadOnlyList<ZiehungsGruppe> Gruppen =>
    [
        .. Enumerable.Range(0, _gruppenAnzahl).Select(g =>
        {
            var plaetze = new int?[Math.Max(_plaetzeJeGruppe, Mitglieder(g).Select(t => _position[t] ?? 0).DefaultIfEmpty(0).Max())];
            foreach (int t in Mitglieder(g))
                if (_position[t] is { } p) plaetze[p - 1] = t;
            return new ZiehungsGruppe(g + 1, GruppenName(g), plaetze);
        })
    ];

    public IReadOnlyList<ZiehungsPaarung> Paarungen => [];

    public int? AktuellesTeam => _offen;

    public IReadOnlyList<ManuellePaarungEingabe> Ergebnis() =>
    [
        .. Enumerable.Range(0, _gruppenAnzahl)
            .SelectMany(g => Mitglieder(g).OrderBy(t => _position[t]).Select(t => _kontext.Gruppenplatz(g + 1, t)))
    ];

    public bool? IstLoesbar() => Vorausschau(null, 0);

    // ── Ansage ────────────────────────────────────────────────────────────────

    public ZiehungsAnsage AnsageErmitteln()
    {
        if (_offen is { } team)
            return _gruppe[team] < 0 ? GruppenAnsage(team) : PositionsAnsage(team);
        return AktuellerTopf() is { } topf ? TeamAnsage(topf) : ZiehungsAnsage.Fertig;
    }

    // Bei einem Topf mit mehreren Durchgängen kann ein Team in diesem Durchgang ohne Gruppe bleiben — dann kommt es im
    // nächsten dran. Sonst findet jedes Team eine Gruppe, solange die Ziehung lösbar ist.
    private ZiehungsAnsage TeamAnsage(int topf)
    {
        bool mehrereDurchgaenge = _kontext.Toepfe[topf].Teilnehmer.Count > _gruppenAnzahl;
        var optionen = OffeneTeams(topf)
            .Select(t => new ZiehungsOption(t, !mehrereDurchgaenge || HatGruppe(t)
                ? null
                : "Für dieses Team bleibt in diesem Durchgang keine Gruppe – es kommt im nächsten an die Reihe."))
            .ToList();
        return new ZiehungsAnsage(ZiehungsSchrittArt.Team, $"Ziehe ein Team aus {_kontext.Toepfe[topf].Name}", optionen);
    }

    private ZiehungsAnsage GruppenAnsage(int team) =>
        new(ZiehungsSchrittArt.Gruppe, $"{Name(team)} – ziehe die Gruppe",
            ZielOptionen.Freigeben([.. Enumerable.Range(0, _gruppenAnzahl).Select(g => new ZiehungsOption(g + 1, GruppenGrund(team, g)))]));

    private ZiehungsAnsage PositionsAnsage(int team)
    {
        int gruppe = _gruppe[team];
        var belegt = Mitglieder(gruppe).Select(t => _position[t]).OfType<int>().ToHashSet();
        return new ZiehungsAnsage(ZiehungsSchrittArt.Position,
            $"{Name(team)} kommt in {GruppenName(gruppe)} – ziehe die Position",
            [.. Enumerable.Range(1, _plaetzeJeGruppe).Select(p => new ZiehungsOption(p, belegt.Contains(p)
                ? $"Position {p} ist in {GruppenName(gruppe)} schon vergeben."
                : null))],
            gruppe + 1);
    }

    // ── Züge ──────────────────────────────────────────────────────────────────

    public void Anwenden(int wert)
    {
        if (_offen is not { } team)
        {
            _gezogenJeTopf[_kontext.Teilnehmer[wert].TopfIndex].Add(wert);
            _offen = wert;
        }
        else if (_gruppe[team] < 0)
        {
            Platzieren(team, wert - 1);
            if (_position[team] is not null) _offen = null;
        }
        else
        {
            _position[team] = wert;
            _offen = null;
        }
    }

    private void Platzieren(int team, int gruppe)
    {
        _gruppe[team] = gruppe;
        if (PositionIstFest(team)) _position[team] = ErsteFreiePosition(gruppe);
    }

    private bool PositionIstFest(int team) =>
        !_positionZiehen || _koepfe.Contains(team) ||
        _variante == Gruppenposition.Topf1Fest && _kontext.Teilnehmer[team].TopfIndex == 0;

    private int ErsteFreiePosition(int gruppe)
    {
        var belegt = Mitglieder(gruppe).Select(t => _position[t]).OfType<int>().ToHashSet();
        return Enumerable.Range(1, int.MaxValue - 1).First(p => !belegt.Contains(p));
    }

    // ── Regeln und Vorausschau ────────────────────────────────────────────────

    private bool HatGruppe(int team)
    {
        var gezogen = _gezogenJeTopf[_kontext.Teilnehmer[team].TopfIndex];
        gezogen.Add(team);
        try { return Enumerable.Range(0, _gruppenAnzahl).Any(g => GruppenGrund(team, g) is null); }
        finally { gezogen.RemoveAt(gezogen.Count - 1); }
    }

    // Erwartet das Team als zuletzt Gezogenen seines Topfs.
    private string? GruppenGrund(int team, int gruppe)
    {
        int topf = _kontext.Teilnehmer[team].TopfIndex;
        if (ImSelbenDurchgang(team).Any(t => _gruppe[t] == gruppe))
            return $"{GruppenName(gruppe)} hat in diesem Durchgang schon ein Team aus {_kontext.Toepfe[topf].Name}.";

        foreach (int mitglied in Mitglieder(gruppe))
            if (_kontext.RegelVerstoss(mitglied, team) is { } regel)
                return $"{regel}: {Name(mitglied)} ist schon in {GruppenName(gruppe)}.";

        if (_kontext.Regeln.Konfoederationsverteilung && _kontext.Slots[team].VerbandId is { } verband &&
            Mitglieder(gruppe).Count(t => _kontext.Slots[t].VerbandId == verband) >= _grenzen.Max(verband))
            return $"Konföderationsverteilung: {GruppenName(gruppe)} hat schon so viele Teams dieses " +
                   "Kontinentalverbands, wie eine Gruppe haben darf.";

        return Vorausschau(team, gruppe) == false ? ManuelleZiehung.NICHT_LOESBAR : null;
    }

    private IEnumerable<int> ImSelbenDurchgang(int team)
    {
        var gezogen = _gezogenJeTopf[_kontext.Teilnehmer[team].TopfIndex];
        int durchgang = gezogen.IndexOf(team) / _gruppenAnzahl;
        return gezogen.Skip(durchgang * _gruppenAnzahl).Take(_gruppenAnzahl).Where(t => t != team);
    }

    // Rechnet die Auslosung mit dem Zwischenstand (und dem Team probeweise in der Gruppe) weiter. Die Gezogenen stehen
    // in jedem Topf vorn und in Ziehungsreihenfolge, damit sie in dieselben Durchgänge fallen; fester Seed, damit
    // dieselbe Lage immer dieselbe Antwort gibt.
    private bool? Vorausschau(int? team, int gruppe)
    {
        var regeln    = _kontext.Regeln;
        var auslosung = new GruppenAuslosung(
            _gruppenAnzahl, [.. _kontext.Slots.Select(s => s.VerbandId)], regeln.Konfoederationsverteilung,
            (a, b) => regeln.Verbietet(_kontext.Slots[a], _kontext.Slots[b]), new Random(0));
        for (int t = 0; t < _gruppe.Length; t++)
            if (_gruppe[t] >= 0) auslosung.Setzen(t, _gruppe[t]);
        if (team is { } probe) auslosung.Setzen(probe, gruppe);

        return auslosung.Loesbar(_kontext.Toepfe.Select((topf, i) =>
            (IReadOnlyList<int>)[.. _gezogenJeTopf[i], .. topf.Teilnehmer.Except(_gezogenJeTopf[i])]));
    }

    // ── Hilfen ────────────────────────────────────────────────────────────────

    private int? AktuellerTopf()
    {
        for (int i = 0; i < _kontext.Toepfe.Count; i++)
            if (_gezogenJeTopf[i].Count < _kontext.Toepfe[i].Teilnehmer.Count) return i;
        return null;
    }

    private IEnumerable<int> OffeneTeams(int topf) => _kontext.Toepfe[topf].Teilnehmer.Except(_gezogenJeTopf[topf]);

    private IEnumerable<int> Mitglieder(int gruppe) => Enumerable.Range(0, _gruppe.Length).Where(t => _gruppe[t] == gruppe);

    private string Name(int team) => _kontext.Teilnehmer[team].Name;

    private static string GruppenName(int gruppe) => $"Gruppe {GruppenHerkunft.Buchstabe(gruppe + 1)}";
}

/// <summary>Sicherheitsnetz der Vorausschau: Bricht ihre Suche an der Grenze ab, gilt der Zug als lösbar — sperrt
/// sie trotzdem alle Ziele, gibt es die nur wegen der Vorausschau gesperrten frei, statt die Ziehung festzufahren.</summary>
internal static class ZielOptionen
{
    public static IReadOnlyList<ZiehungsOption> Freigeben(IReadOnlyList<ZiehungsOption> optionen) =>
        optionen.Any(o => o.IstFrei)
            ? optionen
            : [.. optionen.Select(o => o.SperrGrund == ManuelleZiehung.NICHT_LOESBAR ? o with { SperrGrund = null } : o)];
}
