using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Fussballturnierverwaltung.Application.Spielplan;

public class KoQuellverknuepfung(
    ITurnierRundeRepository rundeRepository,
    ISpielRepository spielRepository,
    ILogger<KoQuellverknuepfung> logger)
{
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
}
