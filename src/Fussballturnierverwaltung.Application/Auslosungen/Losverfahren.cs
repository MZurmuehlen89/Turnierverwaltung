using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Auslosungen;

// TopfRang: 0 = erster Topf. Gruppe/Gruppenplatz: Herkunft aus einer vorangegangenen Gruppenphase — aus dem
// Platzhalternamen („1. Gruppe A“) oder bei echten Mannschaften aus deren Gruppen-Auslosung (#331).
// VerbandId: Kontinentalverband des Landes — nur bei echten Mannschaften bekannt (#356).
internal record TeilnehmerSlot(
    int? MannschaftId, int? LandId, string? PlatzhalterName,
    int TopfRang = 0, int? SetzPosition = null, string? Gruppe = null, int? Gruppenplatz = null,
    int? VerbandId = null)
{
    public static TeilnehmerSlot AusEintrag(
        LostopfEintrag e, int topfRang, IReadOnlyDictionary<int, string> gruppeJeMannschaft)
    {
        if (e.MannschaftId is { } id)
            return new(id, e.Mannschaft?.LandId, null, topfRang, e.SetzPosition,
                gruppeJeMannschaft.GetValueOrDefault(id), VerbandId: e.Mannschaft?.Land?.VerbandId);

        var herkunft = GruppenHerkunft.AusPlatzhalter(e.PlatzhalterName);
        return new(null, null, e.PlatzhalterName, topfRang, e.SetzPosition, herkunft?.Gruppe, herkunft?.Platz);
    }
}

/// <summary>Die bei dieser Auslosung tatsächlich wirksamen Regeln (siehe <see cref="AuslosungsRegelKompatibilitaet"/>).</summary>
public sealed record RegelKontext(
    bool KeinGleichesLand, bool KeineGleicheGruppe, bool HeimrechtNachLos, bool HeimrechtNachSetzliste,
    bool FreilosMoeglich, bool Konfoederationsverteilung, bool MitRueckspiel)
{
    // Paarweise Regeln. Die Konföderationsverteilung zählt je Gruppe und passt nicht in dieses Schema — sie
    // wirkt nur in der Gruppenauslosung (GruppenAuslosung).
    internal bool Verbietet(TeilnehmerSlot a, TeilnehmerSlot b) =>
        PaarungsRegeln.VerletzteRegel(
            KeinGleichesLand, KeineGleicheGruppe, new(a.LandId, a.Gruppe), new(b.LandId, b.Gruppe)) is not null;

    public string EinschraenkungenText =>
        string.Join(", ", new[]
        {
            KeinGleichesLand ? "'Kein gleiches Land'" : null,
            KeineGleicheGruppe ? "'Keine gleiche Gruppe'" : null,
            Konfoederationsverteilung ? "'Konföderationsverteilung'" : null
        }.OfType<string>()) is { Length: > 0 } text ? text : "keine";
}

/// <summary>Die Losverfahren aller Auslosungsarten: verteilt die Lostopf-Einträge auf Gruppen oder bildet daraus
/// Paarungen. Zustandslos und ohne Datenbankbezug — für reproduzierbare Ergebnisse ein <see cref="Random"/> mit
/// festem Seed übergeben.</summary>
public static class Losverfahren
{
    /// <param name="regeln">Die wirksamen Regeln der Auslosung.</param>
    /// <param name="gruppeJeMannschaft">Gruppe („A“, „B“ …) je Mannschafts-Id aus der vorangegangenen Gruppenphase —
    /// nur für „Keine gleiche Gruppe“ nötig, sonst leer.</param>
    public static List<AuslosungsErgebnis> Ausloesen(
        Auslosung auslosung, RegelKontext regeln, IReadOnlyDictionary<int, string> gruppeJeMannschaft, Random zufall)
    {
        var toepfe = auslosung.Lostoepfe
            .OrderBy(l => l.Reihenfolge)
            .Select((l, rang) => l.Eintraege.Select(e => TeilnehmerSlot.AusEintrag(e, rang, gruppeJeMannschaft)).ToList())
            .ToList();

        // Gruppenphase und Liga ordnen die Teilnehmer Gruppen zu (Liga = eine Gruppe), alle anderen Rundenarten
        // bilden Paarungen.
        return auslosung.TurnierRunde?.Rundenart is Rundenart.Gruppenphase or Rundenart.Liga
            ? GruppenVerteilen(auslosung, toepfe, regeln, zufall)
            : PaarungenBilden(auslosung.Auslosungsart, toepfe, regeln, zufall);
    }

    // ── Gruppenphase und Liga (Ergebnis: je Teilnehmer eine Gruppe) ───────────

