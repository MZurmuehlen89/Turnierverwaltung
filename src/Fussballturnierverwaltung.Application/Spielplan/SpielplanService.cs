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
    KoQuellverknuepfung quellverknuepfung,
    PlatzhalterAufloesung aufloesung,
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
            await quellverknuepfung.AlleRundenVerknuepfenAsync(runde.TurnierInstanzId, ct);

        // Issue #346-Folgefund: bereits feststehende Teilnehmer sofort eintragen — sonst zeigte eine neu
        // generierte Runde nach bereits entschiedener Vorrunde wieder reine Platzhalter, bis irgendwo das
        // nächste Ergebnis die Auflösung anstieß (alle Aufrufer: Spielplan, Auslosung, Vorbelegung, Kaskade).
        using (logger.Vorgang($"Platzhalter auflösen nach Generierung (Runde {turnierRundeId})"))
            await aufloesung.PlatzhalterAufloesenAsync(turnierRundeId, ct);
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
                    ? await aufloesung.PlatzhalterNameAufloessenAsync(name, runde, alleRunden, ct, ignoriert)
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
                    ? await aufloesung.PlatzhalterNameAufloessenAsync(heimName, runde, alleRunden, ct, ignoriert)
                    : eintrag.HeimMannschaftId;
                var sollAusw = eintrag.AuswaertsPlatzhalterName is { } auswName
                    ? await aufloesung.PlatzhalterNameAufloessenAsync(auswName, runde, alleRunden, ct, ignoriert)
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
                var beste = await aufloesung.UebergangsregelBesteNAufloesenAsync(regel, ct);
                if (beste.Any(t => t is null)) return (null, "");
                teilnehmer.AddRange(beste.Select(t => t!.Value));
                continue;
            }

            for (int platz = regel.PlatzierungVon; platz <= regel.PlatzierungBis; platz++)
            {
                var teamId = await aufloesung.UebergangsregelPlatzAufloesenAsync(regel, platz, ct);
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
