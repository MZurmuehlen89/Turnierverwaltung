using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Losverfahren für Gruppenphase und Liga: verteilt die Lostopf-Einträge auf Gruppen (Liga = eine
/// Gruppe), je Teilnehmer ein Ergebnis mit der Gruppe als Paarungsnummer. Einstieg über
/// <see cref="Losverfahren.Ausloesen"/>; eine Instanz gilt für eine Gruppenauslosung.</summary>
internal sealed class GruppenLosverfahren
{
    private readonly int _gruppenAnzahl;
    private readonly Gruppenposition _position;
    private readonly Func<TeilnehmerSlot, int?> _kopfGruppe;
    private readonly RegelKontext _regeln;
    private readonly Random _zufall;

    private GruppenLosverfahren(
        Auslosung auslosung, RegelKontext regeln, IReadOnlyDictionary<int, int> gruppenkoepfe, Random zufall)
    {
        _gruppenAnzahl = GruppenAnzahlOderFehler(auslosung);
        _position      = GruppenpositionKompatibilitaet.Wirksam(auslosung.Auslosungsart, auslosung.Gruppenposition);
        _kopfGruppe    = KopfGruppe(gruppenkoepfe, _gruppenAnzahl);
        _regeln        = regeln;
        _zufall        = zufall;
    }

    internal static List<AuslosungsErgebnis> GruppenVerteilen(
        Auslosung auslosung, List<List<TeilnehmerSlot>> toepfe, RegelKontext regeln,
        IReadOnlyDictionary<int, int> gruppenkoepfe, Random zufall)
    {
        if (auslosung.TurnierRunde?.Rundenart == Rundenart.Liga)
            return LigaAusloesen(toepfe, zufall);

        var verfahren = new GruppenLosverfahren(auslosung, regeln, gruppenkoepfe, zufall);
        return auslosung.Auslosungsart switch
        {
            Auslosungsart.NachSetzliste => verfahren.NachSetzlisteAusloesen(toepfe),
            Auslosungsart.NachToepfen   => verfahren.Ausloesen(toepfe, verfahren._kopfGruppe),
            _                           => verfahren.Ausloesen([toepfe.SelectMany(t => t).ToList()], verfahren._kopfGruppe)
        };
    }

    // Gastgeber und Titelverteidiger als feste Gruppenköpfe (#487) — auch hinter einem Platzhalter, sobald sie ihn
    // sicher besetzen. Eine Gruppe jenseits der Gruppenanzahl bleibt wirkungslos.
    private static Func<TeilnehmerSlot, int?> KopfGruppe(IReadOnlyDictionary<int, int> gruppenkoepfe, int gruppenAnzahl) =>
        slot => slot.SpielendeMannschaftId is { } id && gruppenkoepfe.TryGetValue(id, out var g) && g < gruppenAnzahl
            ? g
            : null;

    private static int GruppenAnzahlOderFehler(Auslosung auslosung) =>
        auslosung.TurnierRunde?.GruppenAnzahl
            ?? throw new InvalidOperationException(
                "Gruppenphase-Auslosung: Die Runde hat keine Gruppenanzahl definiert.");

    // Liga: alle Teams bilden eine Gruppe, gelost wird nur die Reihenfolge darin — sie ist die Schlüsselzahl, aus
    // der der Spielplan entsteht (Berger-Schema, reihenfolgeabhängig). Ohne Mischen ergab jede Auslosung dieselbe
    // Reihenfolge und damit denselben Spielplan (#474). Töpfe, Setzliste und Regeln wirken in der Liga nicht.
    private static List<AuslosungsErgebnis> LigaAusloesen(List<List<TeilnehmerSlot>> toepfe, Random zufall) =>
        GruppenZuErgebnisse([toepfe.SelectMany(t => t).OrderBy(_ => zufall.Next()).ToList()]);

    // Gesetzte Teams (mit Setzposition) führen je eine der Gruppen an, die kein fester Gruppenkopf (#487) belegt: in
    // Setzreihenfolge auf die erste freie Gruppe. Sie bleiben Teil ihres Topfs, alle übrigen werden topfweise
    // verteilt (siehe Issue #5-Diskussion, #488).
    private List<AuslosungsErgebnis> NachSetzlisteAusloesen(List<List<TeilnehmerSlot>> toepfe)
    {
        var alle         = toepfe.SelectMany(t => t).ToList();
        var freieGruppen = Enumerable.Range(0, _gruppenAnzahl).Except(alle.Select(_kopfGruppe).OfType<int>());
        var gruppeJeGesetztem = alle
            .Where(s => s.SetzPosition.HasValue && _kopfGruppe(s) is null)
            .OrderBy(s => s.SetzPosition)
            .Zip(freieGruppen)
            .ToDictionary(x => x.First, x => x.Second, ReferenceEqualityComparer.Instance);

        return Ausloesen(
            toepfe, slot => _kopfGruppe(slot) ?? (gruppeJeGesetztem.TryGetValue(slot, out var g) ? g : null));
    }

    // Die Töpfe werden nacheinander gezogen, Gesetzte führen ihre Gruppe an und zählen in ihrem Topf mit (#488) —
    // Ablauf und Regelprüfung siehe GruppenAuslosung (#356). Die Position in der Gruppe legt danach
    // PositionenFestlegen fest.
    private List<AuslosungsErgebnis> Ausloesen(
        IEnumerable<IReadOnlyList<TeilnehmerSlot>> toepfe, Func<TeilnehmerSlot, int?> gesetzteGruppe)
    {
        var (slots, topfIndizes) = Durchnummerieren(toepfe);

        var auslosung = new GruppenAuslosung(
            _gruppenAnzahl, [.. slots.Select(s => s.VerbandId)], _regeln.Konfoederationsverteilung,
            (a, b) => _regeln.Verbietet(slots[a], slots[b]), _zufall);
        var gesetzt = new bool[slots.Count];
        for (int i = 0; i < slots.Count; i++)
        {
            if (gesetzteGruppe(slots[i]) is not { } gruppe) continue;
            auslosung.Setzen(i, gruppe);
            gesetzt[i] = true;
        }

        var gruppeJeSlot = auslosung.Verteilen(topfIndizes)
            ?? throw new InvalidOperationException(
                $"Keine gültige Gruppenauslosung gefunden. Die Einschränkungen ({_regeln.EinschraenkungenText}) " +
                "können mit den gewählten Mannschaften nicht erfüllt werden.");

        var gruppen = Enumerable.Range(0, _gruppenAnzahl).Select(_ => new List<int>()).ToList();
        for (int i = 0; i < slots.Count; i++)
            gruppen[gruppeJeSlot[i]].Add(i);

        return GruppenZuErgebnisse(
            [.. gruppen.Select(g => PositionenFestlegen(g, i => gesetzt[i], i => IstFest(slots[i])).Select(i => slots[i]).ToList())]);
    }

    // Alle Teilnehmer in Topf-Reihenfolge durchnummeriert, dazu je Topf die Nummern seiner Teilnehmer.
    private static (List<TeilnehmerSlot> Slots, List<IReadOnlyList<int>> TopfIndizes) Durchnummerieren(
        IEnumerable<IReadOnlyList<TeilnehmerSlot>> toepfe)
    {
        var slots       = new List<TeilnehmerSlot>();
        var topfIndizes = new List<IReadOnlyList<int>>();
        foreach (var topf in toepfe)
        {
            topfIndizes.Add([.. Enumerable.Range(slots.Count, topf.Count)]);
            slots.AddRange(topf);
        }
        return (slots, topfIndizes);
    }

    private bool IstFest(TeilnehmerSlot slot) => _position switch
    {
        Gruppenposition.NachTopf  => true,
        Gruppenposition.Topf1Fest => slot.TopfRang == 0,
        _                         => false
    };

    // Position in der Gruppe (#475): der Gesetzte steht auf Position 1, danach die festen Teams in Topf-Reihenfolge,
    // die übrigen werden dahinter gelost. Die Reihenfolge bestimmt den Spielplan der Gruppe.
    private IEnumerable<int> PositionenFestlegen(List<int> gruppe, Func<int, bool> istGesetzt, Func<int, bool> istFest)
    {
        var ungesetzt = gruppe.Where(i => !istGesetzt(i)).ToList();
        return gruppe.Where(istGesetzt)
            .Concat(ungesetzt.Where(istFest))
            .Concat(ungesetzt.Where(i => !istFest(i)).OrderBy(_ => _zufall.Next()));
    }

    // Je Teilnehmer ein Ergebnis ohne Gegner, die Paarungsnummer ist die Gruppe (ab 1).
    private static List<AuslosungsErgebnis> GruppenZuErgebnisse(List<List<TeilnehmerSlot>> gruppen) =>
        gruppen
            .SelectMany((gruppe, g) => gruppe.Select(slot => new AuslosungsErgebnis
            {
                PaarungsNummer      = g + 1,
                HeimMannschaftId    = slot.MannschaftId,
                HeimPlatzhalterName = slot.PlatzhalterName
            }))
            .ToList();
}
