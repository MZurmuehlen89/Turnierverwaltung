using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Spiele.Eingaben;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Zufall;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Simulation;

#pragma warning disable S107 // sechs Abhängigkeiten, eine Verantwortung (Spiele würfeln und speichern); Zufallsquelle und Logger sind Querschnitt (#503)
public class SimulationsService(
    ISpielRepository spielRepository,
    ITurnierInstanzRepository instanzRepository,
    SpielErgebnisService ergebnisService,
    ISimulationsLaufRepository laufRepository,
    Zufallsquelle zufallsquelle,
    ILogger<SimulationsService> logger)
#pragma warning restore S107
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
        rng ??= zufallsquelle.Erzeugen();
        bool ausfuehrlich = modus == SimulationsModus.AusfuehrlicheSimulation;
        var ergebnis = SimulationsEngine.Simulieren(heim, auswaerts, verfahren, ausfuehrlich, rng);
        logger.LogDebug("Einzelspiel: {Heim} {H}:{G} {Gast}",
            heim.Name, ergebnis.HeimToreRegulaer, ergebnis.AuswaertsToreRegulaer, auswaerts.Name);
        return ergebnis;
    }

    /// <summary>Rückspiel der freien Hin-/Rückspiel-Simulation (#248), ausführlich und ohne Entscheidungsverfahren –
    /// das hängt am Gesamtstand und wendet der Aufrufer an. Die Risikostufe richtet sich nach dem Gesamtstand (#671).</summary>
    public SimulationsErgebnis RueckspielSimulieren(
        Mannschaft heim, Mannschaft auswaerts, Hinspielstand hinspielstand, Random rng)
        => SimulationsEngine.Simulieren(heim, auswaerts, [], ausfuehrlich: true, rng, hinspielstand: hinspielstand);

    // ── Ein einzelnes Spiel ───────────────────────────────────────────────

    /// <summary>Simuliert ein einzelnes Spiel und speichert das Ergebnis.</summary>
    public async Task<SimulationsErgebnis> SpielSimulierenAsync(
        int spielId,
        Random? rng = null,
        CancellationToken ct = default,
        bool ueberschreiben = false)
    {
        var ergebnis = await SimulierenUndSpeichernAsync(spielId, rng ?? zufallsquelle.Erzeugen(), ct, ueberschreiben);

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
        var spiel = await SpielFuerSimulationLadenAsync(spielId, ueberschreiben, ct);

        // Entscheidungsverfahren nur für KO-Runden: Liga/Gruppenphase erlaubt Unentschieden
        bool istKoRunde = spiel.TurnierRunde?.Rundenart is
            Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;
        var verfahren = istKoRunde ? EntscheidungsverfahrenDerRunde(spiel.TurnierRunde!) : [];

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

        var spielortvorteil = await SpielortvorteilErmittelnAsync(spiel, runde, ct);
        var hinspielstand = istKoRueckspiel
            ? Hinspielstand.AusHinspiel(
                HinRueckspielWertung.HinspielTore(hinspielErgebnis!, heim: true),
                HinRueckspielWertung.HinspielTore(hinspielErgebnis!, heim: false),
                runde.AuswaertstorRegelAktiv)
            : default;

        // Issue #171: Ereignisse werden unabhängig vom gewählten Modus immer berechnet und
        // gespeichert (In-Memory-Berechnung ist vernachlässigbar teuer) — sonst bleibt der
        // Spielverlauf leer, wenn ein schnellsimuliertes Spiel später im Detail betrachtet wird.
        var ergebnis = SimulationsEngine.Simulieren(
            spiel.HeimMannschaft!, spiel.AuswaertsMannschaft!,
            istKoHinspiel || istKoRueckspiel ? [] : verfahren, ausfuehrlich: true, rng, spielortvorteil,
            runde.Ueberraschungsfaktor, hinspielstand);

        if (istKoRueckspiel && verfahren.Count > 0)
            ergebnis = RueckspielEntscheiden(ergebnis, spiel, verfahren, rng, spielortvorteil);

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

    private async Task<Spiel> SpielFuerSimulationLadenAsync(int spielId, bool ueberschreiben, CancellationToken ct)
    {
        var spiel = await spielRepository.FindenNachIdFuerSimulationAsync(spielId, ct)
            ?? throw new InvalidOperationException($"Spiel {spielId} nicht gefunden.");

        if (spiel.Status == SpielStatus.Abgeschlossen && !ueberschreiben)
            throw new InvalidOperationException(
                $"Spiel #{spiel.Spielnummer} ist bereits abgeschlossen.");

        if (spiel.HeimMannschaft is null || spiel.AuswaertsMannschaft is null)
            throw new InvalidOperationException(
                $"Spiel #{spiel.Spielnummer} enthält Platzhalter und kann nicht simuliert werden.");

        return spiel;
    }

    private async Task<Spielortvorteil> SpielortvorteilErmittelnAsync(Spiel spiel, TurnierRunde runde, CancellationToken ct)
    {
        // Issue #352: auf neutralem Platz behält nur ein Gastgeber des Turniers seinen Heimvorteil.
        IReadOnlyCollection<int> gastgeberIds = runde.NeutralerSpielort
            ? await instanzRepository.GastgeberIdsAsync(spiel.TurnierInstanzId, ct)
            : [];
        return SimulationsEngine.SpielortvorteilErmitteln(
            runde.NeutralerSpielort, spiel.HeimMannschaftId, spiel.AuswaertsMannschaftId, gastgeberIds);
    }

    /// <summary>Die Entscheidungsverfahren der Runde in ihrer Reihenfolge — aus dem Regelwerk der Runde, sonst dem
    /// Standard-Regelwerk des Turniers (AD-026).</summary>
    private static List<EntscheidungsVerfahren> EntscheidungsverfahrenDerRunde(TurnierRunde runde)
        => (runde.Regelwerk ?? runde.TurnierInstanz?.StandardRegelwerk)?.Entscheidungsverfahren
               .OrderBy(v => v.Reihenfolge).ToList() ?? [];

    /// <summary>Wendet die Entscheidungsverfahren auf ein K.-o.-Rückspiel an, wenn der Gesamtstand aus Hin- und
    /// Rückspiel ausgeglichen ist (#346); sonst bleibt das Ergebnis der regulären Spielzeit.</summary>
    private static SimulationsErgebnis RueckspielEntscheiden(
        SimulationsErgebnis ergebnis, Spiel spiel, IReadOnlyList<EntscheidungsVerfahren> verfahren, Random rng,
        Spielortvorteil spielortvorteil)
    {
        var runde = spiel.TurnierRunde;
        var hinspiel = spiel.Hinspiel!.Ergebnis!;
        bool noetig = HinRueckspielWertung.EntscheidungsverfahrenNoetig(
            HinRueckspielWertung.HinspielTore(hinspiel, heim: true),
            HinRueckspielWertung.HinspielTore(hinspiel, heim: false),
            ergebnis.HeimToreRegulaer, ergebnis.AuswaertsToreRegulaer,
            runde.AuswaertstorRegelAktiv);
        if (!noetig)
            return ergebnis;

        // #361: die Verlängerung setzt das Rückspiel fort – mit dessen Platzverweisen, nicht denen des Hinspiels.
        var (hv, gv, he, ge, art, extras, losGewinnerHeim) = SimulationsEngine.EntscheidungsverfahrenAnwenden(
            spiel.HeimMannschaft!, spiel.AuswaertsMannschaft!, verfahren, ausfuehrlich: true, rng, spielortvorteil,
            runde.Ueberraschungsfaktor, Platzverweise.AusEreignissen(ergebnis.Ereignisse));
        return ergebnis with
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

    /// <summary>Gemeinsamer Ablauf der Massensimulationen: ist nichts offen, bleibt es bei 0 (ohne Seed zu ziehen);
    /// sonst ein <see cref="Random"/> für den ganzen Vorgang und ein Block über alle Spiele.</summary>
    private async Task<int> OffeneSimulierenAsync(IReadOnlyCollection<Spiel> offene, int? seed, CancellationToken ct)
    {
        if (offene.Count == 0) return 0;

        var rng = new Random(seed ?? zufallsquelle.SeedZiehen());
        return await MehrereSimulierenAsync(offene.Select(s => s.Id), rng, ct);
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
        int? seed = null,
        CancellationToken ct = default)
    {
        // Issue #346: Hinspiele vor Rückspielen — ein K.-o.-Rückspiel braucht das Hinspiel-Ergebnis.
        var offene = new List<Spiel>();
        foreach (var spielId in spielIds)
        {
            var spiel = await spielRepository.FindenNachIdFuerSimulationAsync(spielId, ct);
            if (spiel is null || spiel.Status == SpielStatus.Abgeschlossen) continue;
            offene.Add(spiel);
        }

        int simuliert = await OffeneSimulierenAsync(
            // #479: frühere Runden zuerst — die Auswahl der Mannschaftsansicht reicht über mehrere Runden.
            [.. offene.OrderBy(s => s.TurnierRunde.Reihenfolge).ThenBy(s => s.IstRueckspiel).ThenBy(s => s.Spielnummer)],
            seed, ct);

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
        int? seed = null,
        CancellationToken ct = default)
    {
        var spiele = await spielRepository.AlleNachRundeAsync(turnierRundeId, ct);
        var offene = spiele
            .Where(s => s.Spieltag == spieltag && s.Status != SpielStatus.Abgeschlossen)
            .ToList();

        int simuliert = await OffeneSimulierenAsync(offene, seed, ct);
        if (simuliert == 0) return 0;

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
        int? seed = null,
        CancellationToken ct = default)
    {
        var spiele = await spielRepository.AlleNachRundeAsync(turnierRundeId, ct);
        var offene = spiele
            .Where(s => s.Status != SpielStatus.Abgeschlossen
                     && s.HeimMannschaftId.HasValue
                     && gruppenTeamIds.Contains(s.HeimMannschaftId.Value))
            .ToList();

        int simuliert = await OffeneSimulierenAsync(offene, seed, ct);
        if (simuliert == 0) return 0;

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

        int simuliert = await OffeneSimulierenAsync(offene, seed, ct);
        if (simuliert == 0) return 0;

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
        int? seed = null,
        CancellationToken ct = default)
    {
        var spiele = await spielRepository.AlleNachRundeAsync(turnierRundeId, ct);
        var offene = spiele
            .Where(s => s.IstRueckspiel == istRueckrunde && s.Status != SpielStatus.Abgeschlossen)
            .ToList();

        if (istRueckrunde && offene.Count > 0)
            offene = await KoRueckspieleOhneHinspielErgebnisEntfernenAsync(offene, spiele, ct);

        int simuliert = await OffeneSimulierenAsync(offene, seed, ct);
        if (simuliert == 0) return 0;

        logger.LogInformation("{Haelfte} (Runde {Id}): {Anz} Spiel(e) simuliert.",
            istRueckrunde ? "Rückrunde" : "Hinrunde", turnierRundeId, simuliert);
        return simuliert;
    }

    /// <summary>In einer K.-o.-Runde braucht ein Rückspiel das Hinspiel-Ergebnis — Rückspiele mit offenem Hinspiel
    /// fallen heraus. In einer Liga-Rückrunde bleibt die Auswahl unverändert.</summary>
    private async Task<List<Spiel>> KoRueckspieleOhneHinspielErgebnisEntfernenAsync(
        List<Spiel> offeneRueckspiele, IReadOnlyCollection<Spiel> spieleDerRunde, CancellationToken ct)
    {
        var runde = (await spielRepository.FindenNachIdFuerSimulationAsync(offeneRueckspiele[0].Id, ct))?.TurnierRunde;
        if (runde is null || !HinRueckspielWertung.IstKoRunde(runde))
            return offeneRueckspiele;

        var hinspielMitErgebnis = spieleDerRunde.Where(s => s.Ergebnis is not null).Select(s => s.Id).ToHashSet();
        return offeneRueckspiele.Where(s => s.HinspielId is int h && hinspielMitErgebnis.Contains(h)).ToList();
    }

    // ── Eine Runde ────────────────────────────────────────────────────────

    /// <summary>
    /// Simuliert alle noch nicht abgeschlossenen Spiele einer Runde.
    /// Gibt die Anzahl simulierter Spiele zurück.
    /// </summary>
    public async Task<int> RundeSimulierenAsync(
        int turnierRundeId,
        int? seed = null,
        CancellationToken ct = default)
    {
        var spiele = await spielRepository.AlleNachRundeAsync(turnierRundeId, ct);
        // Issue #346: Hinspiele vor Rückspielen — ein K.-o.-Rückspiel braucht das Hinspiel-Ergebnis.
        var offene = spiele.Where(s => s.Status != SpielStatus.Abgeschlossen)
            .OrderBy(s => s.IstRueckspiel).ThenBy(s => s.Spielnummer)
            .ToList();

        int simuliert = await OffeneSimulierenAsync(offene, seed, ct);
        if (simuliert == 0) return 0;

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
        int tatsaecklicherSeed = seed ?? zufallsquelle.SeedZiehen();
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
            // Issue #346: Hinspiele vor Rückspielen — ein K.-o.-Rückspiel braucht das Hinspiel-Ergebnis. Sonst die
            // Reihenfolge des Repositorys (Spieltag, Spielnummer).
            var offene = spiele.Where(s => s.Status != SpielStatus.Abgeschlossen).OrderBy(s => s.IstRueckspiel);

            // Je Runde ein Block: MehrereSimulierenAsync löst danach die Platzhalter aller Runden auf, die
            // Folgerunde ist beim nächsten Durchlauf besetzt. Bis #629 schrieb hier zusätzlich
            // TurnierBaumService.SiegerFortsteiernAsync die Sieger fort — dieselbe Auflösung ein zweites Mal, und
            // ohne Turnierbaum mit einer Ausnahme, die ein leeres catch samt jedem anderen Fehler verschluckte.
            gesamtSimuliert += await MehrereSimulierenAsync(offene.Select(s => s.Id), rng, ct);
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
                Minute          = e.Minute,
                Nachspielminute = e.Nachspielminute,
                EreignisArt  = e.Art,
                MannschaftId = e.IstHeim ? spiel.HeimMannschaftId : spiel.AuswaertsMannschaftId,
                Beschreibung = e.Beschreibung
            });
            await spielRepository.EreignisseHinzufuegenAsync(spiel.Id, dbEreignisse, ct);
        }
    }
}
