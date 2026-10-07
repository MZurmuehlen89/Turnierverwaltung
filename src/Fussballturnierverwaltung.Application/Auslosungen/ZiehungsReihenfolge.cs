using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Ein einzelner Ziehungsschritt für den TV-Modus: eine Mannschaft aus einem Lostopf, die einem
/// bereits feststehenden Ziel (Gruppe/Paarung) zugeordnet ist. Das Endergebnis steht durch AusfuehrenAsync
/// schon fest (holistischer Zuordnungsalgorithmus) — hier wird nur die Reihenfolge/Dramaturgie der
/// Enthüllung bestimmt.</summary>
public sealed record ZiehungsSchritt(
    LostopfEintrag Eintrag, int TopfId, string TopfName, int ZielNummer,
    bool IstGruppenmodus, bool? IstHeim, int? Position, bool IstFreilos);

/// <summary>Legt fest, in welcher Reihenfolge der TV-Modus eine feststehende Auslosung enthüllt.</summary>
public static class ZiehungsReihenfolge
{
    /// <summary>Baut die Ziehungsreihenfolge für den TV-Modus (Issue #5) auf: analog einer echten Auslosung
    /// wird ein Lostopf vollständig leergezogen, bevor der nächste an die Reihe kommt (Topf-Reihenfolge nach
    /// <see cref="Lostopf.Reihenfolge"/>). Bei Gruppenphase wird zusätzlich pro Gruppe eine von der
    /// Ziehungsreihenfolge unabhängige Position (1..Gruppengröße) verlost — analog historischer
    /// Auslosungsverfahren mit separater Positions-Kugel. Innerhalb eines einzelnen Topfes wird die
    /// Ziehungsreihenfolge gleichmäßig auf alle Zielgruppen verteilt (reihum eine Gruppe nach der anderen,
    /// siehe <see cref="ProGruppeGleichmaessigVerteilen"/>), damit nicht z.B. Gruppe A bereits ihre 2.
    /// Mannschaft aus diesem Topf erhält, während Gruppe B noch gar keine hat — die Umsortierung bleibt aber
    /// strikt auf den jeweiligen Topf beschränkt, ein bereits begonnener Topf wird also weiterhin vollständig
    /// geleert, bevor der nächste beginnt.
    /// Paarungen (K.-o.-Runde) werden dagegen wie beim DFB-Pokal Paarung für Paarung gezogen — erst die
    /// Heim-, dann die Auswärtsmannschaft, bei zwei Töpfen also abwechselnd aus beiden (#408).</summary>
    public static List<ZiehungsSchritt> Aufbauen(
        IEnumerable<Lostopf> lostoepfe, IEnumerable<AuslosungsErgebnis> ergebnisse)
    {
        bool istGruppenmodus = ergebnisse.Any() &&
            ergebnisse.All(e => !e.IstFreilos && e.AuswaertsMannschaftId == null && e.AuswaertsPlatzhalterName == null);

        // Position = tatsächlicher Index des Teams innerhalb seiner Gruppe in `ergebnisse`, NICHT
        // zufällig gewürfelt: SpielplanService.GenerierenAusAuslosungAsync baut den Spielplan (bei
        // Round-Robin/Berger-Rotationsschema reihenfolgeabhängig) exakt aus dieser Reihenfolge —
        // die im TV-Modus gezeigte Position muss mit der tatsächlich gespeicherten übereinstimmen,
        // sonst zeigt die Ziehung eine Position, die mit dem späteren Spielplan nichts zu tun hat.
        var positionProErgebnis = ergebnisse
            .GroupBy(e => e.PaarungsNummer)
            .SelectMany(g => g.Select((e, index) => (Ergebnis: e, Position: index + 1)))
            .ToDictionary(x => x.Ergebnis, x => x.Position);

        bool PasstZuHeim(AuslosungsErgebnis e, LostopfEintrag eintrag) => eintrag.MannschaftId.HasValue
            ? e.HeimMannschaftId == eintrag.MannschaftId
            : e.HeimPlatzhalterName == eintrag.PlatzhalterName;
        bool PasstZuAuswaerts(AuslosungsErgebnis e, LostopfEintrag eintrag) => eintrag.MannschaftId.HasValue
            ? e.AuswaertsMannschaftId == eintrag.MannschaftId
            : e.AuswaertsPlatzhalterName == eintrag.PlatzhalterName;

        var schritte = new List<ZiehungsSchritt>();
        foreach (var topf in lostoepfe.OrderBy(l => l.Reihenfolge))
        {
            var topfSchritte = new List<ZiehungsSchritt>();
            var teamsGemischt = topf.Eintraege.OrderBy(_ => Random.Shared.Next()).ToList();
            foreach (var eintrag in teamsGemischt)
            {
                var treffer = ergebnisse.FirstOrDefault(e => PasstZuHeim(e, eintrag) || PasstZuAuswaerts(e, eintrag));
                if (treffer is null) continue;

                if (istGruppenmodus)
                {
                    int position = positionProErgebnis[treffer];
                    topfSchritte.Add(new ZiehungsSchritt(eintrag, topf.Id, topf.Name, treffer.PaarungsNummer,
                        IstGruppenmodus: true, IstHeim: null, Position: position, IstFreilos: false));
                }
                else
                {
                    bool istHeim = PasstZuHeim(treffer, eintrag);
                    topfSchritte.Add(new ZiehungsSchritt(eintrag, topf.Id, topf.Name, treffer.PaarungsNummer,
                        IstGruppenmodus: false, IstHeim: istHeim, Position: null, IstFreilos: treffer.IstFreilos));
                }
            }

            schritte.AddRange(istGruppenmodus ? ProGruppeGleichmaessigVerteilen(topfSchritte) : topfSchritte);
        }

        return istGruppenmodus
            ? schritte
            : schritte.OrderBy(s => s.ZielNummer).ThenBy(s => s.IstFreilos || s.IstHeim == true ? 0 : 1).ToList();
    }

    /// <summary>Verteilt die Ziehungsschritte EINES Topfes gleichmäßig auf alle Zielgruppen: reihum wird aus
    /// jeder Gruppen-Warteschlange (in zufälliger Gruppen-Reihenfolge je Runde) je ein Schritt entnommen, bis
    /// alle Warteschlangen leer sind. Verhindert, dass innerhalb eines Topfes eine Gruppe bereits mehrere
    /// Mannschaften erhält, während eine andere noch leer ausgeht.</summary>
    private static List<ZiehungsSchritt> ProGruppeGleichmaessigVerteilen(List<ZiehungsSchritt> topfSchritte)
    {
        var proGruppe = topfSchritte
            .GroupBy(s => s.ZielNummer)
            .Select(g => new Queue<ZiehungsSchritt>(g.OrderBy(_ => Random.Shared.Next())))
            .ToList();

        var ergebnis = new List<ZiehungsSchritt>(topfSchritte.Count);
        bool weitereRunde = true;
        while (weitereRunde)
        {
            weitereRunde = false;
            foreach (var queue in proGruppe.OrderBy(_ => Random.Shared.Next()))
            {
                if (queue.Count == 0) continue;
                ergebnis.Add(queue.Dequeue());
                weitereRunde = true;
            }
        }
        return ergebnis;
    }
}
