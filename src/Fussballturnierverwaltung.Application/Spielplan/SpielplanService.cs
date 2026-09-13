using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;
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
                var gruppen = ergebnisse
                    .Where(e => e.HeimMannschaftId.HasValue)
                    .GroupBy(e => e.PaarungsNummer)
                    .OrderBy(g => g.Key)
                    .Select(g => (IReadOnlyList<int>)g.Select(e => e.HeimMannschaftId!.Value).ToList())
                    .ToList();

                if (gruppen.Any())
                {
                    await GenerierenAusGruppenAsync(turnierRundeId, gruppen, ct);
                }
                else
                {
                    // Platzhalter-Gruppenphase: Berger-Round-Robin mit Platzhalternamen
                    var gruppenNamen = ergebnisse
                        .GroupBy(e => e.PaarungsNummer)
                        .OrderBy(g => g.Key)
                        .Select(g => (IReadOnlyList<string>)g.Select(e => e.HeimPlatzhalterName ?? "?").ToList())
                        .ToList();

                    if (!gruppenNamen.Any() || gruppenNamen.All(g => g.Count < 2))
                        throw new InvalidOperationException(ergebnisse.Any()
                            ? "Keine gültigen Gruppen-Zuordnungen in der Auslosung gefunden."
                            : "Die Auslosung hat keine Ergebnisse. Bitte die Auslosung zuerst durchführen.");

                    await GenerierenAusGruppenMitPlatzhalternAsync(turnierRundeId, gruppenNamen, ct);
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
        await SpielQuellenAlleTransitionsVerknuepfenAsync(runde.TurnierInstanzId, ct);
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
        var bereitsGespielt = vorhandeneSpiele
            .Where(s => s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue)
            .Select(s => (
                TeamAId: Math.Min(s.HeimMannschaftId!.Value, s.AuswaertsMannschaftId!.Value),
                TeamBId: Math.Max(s.HeimMannschaftId!.Value, s.AuswaertsMannschaftId!.Value)))
            .Distinct()
            .ToList<(int TeamAId, int TeamBId)>();

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
            Spieltag:        naechsterSpieltag);

        var ergebnis = generator.Generieren(kontext);
        await SpeichernAsync(ergebnis, ct);

        logger.LogInformation(
            "Schweizer Runde {Spieltag} für '{Runde}' generiert: {Anzahl} Spiele.",
            naechsterSpieltag, runde.Name, ergebnis.Spiele.Count);

        return ergebnis;
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
                gruppenMitSpielen.Add((gi, spiel));
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

    public async Task GenerierenAusGruppenMitPlatzhalternAsync(
        int turnierRundeId,
        IReadOnlyList<IReadOnlyList<string>> gruppen,
        CancellationToken ct = default)
    {
        var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Runde mit ID {turnierRundeId} nicht gefunden.");

        int startNr = await spielRepository.NaechsteSpielNummerAsync(runde.TurnierInstanzId, turnierRundeId, runde.Reihenfolge, ct);
        int tempNr  = 1;
        var gruppenMitSpielen = new List<(int GruppenIndex, Spiel Spiel)>();

        for (int gi = 0; gi < gruppen.Count; gi++)
        {
            var namen = gruppen[gi].ToList();
            if (namen.Count < 2) continue;

            var gruppenSpiele = new List<Spiel>();
            bool hatDummy = namen.Count % 2 == 1;
            if (hatDummy) namen.Add(null!);

            int gesamt    = namen.Count;
            int spieltage = gesamt - 1;
            var kreis     = Enumerable.Range(1, gesamt - 1).ToArray();

            for (int spieltag = 0; spieltag < spieltage; spieltag++)
            {
                string? heim0 = spieltag % 2 == 0 ? namen[0]          : namen[kreis[^1]];
                string? ausw0 = spieltag % 2 == 0 ? namen[kreis[^1]]  : namen[0];

                if (heim0 is not null && ausw0 is not null)
                    gruppenSpiele.Add(PlatzhalterSpiel(runde, heim0, ausw0, tempNr++, spieltag + 1));

                for (int i = 0; i < (gesamt - 2) / 2; i++)
                {
                    string? heim = namen[kreis[i]];
                    string? ausw = namen[kreis[gesamt - 3 - i]];
                    if (heim is not null && ausw is not null)
                        gruppenSpiele.Add(PlatzhalterSpiel(runde, heim, ausw, tempNr++, spieltag + 1));
                }

                var letzter = kreis[^1];
                for (int k = gesamt - 2; k > 0; k--) kreis[k] = kreis[k - 1];
                kreis[0] = letzter;
            }

            if (runde.MitRueckspiel)
            {
                foreach (var s in gruppenSpiele.ToList())
                    gruppenSpiele.Add(PlatzhalterSpiel(runde,
                        s.AuswaertsPlatzhalterName!, s.HeimPlatzhalterName!,
                        tempNr++, (s.Spieltag ?? 0) + spieltage, istRueckspiel: true));
            }

            foreach (var spiel in gruppenSpiele)
                gruppenMitSpielen.Add((gi, spiel));
        }

        var alleSpiele = gruppenMitSpielen
            .OrderBy(x => x.Spiel.Spieltag ?? 0)
            .ThenBy(x => x.GruppenIndex)
            .Select((x, i) => { x.Spiel.Spielnummer = startNr + i; return x.Spiel; })
            .ToList();

        if (alleSpiele.Count > 0)
            await spielRepository.HinzufuegenMehrfachAsync(alleSpiele, ct);

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

        var spiele = paarungen.Select((p, i) => new Spiel
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

        logger.LogInformation(
            "Platzhalter-Spielplan für Runde '{Runde}' generiert: {Anzahl} Spiele.",
            runde.Name, spiele.Count);

        return spiele.Count;
    }

    /// <summary>
    /// Löst Platzhalternamen in einer KO-Runde auf: sucht in Vorrunden-Tabellen und KO-Ergebnissen
    /// nach den realen Teams und aktualisiert die Spiele. Gibt die Anzahl aufgelöster Spiele zurück.
    /// </summary>
    public async Task<int> PlatzhalterAufloesenAsync(int turnierRundeId, CancellationToken ct = default)
    {
        var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Runde mit ID {turnierRundeId} nicht gefunden.");

        // Alle Runden derselben TurnierInstanz laden (für Suche nach Vorrunden)
        var alleRunden = await rundeRepository.AlleNachInstanzAsync(runde.TurnierInstanzId, ct);

        var platzhalterSpiele = (await spielRepository.AlleNachRundeAsync(turnierRundeId, ct))
            .Where(s => s.HeimMannschaftId is null || s.AuswaertsMannschaftId is null)
            .ToList();

        if (platzhalterSpiele.Count == 0) return 0;

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

        int aufgeloest = 0;
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

            await spielRepository.PlatzhalterKorrigierenAsync(
                spiel.Id,
                heimId, spiel.HeimPlatzhalterName,
                auswaertsId, spiel.AuswaertsPlatzhalterName,
                ct);
            aufgeloest++;
        }

        logger.LogInformation(
            "Platzhalter in Runde '{Runde}' aufgelöst: {Aufgeloest}/{Gesamt} Spiele.",
            runde.Name, aufgeloest, platzhalterSpiele.Count);

        return aufgeloest;
    }

    /// <summary>Rein lesende Vorschau (Issue #208, Ergänzung zu #202): welcher Mannschaftsname würde nach
    /// dem aktuellen, noch nicht zwingend sicheren Tabellenstand jede noch offene Platzhalter-Seite einer
    /// Runde besetzen? Schreibt nichts in die Datenbank. Liefert nur Einträge für Spiele, bei denen
    /// mindestens eine Seite optimistisch (ohne SicherPositioniert-Gate) ermittelbar ist; bereits real
    /// zugewiesene Seiten liefern keinen Vorschau-Wert (null).</summary>
    public async Task<IReadOnlyDictionary<int, (string? Heim, string? Auswaerts)>> PlatzhalterVorschauAsync(
        int turnierRundeId, CancellationToken ct = default)
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
    public async Task<int> PlatzhalterAlleRundenAufloesenAsync(int turnierInstanzId, CancellationToken ct = default)
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
            var liste = (await spielRepository.AlleNachRundeAsync(quellRunde.Id, ct))
                .OrderBy(s => s.Spielnummer)
                .ToList();
            quellSpieleProRunde[quellRunde.Id] = liste;
            return liste;
        }

        // Bracket-Slot-Position ist ab Erzeugung der Runde stabil — bewusst NICHT wie im Regex-/
        // Uebergangsregel-Pfad (RangAufloesenAsync) auf bereits aufgelöste Spiele gefiltert.
        async Task<(int SpielId, SpielQuellErgebnisTyp Typ)?> QuelleAufloesenAsync(string? platzhalterName)
        {
            if (string.IsNullOrWhiteSpace(platzhalterName)) return null;

            bool verlierer = false;
            var match = _koSiegerRx.Match(platzhalterName);
            if (!match.Success)
            {
                match = _koVerliererRx.Match(platzhalterName);
                verlierer = true;
            }
            if (!match.Success || !int.TryParse(match.Groups[2].Value, out int spielNr)) return null;

            var arten = verlierer
                ? new[] { Rundenart.KoRunde }
                : new[] { Rundenart.KoRunde, Rundenart.Finale };
            var quellRunde = NaechstgelegeneKoQuellrunde(match.Groups[1].Value, arten, zielRunde, alleRunden);
            if (quellRunde is null) return null;

            var quellSpiele = await QuellSpieleAsync(quellRunde);
            if (spielNr < 1 || spielNr > quellSpiele.Count) return null;

            return (quellSpiele[spielNr - 1].Id, verlierer ? SpielQuellErgebnisTyp.Verlierer : SpielQuellErgebnisTyp.Sieger);
        }

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

            if (geaendert)
                await spielRepository.AktualisierenAsync(spiel, ct);
        }
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

        return SiegerOderVerliererId(quellSpiel, typ == SpielQuellErgebnisTyp.Sieger ? 1 : 2);
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
    public async Task<KaskadenPlan> PlatzhalterKaskadePlanenAsync(
        int turnierInstanzId, IReadOnlySet<int> zusaetzlichIgnorierteSpielIds, CancellationToken ct = default)
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

            // IstFreilos ausgeschlossen: GenerierenAusPlatzhalterPaarungenAsync/
            // GenerierenAusGruppenMitPlatzhalternAsync erzeugen dafür kein Spiel — ohne denselben
            // Filter hier würde die positionale Zuordnung unten um ein Freilos verschoben sein.
            var platzhalterEintraege = auslosung.Ergebnisse
                .Where(e => !e.IstFreilos && (e.HeimPlatzhalterName is not null || e.AuswaertsPlatzhalterName is not null))
                .OrderBy(e => e.PaarungsNummer)
                .ToList();
            if (platzhalterEintraege.Count == 0) continue;

            var aktuelleSpiele = (await spielRepository.AlleNachRundeAsync(runde.Id, ct))
                .OrderBy(s => s.Spielnummer)
                .ToList();

            // Eine Auslosung-basierte Platzhalter-Runde ohne ein einziges Spiel muss komplett neu
            // generiert werden — z.B. wenn die enge #125-Logik (FinaleAutomatischGenerierenWennBereitAsync)
            // das einzige, noch nicht gespielte Spiel bereits entfernt hat, weil ihre eigene
            // (Auslosung-unabhängige) Heuristik die Quelle als nicht mehr entschieden einstuft, aber
            // mangels erkanntem Nachfolger nicht selbst neu generiert. Kein Ergebnisverlust möglich,
            // da nichts vorhanden ist.
            if (aktuelleSpiele.Count == 0)
            {
                schritte.Add(new KaskadenRundeNeuGenerierenSchritt(runde.Id, runde.Name));
                continue;
            }

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

                // Nur ein bereits konkret aufgelöster Slot, der nicht mehr zum neu ermittelten Soll-Wert
                // passt, ist ein Kaskaden-Fall — ein noch nie aufgelöster Platzhalter ist normales,
                // ausstehendes Auflösen und kein Rücksetz-Fall.
                bool heimBetroffen = spiel.HeimMannschaftId.HasValue && spiel.HeimMannschaftId != sollHeim;
                bool auswBetroffen = spiel.AuswaertsMannschaftId.HasValue && spiel.AuswaertsMannschaftId != sollAusw;
                if (!heimBetroffen && !auswBetroffen) continue;

                bool hatteErgebnis = spiel.Ergebnis is not null;
                schritte.Add(new KaskadenSpielSchritt(
                    spiel.Id, runde.Id, runde.Name, hatteErgebnis,
                    sollHeim, sollAusw, eintrag.HeimPlatzhalterName, eintrag.AuswaertsPlatzhalterName));

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
        foreach (var schritt in plan.Schritte)
        {
            switch (schritt)
            {
                case KaskadenSpielSchritt spielSchritt:
                    if (spielSchritt.HatteErgebnis)
                        await spielRepository.ErgebnisLoeschenAsync(spielSchritt.SpielId, ct);

                    await spielRepository.PlatzhalterKorrigierenAsync(
                        spielSchritt.SpielId,
                        spielSchritt.NeuesHeimMannschaftId, spielSchritt.HeimPlatzhalterName,
                        spielSchritt.NeuesAuswaertsMannschaftId, spielSchritt.AuswaertsPlatzhalterName,
                        ct);

                    logger.LogInformation(
                        "Spiel {SpielId} in Runde '{Runde}' im Rahmen der Kaskade korrigiert.",
                        spielSchritt.SpielId, spielSchritt.RundeName);
                    break;

                case KaskadenRundeNeuGenerierenSchritt rundeSchritt:
                    var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(rundeSchritt.RundeId, ct);
                    if (runde is null) continue;

                    var auslosung = await auslosungRepository.FindenAbgeschlosseneNachRundeAsync(
                        runde.TurnierInstanzId, runde.Id, ct);
                    if (auslosung is null) continue;

                    await GenerierenAusAuslosungAsync(rundeSchritt.RundeId, [.. auslosung.Ergebnisse], ct);
                    await PlatzhalterAufloesenAsync(rundeSchritt.RundeId, ct);

                    logger.LogInformation(
                        "Runde '{Runde}' im Rahmen der Kaskade neu generiert.", rundeSchritt.RundeName);
                    break;
            }
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

    private static readonly Regex _gruppenSiegerRx     = new(@"^Sieger Gruppe ([A-Z0-9]+)$",   RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _gruppenRangRx       = new(@"^(\d+)\. Gruppe ([A-Z0-9]+)$",  RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _bestePlatzierungRx  = new(@"^(\d+)\. bester (\d+)\. Platz$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _koSiegerRx          = new(@"^Sieger ([A-Za-z0-9.]+) (\d+)$",   RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _koVerliererRx       = new(@"^Verlierer ([A-Za-z0-9.]+) (\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private async Task<int?> PlatzhalterNameAufloessenAsync(
        string name, TurnierRunde runde, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct,
        IReadOnlySet<int>? ignorierteSpielIds = null, bool nurSicherPositioniert = true)
    {
        // Pattern 1: "Sieger Gruppe A" → Rang 1 der Gruppe A
        var m = _gruppenSiegerRx.Match(name);
        if (m.Success)
            return await GruppenrangAufloessenAsync(m.Groups[1].Value, 1, runde, alleRunden, ct, ignorierteSpielIds, nurSicherPositioniert);

        // Pattern 2: "2. Gruppe A" → Rang 2 der Gruppe A
        m = _gruppenRangRx.Match(name);
        if (m.Success && int.TryParse(m.Groups[1].Value, out int rang))
            return await GruppenrangAufloessenAsync(m.Groups[2].Value, rang, runde, alleRunden, ct, ignorierteSpielIds, nurSicherPositioniert);

        // Pattern 3: "2. bester 3. Platz" → gruppenübergreifende Rangfolge-Regel (Issue #213)
        m = _bestePlatzierungRx.Match(name);
        if (m.Success && int.TryParse(m.Groups[1].Value, out int bestenRang) && int.TryParse(m.Groups[2].Value, out int platzierung))
            return await BestePlatzierungAufloessenAsync(bestenRang, platzierung, runde, alleRunden, ct, ignorierteSpielIds);

        // Pattern 4: "Sieger VF 1" → Sieger des 1. Spiels im Viertelfinale
        m = _koSiegerRx.Match(name);
        if (m.Success && int.TryParse(m.Groups[2].Value, out int spielNr))
            return await KoSiegerAufloessenAsync(m.Groups[1].Value, spielNr, runde, alleRunden, ct, ignorierteSpielIds);

        // Pattern 5: "Verlierer HF 1" → Verlierer des 1. Spiels im Halbfinale
        m = _koVerliererRx.Match(name);
        if (m.Success && int.TryParse(m.Groups[2].Value, out spielNr))
            return await KoVerliererAufloessenAsync(m.Groups[1].Value, spielNr, runde, alleRunden, ct, ignorierteSpielIds);

        return null;
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
    private async Task<int?> BestePlatzierungAufloessenAsync(
        int rang, int platzierung, TurnierRunde runde, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct,
        IReadOnlySet<int>? ignorierteSpielIds = null)
    {
        var gruppenphaseRunde = alleRunden
            .Where(r => r.Rundenart == Rundenart.Gruppenphase && r.Reihenfolge < runde.Reihenfolge)
            .OrderByDescending(r => r.Reihenfolge)
            .FirstOrDefault();
        if (gruppenphaseRunde is null) return null;

        var spiele = await spielRepository.AlleNachRundeAsync(gruppenphaseRunde.Id, ct);
        if (ignorierteSpielIds is { Count: > 0 } && spiele.Any(s => ignorierteSpielIds.Contains(s.Id)))
            return null;
        if (spiele.Any(s => s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue && s.Status != SpielStatus.Abgeschlossen))
            return null;

        var rangfolge = await tabellenService.UebergreifendeRangfolgeBerechnenAsync(gruppenphaseRunde.Id, platzierung, ct);
        return rang <= rangfolge.Count ? rangfolge[rang - 1].Mannschaft.Id : null;
    }

    private static int? KuerzelZuEingangsteams(string kuerzel) => kuerzel.ToUpperInvariant() switch
    {
        "HF"      => 4,
        "VF"      => 8,
        "AF"      => 16,
        "FINALE"  => 2,
        _ when kuerzel.StartsWith("R", StringComparison.OrdinalIgnoreCase)
              && int.TryParse(kuerzel[1..], out int n) => n * 2,
        _         => null
    };

    private async Task<int?> KoSiegerAufloessenAsync(
        string kuerzel, int spielNummer, TurnierRunde runde, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct,
        IReadOnlySet<int>? ignorierteSpielIds = null)
    {
        var koRunde = NaechstgelegeneKoQuellrunde(kuerzel, [Rundenart.KoRunde, Rundenart.Finale], runde, alleRunden);
        if (koRunde is null) return null;

        return await RangAufloesenAsync(koRunde, spielNummer, 1, ct, ignorierteSpielIds);
    }

    private async Task<int?> KoVerliererAufloessenAsync(
        string kuerzel, int spielNummer, TurnierRunde runde, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct,
        IReadOnlySet<int>? ignorierteSpielIds = null)
    {
        var koRunde = NaechstgelegeneKoQuellrunde(kuerzel, [Rundenart.KoRunde], runde, alleRunden);
        if (koRunde is null) return null;

        return await RangAufloesenAsync(koRunde, spielNummer, 2, ct, ignorierteSpielIds);
    }

    /// <summary>Findet die nächstgelegene VORHERGEHENDE K.o.-Runde, auf die sich ein Platzhaltertext wie
    /// "Sieger Halbfinale 1" bezieht. Zuerst per EXAKTEM Rundennamen (Fund: <see cref="TeilnehmerPoolHilfsmittel"/>
    /// — bzw. deren WPF-Pendant — erzeugt diesen Text immer aus dem echten Rundennamen, nie aus einer Kurzform;
    /// keine der mitgelieferten Turniervorlagen benennt ihre Runden "VF"/"HF"/"AF", wodurch die Kürzel-Tabelle
    /// unten in der Praxis nie traf und K.o.-Platzhalter nach der ersten K.o.-Runde nie automatisch aufgelöst
    /// wurden — Nutzerfund: "Sieger/Verlierer"-Übergänge zwischen zwei K.o.-Runden funktionierten nie, weder
    /// einzeln noch simuliert, weder produktiv noch in der Vorschau). Fällt nur mangels Namenstreffer auf die
    /// alte Kürzel-zu-Eingangsteams-Tabelle zurück (z.B. "VF" → 8) — deckt Turniere ab, deren Runden tatsächlich
    /// so kurz benannt sind, und hält die bestehenden Tests unverändert grün. Bei mehreren K.o.-Zweigen gleicher
    /// Größe/gleichen Namens im selben Turnier ist das die per <see cref="TurnierRunde.Reihenfolge"/> nächste,
    /// nicht immer die erste.</summary>
    private static TurnierRunde? NaechstgelegeneKoQuellrunde(
        string kuerzel, IReadOnlyList<Rundenart> arten, TurnierRunde runde, IReadOnlyList<TurnierRunde> alleRunden)
    {
        var kandidaten = alleRunden.Where(r => arten.Contains(r.Rundenart) && r.Reihenfolge < runde.Reihenfolge);

        var perName = kandidaten
            .Where(r => string.Equals(r.Name, kuerzel, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Reihenfolge)
            .FirstOrDefault();
        if (perName is not null) return perName;

        int? eingangTeams = KuerzelZuEingangsteams(kuerzel);
        if (eingangTeams is null) return null;

        return kandidaten
            .Where(r => r.EingangsMannschaftsanzahl == eingangTeams.Value)
            .OrderByDescending(r => r.Reihenfolge)
            .FirstOrDefault();
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
        bool offene = (await spielRepository.AlleNachRundeAsync(regel.QuellRundeId, ct))
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
    /// Liga → nicht verwendet (Platzierung = Tabellenrang der Gesamttabelle), bewusst noch ohne
    /// SicherPositioniert-Gate (kein Qualifikationsstatus-Äquivalent in <see cref="TabellenService"/> für
    /// Liga-Tabellen, siehe Folge-Issue zu #202);
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
                // Issue #88: sobald irgendein Ergebnis dieser Quellrunde zur Löschung ansteht, gilt die
                // gesamte Runde vorübergehend als nicht ermittelbar — Spiel trägt kein GruppeNummer-Feld,
                // eine praezisere Nur-diese-Gruppe-Prüfung wäre nur über TabellenService mit einer neuen
                // Ausschluss-Fähigkeit möglich (bewusst nicht umgesetzt, siehe Plan-Kontext).
                if (ignorierteSpielIds is { Count: > 0 } &&
                    (await spielRepository.AlleNachRundeAsync(quellRunde.Id, ct)).Any(s => ignorierteSpielIds.Contains(s.Id)))
                    return null;
                var gruppenTabellen = await tabellenService.GruppenTabellenBerechnenAsync(quellRunde.Id, ct: ct);
                var gruppe = gruppenTabellen.FirstOrDefault(g => g.GruppenNummer == gruppeIndex.Value);
                // Issue #202: der aktuelle Rang allein reicht nicht - er darf durch kein Ergebnis der noch
                // offenen Spiele mehr verdrängbar sein, sonst würde die Platzhalter-Auflösung optimistisch
                // vorgreifen (siehe Qualifikationsstatus-Ampel in der Gruppentabelle, Issue #70/#201) — außer
                // die aufrufende Stelle will bewusst genau diese Optimistisch-Auflösung als Vorschau
                // (Issue #208, nurSicherPositioniert=false), die nichts persistiert.
                return gruppe?.Tabelle.FirstOrDefault(e =>
                    e.Rang == platzierung && (!nurSicherPositioniert || e.SicherPositioniert))?.Mannschaft.Id;
            }

            // Liga-Runden bewusst unverändert: TabelleBerechnenAsync (unten) berechnet noch kein
            // SicherPositioniert-Äquivalent - eigenes Folge-Issue zu #202. SchweizerSystem teilt sich
            // denselben Zweig (Issue #25-Nachlieferung): TabelleBerechnenAsync ist Rundenart-unabhängig
            // und aggregiert einfach alle Spiele der Runde, unabhängig von Voll- oder Teilrunde.
            case Rundenart.Liga:
            case Rundenart.SchweizerSystem:
            {
                if (ignorierteSpielIds is { Count: > 0 } &&
                    (await spielRepository.AlleNachRundeAsync(quellRunde.Id, ct)).Any(s => ignorierteSpielIds.Contains(s.Id)))
                    return null;
                var tabelle = await tabellenService.TabelleBerechnenAsync(quellRunde.Id, ct: ct);
                return tabelle.FirstOrDefault(e => e.Rang == platzierung)?.Mannschaft.Id;
            }

            case Rundenart.KoRunde:
            case Rundenart.Finale:
            case Rundenart.SpielUmPlatzDrei:
            {
                if (gruppeIndex is not { } spielNummer) return null;
                var spiele = (await spielRepository.AlleNachRundeAsync(quellRunde.Id, ct))
                    .Where(s => s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue)
                    .OrderBy(s => s.Spielnummer)
                    .ToList();
                if (spiele.Count < spielNummer) return null;

                var spiel = spiele[spielNummer - 1];
                if (ignorierteSpielIds is not null && ignorierteSpielIds.Contains(spiel.Id)) return null;

                return SiegerOderVerliererId(spiel, platzierung);
            }

            default:
                return null;
        }
    }

    /// <summary>Liefert je nach <paramref name="platzierung"/> (1 = Sieger, 2 = Verlierer) die Mannschafts-ID
    /// aus einem entschiedenen K.o.-Spiel, oder <c>null</c> falls noch kein Ergebnis vorliegt.</summary>
    private static int? SiegerOderVerliererId(Spiel spiel, int platzierung)
    {
        var sieger = spiel.Ergebnis?.SiegerMannschaftId;
        if (sieger is null) return null;

        return platzierung switch
        {
            1 => sieger,
            2 => sieger == spiel.HeimMannschaftId ? spiel.AuswaertsMannschaftId : spiel.HeimMannschaftId,
            _ => null
        };
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
