namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Ziehung von Hand für Liga und Schweizer System (#482): gezogen wird die Reihenfolge der Teams, ihre
/// Position ist die Schlüsselzahl des Spielplans. Töpfe und Regeln wirken hier nicht (wie beim Losen, #474).</summary>
internal sealed class ReihenfolgeZiehung(ZiehungsKontext kontext) : IZiehungsAblauf
{
    private readonly List<int> _reihenfolge = [];

    public IReadOnlyList<ZiehungsGruppe> Gruppen =>
    [
        new ZiehungsGruppe(1, "Teilnehmer",
            [.. Enumerable.Range(0, kontext.Teilnehmer.Count).Select(i => i < _reihenfolge.Count ? _reihenfolge[i] : (int?)null)])
    ];

    public IReadOnlyList<ZiehungsPaarung> Paarungen => [];

    public int? AktuellesTeam => null;

    public bool? IstLoesbar() => true;

    public ZiehungsAnsage AnsageErmitteln()
    {
        if (_reihenfolge.Count == kontext.Teilnehmer.Count) return ZiehungsAnsage.Fertig;

        return new ZiehungsAnsage(ZiehungsSchrittArt.Team,
            $"Ziehe das Team für Schlüsselzahl {_reihenfolge.Count + 1}",
            [.. kontext.Teilnehmer.Where(t => !_reihenfolge.Contains(t.Index)).Select(t => new ZiehungsOption(t.Index, null))]);
    }

    public void Anwenden(int wert) => _reihenfolge.Add(wert);

    public IReadOnlyList<ManuellePaarungEingabe> Ergebnis() =>
        [.. _reihenfolge.Select(t => kontext.Gruppenplatz(1, t))];
}
