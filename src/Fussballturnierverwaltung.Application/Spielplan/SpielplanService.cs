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
    AuslosungService auslosungService,
    ILogger<SpielplanService> logger)
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
        var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Runde mit ID {turnierRundeId} nicht gefunden.");
        await turnierBaumRepository.SpielIdsNullSetzenAsync(turnierRundeId, ct);
        await spielRepository.AlleNachRundeLoeschenAsync(turnierRundeId, ct);
        logger.LogInformation("Spielplan für Runde '{Runde}' gelöscht.", runde.Name);
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

        async Task<int?> AufloessenAsync(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (cache.TryGetValue(name, out var cached)) return cached;
            var result = await PlatzhalterNameAufloessenAsync(name, runde, alleRunden, ct);
            cache[name] = result;
            return result;
        }

        int aufgeloest = 0;
        foreach (var spiel in platzhalterSpiele)
        {
            // Issue #206: jede Seite unabhängig auflösen (TV-Präsentation-Stil) — eine Seite kann
            // schon feststehen, während die andere noch als Platzhaltertext offen bleibt, statt beide
            // Seiten erst gemeinsam zu schreiben, wenn beide auflösbar sind. PlatzhalterKorrigierenAsync
            // (aus #88) unterstützt unabhängige Seiten bereits — für die jeweils noch nicht auflösbare
            // Seite wird schlicht der unveränderte Platzhaltertext zurückgeschrieben (No-Op).
            var heimId      = spiel.HeimMannschaftId      ?? await AufloessenAsync(spiel.HeimPlatzhalterName);
            var auswaertsId = spiel.AuswaertsMannschaftId ?? await AufloessenAsync(spiel.AuswaertsPlatzhalterName);

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

        async Task<int?> VorschauAufloessenAsync(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (cache.TryGetValue(name, out var cached)) return cached;
            var result = await PlatzhalterNameAufloessenAsync(name, runde, alleRunden, ct, nurSicherPositioniert: false);
            cache[name] = result;
            return result;
        }

        var vorschauIds = new Dictionary<int, (int? Heim, int? Auswaerts)>();
        foreach (var spiel in platzhalterSpiele)
        {
            int? heimVorschau      = spiel.HeimMannschaftId      is null ? await VorschauAufloessenAsync(spiel.HeimPlatzhalterName)      : null;
            int? auswaertsVorschau = spiel.AuswaertsMannschaftId is null ? await VorschauAufloessenAsync(spiel.AuswaertsPlatzhalterName) : null;
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
    /// <c>platzhalterSpiele.Count == 0</c>).</summary>
    public async Task PlatzhalterAlleRundenAufloesenAsync(int turnierInstanzId, CancellationToken ct = default)
    {
        var runden = await rundeRepository.AlleNachInstanzAsync(turnierInstanzId, ct);
        foreach (var runde in runden.OrderBy(r => r.Reihenfolge))
            await PlatzhalterAufloesenAsync(runde.Id, ct);
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
            var auslosung = await auslosungService.FindenAbgeschlosseneNachRundeAsync(turnierInstanzId, runde.Id, ct);
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

                    var auslosung = await auslosungService.FindenAbgeschlosseneNachRundeAsync(
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
            if (await auslosungService.FindenAbgeschlosseneNachRundeAsync(turnierInstanzId, ziel.Id, ct) is not null)
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

            await LoeschenAsync(ziel.Id, ct);
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

    private static readonly Regex _gruppenSiegerRx  = new(@"^Sieger Gruppe ([A-Z0-9]+)$",   RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _gruppenRangRx    = new(@"^(\d+)\. Gruppe ([A-Z0-9]+)$",  RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _koSiegerRx       = new(@"^Sieger ([A-Za-z0-9.]+) (\d+)$",   RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _koVerliererRx    = new(@"^Verlierer ([A-Za-z0-9.]+) (\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

        // Pattern 3: "Sieger VF 1" → Sieger des 1. Spiels im Viertelfinale
        m = _koSiegerRx.Match(name);
        if (m.Success && int.TryParse(m.Groups[2].Value, out int spielNr))
            return await KoSiegerAufloessenAsync(m.Groups[1].Value, spielNr, runde, alleRunden, ct, ignorierteSpielIds);

        // Pattern 4: "Verlierer HF 1" → Verlierer des 1. Spiels im Halbfinale
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
        int? eingangTeams = KuerzelZuEingangsteams(kuerzel);
        if (eingangTeams is null) return null;

        // Bei mehreren KO-Zweigen mit gleicher Eingangs-Mannschaftsanzahl im selben Turnier
        // muss die naechstgelegene VORHERGEHENDE Runde verwendet werden, nicht immer die erste.
        var koRunde = alleRunden
            .Where(r => r.Rundenart is Rundenart.KoRunde or Rundenart.Finale &&
                        r.EingangsMannschaftsanzahl == eingangTeams.Value &&
                        r.Reihenfolge < runde.Reihenfolge)
            .OrderByDescending(r => r.Reihenfolge)
            .FirstOrDefault();
        if (koRunde is null) return null;

        return await RangAufloesenAsync(koRunde, spielNummer, 1, ct, ignorierteSpielIds);
    }

    private async Task<int?> KoVerliererAufloessenAsync(
        string kuerzel, int spielNummer, TurnierRunde runde, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct,
        IReadOnlySet<int>? ignorierteSpielIds = null)
    {
        int? eingangTeams = KuerzelZuEingangsteams(kuerzel);
        if (eingangTeams is null) return null;

        // Bei mehreren KO-Zweigen mit gleicher Eingangs-Mannschaftsanzahl im selben Turnier
        // muss die naechstgelegene VORHERGEHENDE Runde verwendet werden, nicht immer die erste.
        var koRunde = alleRunden
            .Where(r => r.Rundenart is Rundenart.KoRunde &&
                        r.EingangsMannschaftsanzahl == eingangTeams.Value &&
                        r.Reihenfolge < runde.Reihenfolge)
            .OrderByDescending(r => r.Reihenfolge)
            .FirstOrDefault();
        if (koRunde is null) return null;

        return await RangAufloesenAsync(koRunde, spielNummer, 2, ct, ignorierteSpielIds);
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
    /// <c>null</c>, solange die Quellrunde für diese Platzierung noch nicht entschieden ist).</summary>
    public async Task<IReadOnlyDictionary<int, int?>> UebergangsregelQualifikantenAsync(
        Uebergangsregel regel, CancellationToken ct = default)
    {
        var ergebnis = new Dictionary<int, int?>();
        for (int platz = regel.PlatzierungVon; platz <= regel.PlatzierungBis; platz++)
            ergebnis[platz] = await RangAufloesenAsync(regel.QuellRunde, regel.GruppeIndex, platz, ct);
        return ergebnis;
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
            // SicherPositioniert-Äquivalent - eigenes Folge-Issue zu #202.
            case Rundenart.Liga:
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

                var spiel  = spiele[spielNummer - 1];
                if (ignorierteSpielIds is not null && ignorierteSpielIds.Contains(spiel.Id)) return null;
                var sieger = spiel.Ergebnis?.SiegerMannschaftId;
                if (sieger is null) return null;

                return platzierung switch
                {
                    1 => sieger,
                    2 => sieger == spiel.HeimMannschaftId ? spiel.AuswaertsMannschaftId : spiel.HeimMannschaftId,
                    _ => null
                };
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
