using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Die Losverfahren aller Auslosungsarten: verteilt die Lostopf-Einträge auf Gruppen oder bildet daraus
/// Paarungen. Zustandslos und ohne Datenbankbezug — für reproduzierbare Ergebnisse ein <see cref="Random"/> mit
/// festem Seed übergeben.</summary>
public static class Losverfahren
{
    public static List<AuslosungsErgebnis> Ausloesen(
        Auslosung auslosung, LosVorbereitung vorbereitung, Random zufall, Qualifikanten? qualifikanten = null)
    {
        var toepfe = auslosung.Lostoepfe
            .OrderBy(l => l.Reihenfolge)
            .Select((l, rang) => TeilnehmerSlot.AusTopf(
                l, rang, vorbereitung.GruppeJeMannschaft, qualifikanten ?? Qualifikanten.Keine).ToList())
            .ToList();

        // Gruppenphase und Liga ordnen die Teilnehmer Gruppen zu (Liga = eine Gruppe), alle anderen Rundenarten
        // bilden Paarungen.
        return auslosung.TurnierRunde?.Rundenart is Rundenart.Gruppenphase or Rundenart.Liga
            ? GruppenLosverfahren.GruppenVerteilen(auslosung, toepfe, vorbereitung.Regeln, vorbereitung.Gruppenkoepfe, zufall)
            : PaarungenBilden(auslosung, toepfe, vorbereitung.Regeln, zufall);
    }

    // ── Paarungen ─────────────────────────────────────────────────────────────

    private static List<AuslosungsErgebnis> PaarungenBilden(
        Auslosung auslosung, List<List<TeilnehmerSlot>> toepfe, RegelKontext regeln, Random zufall) =>
        auslosung.Auslosungsart switch
        {
            Auslosungsart.NachToepfen or
            Auslosungsart.GruppensiegerGegenGruppenzweite => TopfPaareAusloesen(toepfe, regeln, zufall),
            Auslosungsart.NachSetzliste                   => NachSetzlisteAusloesen(toepfe, regeln, zufall),
            Auslosungsart.TopfZuordnung                   => TopfZuordnungAusloesen(
                                                 toepfe, auslosung.TurnierRunde?.GegnerJeTopf ?? 2, regeln, zufall),
            _                                             => ZufaelligAusloesen(toepfe, regeln, zufall)
        };

    // Alle Teams in einem Topf: ohne Topfpaar sind alle im Überhang und werden untereinander gelost — dieselbe Suche
    // wie „Nach Töpfen“ (#625). Bis dahin hatte „Zufällig“ eine eigene Suche, loste das Freilos vorab (ging der Rest
    // mit den Regeln nicht auf, scheiterte die Auslosung, obwohl ein anderes Freilos-Team aufging) und ließ bei
    // ungerader Anzahl ohne Freilos still ein Team fallen. Die TopfRang-Werte der Slots bleiben erhalten.
    private static List<AuslosungsErgebnis> ZufaelligAusloesen(
        List<List<TeilnehmerSlot>> toepfe, RegelKontext regeln, Random zufall) =>
        TopfPaareAusloesen([[.. toepfe.SelectMany(t => t)]], regeln, zufall);

    // Topf 1 gegen Topf 2, Topf 3 gegen Topf 4 …, bis der kleinere Topf leer ist; die übrigen Teams werden
    // untereinander gelost — wie im DFB-Pokal (#495, siehe TopfPaarStruktur). Bis dahin fiel der Überhang
    // stillschweigend aus der Runde. Die Regeln gelten in beiden Phasen, die Suche löst beide gemeinsam: ein
    // ungünstiger Überhang macht die Auslosung nicht unlösbar, wenn eine andere Verteilung aufgeht.
    private static List<AuslosungsErgebnis> TopfPaareAusloesen(
        List<List<TeilnehmerSlot>> toepfe, RegelKontext regeln, Random zufall)
    {
        var slots      = toepfe.SelectMany(t => t).ToList();
        var topfJeTeam = toepfe.SelectMany((topf, i) => topf.Select(_ => i)).ToList();
        var struktur   = new TopfPaarStruktur([.. toepfe.Select(t => t.Count)], topfJeTeam);
        TopfPaarStruktur.UngeradeAnzahlPruefen(slots.Count, regeln);

        var suche = PaarungsSuche.Finden(
            [.. Enumerable.Range(0, slots.Count)],
            (x, y) => struktur.DuerfenSichTreffen(x, y) && !regeln.Verbietet(slots[x], slots[y]),
            freilose: slots.Count % 2, struktur.ImUeberhang, zufall);
        if (suche.Loesbar != true) throw KeineGueltigeAuslosung(regeln);

        // Erst die Paarungen der Topfpaare in Topf-Reihenfolge (das Team des vorderen Topfs zuerst, wie bisher fürs
        // Heimrecht), dann der Überhang in gelöster Reihenfolge, zuletzt das Freilos.
        var paare = suche.Paare
            .Select(p => struktur.UeberKreuz(p.Team, p.Gegner) && p.Gegner < p.Team ? (p.Gegner, p.Team) : p)
            .OrderBy(p => struktur.UeberKreuz(p.Item1, p.Item2) ? p.Item1 : int.MaxValue)
            .Select(p => (slots[p.Item1], slots[p.Item2]));
        var ergebnisse = PaareZuErgebnissen(paare, regeln, zufall);
        foreach (int team in suche.Freilose)
            ergebnisse.Add(Freilos(ergebnisse.Count + 1, slots[team]));
        return ergebnisse;
    }

