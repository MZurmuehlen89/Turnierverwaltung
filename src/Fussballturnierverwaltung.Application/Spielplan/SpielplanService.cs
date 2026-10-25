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
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
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
    SpielplanGeneratorFactory generatorFactory,
    TurnierValidator turnierValidator,
    KoQuellverknuepfung quellverknuepfung,
    PlatzhalterAufloesung aufloesung,
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
        var runde = await RundeLadenAsync(turnierRundeId, ct);

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

        var geordnet = await MannschaftenInReihenfolgeAsync(mannschaftIds, ct);

        int startNr   = await spielRepository.NaechsteSpielNummerAsync(runde.TurnierInstanzId, turnierRundeId, runde.Reihenfolge, ct);
        var generator = generatorFactory.ErstellenFuerRundenart(runde.Rundenart);
        var kontext   = new SpielplanKontext(runde, geordnet, startNr);
        var ergebnis  = generator.Generieren(kontext);

        await SpielplanSpeicherung.ErgebnisSpeichernAsync(spielRepository, ergebnis, ct);

        logger.LogInformation(
            "Spielplan für Runde '{Runde}' generiert: {Anzahl} Spiele.",
            runde.Name, ergebnis.Spiele.Count);

        return ergebnis;
    }

    /// <summary>
    /// Generiert den Spielplan einer Runde direkt aus den Ergebnissen einer abgeschlossenen Auslosung -
    /// wählt je nach Rundenart automatisch die passende Generierungsmethode (Gruppenphase, TopfLiga,
    /// SchweizerSystem, oder direkte/Platzhalter-Paarungen für Liga/KO-Runden). Aufgerufen über
    /// <see cref="PlatzhalterKaskadeService.RundenSpielplanAusAuslosungErsetzenAsync"/> — vom manuellen
    /// "Neu generieren"-Befehl im Spielplan und beim Abschließen einer Auslosung (Issue #19) — sowie von der
    /// Kaskade selbst, wenn sie eine Folgerunde neu generiert.
    /// </summary>
    public async Task GenerierenAusAuslosungAsync(
        int turnierRundeId, IReadOnlyList<AuslosungsErgebnis> ergebnisse, CancellationToken ct = default)
    {
        var runde = await RundeLadenAsync(turnierRundeId, ct);

        switch (runde.Rundenart)
        {
            case Rundenart.Gruppenphase:    await GruppenphaseGenerierenAsync(turnierRundeId, ergebnisse, ct); break;
            case Rundenart.TopfLiga:        await TopfLigaGenerierenAsync(turnierRundeId, ergebnisse, ct); break;
            case Rundenart.SchweizerSystem: await SchweizerSystemGenerierenAsync(turnierRundeId, ergebnisse, ct); break;
            default:                        await PaarungenGenerierenAsync(turnierRundeId, ergebnisse, ct); break;
        }

        await NachGenerierungVerknuepfenAsync(runde, turnierRundeId, ct);
    }

    private async Task NachGenerierungVerknuepfenAsync(TurnierRunde runde, int turnierRundeId, CancellationToken ct)
    {
        // Issue #241: K.o.-Quellverknüpfungen (Sieger/Verlierer eines konkreten Vorrundenspiels) direkt
        // nach jeder Generierung nachziehen — idempotent, unabhängig davon, in welcher Reihenfolge Runden
        // erzeugt werden.
        using (logger.Vorgang($"Quellverknüpfung nach Generierung (Instanz {runde.TurnierInstanzId})"))
            await quellverknuepfung.AlleRundenVerknuepfenAsync(runde.TurnierInstanzId, ct);

        // Issue #346-Folgefund: bereits feststehende Teilnehmer sofort eintragen — sonst zeigte eine neu
        // generierte Runde nach bereits entschiedener Vorrunde wieder reine Platzhalter, bis irgendwo das
        // nächste Ergebnis die Auflösung anstieß (alle Aufrufer: Spielplan, Auslosung, Vorbelegung, Kaskade).
        using (logger.Vorgang($"Platzhalter auflösen nach Generierung (Runde {turnierRundeId})"))
            await aufloesung.RundeAufloesenAsync(turnierRundeId, ct);
    }

    private async Task GruppenphaseGenerierenAsync(int turnierRundeId, IReadOnlyList<AuslosungsErgebnis> ergebnisse, CancellationToken ct)
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
    }

    private async Task TopfLigaGenerierenAsync(int turnierRundeId, IReadOnlyList<AuslosungsErgebnis> ergebnisse, CancellationToken ct)
    {
        var paarungen = ergebnisse
            .Where(e => e.HeimMannschaftId.HasValue && e.AuswaertsMannschaftId.HasValue)
            .OrderBy(e => e.PaarungsNummer)
            .Select(e => (e.HeimMannschaftId!.Value, e.AuswaertsMannschaftId!.Value))
            .ToList<(int, int)>();
        await GenerierenAusPaarungenAsync(turnierRundeId, paarungen, ct);
    }

    private async Task SchweizerSystemGenerierenAsync(int turnierRundeId, IReadOnlyList<AuslosungsErgebnis> ergebnisse, CancellationToken ct)
    {
        var sortiertIds = ergebnisse
            .OrderBy(e => e.PaarungsNummer)
            .SelectMany(e => new[] { e.HeimMannschaftId, e.AuswaertsMannschaftId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        await SchweizerRundeGenerierenAsync(turnierRundeId, sortiertIds, ct);
    }

    private async Task PaarungenGenerierenAsync(int turnierRundeId, IReadOnlyList<AuslosungsErgebnis> ergebnisse, CancellationToken ct)
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
    }

    /// <summary>
    /// Generiert den Spielplan für eine TopfLiga-Runde direkt aus den Auslosungspaarungen.
    /// </summary>
    private async Task<SpielplanErgebnis> GenerierenAusPaarungenAsync(
        int turnierRundeId,
        IReadOnlyList<(int HeimId, int AuswaertsId)> paarungen,
        CancellationToken ct = default)
    {
        var runde = await RundeLadenAsync(turnierRundeId, ct);

        var alleIds      = paarungen.SelectMany(p => new[] { p.HeimId, p.AuswaertsId }).Distinct().ToList();
        var mannschaften = await mannschaftRepository.FindenNachIdsAsync(alleIds, ct);
        int startNr      = await spielRepository.NaechsteSpielNummerAsync(runde.TurnierInstanzId, turnierRundeId, runde.Reihenfolge, ct);

        var generator = generatorFactory.ErstellenFuerRundenart(runde.Rundenart);
        var kontext   = new SpielplanKontext(runde, mannschaften, startNr, paarungen);
        var ergebnis  = generator.Generieren(kontext);

        await SpielplanSpeicherung.ErgebnisSpeichernAsync(spielRepository, ergebnis, ct);

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
    internal async Task<SpielplanErgebnis> SchweizerRundeGenerierenAsync(
        int turnierRundeId,
        IReadOnlyList<int> sortierteMannschaftIds,
        CancellationToken ct = default)
    {
        var runde = await RundeLadenAsync(turnierRundeId, ct);

        if (sortierteMannschaftIds.Count < 2)
            throw new InvalidOperationException(
                "Schweizer System benötigt mindestens 2 Mannschaften.");

        var verlauf  = SchweizerVerlauf.AusSpielen(await spielRepository.AlleNachRundeAsync(turnierRundeId, ct), sortierteMannschaftIds);
        var geordnet = await MannschaftenInReihenfolgeAsync(sortierteMannschaftIds, ct);

        int startNr   = await spielRepository.NaechsteSpielNummerAsync(runde.TurnierInstanzId, turnierRundeId, runde.Reihenfolge, ct);
        var generator = generatorFactory.ErstellenFuerRundenart(runde.Rundenart);
        var kontext   = new SpielplanKontext(
            runde, geordnet, startNr,
            Paarungen:       null,
            BereitsGespielt: verlauf.BereitsGespielt,
            Spieltag:        verlauf.NaechsterSpieltag,
            FreilosHistorie: verlauf.FreilosHistorie,
            HeimBilanz:      verlauf.HeimBilanz);

        var ergebnis = generator.Generieren(kontext);
        await SpielplanSpeicherung.ErgebnisSpeichernAsync(spielRepository, ergebnis, ct);

        logger.LogInformation(
            "Schweizer Runde {Spieltag} für '{Runde}' generiert: {Anzahl} Spiele.",
            verlauf.NaechsterSpieltag, runde.Name, ergebnis.Spiele.Count);

        return ergebnis;
    }

    public Task<IReadOnlyList<Spiel>> SpielplanAbrufenAsync(int turnierRundeId, CancellationToken ct = default)
        => spielRepository.AlleNachRundeAsync(turnierRundeId, ct);

    /// <summary>Löscht den Spielplan einer Runde, ohne die Platzhalter-Kaskade auszulösen — für Aufrufer, die
    /// selbst Teil der Kaskade sind oder sie im Anschluss ohnehin auslösen. Issue #320: ohne diesen Aufruf blieb
    /// ein aus einer zurückgesetzten Auslosung bereits generierter Spielplan als Karteileiche stehen. Issue #306:
    /// würde die <see cref="FinaleAutomatik"/> hier die volle Kaskade auslösen, riefe sie sich reentrant selbst
    /// auf, bevor sie ihr Spiel generiert hat — mit doppelt generiertem Spiel als Folge. Ein Aufruf ohne
    /// existierenden Spielplan ändert nichts.</summary>
    public async Task<TurnierRunde> RundenSpielplanOhneKaskadeLoeschenAsync(int turnierRundeId, CancellationToken ct = default)
    {
        var runde = await RundeLadenAsync(turnierRundeId, ct);
        await turnierBaumRepository.SpielIdsNullSetzenAsync(turnierRundeId, ct);
        await spielRepository.AlleNachRundeLoeschenAsync(turnierRundeId, ct);
        logger.LogInformation("Spielplan für Runde '{Runde}' gelöscht.", runde.Name);
        return runde;
    }

    /// <summary>
    /// Generiert den Spielplan für eine Gruppenphase gruppenweise.
    /// Jede Gruppe bekommt einen eigenen Round-Robin-Spielplan.
    /// </summary>
    private async Task GenerierenAusGruppenAsync(
        int turnierRundeId,
        IReadOnlyList<IReadOnlyList<int>> gruppen,
        CancellationToken ct = default)
    {
        var runde = await RundeLadenAsync(turnierRundeId, ct);

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
            await SpielplanSpeicherung.HinUndRueckspieleSpeichernAsync(
                spielRepository,
                hinRueckspielPaare.Count == 0 ? alleSpiele : [.. hinRueckspielPaare.Select(p => p.Hinspiel)],
                hinRueckspielPaare, ct);

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
        var runde = await RundeLadenAsync(turnierRundeId, ct);

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

        // Der Plan ist nie leer: Gruppen mit weniger als zwei Plätzen weist GenerierenAusAuslosungAsync vorher ab
        var rueckspiele = plan
            .Select((geplant, i) => (geplant.HinspielIndex, Index: i))
            .Where(x => x.HinspielIndex.HasValue)
            .Select(x => (Hinspiel: alleSpiele[x.HinspielIndex!.Value], Rueckspiel: alleSpiele[x.Index]))
            .ToList();
        await SpielplanSpeicherung.HinUndRueckspieleSpeichernAsync(
            spielRepository, [.. alleSpiele.Where(s => !s.IstRueckspiel)], rueckspiele, ct);

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

    /// <summary>
    /// Erstellt Platzhalter-Spiele für eine KO-Runde direkt aus den Auslosungspaarungen.
    /// MannschaftIds bleiben null; Platzhalternamen (z.B. "Sieger Gruppe A") werden gespeichert.
    /// </summary>
    private async Task<int> GenerierenAusPlatzhalterPaarungenAsync(
        int turnierRundeId,
        IReadOnlyList<(string Heim, string Auswaerts)> paarungen,
        CancellationToken ct = default)
    {
        var runde = await RundeLadenAsync(turnierRundeId, ct);

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

            await SpielplanSpeicherung.HinUndRueckspieleSpeichernAsync(spielRepository, spiele, [], ct);
        }
        else
        {
            // Issue #346: je Paarung Hin- und gespiegeltes Rückspiel — erst alle Hinspiele, dann alle Rückspiele
            // nummeriert (so, wie sie ausgetragen werden; wie im KoRundeGenerator).
            var hinspiele = paarungen
                .Select((p, i) => PlatzhalterSpiel(runde, p.Heim, p.Auswaerts, spielNr + i, spieltag: 1))
                .ToList();
            var rueckspiele = hinspiele
                .Select(h => (Hinspiel: h, Rueckspiel: PlatzhalterSpiel(runde, h.AuswaertsPlatzhalterName!,
                    h.HeimPlatzhalterName!, h.Spielnummer + hinspiele.Count, spieltag: 2, istRueckspiel: true)))
                .ToList();
            await SpielplanSpeicherung.HinUndRueckspieleSpeichernAsync(spielRepository, hinspiele, rueckspiele, ct);

            spiele = [.. hinspiele, .. rueckspiele.Select(r => r.Rueckspiel)];
        }

        logger.LogInformation(
            "Platzhalter-Spielplan für Runde '{Runde}' generiert: {Anzahl} Spiele.",
            runde.Name, spiele.Count);

        return spiele.Count;
    }

    internal async Task<TurnierRunde> RundeLadenAsync(int turnierRundeId, CancellationToken ct) =>
        await rundeRepository.FindenNachIdMitRegelwerkAsync(turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Runde mit ID {turnierRundeId} nicht gefunden.");

    /// <summary>Die Mannschaften in der Reihenfolge der Ids (Auslosungs- bzw. Tabellenreihenfolge); unbekannte Ids
    /// entfallen.</summary>
    private async Task<List<Mannschaft>> MannschaftenInReihenfolgeAsync(IReadOnlyList<int> ids, CancellationToken ct)
    {
        var mannschaften = await mannschaftRepository.FindenNachIdsAsync(ids, ct);
        return [.. ids.Select(id => mannschaften.FirstOrDefault(m => m.Id == id)).OfType<Mannschaft>()];
    }
}
