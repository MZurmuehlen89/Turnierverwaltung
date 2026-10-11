using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Fussballturnierverwaltung.Application.Spielplan;

public class SpielplanService(
    ITurnierRundeRepository rundeRepository,
    ISpielRepository spielRepository,
    IMannschaftRepository mannschaftRepository,
    ITurnierBaumRepository turnierBaumRepository,
    IUebergangsregelRepository uebergangsregelRepository,
    SpielplanGeneratorFactory generatorFactory,
    TurnierValidator turnierValidator,
    TabellenService tabellenService,
    TabellenZwischenspeicher zwischenspeicher,
    IAuslosungRepository auslosungRepository,
    ILogger<SpielplanService> logger) : IPlatzhalterKaskadeService
{
    /// <summary>
    /// Generiert den Spielplan für eine Runde. Die Mannschafts-IDs bestimmen Reihenfolge und Paarungen
    /// (typischerweise aus dem Auslosungsergebnis).
    /// </summary>
    public async Task<SpielplanErgebnis> GenerierenAsync(
        int turnierRundeId,
        IReadOnlyList<int> mannschaftIds,
        CancellationToken ct = default)
    {
        var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Runde mit ID {turnierRundeId} nicht gefunden.");

        if (mannschaftIds.Count != runde.EingangsMannschaftsanzahl)
            throw new InvalidOperationException(
                $"Runde '{runde.Name}' erwartet {runde.EingangsMannschaftsanzahl} Mannschaften, " +
                $"übergeben wurden {mannschaftIds.Count}.");

        // K.-o.-Runden brauchen Entscheidungsverfahren im Regelwerk
        if (runde.Regelwerk is not null)
        {
            var rundeEingabe = new TurnierRundeEingabe(
                runde.Name, runde.Rundenart,
                runde.EingangsMannschaftsanzahl, runde.AusgangsMannschaftsanzahl,
                runde.RegelwerkId, runde.AuswaertstorRegelAktiv);
            ValidationException.WerfenWennUngueltig(
                turnierValidator.RundeUndRegelwerkKompatibel(rundeEingabe, runde.Regelwerk));
        }

        var mannschaften = await mannschaftRepository.FindenNachIdsAsync(mannschaftIds, ct);

        // Sicherstellen dass die ursprüngliche Reihenfolge der IDs erhalten bleibt (Auslosungsreihenfolge)
        var geordnet = mannschaftIds
            .Select(id => mannschaften.FirstOrDefault(m => m.Id == id))
            .Where(m => m is not null)
            .Select(m => m!)
            .ToList();

        int startNr   = await spielRepository.NaechsteSpielNummerAsync(runde.TurnierInstanzId, turnierRundeId, runde.Reihenfolge, ct);
        var generator = generatorFactory.ErstellenFuerRundenart(runde.Rundenart);
        var kontext   = new SpielplanKontext(runde, geordnet, startNr);
        var ergebnis  = generator.Generieren(kontext);

        await SpeichernAsync(ergebnis, ct);

        logger.LogInformation(
            "Spielplan für Runde '{Runde}' generiert: {Anzahl} Spiele.",
            runde.Name, ergebnis.Spiele.Count);

        return ergebnis;
    }

    /// <summary>
    /// Generiert den Spielplan einer Runde direkt aus den Ergebnissen einer abgeschlossenen Auslosung -
    /// wählt je nach Rundenart automatisch die passende Generierungsmethode (Gruppenphase, TopfLiga,
    /// SchweizerSystem, oder direkte/Platzhalter-Paarungen für Liga/KO-Runden). Gemeinsam genutzt vom
    /// manuellen "Neu generieren"-Befehl (SpielplanViewModel) und der automatischen Generierung direkt
    /// nach Auslosungs-Abschluss (Issue #19, AuslosungViewModel).
    /// </summary>
    public async Task GenerierenAusAuslosungAsync(
        int turnierRundeId, IReadOnlyList<AuslosungsErgebnis> ergebnisse, CancellationToken ct = default)
    {
        var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Runde mit ID {turnierRundeId} nicht gefunden.");

        switch (runde.Rundenart)
        {
            case Rundenart.Gruppenphase:
            {
                var gruppen = GruppenSpielplan.AusAuslosung(ergebnisse);

                if (gruppen.Count > 0 && gruppen.All(g => g.All(s => s.MannschaftId.HasValue)))
                {
                    await GenerierenAusGruppenAsync(
                        turnierRundeId,
                        [.. gruppen.Select(g => (IReadOnlyList<int>)[.. g.Select(s => s.MannschaftId!.Value)])],
                        ct);
                }
                else
                {
                    // Platzhalter-Gruppenphase, auch gemischt mit festen Teams (#395 — vorher fielen die
                    // Platzhalter einer gemischten Gruppe hier stillschweigend weg): Berger-Round-Robin über Slots
                    if (gruppen.Count == 0 || gruppen.All(g => g.Count < 2))
                        throw new InvalidOperationException(ergebnisse.Any()
                            ? "Keine gültigen Gruppen-Zuordnungen in der Auslosung gefunden."
                            : "Die Auslosung hat keine Ergebnisse. Bitte die Auslosung zuerst durchführen.");

                    await GenerierenAusGruppenMitPlatzhalternAsync(turnierRundeId, gruppen, ct);
                }
                break;
            }

            case Rundenart.TopfLiga:
            {
                var paarungen = ergebnisse
                    .Where(e => e.HeimMannschaftId.HasValue && e.AuswaertsMannschaftId.HasValue)
                    .OrderBy(e => e.PaarungsNummer)
                    .Select(e => (e.HeimMannschaftId!.Value, e.AuswaertsMannschaftId!.Value))
                    .ToList<(int, int)>();
                await GenerierenAusPaarungenAsync(turnierRundeId, paarungen, ct);
                break;
            }

            case Rundenart.SchweizerSystem:
            {
                var sortiertIds = ergebnisse
                    .OrderBy(e => e.PaarungsNummer)
                    .SelectMany(e => new[] { e.HeimMannschaftId, e.AuswaertsMannschaftId })
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .Distinct()
                    .ToList();
                await SchweizerRundeGenerierenAsync(turnierRundeId, sortiertIds, ct);
                break;
            }

            default:
            {
                // Liga, KoRunde, Finale, SpielUmPlatzDrei, Sonderrunde
                var alleErgebnisse = ergebnisse.OrderBy(e => e.PaarungsNummer).ToList();

                bool nurPlatzhalter = alleErgebnisse.Any() &&
                    alleErgebnisse.All(e => !e.HeimMannschaftId.HasValue && !e.AuswaertsMannschaftId.HasValue);

                if (nurPlatzhalter)
                {
                    var paarungen = alleErgebnisse
                        .Where(e => !e.IstFreilos)
                        .OrderBy(e => e.PaarungsNummer)
                        .Select(e => (Heim: e.HeimPlatzhalterName ?? "?", Auswaerts: e.AuswaertsPlatzhalterName ?? "?"))
                        .ToList<(string Heim, string Auswaerts)>();
                    await GenerierenAusPlatzhalterPaarungenAsync(turnierRundeId, paarungen, ct);
                }
                else
                {
                    var mannschaftIds = alleErgebnisse
                        .SelectMany(e => new[] { e.HeimMannschaftId, e.AuswaertsMannschaftId })
                        .Where(id => id.HasValue)
                        .Select(id => id!.Value)
                        .Distinct()
                        .ToList();
                    await GenerierenAsync(turnierRundeId, mannschaftIds, ct);
                }
                break;
            }
        }

        // Issue #241: K.o.-Quellverknüpfungen (Sieger/Verlierer eines konkreten Vorrundenspiels) direkt
        // nach jeder Generierung nachziehen — idempotent, unabhängig davon, in welcher Reihenfolge Runden
        // erzeugt werden.
        using (logger.Vorgang($"Quellverknüpfung nach Generierung (Instanz {runde.TurnierInstanzId})"))
            await SpielQuellenAlleTransitionsVerknuepfenAsync(runde.TurnierInstanzId, ct);

        // Issue #346-Folgefund: bereits feststehende Teilnehmer sofort eintragen — sonst zeigte eine neu
        // generierte Runde nach bereits entschiedener Vorrunde wieder reine Platzhalter, bis irgendwo das
        // nächste Ergebnis die Auflösung anstieß (alle Aufrufer: Spielplan, Auslosung, Vorbelegung, Kaskade).
        using (logger.Vorgang($"Platzhalter auflösen nach Generierung (Runde {turnierRundeId})"))
            await PlatzhalterAufloesenAsync(turnierRundeId, ct);
    }

    /// <summary>
    /// Generiert den Spielplan für eine TopfLiga-Runde direkt aus den Auslosungspaarungen.
    /// </summary>
    public async Task<SpielplanErgebnis> GenerierenAusPaarungenAsync(
        int turnierRundeId,
        IReadOnlyList<(int HeimId, int AuswaertsId)> paarungen,
        CancellationToken ct = default)
    {
        var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Runde mit ID {turnierRundeId} nicht gefunden.");

        var alleIds      = paarungen.SelectMany(p => new[] { p.HeimId, p.AuswaertsId }).Distinct().ToList();
        var mannschaften = await mannschaftRepository.FindenNachIdsAsync(alleIds, ct);
        int startNr      = await spielRepository.NaechsteSpielNummerAsync(runde.TurnierInstanzId, turnierRundeId, runde.Reihenfolge, ct);

        var generator = generatorFactory.ErstellenFuerRundenart(runde.Rundenart);
        var kontext   = new SpielplanKontext(runde, mannschaften, startNr, paarungen);
        var ergebnis  = generator.Generieren(kontext);

        await SpeichernAsync(ergebnis, ct);

        logger.LogInformation(
            "TopfLiga-Spielplan für Runde '{Runde}' generiert: {Anzahl} Spiele.",
            runde.Name, ergebnis.Spiele.Count);

        return ergebnis;
    }

    /// <summary>
    /// Generiert die nächste Schweizer Runde (Spieltag) für eine SchweizerSystem-TurnierRunde.
    /// <paramref name="sortierteMannschaftIds"/> enthält alle teilnehmenden Teams sortiert nach
    /// aktuellem Tabellenstand (Rang 1 zuerst). Für Runde 1 gibt die Reihenfolge das Seeding vor.
    /// </summary>
    public async Task<SpielplanErgebnis> SchweizerRundeGenerierenAsync(
        int turnierRundeId,
        IReadOnlyList<int> sortierteMannschaftIds,
        CancellationToken ct = default)
    {
        var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Runde mit ID {turnierRundeId} nicht gefunden.");

        if (sortierteMannschaftIds.Count < 2)
            throw new InvalidOperationException(
                "Schweizer System benötigt mindestens 2 Mannschaften.");

        var vorhandeneSpiele = await spielRepository.AlleNachRundeAsync(turnierRundeId, ct);

        int naechsterSpieltag = vorhandeneSpiele.Count > 0
            ? vorhandeneSpiele.Select(s => s.Spieltag ?? 0).Max() + 1
            : 1;

        // Normalize to (min, max) for order-independent rematch detection (Platzhalter-Spiele ignorieren)
        var echteSpiele = vorhandeneSpiele
            .Where(s => s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue)
            .ToList();

        var bereitsGespielt = echteSpiele
            .Select(s => (
                TeamAId: Math.Min(s.HeimMannschaftId!.Value, s.AuswaertsMannschaftId!.Value),
                TeamBId: Math.Max(s.HeimMannschaftId!.Value, s.AuswaertsMannschaftId!.Value)))
            .Distinct()
            .ToList<(int TeamAId, int TeamBId)>();

        // Freilos-Historie: es wird kein eigener Spiel-Datensatz für ein Freilos angelegt — ein Team, das
        // an weniger Spieltagen teilgenommen hat als bisher stattgefunden haben, hat mindestens einmal
        // ausgesetzt (jedes Team spielt pro Spieltag höchstens ein Spiel).
        var spieltageGespielt = echteSpiele
            .SelectMany(s => new[] { s.HeimMannschaftId!.Value, s.AuswaertsMannschaftId!.Value })
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        var freilosHistorie = sortierteMannschaftIds
            .Where(id => spieltageGespielt.GetValueOrDefault(id) < naechsterSpieltag - 1)
            .ToHashSet();

        // Heim-Bilanz für den Farbausgleich
        var heimBilanz = echteSpiele
            .GroupBy(s => s.HeimMannschaftId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var mannschaften = await mannschaftRepository.FindenNachIdsAsync(sortierteMannschaftIds, ct);
        var geordnet = sortierteMannschaftIds
            .Select(id => mannschaften.FirstOrDefault(m => m.Id == id))
            .Where(m => m is not null)
            .Select(m => m!)
            .ToList();

        int startNr   = await spielRepository.NaechsteSpielNummerAsync(runde.TurnierInstanzId, turnierRundeId, runde.Reihenfolge, ct);
        var generator = generatorFactory.ErstellenFuerRundenart(runde.Rundenart);
        var kontext   = new SpielplanKontext(
            runde, geordnet, startNr,
            Paarungen:       null,
            BereitsGespielt: bereitsGespielt,
            Spieltag:        naechsterSpieltag,
            FreilosHistorie: freilosHistorie,
            HeimBilanz:      heimBilanz);

        var ergebnis = generator.Generieren(kontext);
        await SpeichernAsync(ergebnis, ct);

        logger.LogInformation(
            "Schweizer Runde {Spieltag} für '{Runde}' generiert: {Anzahl} Spiele.",
            naechsterSpieltag, runde.Name, ergebnis.Spiele.Count);

        return ergebnis;
    }

    /// <summary>Generiert die nächste Schweizer Runde (Spieltag) direkt aus dem aktuellen Tabellenstand — die
    /// GUI-Aufrufstelle für eine bereits laufende Schweizer-Rundenfolge ab Spieltag 2 (Issue #329 Teil E;
    /// <see cref="SchweizerRundeGenerierenAsync"/> bleibt die davon genutzte, unveränderte Kernlogik). Die
    /// vollständige Teamliste kommt bewusst aus der abgeschlossenen Auslosung statt ausschließlich aus
    /// <see cref="TabellenService"/>: ein Team mit Freilos in Spieltag 1 hat zu diesem Zeitpunkt noch kein
    /// gespieltes Spiel und fehlt daher in der aus gespielten Spielen aufgebauten Tabelle — ohne diesen
    /// Rückgriff würde es nach seinem ersten Freilos nie wieder eingeplant.</summary>
    public async Task<SpielplanErgebnis> SchweizerNaechsteRundeGenerierenAsync(int turnierRundeId, CancellationToken ct = default)
    {
        var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Runde mit ID {turnierRundeId} nicht gefunden.");

        var auslosung = await auslosungRepository.FindenAbgeschlosseneNachRundeAsync(runde.TurnierInstanzId, turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Für Runde '{runde.Name}' wurde noch keine Auslosung abgeschlossen.");

        var rosterIds = auslosung.Ergebnisse
            .SelectMany(e => new[] { e.HeimMannschaftId, e.AuswaertsMannschaftId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var tabelle = await tabellenService.TabelleBerechnenAsync(turnierRundeId, ct: ct);
        var rangNachTeamId = tabelle.ToDictionary(e => e.Mannschaft.Id, e => e.Rang);

        var sortierteIds = rosterIds
            .OrderBy(id => rangNachTeamId.TryGetValue(id, out var rang) ? rang : int.MaxValue)
            .ToList();

        return await SchweizerRundeGenerierenAsync(turnierRundeId, sortierteIds, ct);
    }

    public Task<IReadOnlyList<Spiel>> SpielplanAbrufenAsync(int turnierRundeId, CancellationToken ct = default)
        => spielRepository.AlleNachRundeAsync(turnierRundeId, ct);

    public async Task LoeschenAsync(int turnierRundeId, CancellationToken ct = default)
    {
        var runde = await LoeschenOhneKaskadeAsync(turnierRundeId, ct);

        // Issue #306: Die Runde hat gerade ihren entschiedenen Spielplan verloren (typischerweise vor
        // einer Neugenerierung aus einer neuen/zurückgesetzten Auslosung) — Folgerunden können dadurch
        // stale werden, z.B. wenn "Sieger Gruppe A" durch den Verlust der Gruppenphase-Spiele nicht mehr
        // auflösbar ist, ein zuvor aufgelöster Platzhalter-Slot einer Folgerunde aber noch die alte
        // Mannschaft zeigt. Zentral hier statt an jeder der sechs WPF/WinUi/Konsole-Aufrufstellen.
        await KaskadeAusloesenAsync(runde.TurnierInstanzId, ct);
    }

    /// <summary>Issue #320: löscht einen ggf. bereits generierten Spielplan einer Runde, ohne selbst die
    /// Platzhalter-Kaskade auszulösen — für <see cref="Auslosungen.AuslosungService.ZuruecksetzenAsync"/>,
    /// das im Anschluss ohnehin <see cref="KaskadeAusloesenAsync"/> für die ganze Instanz aufruft. Nutzt
    /// dieselbe kaskadenlose Löschung wie <see cref="FinaleAutomatischGenerierenWennBereitAsync"/> (siehe
    /// <see cref="LoeschenOhneKaskadeAsync"/>). Ein Aufruf ohne existierenden Spielplan ist ein No-Op
    /// (<see cref="ISpielRepository.AlleNachRundeLoeschenAsync"/> auf 0 Zeilen).</summary>
    public async Task RundenSpielplanOhneKaskadeLoeschenAsync(int turnierRundeId, CancellationToken ct = default)
        => await LoeschenOhneKaskadeAsync(turnierRundeId, ct);

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

        var runde = await LoeschenOhneKaskadeAsync(turnierRundeId, ct);
        try
        {
            await GenerierenAusAuslosungAsync(turnierRundeId, ergebnisse, ct);
        }
        finally
        {
            await KaskadeAusloesenAsync(runde.TurnierInstanzId, ct);
        }
    }

    /// <summary>Kern von <see cref="LoeschenAsync"/> ohne den Issue-#306-Kaskaden-Trigger — für den internen
    /// Aufruf aus <see cref="FinaleAutomatischGenerierenWennBereitAsync"/>, der selbst bereits Teil der
    /// Kaskade ist: würde dieser Aufruf ebenfalls die volle Kaskade auslösen, riefe er
    /// <see cref="FinaleAutomatischGenerierenWennBereitAsync"/> reentrant erneut auf, bevor der äußere Aufruf
    /// seine eigene <see cref="GenerierenAsync(int,IReadOnlyList{int},CancellationToken)"/>-Zeile erreicht —
    /// mit doppelt generiertem Spiel als Folge (beim manuellen #306-Test gefunden).</summary>
    private async Task<TurnierRunde> LoeschenOhneKaskadeAsync(int turnierRundeId, CancellationToken ct)
    {
        var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Runde mit ID {turnierRundeId} nicht gefunden.");
        await turnierBaumRepository.SpielIdsNullSetzenAsync(turnierRundeId, ct);
        await spielRepository.AlleNachRundeLoeschenAsync(turnierRundeId, ct);
        logger.LogInformation("Spielplan für Runde '{Runde}' gelöscht.", runde.Name);
        return runde;
    }

    /// <summary>
    /// Generiert den Spielplan für eine Gruppenphase gruppenweise.
    /// Jede Gruppe bekommt einen eigenen Round-Robin-Spielplan.
    /// </summary>
    public async Task GenerierenAusGruppenAsync(
        int turnierRundeId,
        IReadOnlyList<IReadOnlyList<int>> gruppen,
        CancellationToken ct = default)
    {
        var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Runde mit ID {turnierRundeId} nicht gefunden.");

        var alleIds      = gruppen.SelectMany(g => g).Distinct().ToList();
        var mannschaften = await mannschaftRepository.FindenNachIdsAsync(alleIds, ct);
        int startNr      = await spielRepository.NaechsteSpielNummerAsync(runde.TurnierInstanzId, turnierRundeId, runde.Reihenfolge, ct);
        var generator    = generatorFactory.ErstellenFuerRundenart(runde.Rundenart);

        var gruppenMitSpielen = new List<(int GruppenIndex, Spiel Spiel)>();
        var hinRueckspielPaare = new List<(Spiel Hinspiel, Spiel Rueckspiel)>();
        int tempNr = 1;

        for (int gi = 0; gi < gruppen.Count; gi++)
        {
            var gruppenTeams = gruppen[gi]
                .Select(id => mannschaften.FirstOrDefault(m => m.Id == id))
                .Where(m => m is not null)
                .Select(m => m!)
                .ToList();

            if (gruppenTeams.Count < 2) continue;

            var kontext  = new SpielplanKontext(runde, gruppenTeams, tempNr);
            var ergebnis = generator.Generieren(kontext);
            foreach (var spiel in ergebnis.Spiele)
            {
                spiel.GruppenNummer = gi + 1;
                gruppenMitSpielen.Add((gi, spiel));
            }
            foreach (var (hinIdx, rueckIdx) in ergebnis.HinRueckspielPaare)
                hinRueckspielPaare.Add((ergebnis.Spiele[hinIdx], ergebnis.Spiele[rueckIdx]));
            tempNr += ergebnis.Spiele.Count;
        }

        var alleSpiele = gruppenMitSpielen
            .OrderBy(x => x.Spiel.Spieltag ?? 0)
            .ThenBy(x => x.GruppenIndex)
            .Select((x, i) => { x.Spiel.Spielnummer = startNr + i; return x.Spiel; })
            .ToList();

        if (alleSpiele.Count > 0)
        {
            if (hinRueckspielPaare.Count == 0)
            {
                await spielRepository.HinzufuegenMehrfachAsync(alleSpiele, ct);
            }
            else
            {
                // Zwei-Phasen-Speicherung: Hinspiele zuerst (EF Core vergibt IDs), dann
                // HinspielId auf die Rückspiele setzen, bevor diese gespeichert werden.
                var hinspiele = hinRueckspielPaare.Select(p => p.Hinspiel).ToList();
                await spielRepository.HinzufuegenMehrfachAsync(hinspiele, ct);

                foreach (var (hin, rueck) in hinRueckspielPaare)
                    rueck.HinspielId = hin.Id;

                var rueckspiele = hinRueckspielPaare.Select(p => p.Rueckspiel).ToList();
                await spielRepository.HinzufuegenMehrfachAsync(rueckspiele, ct);
            }
        }

        logger.LogInformation(
            "Gruppenphase-Spielplan für Runde '{Runde}' generiert: {Gruppen} Gruppen, {Spiele} Spiele.",
            runde.Name, gruppen.Count, alleSpiele.Count);
    }

    // ── Platzhalter-Gruppenphase (Berger-Plan über Slots, Issue #395) ─────

    private async Task GenerierenAusGruppenMitPlatzhalternAsync(
        int turnierRundeId,
        IReadOnlyList<IReadOnlyList<GruppenSlot>> gruppen,
        CancellationToken ct = default)
    {
        var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Runde mit ID {turnierRundeId} nicht gefunden.");

        int startNr = await spielRepository.NaechsteSpielNummerAsync(runde.TurnierInstanzId, turnierRundeId, runde.Reihenfolge, ct);
        var plan    = GruppenSpielplan.Planen(gruppen, runde.MitRueckspiel);

        var alleSpiele = plan.Select((g, i) => new Spiel
        {
            TurnierInstanzId         = runde.TurnierInstanzId,
            TurnierRundeId           = runde.Id,
            Spielnummer              = startNr + i,
            Spieltag                 = g.Spieltag,
            GruppenNummer            = g.GruppenNummer,
            HeimMannschaftId         = g.Heim.MannschaftId,
            HeimPlatzhalterName      = g.Heim.MannschaftId is null ? g.Heim.PlatzhalterName ?? "?" : null,
            AuswaertsMannschaftId    = g.Auswaerts.MannschaftId,
            AuswaertsPlatzhalterName = g.Auswaerts.MannschaftId is null ? g.Auswaerts.PlatzhalterName ?? "?" : null,
            Status                   = SpielStatus.Geplant,
            SimulationsModus         = SimulationsModus.ManuellesErgebnis,
            IstRueckspiel            = g.IstRueckspiel,
        }).ToList();

        // Zwei-Phasen-Speicherung wie GenerierenAusGruppenAsync: Rückspiele erhalten die HinspielId ihres
        // bereits gespeicherten Hinspiels.
        var hinspiele = alleSpiele.Where(s => !s.IstRueckspiel).ToList();
        if (hinspiele.Count > 0)
            await spielRepository.HinzufuegenMehrfachAsync(hinspiele, ct);

        var rueckspiele = new List<Spiel>();
        for (int i = 0; i < plan.Count; i++)
        {
            if (plan[i].HinspielIndex is not int hinIdx) continue;
            alleSpiele[i].HinspielId = alleSpiele[hinIdx].Id;
            rueckspiele.Add(alleSpiele[i]);
        }
        if (rueckspiele.Count > 0)
            await spielRepository.HinzufuegenMehrfachAsync(rueckspiele, ct);

        logger.LogInformation(
            "Platzhalter-Gruppenphase für Runde '{Runde}' generiert: {Gruppen} Gruppen, {Spiele} Spiele.",
            runde.Name, gruppen.Count, alleSpiele.Count);
    }

    private static Spiel PlatzhalterSpiel(TurnierRunde runde, string heim, string auswaerts, int spielNr, int spieltag, bool istRueckspiel = false) =>
        new()
        {
            TurnierInstanzId         = runde.TurnierInstanzId,
            TurnierRundeId           = runde.Id,
            Spielnummer              = spielNr,
            Spieltag                 = spieltag,
            HeimPlatzhalterName      = heim,
            AuswaertsPlatzhalterName = auswaerts,
            Status                   = SpielStatus.Geplant,
            SimulationsModus         = SimulationsModus.ManuellesErgebnis,
            IstRueckspiel            = istRueckspiel,
        };

    public Task<IReadOnlyList<Spiel>> AlleSpieleFuerInstanzAsync(int turnierInstanzId, CancellationToken ct = default)
        => spielRepository.AlleNachInstanzAsync(turnierInstanzId, ct);

    /// <summary>
    /// Erstellt Platzhalter-Spiele für eine KO-Runde direkt aus den Auslosungspaarungen.
    /// MannschaftIds bleiben null; Platzhalternamen (z.B. "Sieger Gruppe A") werden gespeichert.
    /// </summary>
    public async Task<int> GenerierenAusPlatzhalterPaarungenAsync(
        int turnierRundeId,
        IReadOnlyList<(string Heim, string Auswaerts)> paarungen,
        CancellationToken ct = default)
    {
        var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Runde mit ID {turnierRundeId} nicht gefunden.");

        int spielNr = await spielRepository.NaechsteSpielNummerAsync(runde.TurnierInstanzId, turnierRundeId, runde.Reihenfolge, ct);

        List<Spiel> spiele;
        if (!runde.MitRueckspiel)
        {
            spiele = paarungen.Select((p, i) => new Spiel
            {
                TurnierInstanzId         = runde.TurnierInstanzId,
                TurnierRundeId           = turnierRundeId,
                Spielnummer              = spielNr + i,
                HeimPlatzhalterName      = p.Heim,
                AuswaertsPlatzhalterName = p.Auswaerts,
                Status                   = SpielStatus.Geplant,
                SimulationsModus         = SimulationsModus.ManuellesErgebnis,
                IstRueckspiel            = false,
            }).ToList();

            await spielRepository.HinzufuegenMehrfachAsync(spiele, ct);
        }
        else
        {
            // Issue #346: je Paarung Hin- und gespiegeltes Rückspiel — erst alle Hinspiele, dann alle Rückspiele
            // nummeriert (so, wie sie ausgetragen werden; wie im KoRundeGenerator). Zweiphasig gespeichert (wie
            // SpeichernAsync), damit die Rückspiele die HinspielId ihres bereits gespeicherten Hinspiels erhalten.
            var hinspiele = paarungen
                .Select((p, i) => PlatzhalterSpiel(runde, p.Heim, p.Auswaerts, spielNr + i, spieltag: 1))
                .ToList();
            await spielRepository.HinzufuegenMehrfachAsync(hinspiele, ct);

            var rueckspiele = hinspiele
                .Select(h =>
                {
                    var rueck = PlatzhalterSpiel(runde, h.AuswaertsPlatzhalterName!, h.HeimPlatzhalterName!,
                        h.Spielnummer + hinspiele.Count, spieltag: 2, istRueckspiel: true);
                    rueck.HinspielId = h.Id;
                    return rueck;
                })
                .ToList();
            await spielRepository.HinzufuegenMehrfachAsync(rueckspiele, ct);

            spiele = [.. hinspiele, .. rueckspiele];
        }

        logger.LogInformation(
            "Platzhalter-Spielplan für Runde '{Runde}' generiert: {Anzahl} Spiele.",
            runde.Name, spiele.Count);

        return spiele.Count;
    }

    /// <summary>
    /// Löst Platzhalternamen in einer KO-Runde auf: sucht in Vorrunden-Tabellen und KO-Ergebnissen
    /// nach den realen Teams und aktualisiert die Spiele. Gibt die Anzahl aufgelöster Spiele zurück.
    /// </summary>
    public Task<int> PlatzhalterAufloesenAsync(int turnierRundeId, CancellationToken ct = default)
        => zwischenspeicher.MitTabellenZwischenspeicherAsync(() => PlatzhalterAufloesenOhneZwischenspeicherAsync(turnierRundeId, ct));

    private async Task<int> PlatzhalterAufloesenOhneZwischenspeicherAsync(int turnierRundeId, CancellationToken ct)
    {
        // Aufschlüsselung für das Ablaufprotokoll (#414): Laden der Runde und ihrer Spiele, Auflösen der Namen
        // (Tabellen und Quellspiele), Speichern — als Werte in der Abschlusszeile statt eigener Vorgänge, weil
        // die Auflösung nach jedem Ergebnis für jede Runde läuft.
        var uhr = Stopwatch.StartNew();

        var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Runde mit ID {turnierRundeId} nicht gefunden.");

        // Alle Runden derselben TurnierInstanz laden (für Suche nach Vorrunden)
        var alleRunden = await rundeRepository.AlleNachInstanzAsync(runde.TurnierInstanzId, ct);

        var platzhalterSpiele = (await spielRepository.AlleNachRundeAsync(turnierRundeId, ct))
            .Where(s => s.HeimMannschaftId is null || s.AuswaertsMannschaftId is null)
            .ToList();

        if (platzhalterSpiele.Count == 0) return 0;
        long ladenMs = uhr.ElapsedMilliseconds;

        // Platzhalter-Cache aufbauen (jeder eindeutige Name nur einmal auflösen)
        var cache = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
        var quellSpielCache = new Dictionary<int, Spiel?>();

        async Task<int?> AufloessenAsync(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (cache.TryGetValue(name, out var cached)) return cached;
            var result = await PlatzhalterNameAufloessenAsync(name, runde, alleRunden, ct);
            cache[name] = result;
            return result;
        }

        // Issue #241: eine gesetzte Quell-ID (K.o.-zu-K.o.) geht direkt, ohne Text-Regex, vor.
        async Task<int?> AufloesenSeiteAsync(int? quellSpielId, SpielQuellErgebnisTyp? quellTyp, string? platzhalterName)
            => quellSpielId is int qId && quellTyp is { } typ
                ? await QuellSpielAufloesenAsync(qId, typ, quellSpielCache, ct)
                : await AufloessenAsync(platzhalterName);

        // Gesammelt und am Ende in einem Zug gespeichert (#411) — die Auflösung liest nur Quellrunden, keine
        // Korrektur hängt von einer anderen derselben Runde ab.
        var korrekturen = new List<PlatzhalterKorrektur>();
        foreach (var spiel in platzhalterSpiele)
        {
            // Issue #206: jede Seite unabhängig auflösen (TV-Präsentation-Stil) — eine Seite kann
            // schon feststehen, während die andere noch als Platzhaltertext offen bleibt, statt beide
            // Seiten erst gemeinsam zu schreiben, wenn beide auflösbar sind. PlatzhalterKorrigierenAsync
            // (aus #88) unterstützt unabhängige Seiten bereits — für die jeweils noch nicht auflösbare
            // Seite wird schlicht der unveränderte Platzhaltertext zurückgeschrieben (No-Op).
            var heimId      = spiel.HeimMannschaftId      ?? await AufloesenSeiteAsync(spiel.HeimQuellSpielId, spiel.HeimQuellErgebnis, spiel.HeimPlatzhalterName);
            var auswaertsId = spiel.AuswaertsMannschaftId ?? await AufloesenSeiteAsync(spiel.AuswaertsQuellSpielId, spiel.AuswaertsQuellErgebnis, spiel.AuswaertsPlatzhalterName);

            bool heimNeuAufgeloest      = heimId is not null && spiel.HeimMannschaftId is null;
            bool auswaertsNeuAufgeloest = auswaertsId is not null && spiel.AuswaertsMannschaftId is null;
            if (!heimNeuAufgeloest && !auswaertsNeuAufgeloest) continue;

            korrekturen.Add(new PlatzhalterKorrektur(
                spiel.Id, heimId, spiel.HeimPlatzhalterName, auswaertsId, spiel.AuswaertsPlatzhalterName));
        }

        long aufloesenMs = uhr.ElapsedMilliseconds - ladenMs;

        if (korrekturen.Count > 0)
        {
            await spielRepository.PlatzhalterKorrigierenAsync(korrekturen, ct);
            zwischenspeicher.TabellenZwischenspeicherVerwerfen(turnierRundeId);
        }
        long speichernMs = uhr.ElapsedMilliseconds - ladenMs - aufloesenMs;

        logger.LogInformation(
            "Platzhalter in Runde '{Runde}' aufgelöst: {Aufgeloest}/{Gesamt} Spiele " +
            "(Laden {Laden} ms, Auflösen {Aufloesen} ms, Speichern {Speichern} ms).",
            runde.Name, korrekturen.Count, platzhalterSpiele.Count, ladenMs, aufloesenMs, speichernMs);

        return korrekturen.Count;
    }

    /// <summary>Rein lesende Vorschau (Issue #208, Ergänzung zu #202): welcher Mannschaftsname würde nach
    /// dem aktuellen, noch nicht zwingend sicheren Tabellenstand jede noch offene Platzhalter-Seite einer
    /// Runde besetzen? Schreibt nichts in die Datenbank. Liefert nur Einträge für Spiele, bei denen
    /// mindestens eine Seite optimistisch (ohne SicherPositioniert-Gate) ermittelbar ist; bereits real
    /// zugewiesene Seiten liefern keinen Vorschau-Wert (null).</summary>
    public Task<IReadOnlyDictionary<int, (string? Heim, string? Auswaerts)>> PlatzhalterVorschauAsync(
        int turnierRundeId, CancellationToken ct = default)
        => zwischenspeicher.MitTabellenZwischenspeicherAsync(() => PlatzhalterVorschauOhneZwischenspeicherAsync(turnierRundeId, ct));

    private async Task<IReadOnlyDictionary<int, (string? Heim, string? Auswaerts)>> PlatzhalterVorschauOhneZwischenspeicherAsync(
        int turnierRundeId, CancellationToken ct)
    {
        var ergebnis = new Dictionary<int, (string? Heim, string? Auswaerts)>();

        var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Runde mit ID {turnierRundeId} nicht gefunden.");
        var alleRunden = await rundeRepository.AlleNachInstanzAsync(runde.TurnierInstanzId, ct);

        var platzhalterSpiele = (await spielRepository.AlleNachRundeAsync(turnierRundeId, ct))
            .Where(s => s.HeimMannschaftId is null || s.AuswaertsMannschaftId is null)
            .ToList();
        if (platzhalterSpiele.Count == 0) return ergebnis;

        var cache = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
        var quellSpielCache = new Dictionary<int, Spiel?>();

        async Task<int?> VorschauAufloessenAsync(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (cache.TryGetValue(name, out var cached)) return cached;
            var result = await PlatzhalterNameAufloessenAsync(name, runde, alleRunden, ct, nurSicherPositioniert: false);
            cache[name] = result;
            return result;
        }

        // Issue #241: ID-Fastpath auch in der Vorschau, für Konsistenz mit der tatsächlichen Auflösung.
        async Task<int?> VorschauSeiteAsync(int? quellSpielId, SpielQuellErgebnisTyp? quellTyp, string? platzhalterName)
            => quellSpielId is int qId && quellTyp is { } typ
                ? await QuellSpielAufloesenAsync(qId, typ, quellSpielCache, ct)
                : await VorschauAufloessenAsync(platzhalterName);

        var vorschauIds = new Dictionary<int, (int? Heim, int? Auswaerts)>();
        foreach (var spiel in platzhalterSpiele)
        {
            int? heimVorschau      = spiel.HeimMannschaftId      is null ? await VorschauSeiteAsync(spiel.HeimQuellSpielId, spiel.HeimQuellErgebnis, spiel.HeimPlatzhalterName)           : null;
            int? auswaertsVorschau = spiel.AuswaertsMannschaftId is null ? await VorschauSeiteAsync(spiel.AuswaertsQuellSpielId, spiel.AuswaertsQuellErgebnis, spiel.AuswaertsPlatzhalterName) : null;
            if (heimVorschau is not null || auswaertsVorschau is not null)
                vorschauIds[spiel.Id] = (heimVorschau, auswaertsVorschau);
        }
        if (vorschauIds.Count == 0) return ergebnis;

        var teamIds = vorschauIds.Values.SelectMany(v => new[] { v.Heim, v.Auswaerts })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var teams = (await mannschaftRepository.FindenNachIdsAsync(teamIds, ct)).ToDictionary(m => m.Id, m => m.Name);

        foreach (var (spielId, (heimId, auswaertsId)) in vorschauIds)
            ergebnis[spielId] = (
                heimId.HasValue ? teams.GetValueOrDefault(heimId.Value) : null,
                auswaertsId.HasValue ? teams.GetValueOrDefault(auswaertsId.Value) : null);

        return ergebnis;
    }

    /// <summary>Löst die Platzhalter aller Runden der Turnierinstanz auf (Issue #90, Vorwärts-Gegenstück
    /// zur Rückwärts-Kaskade aus #88). Idempotent und günstig für Runden ohne offene Platzhalter-Spiele —
    /// <see cref="PlatzhalterAufloesenAsync"/> selbst kehrt dafür sofort zurück (früher Return bei
    /// <c>platzhalterSpiele.Count == 0</c>). Gibt die Gesamtanzahl neu aufgelöster Spiele zurück.</summary>
    public Task<int> PlatzhalterAlleRundenAufloesenAsync(int turnierInstanzId, CancellationToken ct = default)
        => zwischenspeicher.MitTabellenZwischenspeicherAsync(() => PlatzhalterAlleRundenAufloesenOhneZwischenspeicherAsync(turnierInstanzId, ct));

    private async Task<int> PlatzhalterAlleRundenAufloesenOhneZwischenspeicherAsync(int turnierInstanzId, CancellationToken ct)
    {
        await SpielQuellenAlleTransitionsVerknuepfenAsync(turnierInstanzId, ct);

        var runden = await rundeRepository.AlleNachInstanzAsync(turnierInstanzId, ct);
        int gesamt = 0;
        foreach (var runde in runden.OrderBy(r => r.Reihenfolge))
            gesamt += await PlatzhalterAufloesenAsync(runde.Id, ct);
        return gesamt;
    }

    // ── K.o.-Quellverknüpfung (Issue #241) ──────────────────────────────────

    /// <summary>Löst für alle K.o.-artigen Runden (K.o.-Runde/Finale/Spiel um Platz 3) einer Turnierinstanz
    /// die K.o.-zu-K.o.-Quellverknüpfungen auf. Ersetzt das frühere, ausschließlich in
    /// <c>TurnierBaumService.VerkuepfenPerPlatzhalter</c> lebende Erraten aus Text — die ID wird jetzt
    /// direkt auf <see cref="Spiel"/> persistiert, damit sowohl die Platzhalter-Auflösung als auch der
    /// Turnierbaum dieselbe robuste Quelle nutzen können. Idempotent und günstig.</summary>
    public async Task SpielQuellenAlleTransitionsVerknuepfenAsync(int turnierInstanzId, CancellationToken ct = default)
    {
        var alleRunden = await rundeRepository.AlleNachInstanzAsync(turnierInstanzId, ct);
        var koRunden = alleRunden
            .Where(r => r.Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei)
            .OrderBy(r => r.Reihenfolge);

        foreach (var zielRunde in koRunden)
            await SpielQuellenVerknuepfenAsync(zielRunde, alleRunden, ct);
    }

    /// <summary>Ermittelt für eine K.o.-Zielrunde, welches Vorrundenspiel (Sieger/Verlierer) jeden noch
    /// nicht ID-verknüpften Platzhalter-Slot speist, und persistiert das Ergebnis auf
    /// <see cref="Spiel.HeimQuellSpielId"/>/<see cref="Spiel.AuswaertsQuellSpielId"/>. Arbeitet pro Slot
    /// unabhängig — anders als das frühere <c>TurnierBaumService.VerkuepfenPerPlatzhalter</c>, das einen
    /// gesamten Rundenübergang fälschlich als "erledigt" markierte, sobald irgendein Spiel per Text
    /// zugeordnet werden konnte. Idempotent: bereits verknüpfte Seiten werden übersprungen.</summary>
    public async Task SpielQuellenVerknuepfenAsync(
        TurnierRunde zielRunde, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct = default)
    {
        var zielSpiele = await spielRepository.AlleNachRundeAsync(zielRunde.Id, ct);
        if (zielSpiele.Count == 0) return;

        var quellSpieleProRunde = new Dictionary<int, List<Spiel>>();

        async Task<List<Spiel>> QuellSpieleAsync(TurnierRunde quellRunde)
        {
            if (quellSpieleProRunde.TryGetValue(quellRunde.Id, out var cached)) return cached;
            // Issue #346: "Sieger <Runde> N" meint die N-te Paarung — bei Hin-/Rückspiel deren Rückspiel,
            // das die Gesamtwertung trägt.
            var liste = KoPaarungen.EntscheidungsspieleJePaarung(await spielRepository.AlleNachRundeAsync(quellRunde.Id, ct));
            quellSpieleProRunde[quellRunde.Id] = liste;
            return liste;
        }

        // Bracket-Slot-Position ist ab Erzeugung der Runde stabil — bewusst NICHT wie im Regex-/
        // Uebergangsregel-Pfad (RangAufloesenAsync) auf bereits aufgelöste Spiele gefiltert.
        async Task<(int SpielId, SpielQuellErgebnisTyp Typ)?> QuelleAufloesenAsync(string? platzhalterName)
        {
            if (string.IsNullOrWhiteSpace(platzhalterName)) return null;
            if (PlatzhalterNamen.KoSpielErkennen(platzhalterName) is not { } bezug) return null;

            var quellRunde = PlatzhalterNamen.KoQuellrunde(bezug, zielRunde, alleRunden);
            if (quellRunde is null) return null;

            var quellSpiele = await QuellSpieleAsync(quellRunde);
            if (bezug.SpielNummer < 1 || bezug.SpielNummer > quellSpiele.Count) return null;

            return (quellSpiele[bezug.SpielNummer - 1].Id,
                bezug.Verlierer ? SpielQuellErgebnisTyp.Verlierer : SpielQuellErgebnisTyp.Sieger);
        }

        var geaenderteSpiele = new List<Spiel>();
        foreach (var spiel in zielSpiele)
        {
            bool geaendert = false;

            if (spiel.HeimQuellSpielId is null &&
                await QuelleAufloesenAsync(spiel.HeimPlatzhalterName) is { } heimQuelle)
            {
                spiel.HeimQuellSpielId  = heimQuelle.SpielId;
                spiel.HeimQuellErgebnis = heimQuelle.Typ;
                geaendert = true;
            }

            if (spiel.AuswaertsQuellSpielId is null &&
                await QuelleAufloesenAsync(spiel.AuswaertsPlatzhalterName) is { } auswaertsQuelle)
            {
                spiel.AuswaertsQuellSpielId  = auswaertsQuelle.SpielId;
                spiel.AuswaertsQuellErgebnis = auswaertsQuelle.Typ;
                geaendert = true;
            }

            if (geaendert) geaenderteSpiele.Add(spiel);
        }

        if (geaenderteSpiele.Count == 0) return;

        var uhr = Stopwatch.StartNew();
        await spielRepository.AktualisierenMehrfachAsync(geaenderteSpiele, ct);
        logger.LogInformation(
            "Quellverknüpfung in Runde '{Runde}': {Anzahl} Spiel(e) verknüpft (Speichern {Speichern} ms).",
            zielRunde.Name, geaenderteSpiele.Count, uhr.ElapsedMilliseconds);
    }

    /// <summary>Löst eine K.o.-Quellverknüpfung (<see cref="Spiel.HeimQuellSpielId"/>/-<c>Ergebnis</c>) direkt
    /// zur Mannschafts-ID auf, ohne Text-Regex — Gegenstück zum entsprechenden Zweig in
    /// <see cref="RangAufloesenAsync"/>, aber ohne dessen "nur bereits aufgelöste Spiele"-Filter, da die
    /// Quell-ID bereits eindeutig auf ein konkretes Spiel zeigt.</summary>
    private async Task<int?> QuellSpielAufloesenAsync(
        int quellSpielId, SpielQuellErgebnisTyp typ, Dictionary<int, Spiel?> cache, CancellationToken ct)
    {
        if (!cache.TryGetValue(quellSpielId, out var quellSpiel))
        {
            quellSpiel = await spielRepository.FindenNachIdMitErgebnisAsync(quellSpielId, ct);
            cache[quellSpielId] = quellSpiel;
        }
        if (quellSpiel is null) return null;

        return KoPaarungen.SiegerOderVerliererId(quellSpiel, typ == SpielQuellErgebnisTyp.Sieger ? 1 : 2);
    }

    // ── Kaskaden-Reset für Folgerunden (Issue #88) ─────────────────────────

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
        => zwischenspeicher.MitTabellenZwischenspeicherAsync(() => PlatzhalterKaskadePlanenOhneZwischenspeicherAsync(turnierInstanzId, zusaetzlichIgnorierteSpielIds, ct));

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
            zwischenspeicher.SpielSchluesselMerken(runde.Id, alleRundenSpiele);

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
                    ? await PlatzhalterNameAufloessenAsync(name, runde, alleRunden, ct, ignoriert)
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
                    ? await PlatzhalterNameAufloessenAsync(heimName, runde, alleRunden, ct, ignoriert)
                    : eintrag.HeimMannschaftId;
                var sollAusw = eintrag.AuswaertsPlatzhalterName is { } auswName
                    ? await PlatzhalterNameAufloessenAsync(auswName, runde, alleRunden, ct, ignoriert)
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

            await GenerierenAusAuslosungAsync(rundeSchritt.RundeId, [.. auslosung.Ergebnisse], ct);

            logger.LogInformation(
                "Runde '{Runde}' im Rahmen der Kaskade neu generiert.", rundeSchritt.RundeName);
        }
    }

    /// <summary>Kombiniert Rückwärts-Kaskade (<see cref="PlatzhalterKaskadePlanenAsync"/>/
    /// <see cref="PlatzhalterKaskadeAusfuehrenAsync"/>, Issue #88) und automatische Finale-Generierung
    /// (<see cref="FinaleAutomatischGenerierenWennBereitAsync"/>, Issue #113) zu einem einzigen Aufruf.
    /// Zentraler Trigger-Punkt für jede Stelle, die den Stand einer Turnierinstanz nachträglich ändert und
    /// dadurch Folgerunden ungültig machen kann — bisher nur <see cref="Spiele.SpielErgebnisService"/>
    /// (Ergebnis-Löschen), jetzt auch <see cref="Auslosungen.AuslosungService"/> (Auslosung ausführen/
    /// zurücksetzen, Issue #306). Idempotent und günstig, wenn nichts betroffen ist.</summary>
    public async Task KaskadeAusloesenAsync(int turnierInstanzId, CancellationToken ct = default)
    {
        using var vorgang = logger.Vorgang($"Kaskade (Instanz {turnierInstanzId})");

        await FinaleAutomatischGenerierenWennBereitAsync(turnierInstanzId, ct);

        var plan = await PlatzhalterKaskadePlanenAsync(turnierInstanzId, new HashSet<int>(), ct);
        await PlatzhalterKaskadeAusfuehrenAsync(plan, ct);

        await FinaleAutomatischGenerierenWennBereitAsync(turnierInstanzId, ct);
    }

    // ── Automatische Finale-Generierung ohne Auslosung (Issue #113) ────────

    /// <summary>
    /// Hält Zielrunden mit genau zwei Eingangsmannschaften synchron mit dem Stand ihrer(n) Quellrunde(n) — ohne
    /// dass für die Zielrunde eine eigene Auslosung durchlaufen werden muss (bei nur zwei möglichen Teilnehmern
    /// gibt es ohnehin keinen Freiheitsgrad, wer gegen wen antritt). Generiert das Spiel sobald die Quellrunde(n)
    /// vollständig entschieden sind, und entfernt/ersetzt ein noch nicht gespieltes Spiel wieder, falls sich der
    /// Quellrunden-Stand nachträglich ändert (Ergebnis zurückgesetzt oder neu simuliert). Eine bereits gespielte
    /// Zielrunde (mit Ergebnis) wird nie automatisch verändert.
    /// Zwei Wege werden unterstützt: (1) explizite <see cref="Uebergangsregel"/>n, die zusammen genau zwei
    /// Qualifikanten für die Zielrunde liefern (Issue #29/#130); (2) als Fallback für Turniere ohne solche Regeln
    /// die ursprüngliche Heuristik aus Issue #113 (nächstgelegene einbeinige K.-o.-Vorrunde mit zwei Ausgangsteams).
    /// Aggregat-Sieger-Ermittlung über zwei Spielbeine ist bewusst nicht Teil dieser Phase (weder für die
    /// Quellrunde noch für die Zielrunde).
    /// Aufrufpunkt: direkt nach dem Speichern/Zurücksetzen/Simulieren eines Ergebnisses (siehe SpielplanViewModel),
    /// da die Zielrunde vor der automatischen Generierung noch keine Spiele hat und der reguläre
    /// "Platzhalter aktualisieren"-Button dafür nicht sichtbar/erreichbar ist.
    /// </summary>
    public async Task FinaleAutomatischGenerierenWennBereitAsync(int turnierInstanzId, CancellationToken ct = default)
    {
        using var vorgang = logger.Vorgang($"Finale-Generierung prüfen (Instanz {turnierInstanzId})");

        var alleRunden = await rundeRepository.AlleNachInstanzAsync(turnierInstanzId, ct);
        var alleRegeln = await uebergangsregelRepository.AlleNachTurnierInstanzIdAsync(turnierInstanzId, ct);

        var zielRunden = alleRunden
            .Where(r => r.EingangsMannschaftsanzahl == 2)
            .ToList();

        foreach (var ziel in zielRunden)
        {
            // Zielrunden mit eigener Auslosung gehören ausschließlich der breiteren #88-Kaskade
            // (PlatzhalterKaskadePlanenAsync/-AusfuehrenAsync) — sonst würde diese Heuristik eine gerade
            // frisch aus der Auslosung regenerierte Platzhalter-Paarung sofort wieder entfernen, da sie
            // Auslosung-Zustand nicht kennt (nur Ergebnis-Vorhandensein prüft).
            if (await auslosungRepository.FindenAbgeschlosseneNachRundeAsync(turnierInstanzId, ziel.Id, ct) is not null)
                continue;

            var zielSpiele = await spielRepository.AlleNachRundeAsync(ziel.Id, ct);

            // Bereits gespielt oder unerwartete Struktur (z.B. mehr als ein Spiel) - nie automatisch anfassen.
            if (zielSpiele.Count > 1 || zielSpiele.Any(s => s.Ergebnis is not null))
                continue;

            var regelnFuerZiel = alleRegeln.Where(r => r.ZielRundeId == ziel.Id).ToList();
            var (sollTeilnehmer, quelleBeschreibung) = regelnFuerZiel.Count > 0
                ? await QualifikantenAusRegelnAsync(regelnFuerZiel, ct)
                : await QualifikantenAusFinaleHeuristikAsync(ziel, alleRunden, ct);

            if (zielSpiele.Count == 0)
            {
                if (sollTeilnehmer is null) continue; // Quellrunde(n) noch nicht (vollständig) entschieden
                await GenerierenAsync(ziel.Id, sollTeilnehmer, ct);
                logger.LogInformation(
                    "Runde '{Runde}' automatisch ohne Auslosung generiert (Teilnehmer aus {Quelle}).",
                    ziel.Name, quelleBeschreibung);
                continue;
            }

            // Genau ein noch nicht gespieltes Spiel vorhanden - prüfen ob es noch zum Quellrunden-Stand passt.
            var aktuellesSpiel = zielSpiele[0];
            var aktuelleTeilnehmer = new[] { aktuellesSpiel.HeimMannschaftId, aktuellesSpiel.AuswaertsMannschaftId }
                .Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
            bool stimmtUeberein = sollTeilnehmer is not null
                && aktuelleTeilnehmer.SetEquals(sollTeilnehmer);
            if (stimmtUeberein) continue; // bereits korrekt

            await LoeschenOhneKaskadeAsync(ziel.Id, ct);
            logger.LogInformation(
                "Automatisch generiertes, noch nicht gespieltes Spiel in Runde '{Runde}' entfernt " +
                "(Quellrunden-Stand hat sich geändert).", ziel.Name);

            if (sollTeilnehmer is not null)
            {
                await GenerierenAsync(ziel.Id, sollTeilnehmer, ct);
                logger.LogInformation(
                    "Runde '{Runde}' automatisch neu generiert (Teilnehmer aus {Quelle}).",
                    ziel.Name, quelleBeschreibung);
            }
        }
    }

    /// <summary>Ermittelt die beiden Qualifikanten für eine Zielrunde aus expliziten <see cref="Uebergangsregel"/>n.
    /// Liefert <c>null</c>, solange nicht alle beteiligten Quellrunden vollständig entschieden sind.</summary>
    private async Task<(List<int>? Teilnehmer, string Beschreibung)> QualifikantenAusRegelnAsync(
        IReadOnlyList<Uebergangsregel> regeln, CancellationToken ct)
    {
        var teilnehmer = new List<int>();
        foreach (var regel in regeln)
        {
            if (regel.AnzahlBeste is not null)
            {
                var beste = await BesteNAufloesenAsync(regel, ct);
                if (beste.Any(t => t is null)) return (null, "");
                teilnehmer.AddRange(beste.Select(t => t!.Value));
                continue;
            }

            for (int platz = regel.PlatzierungVon; platz <= regel.PlatzierungBis; platz++)
            {
                var teamId = await RangAufloesenAsync(regel.QuellRunde, regel.GruppeIndex, platz, ct);
                if (teamId is null) return (null, "");
                teilnehmer.Add(teamId.Value);
            }
        }

        var beschreibung = string.Join(", ", regeln.Select(r => $"'{r.QuellRunde.Name}'").Distinct());
        return teilnehmer.Count == 2 ? (teilnehmer, beschreibung) : (null, beschreibung);
    }

    /// <summary>Fallback für Turniere ohne explizite <see cref="Uebergangsregel"/>n: sucht die nächstgelegene
    /// einbeinige K.-o.-Vorrunde mit zwei Ausgangsteams und ermittelt deren beide Qualifikanten (Issue #113) —
    /// bei <paramref name="ziel"/> = <see cref="Rundenart.SpielUmPlatzDrei"/> die Verlierer statt der Sieger
    /// (Issue #199, analog zur Platzierung=2-Auflösung im K.o.-Zweig von RangAufloesenAsync).</summary>
    private async Task<(List<int>? Teilnehmer, string Beschreibung)> QualifikantenAusFinaleHeuristikAsync(
        TurnierRunde ziel, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct)
    {
        var vorRunde = alleRunden
            .Where(r => r.Reihenfolge < ziel.Reihenfolge
                     && r.Rundenart == Rundenart.KoRunde
                     && r.AusgangsMannschaftsanzahl == 2
                     && !r.MitRueckspiel)
            .OrderByDescending(r => r.Reihenfolge)
            .FirstOrDefault();

        if (vorRunde is null) return (null, "");

        var vorRundenSpiele = await spielRepository.AlleNachRundeAsync(vorRunde.Id, ct);
        if (vorRundenSpiele.Count == 0 || vorRundenSpiele.Any(s => s.Ergebnis?.SiegerMannschaftId is null))
            return (null, $"'{vorRunde.Name}'"); // noch nicht (vollständig) entschieden

        bool verlierer = ziel.Rundenart == Rundenart.SpielUmPlatzDrei;
        var qualifikanten = vorRundenSpiele
            .OrderBy(s => s.Spielnummer)
            .Select(s => verlierer
                ? (s.Ergebnis!.SiegerMannschaftId == s.HeimMannschaftId ? s.AuswaertsMannschaftId : s.HeimMannschaftId)
                : s.Ergebnis!.SiegerMannschaftId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        return qualifikanten.Count == 2 ? (qualifikanten, $"'{vorRunde.Name}'") : (null, $"'{vorRunde.Name}'");
    }

    // ── Platzhalter-Auflösungslogik ───────────────────────────────────────

    private async Task<int?> PlatzhalterNameAufloessenAsync(
        string name, TurnierRunde runde, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct,
        IReadOnlySet<int>? ignorierteSpielIds = null, bool nurSicherPositioniert = true) =>
        PlatzhalterNamen.Erkennen(name) switch
        {
            PlatzhalterBezug.GruppenRang g => await GruppenrangAufloessenAsync(
                g.GruppenKennung, g.Rang, runde, alleRunden, ct, ignorierteSpielIds, nurSicherPositioniert),
            PlatzhalterBezug.KombinationsPlatz k => await KombinationsPlatzAufloesenAsync(
                name, k.Platzierung, runde, alleRunden, ct, ignorierteSpielIds),
            PlatzhalterBezug.BestePlatzierung b => await BestePlatzierungAufloessenAsync(
                b.Rang, b.Platzierung, runde, alleRunden, ct, ignorierteSpielIds),
            PlatzhalterBezug.KoSpiel k => await KoSpielAufloesenAsync(k, runde, alleRunden, ct, ignorierteSpielIds),
            PlatzhalterBezug.TabellenPlatz t => await TabellenPlatzAufloesenAsync(
                t, runde, alleRunden, ct, ignorierteSpielIds, nurSicherPositioniert),
            _ => null,
        };

    private async Task<int?> TabellenPlatzAufloesenAsync(
        PlatzhalterBezug.TabellenPlatz bezug, TurnierRunde runde, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct,
        IReadOnlySet<int>? ignorierteSpielIds, bool nurSicherPositioniert)
    {
        var quellRunde = alleRunden
            .Where(r => r.Reihenfolge < runde.Reihenfolge
                     && string.Equals(r.Name, bezug.RundenName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Reihenfolge)
            .FirstOrDefault();
        if (quellRunde is null) return null;

        return await RangAufloesenAsync(quellRunde, null, bezug.Platz, ct, ignorierteSpielIds, nurSicherPositioniert);
    }

    private async Task<int?> GruppenrangAufloessenAsync(
        string gruppenKennung, int rang, TurnierRunde runde, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct,
        IReadOnlySet<int>? ignorierteSpielIds = null, bool nurSicherPositioniert = true)
    {
        // Gruppenkennung → Gruppenindex (A=1, B=2, ... oder direkte Zahl)
        int gruppenNummer = gruppenKennung.Length == 1 && char.IsLetter(gruppenKennung[0])
            ? char.ToUpper(gruppenKennung[0]) - 'A' + 1
            : int.TryParse(gruppenKennung, out int n) ? n : 0;

        if (gruppenNummer <= 0) return null;

        // Bei mehreren Gruppenphase-Runden im selben Turnier (z.B. "Gruppenphase 1" + "Gruppenphase 2")
        // muss die naechstgelegene VORHERGEHENDE Gruppenphase verwendet werden, nicht immer die erste.
        var gruppenphaseRunde = alleRunden
            .Where(r => r.Rundenart == Rundenart.Gruppenphase && r.Reihenfolge < runde.Reihenfolge)
            .OrderByDescending(r => r.Reihenfolge)
            .FirstOrDefault();
        if (gruppenphaseRunde is null) return null;

        return await RangAufloesenAsync(gruppenphaseRunde, gruppenNummer, rang, ct, ignorierteSpielIds, nurSicherPositioniert);
    }

    /// <summary>Löst "N. bester X. Platz" auf (Issue #213): sucht die nächstgelegene vorhergehende
    /// Gruppenphase-Runde, prüft dasselbe konservative All-or-nothing-Gate wie <see cref="BesteNAufloesenAsync"/>
    /// (alle Spiele der Gruppenphase abgeschlossen), und liefert das N-te Team der über
    /// <see cref="Fussballturnierverwaltung.Application.Tabelle.TabellenService.UebergreifendeRangfolgeBerechnenAsync"/>
    /// ermittelten Cross-Group-Rangfolge an Platzierung X. Funktioniert bewusst auch ohne konfigurierte
    /// <see cref="Uebergangsregel"/> (Freitext-Turniere).</summary>
    /// <summary>Issue #372: löst einen Platz wie "3. Gruppe A/D/E/F" über die Kombinationstabelle der Zielrunde
    /// auf — erst die besten Dritten über alle Gruppen ermitteln, deren Gruppenbuchstaben als Schlüssel
    /// nachschlagen, dann den Dritten der dort eingetragenen Gruppe liefern. Wie bei
    /// <see cref="BestePlatzierungAufloessenAsync"/> erst nach vollständig gespielter Gruppenphase: vorher
    /// steht die Menge der qualifizierten Gruppen nicht fest.</summary>
    private async Task<int?> KombinationsPlatzAufloesenAsync(
        string name, int platzierung, TurnierRunde runde, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct,
        IReadOnlySet<int>? ignorierteSpielIds)
    {
        if (Kombinationstabelle.AusJson(runde.KombinationstabelleJson) is not { AnzahlBeste: > 0 } tabelle) return null;

        var gruppenphaseRunde = alleRunden
            .Where(r => r.Rundenart == Rundenart.Gruppenphase && r.Reihenfolge < runde.Reihenfolge)
            .OrderByDescending(r => r.Reihenfolge)
            .FirstOrDefault();
        if (gruppenphaseRunde is null) return null;

        if (await zwischenspeicher.EnthaeltIgnoriertesSpielAsync(gruppenphaseRunde.Id, ignorierteSpielIds, ct)) return null;
        var spiele = await zwischenspeicher.RundenSpieleAsync(gruppenphaseRunde.Id, ct);
        if (spiele.Count == 0 || spiele.Any(s => s.Status != SpielStatus.Abgeschlossen))
            return null;

        var rangfolge = await tabellenService.UebergreifendeRangfolgeBerechnenAsync(gruppenphaseRunde.Id, platzierung, ct);
        if (rangfolge.Count < tabelle.AnzahlBeste) return null;
        var besteIds = rangfolge.Take(tabelle.AnzahlBeste).Select(e => e.Mannschaft.Id).ToHashSet();

        var gruppenTabellen = await zwischenspeicher.GruppenTabellenAsync(gruppenphaseRunde.Id, ct);
        var qualifizierteNachGruppe = new Dictionary<string, int>();
        foreach (var gruppe in gruppenTabellen)
        {
            var mannschaftId = gruppe.Tabelle.FirstOrDefault(e => e.Rang == platzierung)?.Mannschaft.Id;
            if (mannschaftId is { } id && besteIds.Contains(id) && gruppe.GruppenNummer is >= 1 and <= 26)
                qualifizierteNachGruppe[((char)('A' + gruppe.GruppenNummer - 1)).ToString()] = id;
        }
        if (qualifizierteNachGruppe.Count != tabelle.AnzahlBeste) return null;

        return tabelle.GruppeFuer(name, qualifizierteNachGruppe.Keys) is { } zielGruppe
            && qualifizierteNachGruppe.TryGetValue(zielGruppe, out var ergebnis)
            ? ergebnis
            : null;
    }

    private async Task<int?> BestePlatzierungAufloessenAsync(
        int rang, int platzierung, TurnierRunde runde, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct,
        IReadOnlySet<int>? ignorierteSpielIds = null)
    {
        var gruppenphaseRunde = alleRunden
            .Where(r => r.Rundenart == Rundenart.Gruppenphase && r.Reihenfolge < runde.Reihenfolge)
            .OrderByDescending(r => r.Reihenfolge)
            .FirstOrDefault();
        if (gruppenphaseRunde is null) return null;

        if (await zwischenspeicher.EnthaeltIgnoriertesSpielAsync(gruppenphaseRunde.Id, ignorierteSpielIds, ct)) return null;
        var spiele = await zwischenspeicher.RundenSpieleAsync(gruppenphaseRunde.Id, ct);
        if (spiele.Any(s => s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue && s.Status != SpielStatus.Abgeschlossen))
            return null;

        var rangfolge = await tabellenService.UebergreifendeRangfolgeBerechnenAsync(gruppenphaseRunde.Id, platzierung, ct);
        return rang <= rangfolge.Count ? rangfolge[rang - 1].Mannschaft.Id : null;
    }

    private async Task<int?> KoSpielAufloesenAsync(
        PlatzhalterBezug.KoSpiel bezug, TurnierRunde runde, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct,
        IReadOnlySet<int>? ignorierteSpielIds)
    {
        var koRunde = PlatzhalterNamen.KoQuellrunde(bezug, runde, alleRunden);
        if (koRunde is null) return null;

        return await RangAufloesenAsync(koRunde, bezug.SpielNummer, bezug.Verlierer ? 2 : 1, ct, ignorierteSpielIds);
    }

    // ── Regelbasierte Auflösung (Issue #130) ──────────────────────────────

    /// <summary>Löst eine Platzierung einer <see cref="Uebergangsregel"/> zum tatsächlichen Mannschaft-Id auf.
    /// Liefert <c>null</c>, solange die Quellrunde noch nicht (für diese Platzierung) entschieden ist.</summary>
    public Task<int?> UebergangsregelPlatzAufloesenAsync(Uebergangsregel regel, int platzierung, CancellationToken ct = default)
    {
        if (platzierung < regel.PlatzierungVon || platzierung > regel.PlatzierungBis)
            throw new ArgumentOutOfRangeException(nameof(platzierung),
                $"Platzierung {platzierung} liegt außerhalb des Regel-Bereichs {regel.PlatzierungVon}-{regel.PlatzierungBis}.");

        return RangAufloesenAsync(regel.QuellRunde, regel.GruppeIndex, platzierung, ct);
    }

    /// <summary>Löst alle Platzierungen einer <see cref="Uebergangsregel"/> auf (Platzierung → Mannschaft-Id oder
    /// <c>null</c>, solange die Quellrunde für diese Platzierung noch nicht entschieden ist). Bei einer
    /// gruppenübergreifenden Rangfolge-Regel (<see cref="Uebergangsregel.AnzahlBeste"/> gesetzt, Issue #184) ist der
    /// Schlüssel der Rang innerhalb des Cross-Group-Pools (1..AnzahlBeste), nicht die absolute Gruppenplatzierung.</summary>
    public async Task<IReadOnlyDictionary<int, int?>> UebergangsregelQualifikantenAsync(
        Uebergangsregel regel, CancellationToken ct = default)
    {
        if (regel.AnzahlBeste is not null)
        {
            var beste = await BesteNAufloesenAsync(regel, ct);
            return beste.Select((teamId, i) => (Rang: i + 1, TeamId: teamId))
                .ToDictionary(x => x.Rang, x => x.TeamId);
        }

        var ergebnis = new Dictionary<int, int?>();
        for (int platz = regel.PlatzierungVon; platz <= regel.PlatzierungBis; platz++)
            ergebnis[platz] = await RangAufloesenAsync(regel.QuellRunde, regel.GruppeIndex, platz, ct);
        return ergebnis;
    }

    /// <summary>Löst eine gruppenübergreifende Rangfolge-Regel (Issue #184) auf: nimmt das Team an
    /// <see cref="Uebergangsregel.PlatzierungVon"/> aus jeder Gruppe der Quellrunde, vergleicht diese Kandidaten
    /// nach den Ranking-Regeln des Regelwerks und liefert die <see cref="Uebergangsregel.AnzahlBeste"/> besten.
    /// Bewusst konservatives Gate: löst erst auf, sobald ALLE Spiele der Quellrunde (alle Gruppen) abgeschlossen
    /// sind — kein Extremrang-artiger Optimistic-Preview wie bei #202/#206, das wäre eine kombinatorische
    /// Erweiterung über Gruppengrenzen hinweg und würde den Aufwand dieses Features deutlich sprengen.</summary>
    public Task<IReadOnlyList<int?>> UebergangsregelBesteNAufloesenAsync(Uebergangsregel regel, CancellationToken ct = default)
        => BesteNAufloesenAsync(regel, ct);

    private async Task<IReadOnlyList<int?>> BesteNAufloesenAsync(Uebergangsregel regel, CancellationToken ct)
    {
        int n = regel.AnzahlBeste!.Value;
        bool offene = (await zwischenspeicher.RundenSpieleAsync(regel.QuellRundeId, ct))
            .Any(s => s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue && s.Status != SpielStatus.Abgeschlossen);
        if (offene) return [.. Enumerable.Repeat((int?)null, n)];

        var rangfolge = await tabellenService.UebergreifendeRangfolgeBerechnenAsync(regel.QuellRundeId, regel.PlatzierungVon, ct);
        return Enumerable.Range(0, n)
            .Select(i => i < rangfolge.Count ? (int?)rangfolge[i].Mannschaft.Id : null)
            .ToList();
    }

    /// <summary>
    /// Kern-Auflösung "Platzierung X einer Quellrunde → Mannschaft-Id", regelbasiert und unabhängig davon, ob die
    /// Koordinaten (Quellrunde/Gruppenindex/Platzierung) aus einer <see cref="Uebergangsregel"/> oder — für
    /// bestehende, per Freitext angelegte Turniere — aus dem Regex-Parser stammen. Die Bedeutung von
    /// <paramref name="gruppeIndex"/> hängt von der Rundenart der Quellrunde ab:
    /// Gruppenphase → Gruppennummer (Platzierung = Tabellenrang in dieser Gruppe), löst erst auf, sobald der
    /// Rang laut <see cref="TabellenEintrag.SicherPositioniert"/> nicht mehr verdrängbar ist (Issue #202);
    /// Liga/TopfLiga/Schweizer System → nicht verwendet (Platzierung = Tabellenrang der Gesamttabelle), mit
    /// Punkte-Schranken-Gate aus <see cref="TabellenService.LigaTabelleMitSicherheitBerechnenAsync"/> (Issue #343);
    /// K.-o.-Runde/Finale → Spielnummer innerhalb der Runde (Platzierung 1 = Sieger, 2 = Verlierer), bereits
    /// exakt über ein eingetragenes Ergebnis abgesichert, keine Simulation nötig.
    /// </summary>
    private async Task<int?> RangAufloesenAsync(
        TurnierRunde quellRunde, int? gruppeIndex, int platzierung, CancellationToken ct,
        IReadOnlySet<int>? ignorierteSpielIds = null, bool nurSicherPositioniert = true)
    {
        switch (quellRunde.Rundenart)
        {
            case Rundenart.Gruppenphase:
            {
                if (gruppeIndex is null) return null;
                // Issue #88: sobald ein Ergebnis dieser Gruppe zur Löschung ansteht, gilt sie vorübergehend als
                // nicht ermittelbar. Seit #397 trägt das Spiel seine Gruppennummer — ohne sie (Altdaten) bleibt
                // es bei der ganzen Runde.
                if (await zwischenspeicher.EnthaeltIgnoriertesSpielAsync(quellRunde.Id, ignorierteSpielIds, ct, gruppeIndex.Value))
                    return null;
                var gruppenTabellen = await zwischenspeicher.GruppenTabellenAsync(quellRunde.Id, ct);
                var gruppe = gruppenTabellen.FirstOrDefault(g => g.GruppenNummer == gruppeIndex.Value);
                // Issue #202: der aktuelle Rang allein reicht nicht - er darf durch kein Ergebnis der noch
                // offenen Spiele mehr verdrängbar sein, sonst würde die Platzhalter-Auflösung optimistisch
                // vorgreifen (siehe Qualifikationsstatus-Ampel in der Gruppentabelle, Issue #70/#201) — außer
                // die aufrufende Stelle will bewusst genau diese Optimistisch-Auflösung als Vorschau
                // (Issue #208, nurSicherPositioniert=false), die nichts persistiert.
                return gruppe?.Tabelle.FirstOrDefault(e =>
                    e.Rang == platzierung && (!nurSicherPositioniert || e.SicherPositioniert))?.Mannschaft.Id;
            }

            // Liga/TopfLiga/SchweizerSystem teilen sich eine Gesamttabelle über alle Spiele der Runde (Issue
            // #25-Nachlieferung). Issue #343: wie bei der Gruppenphase (#202) nur fest auflösen, wenn der Rang
            // durch die verbleibenden Spiele nicht mehr verdrängbar ist — die optimistische Vorschau (#208,
            // nurSicherPositioniert=false) nutzt weiter den aktuellen Tabellenstand.
            case Rundenart.Liga:
            case Rundenart.TopfLiga:
            case Rundenart.SchweizerSystem:
            {
                if (await zwischenspeicher.EnthaeltIgnoriertesSpielAsync(quellRunde.Id, ignorierteSpielIds, ct)) return null;
                var tabelle = await zwischenspeicher.LigaTabelleAsync(quellRunde.Id, nurSicherPositioniert, ct);
                return tabelle.FirstOrDefault(e =>
                    e.Rang == platzierung && (!nurSicherPositioniert || e.SicherPositioniert))?.Mannschaft.Id;
            }

            case Rundenart.KoRunde:
            case Rundenart.Finale:
            case Rundenart.SpielUmPlatzDrei:
            {
                if (gruppeIndex is not { } spielNummer) return null;
                // Issue #346: gezählt wird je Paarung (Hinspiel); entschieden wird bei Hin-/Rückspiel im
                // Rückspiel, das den Gesamtsieger trägt.
                var alleSpiele = await zwischenspeicher.RundenSpieleAsync(quellRunde.Id, ct);
                var rueckspielJeHinspiel = KoPaarungen.RueckspielJeHinspiel(alleSpiele);
                var paarungen = alleSpiele
                    .Where(s => !s.IstRueckspiel && s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue)
                    .OrderBy(s => s.Spielnummer)
                    .ToList();
                if (paarungen.Count < spielNummer) return null;

                var hinspiel = paarungen[spielNummer - 1];
                var spiel    = rueckspielJeHinspiel.GetValueOrDefault(hinspiel.Id) ?? hinspiel;
                if (ignorierteSpielIds is not null &&
                    (ignorierteSpielIds.Contains(hinspiel.Id) || ignorierteSpielIds.Contains(spiel.Id)))
                    return null;

                return KoPaarungen.SiegerOderVerliererId(spiel, platzierung);
            }

            default:
                return null;
        }
    }

    private async Task SpeichernAsync(SpielplanErgebnis ergebnis, CancellationToken ct)
    {
        if (!ergebnis.HatHinUndRueckspiele)
        {
            await spielRepository.HinzufuegenMehrfachAsync(ergebnis.Spiele, ct);
            return;
        }

        // Schritt 1: Hinspiele speichern → EF Core setzt IDs
        var hinspielIndizes = ergebnis.HinRueckspielPaare.Select(p => p.HinspielIndex).ToHashSet();
        var hinspiele = ergebnis.Spiele
            .Where((_, i) => hinspielIndizes.Contains(i)).ToList();
        await spielRepository.HinzufuegenMehrfachAsync(hinspiele, ct);

        // Schritt 2: HinspielId auf Rückspiele setzen und speichern
        foreach (var (hinIdx, rueckIdx) in ergebnis.HinRueckspielPaare)
            ergebnis.Spiele[rueckIdx].HinspielId = ergebnis.Spiele[hinIdx].Id;

        var rueckspielIndizes = ergebnis.HinRueckspielPaare.Select(p => p.RueckspielIndex).ToHashSet();
        var rueckspiele = ergebnis.Spiele
            .Where((_, i) => rueckspielIndizes.Contains(i)).ToList();
        await spielRepository.HinzufuegenMehrfachAsync(rueckspiele, ct);
    }
}
