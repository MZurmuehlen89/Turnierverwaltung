using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Spielplan;

#pragma warning disable S107 // eine Verantwortung (Spielplan einer Runde erzeugen und löschen): Runde, Spiele, Mannschaften, Generatoren, Regelwerksprüfung und Auflösung brauchen die Generierungswege; ein weiterer Schnitt diente nur der Zahl (#504)
public class SpielplanService(
    ITurnierRundeRepository rundeRepository,
    ISpielRepository spielRepository,
    IMannschaftRepository mannschaftRepository,
    SpielplanGeneratorFactory generatorFactory,
    TurnierValidator turnierValidator,
    PlatzhalterAufloesung aufloesung,
    ILogger<SpielplanService> logger)
#pragma warning restore S107
{
    /// <summary>
    /// Generiert den Spielplan für eine Runde. Die Mannschafts-IDs bestimmen Reihenfolge und Paarungen
    /// (typischerweise aus dem Auslosungsergebnis).
    /// </summary>
    public async Task<SpielplanErgebnis> GenerierenAsync(
        int turnierRundeId,
        IReadOnlyList<int> mannschaftIds,
        CancellationToken ct = default) =>
        await GenerierenAsync(await RundeLadenAsync(turnierRundeId, ct), mannschaftIds, ct);

    private async Task<SpielplanErgebnis> GenerierenAsync(
        TurnierRunde runde,
        IReadOnlyList<int> mannschaftIds,
        CancellationToken ct)
    {
        if (mannschaftIds.Count != runde.EingangsMannschaftsanzahl)
            throw new InvalidOperationException(
                $"Runde '{runde.Name}' erwartet {runde.EingangsMannschaftsanzahl} Mannschaften, " +
                $"übergeben wurden {mannschaftIds.Count}.");

        var geordnet = await MannschaftenInReihenfolgeAsync(mannschaftIds, ct);
        return await MitGeneratorErzeugenAsync(runde, startNr => new SpielplanKontext(runde, geordnet, startNr), ct);
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

        await (runde.Rundenart switch
        {
            Rundenart.Gruppenphase    => GruppenphaseGenerierenAsync(runde, ergebnisse, ct),
            Rundenart.TopfLiga        => GenerierenAusPaarungenAsync(runde, AuslosungsAuswertung.TopfLigaPaarungen(ergebnisse), ct),
            Rundenart.SchweizerSystem => SchweizerRundeGenerierenAsync(runde, AuslosungsAuswertung.MannschaftenInPaarungsreihenfolge(ergebnisse), ct),
            _                         => PaarungenGenerierenAsync(runde, ergebnisse, ct),
        });

        await aufloesung.NachGenerierungAufloesenAsync(runde, ct);
    }

    private async Task GruppenphaseGenerierenAsync(TurnierRunde runde, IReadOnlyList<AuslosungsErgebnis> ergebnisse, CancellationToken ct)
    {
        var gruppen = GruppenSpielplan.AusAuslosung(ergebnisse);

        // Feste Teams und Platzhalter (auch gemischt, #395) über einen Berger-Plan je Gruppe — denselben, gegen den
        // die Kaskade den gespeicherten Plan prüft (#620; vorher baute ein zweiter Weg Gruppen mit festen Teams)
        if (gruppen.All(g => g.Count < 2))
            throw new InvalidOperationException(ergebnisse.Any()
                ? "Keine gültigen Gruppen-Zuordnungen in der Auslosung gefunden."
                : "Die Auslosung hat keine Ergebnisse. Bitte die Auslosung zuerst durchführen.");

        var plan = GruppenSpielplan.Planen(gruppen, runde.MitRueckspiel);
        await SpieleErzeugenAsync(runde, startNr => GruppenSpielplan.SpieleErzeugen(runde, plan, startNr), ct);
    }

    // Liga, K.-o.-Runde, Finale, Spiel um Platz 3: Platzhalter-Paarungen (z. B. „Sieger Gruppe A“, Mannschaften noch
    // offen) werden übernommen, feste Teams paart der Generator in Paarungsreihenfolge
    private Task PaarungenGenerierenAsync(TurnierRunde runde, IReadOnlyList<AuslosungsErgebnis> ergebnisse, CancellationToken ct)
    {
        if (!AuslosungsAuswertung.NurPlatzhalter(ergebnisse))
            return GenerierenAsync(runde, AuslosungsAuswertung.MannschaftenInPaarungsreihenfolge(ergebnisse), ct);

        var paarungen = AuslosungsAuswertung.PlatzhalterPaarungen(ergebnisse);
        return SpieleErzeugenAsync(runde, startNr => PlatzhalterKoSpiele.Erzeugen(runde, paarungen, startNr), ct);
    }

    /// <summary>
    /// Generiert den Spielplan für eine TopfLiga-Runde direkt aus den Auslosungspaarungen.
    /// </summary>
    private async Task GenerierenAusPaarungenAsync(
        TurnierRunde runde,
        IReadOnlyList<(int HeimId, int AuswaertsId)> paarungen,
        CancellationToken ct)
    {
        var alleIds      = paarungen.SelectMany(p => new[] { p.HeimId, p.AuswaertsId }).Distinct().ToList();
        var mannschaften = await mannschaftRepository.FindenNachIdsAsync(alleIds, ct);
        await MitGeneratorErzeugenAsync(runde, startNr => new SpielplanKontext(runde, mannschaften, startNr, paarungen), ct);
    }

    /// <summary>
    /// Generiert die nächste Schweizer Runde (Spieltag) für eine SchweizerSystem-TurnierRunde.
    /// <paramref name="sortierteMannschaftIds"/> enthält alle teilnehmenden Teams sortiert nach
    /// aktuellem Tabellenstand (Rang 1 zuerst). Für Runde 1 gibt die Reihenfolge das Seeding vor.
    /// </summary>
    internal async Task<SpielplanErgebnis> SchweizerRundeGenerierenAsync(
        TurnierRunde runde,
        IReadOnlyList<int> sortierteMannschaftIds,
        CancellationToken ct)
    {
        if (sortierteMannschaftIds.Count < 2)
            throw new InvalidOperationException(
                "Schweizer System benötigt mindestens 2 Mannschaften.");

        var verlauf  = SchweizerVerlauf.AusSpielen(await spielRepository.AlleNachRundeAsync(runde.Id, ct), sortierteMannschaftIds);
        var geordnet = await MannschaftenInReihenfolgeAsync(sortierteMannschaftIds, ct);

        return await MitGeneratorErzeugenAsync(runde, startNr => new SpielplanKontext(
            runde, geordnet, startNr,
            Paarungen:       null,
            BereitsGespielt: verlauf.BereitsGespielt,
            Spieltag:        verlauf.NaechsterSpieltag,
            FreilosHistorie: verlauf.FreilosHistorie,
            HeimBilanz:      verlauf.HeimBilanz), ct);
    }

    // ── Generator-Wege: Startnummer → Generator → Speichern ─────────────────

    /// <summary>Gemeinsamer Ablauf der Wege über einen <see cref="ISpielplanGenerator"/> (Mannschaftsliste, TopfLiga,
    /// Schweizer System): Regelwerk prüfen, an die Spielnummern der vorigen Runden anschließen, generieren, speichern.
    /// <paramref name="kontextAb"/> bekommt die erste freie Spielnummer.</summary>
    private async Task<SpielplanErgebnis> MitGeneratorErzeugenAsync(
        TurnierRunde runde, Func<int, SpielplanKontext> kontextAb, CancellationToken ct)
    {
        RegelwerkPruefen(runde);

        int startNr  = await spielRepository.NaechsteSpielNummerAsync(runde.TurnierInstanzId, runde.Id, runde.Reihenfolge, ct);
        var ergebnis = generatorFactory.ErstellenFuerRundenart(runde.Rundenart).Generieren(kontextAb(startNr));
        await SpielplanSpeicherung.ErgebnisSpeichernAsync(spielRepository, ergebnis, ct);

        logger.LogInformation(
            "Spielplan ({Rundenart}) für Runde '{Runde}' generiert: {Anzahl} Spiele.",
            runde.Rundenart, runde.Name, ergebnis.Spiele.Count);
        return ergebnis;
    }

    /// <summary>K.-o.-Runden brauchen ein Entscheidungsverfahren im Regelwerk; andere Rundenarten lässt
    /// <see cref="TurnierValidator.RundeUndRegelwerkKompatibel"/> durch. Platzhalter-Spielpläne laufen bewusst nicht
    /// hier durch: Sie sind nur eine Vorstufe, das Regelwerk zählt erst mit festen Mannschaften (Nutzer, #620).</summary>
    private void RegelwerkPruefen(TurnierRunde runde)
    {
        if (runde.Regelwerk is null) return;

        var rundeEingabe = new TurnierRundeEingabe(
            runde.Name, runde.Rundenart,
            runde.EingangsMannschaftsanzahl, runde.AusgangsMannschaftsanzahl,
            runde.RegelwerkId, runde.AuswaertstorRegelAktiv);
        ValidationException.WerfenWennUngueltig(
            turnierValidator.RundeUndRegelwerkKompatibel(rundeEingabe, runde.Regelwerk));
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
        await spielRepository.AlleNachRundeLoeschenAsync(turnierRundeId, ct);
        logger.LogInformation("Spielplan für Runde '{Runde}' gelöscht.", runde.Name);
        return runde;
    }

    // ── Wege ohne Generator: Gruppen-Plan und K.-o.-Platzhalter ─────────────

    /// <summary>Gemeinsamer Ablauf der Wege, die ihre Spiele selbst bauen (<see cref="GruppenSpielplan"/>,
    /// <see cref="PlatzhalterKoSpiele"/>): an die Spielnummern der vorigen Runden anschließen, erzeugen, speichern.
    /// <paramref name="spieleAb"/> bekommt die erste freie Spielnummer.</summary>
    private async Task SpieleErzeugenAsync(
        TurnierRunde runde,
        Func<int, (List<Spiel> Hinspiele, List<(Spiel Hinspiel, Spiel Rueckspiel)> Rueckspiele)> spieleAb,
        CancellationToken ct)
    {
        int startNr = await spielRepository.NaechsteSpielNummerAsync(runde.TurnierInstanzId, runde.Id, runde.Reihenfolge, ct);
        var (hinspiele, rueckspiele) = spieleAb(startNr);
        await SpielplanSpeicherung.HinUndRueckspieleSpeichernAsync(spielRepository, hinspiele, rueckspiele, ct);

        logger.LogInformation(
            "Spielplan ({Rundenart}) für Runde '{Runde}' generiert: {Anzahl} Spiele.",
            runde.Rundenart, runde.Name, hinspiele.Count + rueckspiele.Count);
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