    private static List<AuslosungsErgebnis> GruppenVerteilen(
        Auslosung auslosung, List<List<TeilnehmerSlot>> toepfe, RegelKontext regeln, Random zufall)
    {
        if (auslosung.TurnierRunde?.Rundenart == Rundenart.Liga)
            return LigaAusloesen(toepfe, zufall);

        int gruppenAnzahl = GruppenAnzahlOderFehler(auslosung);
        return auslosung.Auslosungsart switch
        {
            Auslosungsart.NachToepfen   => GruppenphaseNachToepfenAusloesen(toepfe, gruppenAnzahl, regeln, zufall),
            Auslosungsart.NachSetzliste => GruppenphaseNachSetzlisteAusloesen(toepfe, gruppenAnzahl, regeln, zufall),
            _                           => GruppenAusloesen([], [toepfe.SelectMany(t => t).ToList()], gruppenAnzahl, regeln, zufall)
        };
    }

    private static int GruppenAnzahlOderFehler(Auslosung auslosung) =>
        auslosung.TurnierRunde?.GruppenAnzahl
            ?? throw new InvalidOperationException(
                "Gruppenphase-Auslosung: Die Runde hat keine Gruppenanzahl definiert.");

    // Liga: alle Teams bilden eine Gruppe, gelost wird nur die Reihenfolge darin — sie ist die Schlüsselzahl, aus
    // der der Spielplan entsteht (Berger-Schema, reihenfolgeabhängig). Ohne Mischen ergab jede Auslosung dieselbe
    // Reihenfolge und damit denselben Spielplan (#474). Töpfe, Setzliste und Regeln wirken in der Liga nicht.
    private static List<AuslosungsErgebnis> LigaAusloesen(List<List<TeilnehmerSlot>> toepfe, Random zufall) =>
        GruppenZuErgebnisse([toepfe.SelectMany(t => t).OrderBy(_ => zufall.Next()).ToList()]);

    // Jeder Topf wird vollständig auf die Gruppen verteilt, bevor der nächste drankommt.
    private static List<AuslosungsErgebnis> GruppenphaseNachToepfenAusloesen(
        List<List<TeilnehmerSlot>> toepfe, int gruppenAnzahl, RegelKontext regeln, Random zufall) =>
        GruppenAusloesen([], toepfe, gruppenAnzahl, regeln, zufall);

    // Gesetzte Teams (mit Setzposition) führen je eine Gruppe an, die übrigen werden topfweise verteilt.
    private static List<AuslosungsErgebnis> GruppenphaseNachSetzlisteAusloesen(
        List<List<TeilnehmerSlot>> toepfe, int gruppenAnzahl, RegelKontext regeln, Random zufall)
    {
        var gesetzte = toepfe
            .SelectMany(t => t)
            .Where(s => s.SetzPosition.HasValue)
            .OrderBy(s => s.SetzPosition)
            .Take(gruppenAnzahl)
            .ToList();

        // Ungesetzte Teams werden topfweise verteilt (ein Topf wird komplett auf die Gruppen aufgeteilt, bevor der
        // nächste Topf drankommt) — analog zu GruppenphaseNachToepfenAusloesen (siehe Issue #5-Diskussion).
        return GruppenAusloesen(
            gesetzte, toepfe.Select(t => t.Where(s => !s.SetzPosition.HasValue).ToList()), gruppenAnzahl, regeln, zufall);
    }

