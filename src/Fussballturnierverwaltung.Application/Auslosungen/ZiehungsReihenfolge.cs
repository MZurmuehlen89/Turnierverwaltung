using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Ein einzelner Ziehungsschritt für den TV-Modus: ein Eintrag aus einem Lostopf, der einem
/// bereits feststehenden Ziel (Gruppe/Paarung) zugeordnet ist. Das Endergebnis steht durch AusfuehrenAsync
/// schon fest (holistischer Zuordnungsalgorithmus) — hier wird nur die Reihenfolge/Dramaturgie der
/// Enthüllung bestimmt.</summary>
public sealed record ZiehungsSchritt(
    LostopfEintrag Eintrag, int TopfId, string TopfName, int ZielNummer,
    bool IstGruppenmodus, bool? IstHeim, int? Position, bool IstFreilos);

/// <summary>Legt fest, in welcher Reihenfolge der TV-Modus eine feststehende Auslosung enthüllt. Zustandslos —
/// für reproduzierbare Ergebnisse ein <see cref="Random"/> mit festem Seed übergeben.</summary>
public static class ZiehungsReihenfolge
{
    /// <summary>Baut die Ziehungsreihenfolge für den TV-Modus (Issue #5) auf: analog einer echten Auslosung
    /// wird ein Lostopf vollständig leergezogen, bevor der nächste an die Reihe kommt (Topf-Reihenfolge nach
    /// <see cref="Lostopf.Reihenfolge"/>). Bei Gruppenphase wird zusätzlich pro Gruppe eine von der
    /// Ziehungsreihenfolge unabhängige Position (1..Gruppengröße) gezeigt — analog historischer
    /// Auslosungsverfahren mit separater Positions-Kugel. Innerhalb eines einzelnen Topfes wird die
    /// Ziehungsreihenfolge gleichmäßig auf alle Zielgruppen verteilt (reihum eine Gruppe nach der anderen,
    /// siehe <see cref="ProGruppeGleichmaessigVerteilen"/>), damit nicht z.B. Gruppe A bereits ihre 2.
    /// Mannschaft aus diesem Topf erhält, während Gruppe B noch gar keine hat — die Umsortierung bleibt aber
    /// strikt auf den jeweiligen Topf beschränkt, ein bereits begonnener Topf wird also weiterhin vollständig
    /// geleert, bevor der nächste beginnt.
    /// Paarungen (K.-o.-Runde) werden dagegen wie beim DFB-Pokal Paarung für Paarung gezogen — erst die
    /// Heim-, dann die Auswärtsmannschaft, bei zwei Töpfen also abwechselnd aus beiden (#408).</summary>
    public static List<ZiehungsSchritt> Aufbauen(
        IEnumerable<Lostopf> lostoepfe, IEnumerable<AuslosungsErgebnis> ergebnisse, Random zufall)
    {
        var toepfe        = lostoepfe.OrderBy(l => l.Reihenfolge).ToList();
        var ergebnisListe = ergebnisse.ToList();

        return IstGruppenmodus(ergebnisListe)
            ? GruppenSchritte(toepfe, ergebnisListe, zufall)
            : PaarungsSchritte(toepfe, ergebnisListe, zufall);
    }

    /// <summary>Eine Gruppenauslosung speichert je Mannschaft ein Ergebnis ohne Gegner, die Paarungsnummer ist
    /// die Gruppe.</summary>
    private static bool IstGruppenmodus(List<AuslosungsErgebnis> ergebnisse) =>
        ergebnisse.Count > 0 &&
        ergebnisse.All(e => !e.IstFreilos && e.AuswaertsMannschaftId == null && e.AuswaertsPlatzhalterName == null);

    // ── Gruppenphase ──────────────────────────────────────────────────────────

    private static List<ZiehungsSchritt> GruppenSchritte(
        List<Lostopf> toepfe, List<AuslosungsErgebnis> ergebnisse, Random zufall)
    {
        var positionJeErgebnis = PositionJeErgebnis(ergebnisse);

        return toepfe
            .SelectMany(topf => ProGruppeGleichmaessigVerteilen(
                GezogeneEintraege(topf, ergebnisse, zufall)
                    .Select(z => new ZiehungsSchritt(z.Eintrag, topf.Id, topf.Name, z.Ergebnis.PaarungsNummer,
                        IstGruppenmodus: true, IstHeim: null, Position: positionJeErgebnis[z.Ergebnis], IstFreilos: false))
                    .ToList(),
                zufall))
            .ToList();
    }

    /// <summary>Position = tatsächlicher Index des Teams innerhalb seiner Gruppe in den Ergebnissen, NICHT
    /// zufällig gewürfelt: SpielplanService.GenerierenAusAuslosungAsync baut den Spielplan (bei
    /// Round-Robin/Berger-Rotationsschema reihenfolgeabhängig) exakt aus dieser Reihenfolge — die im TV-Modus
    /// gezeigte Position muss mit der tatsächlich gespeicherten übereinstimmen, sonst zeigt die Ziehung eine
    /// Position, die mit dem späteren Spielplan nichts zu tun hat.</summary>
    private static Dictionary<AuslosungsErgebnis, int> PositionJeErgebnis(List<AuslosungsErgebnis> ergebnisse) =>
        ergebnisse
            .GroupBy(e => e.PaarungsNummer)
            .SelectMany(g => g.Select((e, index) => (Ergebnis: e, Position: index + 1)))
            .ToDictionary(x => x.Ergebnis, x => x.Position);

