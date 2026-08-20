using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Spiele.Eingaben;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Simulation;

public class SimulationsService(
    ISpielRepository spielRepository,
    ITurnierInstanzRepository instanzRepository,
    SpielErgebnisService ergebnisService,
    TurnierBaumService turnierBaumService,
    ISimulationsLaufRepository laufRepository,
    ILogger<SimulationsService> logger)
{
    public Task<IReadOnlyList<SimulationsLauf>> LaeufeAbrufenAsync(
        int turnierInstanzId, CancellationToken ct = default)
        => laufRepository.AlleNachInstanzAsync(turnierInstanzId, ct);

    // ── Freie Einzelspiel-Simulation (kein DB-Bezug) ─────────────────────

    public SimulationsErgebnis EinzelspielSimulieren(
        Mannschaft heim,
        Mannschaft auswaerts,
        IReadOnlyList<EntscheidungsVerfahren> verfahren,
        SimulationsModus modus,
        Random? rng = null)
    {
        rng ??= new Random();
        bool ausfuehrlich = modus == SimulationsModus.AusfuehrlicheSimulation;
        var ergebnis = SimulationsEngine.Simulieren(heim, auswaerts, verfahren, ausfuehrlich, rng);
        logger.LogInformation("Einzelspiel: {Heim} {H}:{G} {Gast}",
            heim.Name, ergebnis.HeimToreRegulaer, ergebnis.AuswaertsToreRegulaer, auswaerts.Name);
        return ergebnis;
    }

    // ── Ein einzelnes Spiel ───────────────────────────────────────────────

    /// <summary>Simuliert ein einzelnes Spiel und speichert das Ergebnis.</summary>
    public async Task<SimulationsErgebnis> SpielSimulierenAsync(
        int spielId,
        SimulationsModus modus,
        Random? rng = null,
        CancellationToken ct = default,
        bool ueberschreiben = false)
    {
        var spiel = await spielRepository.FindenNachIdFuerSimulationAsync(spielId, ct)
            ?? throw new InvalidOperationException($"Spiel {spielId} nicht gefunden.");

        if (spiel.Status == SpielStatus.Abgeschlossen && !ueberschreiben)
            throw new InvalidOperationException(
                $"Spiel #{spiel.Spielnummer} ist bereits abgeschlossen.");

        rng ??= new Random();

        // Entscheidungsverfahren nur für KO-Runden: Liga/Gruppenphase erlaubt Unentschieden
        bool istKoRunde = spiel.TurnierRunde?.Rundenart is
            Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;
        var verfahren = istKoRunde
            ? (spiel.TurnierRunde?.Regelwerk?.Entscheidungsverfahren
                .OrderBy(v => v.Reihenfolge).ToList() ?? [])
            : [];

        if (spiel.HeimMannschaft is null || spiel.AuswaertsMannschaft is null)
            throw new InvalidOperationException(
                $"Spiel #{spiel.Spielnummer} enthält Platzhalter und kann nicht simuliert werden.");

        // Issue #171: Ereignisse werden unabhängig vom gewählten Modus immer berechnet und
        // gespeichert (In-Memory-Berechnung ist vernachlässigbar teuer) — sonst bleibt der
        // Spielverlauf leer, wenn ein schnellsimuliertes Spiel später im Detail betrachtet wird.
        var ergebnis = SimulationsEngine.Simulieren(
            spiel.HeimMannschaft, spiel.AuswaertsMannschaft, verfahren, ausfuehrlich: true, rng);

        await SpeichernAsync(spiel, ergebnis, ct);

        logger.LogInformation(
            "Spiel #{Nr} simuliert: {Heim} {H}:{G} {Gast} ({Art})",
            spiel.Spielnummer, spiel.HeimMannschaft.Name,
            ergebnis.HeimToreRegulaer, ergebnis.AuswaertsToreRegulaer,
            spiel.AuswaertsMannschaft.Name, ergebnis.EntscheidungArt);

        // Issue #90: simulierte Ergebnisse laufen nicht über SpielErgebnisService.ErgebnisErfassenAsync
        // (direkter Repository-Zugriff oben in SpeichernAsync), daher hier explizit derselbe Trigger.
        await ergebnisService.PlatzhalterAutomatischAufloesenAsync(spielId, ct);

        return ergebnis;
    }

    // ── Eine freie Auswahl ────────────────────────────────────────────────

    /// <summary>
    /// Simuliert genau die übergebenen, noch nicht abgeschlossenen Spiele — für eine frei
    /// getroffene Mehrfachauswahl in der WPF-Spielliste (Issue #207), unabhängig von
    /// Runde/Gruppe/Spieltag. Bereits abgeschlossene oder nicht gefundene Spiele werden
    /// übersprungen statt die restliche Auswahl abzubrechen. Gibt die Anzahl simulierter Spiele zurück.
    /// </summary>
    public async Task<int> SpieleSimulierenAsync(
        IReadOnlyCollection<int> spielIds,
        SimulationsModus modus,
        int? seed = null,
        CancellationToken ct = default)
    {
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        int simuliert = 0;

        foreach (var spielId in spielIds)
        {
            var spiel = await spielRepository.FindenNachIdFuerSimulationAsync(spielId, ct);
            if (spiel is null || spiel.Status == SpielStatus.Abgeschlossen) continue;

            await SpielSimulierenAsync(spielId, modus, rng, ct);
            simuliert++;
        }

        logger.LogInformation("Auswahl: {Anz} Spiel(e) simuliert.", simuliert);
        return simuliert;
    }

    // ── Ein Spieltag ──────────────────────────────────────────────────────

    /// <summary>
    /// Simuliert alle noch nicht abgeschlossenen Spiele eines Spieltags einer Runde.
    /// Gibt die Anzahl simulierter Spiele zurück.
    /// </summary>
    public async Task<int> SpieltageSimulierenAsync(
        int turnierRundeId,
        int spieltag,
        SimulationsModus modus,
        int? seed = null,
        CancellationToken ct = default)
    {
        var spiele = await spielRepository.AlleNachRundeAsync(turnierRundeId, ct);
        var offene = spiele
            .Where(s => s.Spieltag == spieltag && s.Status != SpielStatus.Abgeschlossen)
            .ToList();

        if (!offene.Any()) return 0;

        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        int simuliert = 0;

        foreach (var spiel in offene)
        {
            await SpielSimulierenAsync(spiel.Id, modus, rng, ct);
            simuliert++;
        }

        logger.LogInformation("Spieltag {Tag} (Runde {Id}): {Anz} Spiel(e) simuliert.",
            spieltag, turnierRundeId, simuliert);
        return simuliert;
    }

    // ── Eine Gruppe ───────────────────────────────────────────────────────

    /// <summary>
    /// Simuliert alle noch nicht abgeschlossenen Spiele einer Gruppe innerhalb einer Runde.
    /// <paramref name="gruppenTeamIds"/> enthält die MannschaftIds der Gruppe (bestimmt via HeimMannschaftId).
    /// </summary>
    public async Task<int> GruppeSimulierenAsync(
        int turnierRundeId,
        IReadOnlyCollection<int> gruppenTeamIds,
        SimulationsModus modus,
        int? seed = null,
        CancellationToken ct = default)
    {
        var spiele = await spielRepository.AlleNachRundeAsync(turnierRundeId, ct);
        var offene = spiele
            .Where(s => s.Status != SpielStatus.Abgeschlossen
                     && s.HeimMannschaftId.HasValue
                     && gruppenTeamIds.Contains(s.HeimMannschaftId.Value))
            .ToList();

        if (!offene.Any()) return 0;

        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        int simuliert = 0;

        foreach (var spiel in offene)
        {
            await SpielSimulierenAsync(spiel.Id, modus, rng, ct);
            simuliert++;
        }

        logger.LogInformation("Gruppe (Runde {Id}): {Anz} Spiel(e) simuliert.", turnierRundeId, simuliert);
        return simuliert;
    }

    // ── Spieltag innerhalb einer Gruppe ───────────────────────────────────

    /// <summary>
    /// Simuliert alle noch nicht abgeschlossenen Spiele eines Spieltags innerhalb einer Gruppe.
    /// </summary>
    public async Task<int> SpieltagInGruppeSimulierenAsync(
        int turnierRundeId,
        int spieltag,
        IReadOnlyCollection<int> gruppenTeamIds,
        SimulationsModus modus,
        int? seed = null,
        CancellationToken ct = default)
    {
        var spiele = await spielRepository.AlleNachRundeAsync(turnierRundeId, ct);
        var offene = spiele
            .Where(s => s.Spieltag == spieltag
                     && s.Status != SpielStatus.Abgeschlossen
                     && s.HeimMannschaftId.HasValue
                     && gruppenTeamIds.Contains(s.HeimMannschaftId.Value))
            .ToList();

        if (!offene.Any()) return 0;

        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        int simuliert = 0;

        foreach (var spiel in offene)
        {
            await SpielSimulierenAsync(spiel.Id, modus, rng, ct);
            simuliert++;
        }

        logger.LogInformation("Spieltag {Tag} (Gruppe, Runde {Id}): {Anz} Spiel(e) simuliert.",
            spieltag, turnierRundeId, simuliert);
        return simuliert;
    }

    // ── Eine Runde ────────────────────────────────────────────────────────

    /// <summary>
    /// Simuliert alle noch nicht abgeschlossenen Spiele einer Runde.
    /// Gibt die Anzahl simulierter Spiele zurück.
    /// </summary>
    public async Task<int> RundeSimulierenAsync(
        int turnierRundeId,
        SimulationsModus modus,
        int? seed = null,
        CancellationToken ct = default)
    {
        var spiele = await spielRepository.AlleNachRundeAsync(turnierRundeId, ct);
        var offene = spiele.Where(s => s.Status != SpielStatus.Abgeschlossen).ToList();

        if (!offene.Any()) return 0;

        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        int simuliert = 0;

        foreach (var spiel in offene)
        {
            await SpielSimulierenAsync(spiel.Id, modus, rng, ct);
            simuliert++;
        }

        logger.LogInformation("Runde {Id}: {Anz} Spiel(e) simuliert.", turnierRundeId, simuliert);
        return simuliert;
    }

    // ── Ganzes Turnier ────────────────────────────────────────────────────

    /// <summary>
    /// Simuliert alle offenen Spiele des Turniers (Runde für Runde).
    /// Nach jeder K.-o.-Runde werden Sieger fortgeschrieben.
    /// Gibt einen SimulationsLauf zurück.
    /// </summary>
    public async Task<SimulationsLauf> TurnierSimulierenAsync(
        int turnierInstanzId,
        SimulationsModus modus,
        int? seed = null,
        CancellationToken ct = default)
    {
        int tatsaecklicherSeed = seed ?? new Random().Next();
        var rng = new Random(tatsaecklicherSeed);

        var lauf = new SimulationsLauf
        {
            TurnierInstanzId = turnierInstanzId,
            StartZeitpunkt   = DateTime.UtcNow,
            ZufallsSeed      = tatsaecklicherSeed,
            Beschreibung     = $"{modus} (Seed: {tatsaecklicherSeed})"
        };
        await laufRepository.HinzufuegenAsync(lauf, ct);

        var instanz = await instanzRepository.FindenNachIdMitRundenAsync(turnierInstanzId, ct)
            ?? throw new InvalidOperationException($"TurnierInstanz {turnierInstanzId} nicht gefunden.");

        int gesamtSimuliert = 0;
        var runden = instanz.Runden.OrderBy(r => r.Reihenfolge).ToList();

        foreach (var runde in runden)
        {
            var spiele = await spielRepository.AlleNachRundeAsync(runde.Id, ct);
            var offene = spiele.Where(s => s.Status != SpielStatus.Abgeschlossen).ToList();

            foreach (var spiel in offene)
            {
                await SpielSimulierenAsync(spiel.Id, modus, rng, ct);
                gesamtSimuliert++;
            }

            // Nach K.-o.-Runden: Sieger in den Bracket fortschreiben
            bool istKo = runde.Rundenart is Rundenart.KoRunde
                                         or Rundenart.Finale
                                         or Rundenart.SpielUmPlatzDrei;
            if (istKo && offene.Any())
            {
                try { await turnierBaumService.SiegerFortsteiernAsync(turnierInstanzId, ct); }
                catch { /* Kein Turnierbaum vorhanden — kein Fehler */ }
            }
        }

        lauf.EndZeitpunkt = DateTime.UtcNow;
        await laufRepository.AktualisierenAsync(lauf, ct);

        logger.LogInformation(
            "Turnier {Id} simuliert: {Anz} Spiel(e), Seed {Seed}.",
            turnierInstanzId, gesamtSimuliert, tatsaecklicherSeed);

        return lauf;
    }

    // ── Speichern ─────────────────────────────────────────────────────────

    private async Task SpeichernAsync(Spiel spiel, SimulationsErgebnis sim, CancellationToken ct)
    {
        var eingabe = new ErgebnisErfassenEingabe(
            sim.HeimToreRegulaer,
            sim.AuswaertsToreRegulaer,
            sim.EntscheidungArt,
            sim.HeimToreVerlaengerung,
            sim.AuswaertsToreVerlaengerung,
            sim.HeimElfmetertore,
            sim.AuswaertsElfmetertore);

        await ergebnisService.ErgebnisErfassenAsync(spiel.Id, eingabe, ct);

        if (sim.Ereignisse.Any())
        {
            var dbEreignisse = sim.Ereignisse.Select(e => new SpielEreignis
            {
                SpielId      = spiel.Id,
                Minute       = e.Minute,
                EreignisArt  = e.Art,
                MannschaftId = e.IstHeim ? spiel.HeimMannschaftId : spiel.AuswaertsMannschaftId,
                Beschreibung = e.Beschreibung
            });
            await spielRepository.EreignisseHinzufuegenAsync(spiel.Id, dbEreignisse, ct);
        }
    }
}
