using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Losverfahren für Gruppenphase und Liga: verteilt die Lostopf-Einträge auf Gruppen (Liga = eine
/// Gruppe), je Teilnehmer ein Ergebnis mit der Gruppe als Paarungsnummer. Einstieg über
/// <see cref="Losverfahren.Ausloesen"/>.</summary>
internal static class GruppenLosverfahren
{
    internal static List<AuslosungsErgebnis> GruppenVerteilen(
        Auslosung auslosung, List<List<TeilnehmerSlot>> toepfe, RegelKontext regeln,
        IReadOnlyDictionary<int, int> gruppenkoepfe, Random zufall)
    {
        if (auslosung.TurnierRunde?.Rundenart == Rundenart.Liga)
            return LigaAusloesen(toepfe, zufall);

        int gruppenAnzahl = GruppenAnzahlOderFehler(auslosung);
        var position      = GruppenpositionKompatibilitaet.Wirksam(auslosung.Auslosungsart, auslosung.Gruppenposition);
        var kopfGruppe    = KopfGruppe(gruppenkoepfe, gruppenAnzahl);
        return auslosung.Auslosungsart switch
        {
            Auslosungsart.NachSetzliste => GruppenphaseNachSetzlisteAusloesen(toepfe, kopfGruppe, gruppenAnzahl, position, regeln, zufall),
            Auslosungsart.NachToepfen   => GruppenAusloesen(toepfe, kopfGruppe, gruppenAnzahl, position, regeln, zufall),
            _                           => GruppenAusloesen([toepfe.SelectMany(t => t).ToList()], kopfGruppe, gruppenAnzahl, position, regeln, zufall)
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
    private static List<AuslosungsErgebnis> GruppenphaseNachSetzlisteAusloesen(
        List<List<TeilnehmerSlot>> toepfe, Func<TeilnehmerSlot, int?> kopfGruppe, int gruppenAnzahl,
        Gruppenposition position, RegelKontext regeln, Random zufall)
    {
        var alle         = toepfe.SelectMany(t => t).ToList();
        var freieGruppen = Enumerable.Range(0, gruppenAnzahl).Except(alle.Select(kopfGruppe).OfType<int>());
        var gruppeJeGesetztem = alle
            .Where(s => s.SetzPosition.HasValue && kopfGruppe(s) is null)
            .OrderBy(s => s.SetzPosition)
            .Zip(freieGruppen)
            .ToDictionary(x => x.First, x => x.Second, ReferenceEqualityComparer.Instance);

        return GruppenAusloesen(
            toepfe, slot => kopfGruppe(slot) ?? (gruppeJeGesetztem.TryGetValue(slot, out var g) ? g : null),
            gruppenAnzahl, position, regeln, zufall);
    }

    // Die Töpfe werden nacheinander gezogen, Gesetzte führen ihre Gruppe an und zählen in ihrem Topf mit (#488) —
    // Ablauf und Regelprüfung siehe GruppenAuslosung (#356). Die Position in der Gruppe legt danach
    // PositionenFestlegen fest.
    private static List<AuslosungsErgebnis> GruppenAusloesen(
        IEnumerable<IReadOnlyList<TeilnehmerSlot>> toepfe, Func<TeilnehmerSlot, int?> gesetzteGruppe,
        int gruppenAnzahl, Gruppenposition position, RegelKontext regeln, Random zufall)
    {
        var slots       = new List<TeilnehmerSlot>();
        var topfIndizes = new List<IReadOnlyList<int>>();
        foreach (var topf in toepfe)
        {
            topfIndizes.Add([.. Enumerable.Range(slots.Count, topf.Count)]);
            slots.AddRange(topf);
        }

        var auslosung = new GruppenAuslosung(
            gruppenAnzahl, [.. slots.Select(s => s.VerbandId)], regeln.Konfoederationsverteilung,
            (a, b) => regeln.Verbietet(slots[a], slots[b]), zufall);
        var gesetzt = new bool[slots.Count];
        for (int i = 0; i < slots.Count; i++)
        {
            if (gesetzteGruppe(slots[i]) is not { } gruppe) continue;
            auslosung.Setzen(i, gruppe);
            gesetzt[i] = true;
        }

        var gruppeJeSlot = auslosung.Verteilen(topfIndizes)
            ?? throw new InvalidOperationException(
                $"Keine gültige Gruppenauslosung gefunden. Die Einschränkungen ({regeln.EinschraenkungenText}) " +
                "können mit den gewählten Mannschaften nicht erfüllt werden.");

        var gruppen = Enumerable.Range(0, gruppenAnzahl).Select(_ => new List<int>()).ToList();
        for (int i = 0; i < slots.Count; i++)
            gruppen[gruppeJeSlot[i]].Add(i);

        bool IstFest(int i) => position switch
        {
            Gruppenposition.NachTopf  => true,
            Gruppenposition.Topf1Fest => slots[i].TopfRang == 0,
            _                         => false
        };
        return GruppenZuErgebnisse(
            [.. gruppen.Select(g => PositionenFestlegen(g, i => gesetzt[i], IstFest, zufall).Select(i => slots[i]).ToList())]);
    }

    // Position in der Gruppe (#475): der Gesetzte steht auf Position 1, danach die festen Teams in Topf-Reihenfolge,
    // die übrigen werden dahinter gelost. Die Reihenfolge bestimmt den Spielplan der Gruppe.
    private static IEnumerable<int> PositionenFestlegen(
        List<int> gruppe, Func<int, bool> istGesetzt, Func<int, bool> istFest, Random zufall)
    {
        var ungesetzt = gruppe.Where(i => !istGesetzt(i)).ToList();
        return gruppe.Where(istGesetzt)
            .Concat(ungesetzt.Where(istFest))
            .Concat(ungesetzt.Where(i => !istFest(i)).OrderBy(_ => zufall.Next()));
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
