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
        logger.LogDebug("Einzelspiel: {Heim} {H}:{G} {Gast}",
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
        var ergebnis = await SimulierenUndSpeichernAsync(spielId, rng ?? new Random(), ct, ueberschreiben);

        // Issue #90: Vorwärts-Auflösung der Folgerunden-Platzhalter — hier für das eine Spiel, bei
        // Massensimulationen einmal je Block (siehe MehrereSimulierenAsync).
        await ergebnisService.PlatzhalterAutomatischAufloesenAsync(spielId, ct);

        return ergebnis;
    }

    /// <summary>Würfelt und speichert ein Spiel, ohne die Platzhalter der Folgerunden aufzulösen — die
    /// Auflösung ist mit Abstand der teuerste Schritt (Issue #386: ~99 % der Laufzeit einer Massensimulation)
    /// und übernimmt der Aufrufer.</summary>
    private async Task<SimulationsErgebnis> SimulierenUndSpeichernAsync(
        int spielId, Random rng, CancellationToken ct, bool ueberschreiben = false)
    {
        var spiel = await spielRepository.FindenNachIdFuerSimulationAsync(spielId, ct)
            ?? throw new InvalidOperationException($"Spiel {spielId} nicht gefunden.");

        if (spiel.Status == SpielStatus.Abgeschlossen && !ueberschreiben)
            throw new InvalidOperationException(
                $"Spiel #{spiel.Spielnummer} ist bereits abgeschlossen.");

        // Entscheidungsverfahren nur für KO-Runden: Liga/Gruppenphase erlaubt Unentschieden
        bool istKoRunde = spiel.TurnierRunde?.Rundenart is
            Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;
        var verfahren = istKoRunde
            ? ((spiel.TurnierRunde?.Regelwerk ?? spiel.TurnierRunde?.TurnierInstanz?.StandardRegelwerk)?.Entscheidungsverfahren
                .OrderBy(v => v.Reihenfolge).ToList() ?? [])
            : [];

        if (spiel.HeimMannschaft is null || spiel.AuswaertsMannschaft is null)
            throw new InvalidOperationException(
                $"Spiel #{spiel.Spielnummer} enthält Platzhalter und kann nicht simuliert werden.");

        // Issue #346: in K.-o.-Paarungen mit Hin- und Rückspiel entscheidet erst das Gesamtergebnis, ob
        // überhaupt ein Entscheidungsverfahren fällig ist — das Hinspiel läuft nie in die Verlängerung,
        // das Rückspiel nur bei ausgeglichenem Gesamtstand (analog zur freien Simulation aus #237).
        var runde = spiel.TurnierRunde!;
        bool istKoHinspiel   = istKoRunde && HinRueckspielWertung.IstKoHinspiel(spiel, runde);
        bool istKoRueckspiel = istKoRunde && HinRueckspielWertung.IstKoRueckspiel(spiel, runde);
        var hinspielErgebnis = spiel.Hinspiel?.Ergebnis;
        if (istKoRueckspiel && hinspielErgebnis is null)
            throw new InvalidOperationException(
                $"Spiel #{spiel.Spielnummer} ist ein Rückspiel, dessen Hinspiel noch kein Ergebnis hat.");

        // Issue #352: auf neutralem Platz behält nur ein Gastgeber des Turniers seinen Heimvorteil.
        IReadOnlyCollection<int> gastgeberIds = runde.NeutralerSpielort
            ? await instanzRepository.GastgeberIdsAsync(spiel.TurnierInstanzId, ct)
            : [];
        var spielortvorteil = SimulationsEngine.SpielortvorteilErmitteln(
            runde.NeutralerSpielort, spiel.HeimMannschaftId, spiel.AuswaertsMannschaftId, gastgeberIds);

        // Issue #171: Ereignisse werden unabhängig vom gewählten Modus immer berechnet und
        // gespeichert (In-Memory-Berechnung ist vernachlässigbar teuer) — sonst bleibt der
        // Spielverlauf leer, wenn ein schnellsimuliertes Spiel später im Detail betrachtet wird.
        var ergebnis = SimulationsEngine.Simulieren(
            spiel.HeimMannschaft, spiel.AuswaertsMannschaft,
            istKoHinspiel || istKoRueckspiel ? [] : verfahren, ausfuehrlich: true, rng, spielortvorteil,
            runde.Ueberraschungsfaktor);

        if (istKoRueckspiel && verfahren.Count > 0 &&
            HinRueckspielWertung.EntscheidungsverfahrenNoetig(
                HinRueckspielWertung.HinspielTore(hinspielErgebnis!, heim: true),
                HinRueckspielWertung.HinspielTore(hinspielErgebnis!, heim: false),
                ergebnis.HeimToreRegulaer, ergebnis.AuswaertsToreRegulaer,
                runde.AuswaertstorRegelAktiv))
        {
            var (hv, gv, he, ge, art, extras, losGewinnerHeim) = SimulationsEngine.EntscheidungsverfahrenAnwenden(
                spiel.HeimMannschaft, spiel.AuswaertsMannschaft, verfahren, ausfuehrlich: true, rng, spielortvorteil,
                runde.Ueberraschungsfaktor);
            ergebnis = ergebnis with
            {
                HeimToreVerlaengerung      = hv,
                AuswaertsToreVerlaengerung = gv,
                HeimElfmetertore           = he,
                AuswaertsElfmetertore      = ge,
                EntscheidungArt            = art,
                Ereignisse                 = [.. ergebnis.Ereignisse, .. extras],
                LosGewinnerHeim            = losGewinnerHeim,
            };
        }

        await SpeichernAsync(spiel, ergebnis, ct);

        // Debug statt Information: je Spiel eine Zeile würde das Ablaufprotokoll (#413) bei einer
        // Massensimulation fluten — dort genügt die Zusammenfassung je Block.
        logger.LogDebug(
            "Spiel #{Nr} simuliert: {Heim} {H}:{G} {Gast} ({Art})",
            spiel.Spielnummer, spiel.HeimMannschaft.Name,
            ergebnis.HeimToreRegulaer, ergebnis.AuswaertsToreRegulaer,
            spiel.AuswaertsMannschaft.Name, ergebnis.EntscheidungArt);

        return ergebnis;
    }

    /// <summary>Simuliert die übergebenen Spiele in dieser Reihenfolge und löst danach einmal die Platzhalter
    /// der Folgerunden auf (Issue #386) statt nach jedem Spiel. Unbedenklich, weil Spiele innerhalb eines
    /// Blocks nicht über Platzhalter voneinander abhängen (ein K.-o.-Rückspiel hängt über
    /// <see cref="Spiel.HinspielId"/> am Hinspiel, nicht über einen Platzhalter) und die Oberfläche erst nach
    /// dem Block neu lädt. Die Reihenfolge der Zufallszüge und damit die Reproduzierbarkeit per Seed bleibt
    /// unverändert. Bricht ein Spiel ab, werden die bis dahin gespeicherten Ergebnisse trotzdem aufgelöst.</summary>
    private async Task<int> MehrereSimulierenAsync(IEnumerable<int> spielIds, Random rng, CancellationToken ct)
    {
        int? letzteSpielId = null;
        int simuliert = 0;
        try
        {
            foreach (var spielId in spielIds)
            {
                await SimulierenUndSpeichernAsync(spielId, rng, ct);
                letzteSpielId = spielId;
                simuliert++;
            }
        }
        finally
        {
            if (letzteSpielId is int id)
                await ergebnisService.PlatzhalterAutomatischAufloesenAsync(id, ct);
        }
        return simuliert;
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

        // Issue #346: Hinspiele vor Rückspielen — ein K.-o.-Rückspiel braucht das Hinspiel-Ergebnis.
        var offene = new List<Spiel>();
        foreach (var spielId in spielIds)
        {
            var spiel = await spielRepository.FindenNachIdFuerSimulationAsync(spielId, ct);
            if (spiel is null || spiel.Status == SpielStatus.Abgeschlossen) continue;
            offene.Add(spiel);
        }

        int simuliert = await MehrereSimulierenAsync(
            offene.OrderBy(s => s.IstRueckspiel).ThenBy(s => s.Spielnummer).Select(s => s.Id), rng, ct);

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
        int simuliert = await MehrereSimulierenAsync(offene.Select(s => s.Id), rng, ct);

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
        int simuliert = await MehrereSimulierenAsync(offene.Select(s => s.Id), rng, ct);

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
        int simuliert = await MehrereSimulierenAsync(offene.Select(s => s.Id), rng, ct);

        logger.LogInformation("Spieltag {Tag} (Gruppe, Runde {Id}): {Anz} Spiel(e) simuliert.",
            spieltag, turnierRundeId, simuliert);
        return simuliert;
    }

    // ── Hin-/Rückrunde ────────────────────────────────────────────────────

    /// <summary>
    /// Simuliert alle noch nicht abgeschlossenen Spiele einer Rundenhälfte (Hin- oder Rückrunde,
    /// nach <see cref="Spiel.IstRueckspiel"/> unterschieden) — für Liga-Runden mit Rückspiel
    /// (Issue #100) und K.-o.-Runden mit Hin- und Rückspiel (Issue #387), analog zu
    /// <see cref="RundeSimulierenAsync"/> für die gesamte Runde. Ein K.-o.-Rückspiel braucht das
    /// Hinspiel-Ergebnis — Rückspiele mit offenem Hinspiel werden dort übersprungen.
    /// Gibt die Anzahl simulierter Spiele zurück.
    /// </summary>
    public async Task<int> RundenHaelfteSimulierenAsync(
        int turnierRundeId,
        bool istRueckrunde,
        SimulationsModus modus,
        int? seed = null,
        CancellationToken ct = default)
    {
        var spiele = await spielRepository.AlleNachRundeAsync(turnierRundeId, ct);
        var offene = spiele
            .Where(s => s.IstRueckspiel == istRueckrunde && s.Status != SpielStatus.Abgeschlossen)
            .ToList();

        if (!offene.Any()) return 0;

        if (istRueckrunde)
        {
            var runde = (await spielRepository.FindenNachIdFuerSimulationAsync(offene[0].Id, ct))?.TurnierRunde;
            if (runde is not null && HinRueckspielWertung.IstKoRunde(runde))
            {
                var hinspielMitErgebnis = spiele.Where(s => s.Ergebnis is not null).Select(s => s.Id).ToHashSet();
                offene = offene.Where(s => s.HinspielId is int h && hinspielMitErgebnis.Contains(h)).ToList();
                if (!offene.Any()) return 0;
            }
        }

        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        int simuliert = await MehrereSimulierenAsync(offene.Select(s => s.Id), rng, ct);

        logger.LogInformation("{Haelfte} (Runde {Id}): {Anz} Spiel(e) simuliert.",
            istRueckrunde ? "Rückrunde" : "Hinrunde", turnierRundeId, simuliert);
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
        // Issue #346: Hinspiele vor Rückspielen — ein K.-o.-Rückspiel braucht das Hinspiel-Ergebnis.
        var offene = spiele.Where(s => s.Status != SpielStatus.Abgeschlossen)
            .OrderBy(s => s.IstRueckspiel).ThenBy(s => s.Spielnummer)
            .ToList();

        if (!offene.Any()) return 0;

        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        int simuliert = await MehrereSimulierenAsync(offene.Select(s => s.Id), rng, ct);

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

            // Je Runde ein Block: die Folgerunde braucht die aufgelösten Platzhalter erst beim nächsten Durchlauf.
            gesamtSimuliert += await MehrereSimulierenAsync(offene.Select(s => s.Id), rng, ct);

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
            sim.AuswaertsElfmetertore,
            sim.HalbzeitHeimTore,
            sim.HalbzeitAuswaertsTore,
            sim.LosGewinnerHeim);

        await ergebnisService.ErgebnisErfassenAsync(spiel.Id, eingabe, ct, platzhalterAufloesen: false);

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
