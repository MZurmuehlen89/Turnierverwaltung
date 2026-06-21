using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
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
    SpielplanGeneratorFactory generatorFactory,
    TurnierValidator turnierValidator,
    TabellenService tabellenService,
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

        int maxNr = await spielRepository.HoechsteSpielNummerAsync(runde.TurnierInstanzId, ct);
        var generator = generatorFactory.ErstellenFuerRundenart(runde.Rundenart);
        var kontext   = new SpielplanKontext(runde, geordnet, maxNr + 1);
        var ergebnis  = generator.Generieren(kontext);

        await SpeichernAsync(ergebnis, ct);

        logger.LogInformation(
            "Spielplan für Runde '{Runde}' generiert: {Anzahl} Spiele.",
            runde.Name, ergebnis.Spiele.Count);

        return ergebnis;
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
        int maxNr        = await spielRepository.HoechsteSpielNummerAsync(runde.TurnierInstanzId, ct);

        var generator = generatorFactory.ErstellenFuerRundenart(runde.Rundenart);
        var kontext   = new SpielplanKontext(runde, mannschaften, maxNr + 1, paarungen);
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

        int maxNr     = await spielRepository.HoechsteSpielNummerAsync(runde.TurnierInstanzId, ct);
        var generator = generatorFactory.ErstellenFuerRundenart(runde.Rundenart);
        var kontext   = new SpielplanKontext(
            runde, geordnet, maxNr + 1,
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
        int maxNr        = await spielRepository.HoechsteSpielNummerAsync(runde.TurnierInstanzId, ct);
        int spielNr      = maxNr + 1;
        var generator    = generatorFactory.ErstellenFuerRundenart(runde.Rundenart);
        var alleSpiele   = new List<Spiel>();

        foreach (var gruppe in gruppen)
        {
            var gruppenTeams = gruppe
                .Select(id => mannschaften.FirstOrDefault(m => m.Id == id))
                .Where(m => m is not null)
                .Select(m => m!)
                .ToList();

            if (gruppenTeams.Count < 2) continue;

            var kontext  = new SpielplanKontext(runde, gruppenTeams, spielNr);
            var ergebnis = generator.Generieren(kontext);
            alleSpiele.AddRange(ergebnis.Spiele);
            spielNr += ergebnis.Spiele.Count;
        }

        if (alleSpiele.Count > 0)
            await spielRepository.HinzufuegenMehrfachAsync(alleSpiele, ct);

        logger.LogInformation(
            "Gruppenphase-Spielplan für Runde '{Runde}' generiert: {Gruppen} Gruppen, {Spiele} Spiele.",
            runde.Name, gruppen.Count, alleSpiele.Count);
    }

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

        int maxNr   = await spielRepository.HoechsteSpielNummerAsync(runde.TurnierInstanzId, ct);
        int spielNr = maxNr + 1;

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

        var platzhalterSpiele = (await spielRepository.AlleNachRundeAsync(turnierRundeId, ct))
            .Where(s => s.HeimMannschaftId is null || s.AuswaertsMannschaftId is null)
            .ToList();

        if (platzhalterSpiele.Count == 0) return 0;

        // Alle Runden derselben TurnierInstanz laden (für Suche nach Vorrunden)
        var alleRunden = await rundeRepository.AlleNachInstanzAsync(runde.TurnierInstanzId, ct);

        // Platzhalter-Cache aufbauen (jeder eindeutige Name nur einmal auflösen)
        var cache = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);

        async Task<int?> AufloessenAsync(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (cache.TryGetValue(name, out var cached)) return cached;
            var result = await PlatzhalterNameAufloessenAsync(name, alleRunden, ct);
            cache[name] = result;
            return result;
        }

        int aufgeloest = 0;
        foreach (var spiel in platzhalterSpiele)
        {
            var heimId     = spiel.HeimMannschaftId     ?? await AufloessenAsync(spiel.HeimPlatzhalterName);
            var auswaertsId = spiel.AuswaertsMannschaftId ?? await AufloessenAsync(spiel.AuswaertsPlatzhalterName);

            if (heimId is null || auswaertsId is null) continue;

            await spielRepository.PlatzhalterAktualisierenAsync(spiel.Id, heimId.Value, auswaertsId.Value, ct);
            aufgeloest++;
        }

        logger.LogInformation(
            "Platzhalter in Runde '{Runde}' aufgelöst: {Aufgeloest}/{Gesamt} Spiele.",
            runde.Name, aufgeloest, platzhalterSpiele.Count);

        return aufgeloest;
    }

    // ── Platzhalter-Auflösungslogik ───────────────────────────────────────

    private static readonly Regex _gruppenSiegerRx  = new(@"^Sieger Gruppe ([A-Z0-9]+)$",  RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _gruppenRangRx     = new(@"^(\d+)\. Gruppe ([A-Z0-9]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _koSiegerRx        = new(@"^Sieger (AF|VF|HF)(\d+)$",    RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private async Task<int?> PlatzhalterNameAufloessenAsync(
        string name, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct)
    {
        // Pattern 1: "Sieger Gruppe A" → Rang 1 der Gruppe A
        var m = _gruppenSiegerRx.Match(name);
        if (m.Success)
            return await GruppenrangAufloessenAsync(m.Groups[1].Value, 1, alleRunden, ct);

        // Pattern 2: "2. Gruppe A" → Rang 2 der Gruppe A
        m = _gruppenRangRx.Match(name);
        if (m.Success && int.TryParse(m.Groups[1].Value, out int rang))
            return await GruppenrangAufloessenAsync(m.Groups[2].Value, rang, alleRunden, ct);

        // Pattern 3: "Sieger AF1" → Sieger des 1. Spiels im Achtelfinale
        m = _koSiegerRx.Match(name);
        if (m.Success && int.TryParse(m.Groups[2].Value, out int spielNr))
            return await KoSiegerAufloessenAsync(m.Groups[1].Value, spielNr, alleRunden, ct);

        return null;
    }

    private async Task<int?> GruppenrangAufloessenAsync(
        string gruppenKennung, int rang, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct)
    {
        // Gruppenkennung → Gruppenindex (A=1, B=2, ... oder direkte Zahl)
        int gruppenNummer = gruppenKennung.Length == 1 && char.IsLetter(gruppenKennung[0])
            ? char.ToUpper(gruppenKennung[0]) - 'A' + 1
            : int.TryParse(gruppenKennung, out int n) ? n : 0;

        if (gruppenNummer <= 0) return null;

        var gruppenphaseRunde = alleRunden.FirstOrDefault(r => r.Rundenart == Rundenart.Gruppenphase);
        if (gruppenphaseRunde is null) return null;

        var gruppenTabellen = await tabellenService.GruppenTabellenBerechnenAsync(gruppenphaseRunde.Id, ct);
        var gruppe = gruppenTabellen.FirstOrDefault(g => g.GruppenNummer == gruppenNummer);
        if (gruppe is null) return null;

        return gruppe.Tabelle.Count >= rang
            ? gruppe.Tabelle[rang - 1].Mannschaft.Id
            : null;
    }

    private async Task<int?> KoSiegerAufloessenAsync(
        string abkuerzung, int spielNummer, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct)
    {
        string nameFragment = abkuerzung.ToUpperInvariant() switch
        {
            "AF" => "Achtelfinale",
            "VF" => "Viertelfinale",
            "HF" => "Halbfinale",
            _    => string.Empty
        };
        if (nameFragment.Length == 0) return null;

        var koRunde = alleRunden.FirstOrDefault(r =>
            r.Name.Contains(nameFragment, StringComparison.OrdinalIgnoreCase));
        if (koRunde is null) return null;

        var koSpiele = (await spielRepository.AlleNachRundeAsync(koRunde.Id, ct))
            .Where(s => s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue)
            .OrderBy(s => s.Spielnummer)
            .ToList();

        if (koSpiele.Count < spielNummer) return null;

        return koSpiele[spielNummer - 1].Ergebnis?.SiegerMannschaftId;
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