    // Gesetzte Teams führen je eine Gruppe an, die Töpfe werden danach nacheinander gezogen — Ablauf und
    // Regelprüfung siehe GruppenAuslosung (#356).
    private static List<AuslosungsErgebnis> GruppenAusloesen(
        IReadOnlyList<TeilnehmerSlot> gesetzte, IEnumerable<IReadOnlyList<TeilnehmerSlot>> toepfe,
        int gruppenAnzahl, RegelKontext regeln, Random zufall)
    {
        var slots       = new List<TeilnehmerSlot>(gesetzte);
        var topfIndizes = new List<IReadOnlyList<int>>();
        foreach (var topf in toepfe)
        {
            topfIndizes.Add([.. Enumerable.Range(slots.Count, topf.Count)]);
            slots.AddRange(topf);
        }

        var auslosung = new GruppenAuslosung(
            gruppenAnzahl, [.. slots.Select(s => s.VerbandId)], regeln.Konfoederationsverteilung,
            (a, b) => regeln.Verbietet(slots[a], slots[b]), zufall);
        for (int i = 0; i < gesetzte.Count; i++)
            auslosung.Setzen(i, i);

        var gruppeJeSlot = auslosung.Verteilen(topfIndizes)
            ?? throw new InvalidOperationException(
                $"Keine gültige Gruppenauslosung gefunden. Die Einschränkungen ({regeln.EinschraenkungenText}) " +
                "können mit den gewählten Mannschaften nicht erfüllt werden.");

        var gruppen = Enumerable.Range(0, gruppenAnzahl).Select(_ => new List<TeilnehmerSlot>()).ToList();
        for (int i = 0; i < slots.Count; i++)
            gruppen[gruppeJeSlot[i]].Add(slots[i]);
        return GruppenZuErgebnisse(gruppen);
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

    // ── Paarungen ─────────────────────────────────────────────────────────────

    private static List<AuslosungsErgebnis> PaarungenBilden(
        Auslosungsart auslosungsart, List<List<TeilnehmerSlot>> toepfe, RegelKontext regeln, Random zufall) =>
        auslosungsart switch
        {
            Auslosungsart.NachToepfen or
            Auslosungsart.GruppensiegerGegenGruppenzweite => NachToepfenAusloesen(toepfe, regeln, zufall),
            Auslosungsart.NachSetzliste                   => NachSetzlisteAusloesen(toepfe, regeln, zufall),
            Auslosungsart.TopfZuordnung                   => TopfZuordnungAusloesen(toepfe, regeln, zufall),
            _                                             => ZufaelligAusloesen(toepfe, regeln, zufall)
        };

    private static List<AuslosungsErgebnis> ZufaelligAusloesen(
        List<List<TeilnehmerSlot>> toepfe, RegelKontext regeln, Random zufall)
    {
        var slots = toepfe.SelectMany(t => t).ToList();
        TeilnehmerSlot? freilosSlot = null;

        if (regeln.FreilosMoeglich && slots.Count % 2 == 1)
        {
            int freilosIdx = zufall.Next(slots.Count);
            freilosSlot = slots[freilosIdx];
            slots.RemoveAt(freilosIdx);
        }

        var ergebnisse = PaareZuErgebnissen(PaarenMitEinschraenkungen(slots, regeln, zufall), regeln, zufall);
        if (freilosSlot is not null)
            ergebnisse.Add(Freilos(ergebnisse.Count + 1, freilosSlot));
        return ergebnisse;
    }

    // Topf 1 gegen Topf 2, Topf 3 gegen Topf 4 … — ein überzähliger letzter Topf bleibt ohne Paarung.
    private static List<AuslosungsErgebnis> NachToepfenAusloesen(
        List<List<TeilnehmerSlot>> toepfe, RegelKontext regeln, Random zufall) =>
        PaareZuErgebnissen(
            Enumerable.Range(0, toepfe.Count / 2)
                .SelectMany(i => ZweiTopfPaaren(toepfe[2 * i], toepfe[2 * i + 1], regeln, zufall)),
            regeln, zufall);

    // Gesetzte (nach Setzposition) gegen Ungesetzte — dieselbe Zwei-Topf-Paarung wie „Nach Töpfen“, damit auch
    // hier alle Einschränkungen und Heimrecht-Regeln greifen (bis #331 ignorierte „Nach Setzliste“ sie alle).
    private static List<AuslosungsErgebnis> NachSetzlisteAusloesen(
        List<List<TeilnehmerSlot>> toepfe, RegelKontext regeln, Random zufall)
    {
        var alle = toepfe.SelectMany(t => t).ToList();
        var gesetzte   = alle.Where(s => s.SetzPosition.HasValue).OrderBy(s => s.SetzPosition).ToList();
        var ungesetzte = alle.Where(s => !s.SetzPosition.HasValue).ToList();

        return PaareZuErgebnissen(ZweiTopfPaaren(gesetzte, ungesetzte, regeln, zufall), regeln, zufall);
    }

    // ── TopfZuordnung (UCL-Ligaphase) ─────────────────────────────────────────

    // Die Länder-Regeln (kein gleiches Land, höchstens 2 Gegner aus demselben Land) sind fest an die Regel
    // "Kein gleiches Land" gekoppelt (Nutzerentscheidung Issue #347, keine eigene AuslosungsRegelArt).
    private static List<AuslosungsErgebnis> TopfZuordnungAusloesen(
        List<List<TeilnehmerSlot>> lostoepfe, RegelKontext regeln, Random zufall)
    {
        var keinGleichesLand = regeln.KeinGleichesLand;
        if (keinGleichesLand) LandverteilungPruefen(lostoepfe);

        var teilnehmer = lostoepfe.SelectMany((topf, i) => topf.Select(slot => (Slot: slot, Topf: i))).ToList();
        var paarungen  = new LigaphasenAuslosung(
                teilnehmer.Select(t => t.Topf).ToList(),
                teilnehmer.Select(t => t.Slot.LandId).ToList(),
                keinGleichesLand,
                zufall)
            .Ausloesen()
            ?? throw new InvalidOperationException(
                "Keine gültige Topf-Zuordnung gefunden: Die Einschränkungen (kein gleiches Land, höchstens " +
                $"{LigaphasenAuslosung.MAX_GEGNER_JE_LAND} Gegner aus demselben Land, konfliktfreie Spieltage) " +
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

    // ── Paarungssuche mit Einschränkungen ─────────────────────────────────────

    // Obergrenze für die Backtracking-Schritte — eine unerfüllbare Kombination bricht so zuverlässig ab, statt
    // bei großen Töpfen exponentiell lange zu suchen.
    private const int MAX_SUCHSCHRITTE = 200_000;

    // Backtracking statt blindem Neu-Mischen (#331): findet eine Paarung garantiert, sofern eine existiert — mit
    // „Kein gleiches Land“ und „Keine gleiche Gruppe“ gleichzeitig wurde die Trefferquote zufälliger Versuche sonst
    // zu klein. Die Kandidaten werden vorab gemischt, die Auslosung bleibt zufällig.
    private static List<(TeilnehmerSlot, TeilnehmerSlot)> PaarenMitEinschraenkungen(
        List<TeilnehmerSlot> slots, RegelKontext regeln, Random zufall)
    {
        var gemischt = slots.OrderBy(_ => zufall.Next()).ToList();
        var vergeben = new bool[gemischt.Count];
        var paare    = new List<(TeilnehmerSlot, TeilnehmerSlot)>();
        int schritte = 0;

        bool Suchen()
        {
            // Bei ungerader Anzahl ohne Freilos bleibt ein Team übrig — wie bisher ohne Paarung.
            if (vergeben.Count(v => !v) < 2) return true;
            int erster = Array.IndexOf(vergeben, false);
            if (++schritte > MAX_SUCHSCHRITTE) return false;

            vergeben[erster] = true;
            for (int j = erster + 1; j < gemischt.Count; j++)
            {
                if (vergeben[j] || regeln.Verbietet(gemischt[erster], gemischt[j])) continue;
                vergeben[j] = true;
                paare.Add((gemischt[erster], gemischt[j]));
                if (Suchen()) return true;
                paare.RemoveAt(paare.Count - 1);
                vergeben[j] = false;
            }
            vergeben[erster] = false;
            return false;
        }

        if (Suchen()) return paare;
        throw KeineGueltigeAuslosung(regeln);
    }

    // Paart topf1[i] mit je einem anderen Team aus topf2 (bei ungleicher Größe nur die ersten Min(n1, n2)).
    private static List<(TeilnehmerSlot, TeilnehmerSlot)> ZweiTopfPaaren(
        List<TeilnehmerSlot> topf1, List<TeilnehmerSlot> topf2, RegelKontext regeln, Random zufall)
    {
        var kandidaten = topf2.OrderBy(_ => zufall.Next()).ToList();
        int anzahl     = Math.Min(topf1.Count, kandidaten.Count);
        var zuordnung  = new int[anzahl];
        var vergeben   = new bool[kandidaten.Count];
        int schritte   = 0;

        bool Suchen(int i)
        {
            if (i == anzahl) return true;
            if (++schritte > MAX_SUCHSCHRITTE) return false;

            for (int j = 0; j < kandidaten.Count; j++)
            {
                if (vergeben[j] || regeln.Verbietet(topf1[i], kandidaten[j])) continue;
                vergeben[j] = true;
                zuordnung[i] = j;
                if (Suchen(i + 1)) return true;
                vergeben[j] = false;
            }
            return false;
        }

        if (Suchen(0))
            return Enumerable.Range(0, anzahl).Select(i => (topf1[i], kandidaten[zuordnung[i]])).ToList();
        throw KeineGueltigeAuslosung(regeln);
    }

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
    private static (TeilnehmerSlot Heim, TeilnehmerSlot Auswaerts) HeimrechtFestlegen(
        TeilnehmerSlot a, TeilnehmerSlot b, RegelKontext regeln, Random zufall)
    {
        if (regeln.HeimrechtNachSetzliste)
        {
            int vergleich = Rangfolge(a, b);
            if (vergleich != 0)
            {
                var (besser, schlechter) = vergleich < 0 ? (a, b) : (b, a);
                return regeln.MitRueckspiel ? (schlechter, besser) : (besser, schlechter);
            }
        }

        if (regeln.HeimrechtNachLos && zufall.Next(2) == 0)
            return (b, a);
        return (a, b);
    }

    // Kleiner = besser: höherer Topf, dann bessere Setzposition, dann besserer Gruppenplatz.
    private static int Rangfolge(TeilnehmerSlot a, TeilnehmerSlot b)
    {
        int vergleich = a.TopfRang.CompareTo(b.TopfRang);
        if (vergleich != 0) return vergleich;
        vergleich = (a.SetzPosition ?? int.MaxValue).CompareTo(b.SetzPosition ?? int.MaxValue);
        if (vergleich != 0) return vergleich;
        return (a.Gruppenplatz ?? int.MaxValue).CompareTo(b.Gruppenplatz ?? int.MaxValue);
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