    // Gesetzte (Topf 1, #563) gegen alle Übrigen — dieselbe Topfpaar-Auslosung wie „Nach Töpfen“, damit auch
    // hier alle Einschränkungen und Heimrecht-Regeln greifen (bis #331 ignorierte „Nach Setzliste“ sie alle) und
    // die übrigen Teams der größeren Seite untereinander gelost werden, statt herauszufallen (#495).
    private static List<AuslosungsErgebnis> NachSetzlisteAusloesen(
        List<List<TeilnehmerSlot>> toepfe, RegelKontext regeln, Random zufall)
    {
        var alle = toepfe.SelectMany(t => t).ToList();
        var gesetzte   = alle.Where(s => s.SetzPosition.HasValue).OrderBy(s => s.SetzPosition).ToList();
        var ungesetzte = alle.Where(s => !s.SetzPosition.HasValue).ToList();

        return TopfPaareAusloesen([gesetzte, ungesetzte], regeln, zufall);
    }

    // ── TopfZuordnung (UCL-Ligaphase) ─────────────────────────────────────────

    // Die Länder-Regeln (kein gleiches Land, höchstens 2 Gegner aus demselben Land) sind fest an die Regel
    // "Kein gleiches Land" gekoppelt (Nutzerentscheidung Issue #347, keine eigene AuslosungsRegelArt).
    private static List<AuslosungsErgebnis> TopfZuordnungAusloesen(
        List<List<TeilnehmerSlot>> lostoepfe, int gegnerJeTopf, RegelKontext regeln, Random zufall)
    {
        var keinGleichesLand = regeln.KeinGleichesLand;
        if (keinGleichesLand) LandverteilungPruefen(lostoepfe);

        var teilnehmer = lostoepfe.SelectMany((topf, i) => topf.Select(slot => (Slot: slot, Topf: i))).ToList();
        var paarungen  = new LigaphasenAuslosung(
                teilnehmer.Select(t => t.Topf).ToList(),
                teilnehmer.Select(t => t.Slot.LandId).ToList(),
                keinGleichesLand,
                gegnerJeTopf,
                zufall)
            .Ausloesen()
            ?? throw new InvalidOperationException(
                "Keine gültige Topf-Zuordnung gefunden: Die Einschränkungen (kein gleiches Land, höchstens " +
                $"{LigaphasenBelegung.MAX_GEGNER_JE_LAND} Gegner aus demselben Land, konfliktfreie Spieltage) " +
                "sind mit dieser Topf-Besetzung nicht erfüllbar.");

        return paarungen
            .Select((p, i) => Paarung(i + 1, teilnehmer[p.Heim].Slot, teilnehmer[p.Gast].Slot))
            .ToList();
    }

    // Innerhalb eines Topfs braucht jedes Team einen Gastgeber und einen Gast aus einem anderen Land — mehr als
    // die Hälfte eines Topfs aus einem Land ist daher nie auslosbar. Vorab prüfen, statt erst nach erfolgloser
    // Suche mit einer unspezifischen Meldung zu scheitern.
    private static void LandverteilungPruefen(List<List<TeilnehmerSlot>> lostoepfe)
    {
        for (int i = 0; i < lostoepfe.Count; i++)
        {
            var topf = lostoepfe[i];
            int groessteGruppe = topf.Where(t => t.LandId.HasValue).GroupBy(t => t.LandId)
                .Select(g => g.Count()).DefaultIfEmpty(0).Max();
            if (groessteGruppe > topf.Count / 2)
                throw new InvalidOperationException(
                    $"Topf {i + 1} enthält {groessteGruppe} von {topf.Count} Mannschaften aus demselben Land. " +
                    "Mit der Regel 'Kein gleiches Land' darf höchstens die Hälfte eines Topfs aus einem Land kommen.");
        }
    }

    // ── Fehlermeldung ─────────────────────────────────────────────────────────

    private static InvalidOperationException KeineGueltigeAuslosung(RegelKontext regeln) =>
        new($"Keine gültige Auslosung gefunden. Die Einschränkungen ({regeln.EinschraenkungenText}) " +
            "können mit den gewählten Mannschaften nicht erfüllt werden.");

    // ── Heimrecht und Ergebnisse ──────────────────────────────────────────────

    // Legt je Paar das Heimrecht fest und nummeriert die Paarungen ab 1 durch.
    private static List<AuslosungsErgebnis> PaareZuErgebnissen(
        IEnumerable<(TeilnehmerSlot, TeilnehmerSlot)> paare, RegelKontext regeln, Random zufall) =>
        paare
            .Select((paar, i) =>
            {
                var (heim, auswaerts) = HeimrechtFestlegen(paar.Item1, paar.Item2, regeln, zufall);
                return Paarung(i + 1, heim, auswaerts);
            })
            .ToList();

