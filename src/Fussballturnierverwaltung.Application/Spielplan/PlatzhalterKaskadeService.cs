using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Spielplan;

public class PlatzhalterKaskadeService(
    ITurnierRundeRepository rundeRepository,
    ISpielRepository spielRepository,
    IAuslosungRepository auslosungRepository,
    TabellenZwischenspeicher zwischenspeicher,
    PlatzhalterAufloesung aufloesung,
    SpielplanService spielplanService,
    FinaleAutomatik finaleAutomatik,
    ILogger<PlatzhalterKaskadeService> logger) : IPlatzhalterKaskadeService
{
    /// <summary>
    /// Ermittelt (reine Leseoperation, keine Schreibzugriffe) auf Spiel-Ebene, welche bereits
    /// aufgelösten Platzhalter-Zuordnungen nicht mehr zum aktuellen Stand ihrer Quellrunde(n) passen.
    /// Bewusst pro Spiel statt pro Runde — eine Runde kann mehrere unabhängige Platzhalter-Paarungen
    /// enthalten (z.B. zwei Halbfinal-Spiele aus zwei verschiedenen Gruppen), von denen nur eine
    /// betroffen ist; die andere darf inklusive ihres Ergebnisses nicht angefasst werden.
    /// <paramref name="zusaetzlichIgnorierteSpielIds"/> sind Spiele, deren Ergebnis (real oder nur
    /// hypothetisch für eine Vorschau) als bereits entfernt gilt. Runden werden in aufsteigender
    /// <see cref="TurnierRunde.Reihenfolge"/> geprüft — eine Quellrunde hat in diesem Domain-Modell
    /// immer eine niedrigere Reihenfolge als ihre Zielrunde, ein einziger Durchlauf reicht.
    /// </summary>
    public Task<KaskadenPlan> PlatzhalterKaskadePlanenAsync(
        int turnierInstanzId, IReadOnlySet<int> zusaetzlichIgnorierteSpielIds, CancellationToken ct = default)
        => zwischenspeicher.DurchlaufAsync(() => PlatzhalterKaskadePlanenOhneZwischenspeicherAsync(turnierInstanzId, zusaetzlichIgnorierteSpielIds, ct));

    private async Task<KaskadenPlan> PlatzhalterKaskadePlanenOhneZwischenspeicherAsync(
        int turnierInstanzId, IReadOnlySet<int> zusaetzlichIgnorierteSpielIds, CancellationToken ct)
    {
        var alleRunden = (await rundeRepository.AlleNachInstanzAsync(turnierInstanzId, ct))
            .OrderBy(r => r.Reihenfolge)
            .ToList();

        var ignoriert = new HashSet<int>(zusaetzlichIgnorierteSpielIds);
        var schritte  = new List<KaskadenSchritt>();

        foreach (var runde in alleRunden)
        {
            var auslosung = await auslosungRepository.FindenAbgeschlosseneNachRundeAsync(turnierInstanzId, runde.Id, ct);
            if (auslosung is null) continue;

            // IstFreilos ausgeschlossen: GenerierenAusPlatzhalterPaarungenAsync erzeugt dafür kein Spiel —
            // ohne denselben Filter hier würde die positionale Zuordnung unten um ein Freilos verschoben sein.
            var platzhalterEintraege = auslosung.Ergebnisse
                .Where(e => !e.IstFreilos && (e.HeimPlatzhalterName is not null || e.AuswaertsPlatzhalterName is not null))
                .OrderBy(e => e.PaarungsNummer)
                .ToList();
            if (platzhalterEintraege.Count == 0) continue;

            var alleRundenSpiele = await zwischenspeicher.RundenSpieleAsync(runde.Id, ct);

            // Eine Auslosung-basierte Platzhalter-Runde ohne ein einziges Spiel muss komplett neu
            // generiert werden — z.B. wenn die enge #125-Logik (FinaleAutomatischGenerierenWennBereitAsync)
            // das einzige, noch nicht gespielte Spiel bereits entfernt hat, weil ihre eigene
            // (Auslosung-unabhängige) Heuristik die Quelle als nicht mehr entschieden einstuft, aber
            // mangels erkanntem Nachfolger nicht selbst neu generiert. Kein Ergebnisverlust möglich,
            // da nichts vorhanden ist.
            if (alleRundenSpiele.Count == 0)
            {
                schritte.Add(new KaskadenRundeNeuGenerierenSchritt(runde.Id, runde.Name));
                continue;
            }

            // Issue #395: Gruppen-Auslosungen haben einen Eintrag pro Team, nicht pro Spiel — die
            // Eintrag-zu-Spiel-Zuordnung unten passt dort nicht. Stattdessen den Soll-Spielplan mit demselben
            // Berger-Plan wie bei der Generierung nachbauen und Spiel für Spiel vergleichen.
            if (runde.Rundenart == Rundenart.Gruppenphase)
            {
                var geplant = GruppenSpielplan.Planen(GruppenSpielplan.AusAuslosung(auslosung.Ergebnisse), runde.MitRueckspiel);
                var spieleNachNummer = alleRundenSpiele.OrderBy(s => s.Spielnummer).ToList();
                if (!GruppenSpielplan.PasstZu(geplant, spieleNachNummer))
                {
                    // Unbekannte Struktur (z.B. vor #395 ohne Platzhalter generierte gemischte Gruppe) — lieber
                    // nicht anfassen als falsche Spiele zurücksetzen.
                    logger.LogWarning(
                        "Kaskade: Spielplan der Gruppenphase '{Runde}' passt nicht zur Auslosung, Runde wird übersprungen.",
                        runde.Name);
                    continue;
                }

                for (int i = 0; i < geplant.Count; i++)
                {
                    var (heim, ausw) = (geplant[i].Heim, geplant[i].Auswaerts);
                    SpielPruefen(spieleNachNummer[i],
                        await SlotAufloesenAsync(heim), await SlotAufloesenAsync(ausw),
                        heim.PlatzhalterName, ausw.PlatzhalterName);
                }
                continue;
            }

            async Task<int?> SlotAufloesenAsync(GruppenSlot slot) =>
                slot.MannschaftId ?? (slot.PlatzhalterName is { } name
                    ? await aufloesung.NameAufloesenAsync(name, runde, alleRunden, ignoriert, ct)
                    : null);

            // Issue #346: in K.o.-Runden mit Rückspiel gehört zu jedem Auslosungs-Eintrag ein Hinspiel plus
            // dessen gespiegeltes Rückspiel — positional gezählt wird nur über die Hinspiele, sonst
            // verrutscht die Zuordnung ab der zweiten Paarung.
            bool paarweise = runde.MitRueckspiel &&
                runde.Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;
            var rueckspielJeHinspiel = paarweise ? KoPaarungen.RueckspielJeHinspiel(alleRundenSpiele) : [];
            var aktuelleSpiele = alleRundenSpiele
                .Where(s => !paarweise || !s.IstRueckspiel)
                .OrderBy(s => s.Spielnummer)
                .ToList();

            // Positionale Zuordnung Auslosungs-Eintrag <-> aktuelles Spiel — exakt die Reihenfolge, in der
            // GenerierenAusPlatzhalterPaarungenAsync/GenerierenAusGruppenMitPlatzhalternAsync sie erzeugt haben.
            for (int i = 0; i < platzhalterEintraege.Count && i < aktuelleSpiele.Count; i++)
            {
                var eintrag = platzhalterEintraege[i];
                var spiel   = aktuelleSpiele[i];

                var sollHeim = eintrag.HeimPlatzhalterName is { } heimName
                    ? await aufloesung.NameAufloesenAsync(heimName, runde, alleRunden, ignoriert, ct)
                    : eintrag.HeimMannschaftId;
                var sollAusw = eintrag.AuswaertsPlatzhalterName is { } auswName
                    ? await aufloesung.NameAufloesenAsync(auswName, runde, alleRunden, ignoriert, ct)
                    : eintrag.AuswaertsMannschaftId;

                SpielPruefen(spiel, sollHeim, sollAusw, eintrag.HeimPlatzhalterName, eintrag.AuswaertsPlatzhalterName);

                // Rückspiel mit vertauschten Rollen
                if (rueckspielJeHinspiel.GetValueOrDefault(spiel.Id) is { } rueckspiel)
                    SpielPruefen(rueckspiel, sollAusw, sollHeim, eintrag.AuswaertsPlatzhalterName, eintrag.HeimPlatzhalterName);
            }

            void SpielPruefen(Spiel spiel, int? sollHeim, int? sollAusw, string? heimName, string? auswName)
            {
                // Nur ein bereits konkret aufgelöster Slot, der nicht mehr zum neu ermittelten Soll-Wert
                // passt, ist ein Kaskaden-Fall — ein noch nie aufgelöster Platzhalter ist normales,
                // ausstehendes Auflösen und kein Rücksetz-Fall.
                bool heimBetroffen = spiel.HeimMannschaftId.HasValue && spiel.HeimMannschaftId != sollHeim;
                bool auswBetroffen = spiel.AuswaertsMannschaftId.HasValue && spiel.AuswaertsMannschaftId != sollAusw;
                if (!heimBetroffen && !auswBetroffen) return;

                bool hatteErgebnis = spiel.Ergebnis is not null;
                schritte.Add(new KaskadenSpielSchritt(
                    spiel.Id, runde.Id, runde.Name, hatteErgebnis,
                    sollHeim, sollAusw, heimName, auswName));

                if (hatteErgebnis) ignoriert.Add(spiel.Id);
            }
        }

        return new KaskadenPlan(schritte);
    }

    /// <summary>Führt einen zuvor mit <see cref="PlatzhalterKaskadePlanenAsync"/> berechneten Plan aus.
    /// Betroffene Einzelspiele werden gezielt korrigiert (Ergebnis ggf. löschen, dann entweder direkt auf
    /// die neu ermittelten Teams setzen oder — falls noch nicht vollständig auflösbar — auf Platzhalter
    /// zurücksetzen), ohne andere, nicht betroffene Spiele derselben Runde anzufassen. Nur echt leere
    /// Runden werden komplett neu aus ihrer Auslosung generiert.</summary>
    public async Task PlatzhalterKaskadeAusfuehrenAsync(KaskadenPlan plan, CancellationToken ct = default)
    {
        // Spielkorrekturen gebündelt und vor den Neu-Generierungen (#411/#414, vorher zwei Datenbankzugriffe je
        // Spiel): ihre Soll-Werte stehen im Plan fest, und eine neu generierte Runde liest beim Auflösen ihrer
        // Platzhalter den Stand der Vorrunden — der muss dann schon korrigiert sein.
        var spielSchritte = plan.Schritte.OfType<KaskadenSpielSchritt>().ToList();
        if (spielSchritte.Count > 0)
        {
            await spielRepository.ErgebnisseLoeschenAsync(
                [.. spielSchritte.Where(s => s.HatteErgebnis).Select(s => s.SpielId)], ct);
            await spielRepository.PlatzhalterKorrigierenAsync(
                [.. spielSchritte.Select(s => new PlatzhalterKorrektur(
                    s.SpielId,
                    s.NeuesHeimMannschaftId, s.HeimPlatzhalterName,
                    s.NeuesAuswaertsMannschaftId, s.AuswaertsPlatzhalterName))],
                ct);

            foreach (var rundenSchritte in spielSchritte.GroupBy(s => s.RundeName))
                logger.LogInformation(
                    "{Anzahl} Spiel(e) in Runde '{Runde}' im Rahmen der Kaskade korrigiert.",
                    rundenSchritte.Count(), rundenSchritte.Key);
        }

        foreach (var rundeSchritt in plan.Schritte.OfType<KaskadenRundeNeuGenerierenSchritt>())
        {
            var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(rundeSchritt.RundeId, ct);
            if (runde is null) continue;

            var auslosung = await auslosungRepository.FindenAbgeschlosseneNachRundeAsync(
                runde.TurnierInstanzId, runde.Id, ct);
            if (auslosung is null) continue;

            await spielplanService.GenerierenAusAuslosungAsync(rundeSchritt.RundeId, [.. auslosung.Ergebnisse], ct);

            logger.LogInformation(
                "Runde '{Runde}' im Rahmen der Kaskade neu generiert.", rundeSchritt.RundeName);
        }
    }

    /// <summary>Kombiniert Rückwärts-Kaskade (<see cref="PlatzhalterKaskadePlanenAsync"/>/
    /// <see cref="PlatzhalterKaskadeAusfuehrenAsync"/>, Issue #88) und automatische Finale-Generierung
    /// (<see cref="FinaleAutomatik.FinaleAutomatischGenerierenWennBereitAsync"/>, Issue #113) zu einem einzigen Aufruf.
    /// Zentraler Trigger-Punkt für jede Stelle, die den Stand einer Turnierinstanz nachträglich ändert und
    /// dadurch Folgerunden ungültig machen kann — bisher nur <see cref="Spiele.SpielErgebnisService"/>
    /// (Ergebnis-Löschen), jetzt auch <see cref="Auslosungen.AuslosungService"/> (Auslosung ausführen/
    /// zurücksetzen, Issue #306). Idempotent und günstig, wenn nichts betroffen ist.</summary>
    public async Task KaskadeAusloesenAsync(int turnierInstanzId, CancellationToken ct = default)
    {
        using var vorgang = logger.Vorgang($"Kaskade (Instanz {turnierInstanzId})");

        await finaleAutomatik.FinaleAutomatischGenerierenWennBereitAsync(turnierInstanzId, ct);

        var plan = await PlatzhalterKaskadePlanenAsync(turnierInstanzId, new HashSet<int>(), ct);
        await PlatzhalterKaskadeAusfuehrenAsync(plan, ct);

        await finaleAutomatik.FinaleAutomatischGenerierenWennBereitAsync(turnierInstanzId, ct);
    }

    public async Task LoeschenAsync(int turnierRundeId, CancellationToken ct = default)
    {
        var runde = await spielplanService.LoeschenOhneKaskadeAsync(turnierRundeId, ct);

        // Issue #306: Die Runde hat gerade ihren entschiedenen Spielplan verloren (typischerweise vor
        // einer Neugenerierung aus einer neuen/zurückgesetzten Auslosung) — Folgerunden können dadurch
        // stale werden, z.B. wenn "Sieger Gruppe A" durch den Verlust der Gruppenphase-Spiele nicht mehr
        // auflösbar ist, ein zuvor aufgelöster Platzhalter-Slot einer Folgerunde aber noch die alte
        // Mannschaft zeigt. Zentral hier statt an jeder der sechs WPF/WinUi/Konsole-Aufrufstellen.
        await KaskadeAusloesenAsync(runde.TurnierInstanzId, ct);
    }

    public Task RundenSpielplanOhneKaskadeLoeschenAsync(int turnierRundeId, CancellationToken ct = default)
        => spielplanService.RundenSpielplanOhneKaskadeLoeschenAsync(turnierRundeId, ct);

    /// <summary>Issue #411: Spielplan einer Runde in einem Zug ersetzen — alten Spielplan kaskadenlos löschen, aus
    /// den Auslosungsergebnissen neu generieren, dann einmal die Kaskade für die Folgerunden. Vorher lief die
    /// Kaskade schon nach dem Abschließen der Auslosung und generierte eine Platzhalter-Runde dabei selbst
    /// (leere Runde mit abgeschlossener Auslosung), woraufhin der Aufrufer löschte, erneut generierte und die
    /// Kaskade ein zweites Mal auslöste. Die Kaskade läuft auch, wenn die Generierung scheitert: Folgerunden
    /// dürfen nicht auf dem gerade gelöschten Spielplan stehen bleiben (#306).</summary>
    public async Task RundenSpielplanAusAuslosungErsetzenAsync(
        int turnierRundeId, IReadOnlyList<AuslosungsErgebnis> ergebnisse, CancellationToken ct = default)
    {
        using var vorgang = logger.Vorgang($"Spielplan aus Auslosung ersetzen (Runde {turnierRundeId})");

        var runde = await spielplanService.LoeschenOhneKaskadeAsync(turnierRundeId, ct);
        try
        {
            await spielplanService.GenerierenAusAuslosungAsync(turnierRundeId, ergebnisse, ct);
        }
        finally
        {
            await KaskadeAusloesenAsync(runde.TurnierInstanzId, ct);
        }
    }
}