    /// <summary>Verteilt die Ziehungsschritte EINES Topfes gleichmäßig auf alle Zielgruppen: reihum wird aus
    /// jeder Gruppen-Warteschlange (in zufälliger Gruppen-Reihenfolge je Runde) je ein Schritt entnommen, bis
    /// alle Warteschlangen leer sind. Verhindert, dass innerhalb eines Topfes eine Gruppe bereits mehrere
    /// Mannschaften erhält, während eine andere noch leer ausgeht.</summary>
    private static List<ZiehungsSchritt> ProGruppeGleichmaessigVerteilen(List<ZiehungsSchritt> topfSchritte, Random zufall)
    {
        var proGruppe = topfSchritte
            .GroupBy(s => s.ZielNummer)
            .Select(g => new Queue<ZiehungsSchritt>(g.OrderBy(_ => zufall.Next())))
            .ToList();

        var ergebnis = new List<ZiehungsSchritt>(topfSchritte.Count);
        bool weitereRunde = true;
        while (weitereRunde)
        {
            weitereRunde = false;
            foreach (var queue in proGruppe.OrderBy(_ => zufall.Next()))
            {
                if (queue.Count == 0) continue;
                ergebnis.Add(queue.Dequeue());
                weitereRunde = true;
            }
        }
        return ergebnis;
    }

    // ── Paarungen (K.-o.-Runde) ───────────────────────────────────────────────

    private static List<ZiehungsSchritt> PaarungsSchritte(
        List<Lostopf> toepfe, List<AuslosungsErgebnis> ergebnisse, Random zufall) =>
        toepfe
            .SelectMany(topf => GezogeneEintraege(topf, ergebnisse, zufall)
                .Select(z => new ZiehungsSchritt(z.Eintrag, topf.Id, topf.Name, z.Ergebnis.PaarungsNummer,
                    IstGruppenmodus: false, IstHeim: PasstZuHeim(z.Ergebnis, z.Eintrag), Position: null,
                    IstFreilos: z.Ergebnis.IstFreilos)))
            .OrderBy(s => s.ZielNummer)
            .ThenBy(s => s.IstFreilos || s.IstHeim == true ? 0 : 1)
            .ToList();

    // ── Zwischenstand ─────────────────────────────────────────────────────────

    /// <summary>Nach so vielen fertigen Paarungen zeigt die Ziehung einer K.-o.-Runde die Gesamtübersicht.</summary>
    public const int PAARUNGEN_JE_ZWISCHENSTAND = 8;

    /// <summary>Titel der Gesamtübersicht, die der TV-Modus nach dem Schritt <paramref name="index"/> einschiebt
    /// (#408) — <c>null</c>, wenn dort keine fällig ist. Fällig ist sie bei der Gruppenphase nach jedem geleerten
    /// Topf, bei Paarungen nach je <see cref="PAARUNGEN_JE_ZWISCHENSTAND"/> fertigen; nach dem letzten Schritt nie,
    /// dort folgt direkt das Ergebnis.</summary>
    public static string? ZwischenstandNach(IReadOnlyList<ZiehungsSchritt> schritte, int index)
    {
        if (index >= schritte.Count - 1) return null;

        var (schritt, naechster) = (schritte[index], schritte[index + 1]);
        if (schritt.IstGruppenmodus)
            return naechster.TopfId != schritt.TopfId ? $"{schritt.TopfName} komplett" : null;

        if (naechster.ZielNummer == schritt.ZielNummer) return null;

        int fertig = FertigePaarungen(schritte, index);
        return fertig % PAARUNGEN_JE_ZWISCHENSTAND == 0
            ? $"{fertig} von {schritte.Select(s => s.ZielNummer).Distinct().Count()} Paarungen gezogen"
            : null;
    }

    /// <summary>Eine Paarung ist fertig, sobald der nächste Schritt zu einer anderen gehört.</summary>
    private static int FertigePaarungen(IReadOnlyList<ZiehungsSchritt> schritte, int bisIndex) =>
        Enumerable.Range(0, bisIndex + 1).Count(i => schritte[i + 1].ZielNummer != schritte[i].ZielNummer);

    // ── Zuordnung Eintrag → Ergebnis ──────────────────────────────────────────

    /// <summary>Die Einträge eines Topfs in zufälliger Reihenfolge, jeweils mit dem Ergebnis, in dem sie
    /// stehen — Einträge ohne Ergebnis entfallen.</summary>
    private static IEnumerable<(LostopfEintrag Eintrag, AuslosungsErgebnis Ergebnis)> GezogeneEintraege(
        Lostopf topf, List<AuslosungsErgebnis> ergebnisse, Random zufall) =>
        topf.Eintraege
            .OrderBy(_ => zufall.Next())
            .Select(eintrag => (
                Eintrag: eintrag,
                Ergebnis: ergebnisse.FirstOrDefault(e => PasstZuHeim(e, eintrag) || PasstZuAuswaerts(e, eintrag))))
            .Where(z => z.Ergebnis is not null)
            .Select(z => (z.Eintrag, z.Ergebnis!));

    private static bool PasstZuHeim(AuslosungsErgebnis e, LostopfEintrag eintrag) => eintrag.MannschaftId.HasValue
        ? e.HeimMannschaftId == eintrag.MannschaftId
        : e.HeimPlatzhalterName == eintrag.PlatzhalterName;

    private static bool PasstZuAuswaerts(AuslosungsErgebnis e, LostopfEintrag eintrag) => eintrag.MannschaftId.HasValue
        ? e.AuswaertsMannschaftId == eintrag.MannschaftId
        : e.AuswaertsPlatzhalterName == eintrag.PlatzhalterName;
}