    // Heimrecht nach Setzliste (#331): das besser eingestufte Team hat Heimrecht — bei Hin- und Rückspiel im
    // Rückspiel (UEFA-Praxis, z. B. Gruppensieger im Nations-League-Viertelfinale). Das Rückspiel entsteht
    // gespiegelt aus dem Hinspiel, der Bessere steht hier also auswärts.
    // Heimrecht für Unterklassige (#393): das Team der tieferen Spielklasse hat Heimrecht, bei Hin- und Rückspiel
    // ebenso im Rückspiel; sonst entscheidet das Los.
    // Heimrecht für den unteren Topf (#470): das Team des hinteren Topfs hat Heimrecht (DFB-Pokal 1. Runde); zwei
    // Teams desselben Topfs (Überhang) — das Los.
    // Ohne zufall (Ziehung von Hand, #482) ersetzt die Ziehung das Los: a, das zuerst gezogene Team, spielt zu Hause.
    internal static (TeilnehmerSlot Heim, TeilnehmerSlot Auswaerts) HeimrechtFestlegen(
        TeilnehmerSlot a, TeilnehmerSlot b, RegelKontext regeln, Random? zufall)
    {
        if (regeln.HeimrechtFuerUnterenTopf && a.TopfRang != b.TopfRang)
            return a.TopfRang > b.TopfRang ? MitHeimrecht(a, b, regeln.MitRueckspiel) : MitHeimrecht(b, a, regeln.MitRueckspiel);
        if (regeln.HeimrechtFuerUnterenTopf)
            return zufall?.Next(2) == 0 ? (b, a) : (a, b);

        if (regeln.HeimrechtFuerUnterklassige is { } spielklassen)
        {
            if (spielklassen.HatHeimrechtGegen(a.SpielendeMannschaftId, b.SpielendeMannschaftId))
                return MitHeimrecht(a, b, regeln.MitRueckspiel);
            if (spielklassen.HatHeimrechtGegen(b.SpielendeMannschaftId, a.SpielendeMannschaftId))
                return MitHeimrecht(b, a, regeln.MitRueckspiel);
            return zufall?.Next(2) == 0 ? (b, a) : (a, b);
        }

        if (regeln.HeimrechtNachSetzliste)
        {
            int vergleich = Rangfolge(a, b);
            if (vergleich != 0)
            {
                var (besser, schlechter) = vergleich < 0 ? (a, b) : (b, a);
                return MitHeimrecht(besser, schlechter, regeln.MitRueckspiel);
            }
        }

        if (regeln.HeimrechtNachLos && zufall?.Next(2) == 0)
            return (b, a);
        return (a, b);
    }

    // Heimrecht heißt bei Hin- und Rückspiel: das Rückspiel zu Hause — gelost wird das Hinspiel.
    private static (TeilnehmerSlot Heim, TeilnehmerSlot Auswaerts) MitHeimrecht(
        TeilnehmerSlot mitHeimrecht, TeilnehmerSlot gegner, bool mitRueckspiel) =>
        mitRueckspiel ? (gegner, mitHeimrecht) : (mitHeimrecht, gegner);

    // Kleiner = besser: höherer Topf, dann besserer Gruppenplatz, dann frühere Setzposition. Die Setzposition ist
    // nur die Reihenfolge in Topf 1 (#563) und entscheidet deshalb erst zuletzt — ein Gruppensieger bleibt besser
    // als ein Zweiter, der zufällig vor ihm im Topf liegt.
    private static int Rangfolge(TeilnehmerSlot a, TeilnehmerSlot b)
    {
        int vergleich = a.TopfRang.CompareTo(b.TopfRang);
        if (vergleich != 0) return vergleich;
        vergleich = (a.Gruppenplatz ?? int.MaxValue).CompareTo(b.Gruppenplatz ?? int.MaxValue);
        if (vergleich != 0) return vergleich;
        return (a.SetzPosition ?? int.MaxValue).CompareTo(b.SetzPosition ?? int.MaxValue);
    }

    private static AuslosungsErgebnis Paarung(int nr, TeilnehmerSlot heim, TeilnehmerSlot auswaerts) =>
        new()
        {
            PaarungsNummer           = nr,
            HeimMannschaftId         = heim.MannschaftId,
            AuswaertsMannschaftId    = auswaerts.MannschaftId,
            HeimPlatzhalterName      = heim.PlatzhalterName,
            AuswaertsPlatzhalterName = auswaerts.PlatzhalterName
        };

    private static AuslosungsErgebnis Freilos(int nr, TeilnehmerSlot slot) =>
        new()
        {
            PaarungsNummer      = nr,
            HeimMannschaftId    = slot.MannschaftId,
            HeimPlatzhalterName = slot.PlatzhalterName,
            IstFreilos          = true
        };
}
