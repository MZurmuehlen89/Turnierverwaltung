using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>K.o.-Quellverknüpfung (Issue #241): hält an jedem Spiel einer K.o.-Runde fest, welches Vorrundenspiel
/// seine Seiten als Sieger oder Verlierer speist. Ersetzt das frühere, ausschließlich in
/// <c>TurnierBaumService.VerkuepfenPerPlatzhalter</c> lebende Erraten aus Text — die ID steht direkt am
/// <see cref="Spiel"/>, damit Platzhalter-Auflösung und Turnierbaum dieselbe robuste Quelle nutzen.</summary>
public class KoQuellverknuepfung(
    ITurnierRundeRepository rundeRepository,
    ISpielRepository spielRepository,
    ILogger<KoQuellverknuepfung> logger)
{
    /// <summary>Verknüpft alle K.o.-artigen Runden (K.o.-Runde/Finale/Spiel um Platz 3) einer Turnierinstanz.
    /// Idempotent und günstig: bereits verknüpfte Seiten werden übersprungen.</summary>
    public async Task AlleRundenVerknuepfenAsync(int turnierInstanzId, CancellationToken ct = default)
    {
        var alleRunden = await rundeRepository.AlleNachInstanzAsync(turnierInstanzId, ct);
        var koRunden = alleRunden
            .Where(r => r.Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei)
            .OrderBy(r => r.Reihenfolge);

        foreach (var zielRunde in koRunden)
            await RundeVerknuepfenAsync(new Zielrunde(zielRunde, alleRunden), ct);
    }

    /// <summary>Die Runde, deren Spiele gerade verknüpft werden, samt den dafür schon geladenen Quellrunden.</summary>
    private sealed record Zielrunde(TurnierRunde Runde, IReadOnlyList<TurnierRunde> AlleRunden)
    {
        public Dictionary<int, List<Spiel>> EntscheidungsspieleJeQuellrunde { get; } = [];
    }

    /// <summary>Arbeitet je Seite unabhängig — anders als das frühere
    /// <c>TurnierBaumService.VerkuepfenPerPlatzhalter</c>, das einen gesamten Rundenübergang fälschlich als
    /// "erledigt" markierte, sobald irgendein Spiel per Text zugeordnet werden konnte.</summary>
    private async Task RundeVerknuepfenAsync(Zielrunde ziel, CancellationToken ct)
    {
        var zielSpiele = await spielRepository.AlleNachRundeAsync(ziel.Runde.Id, ct);

        var geaenderteSpiele = new List<Spiel>();
        foreach (var spiel in zielSpiele)
            if (await SeitenVerknuepfenAsync(spiel, ziel, ct)) geaenderteSpiele.Add(spiel);
        if (geaenderteSpiele.Count == 0) return;

        var uhr = Stopwatch.StartNew();
        await spielRepository.AktualisierenMehrfachAsync(geaenderteSpiele, ct);
        logger.LogInformation(
            "Quellverknüpfung in Runde '{Runde}': {Anzahl} Spiel(e) verknüpft (Speichern {Speichern} ms).",
            ziel.Runde.Name, geaenderteSpiele.Count, uhr.ElapsedMilliseconds);
    }

    /// <summary>Trägt die Quelle jeder noch nicht verknüpften Seite am Spiel ein und meldet, ob sich etwas
    /// geändert hat.</summary>
    private async Task<bool> SeitenVerknuepfenAsync(Spiel spiel, Zielrunde ziel, CancellationToken ct)
    {
        bool geaendert = false;

        if (spiel.HeimQuellSpielId is null &&
            await QuelleAsync(spiel.HeimPlatzhalterName, ziel, ct) is { } heimQuelle)
        {
            spiel.HeimQuellSpielId  = heimQuelle.SpielId;
            spiel.HeimQuellErgebnis = heimQuelle.Typ;
            geaendert = true;
        }

        if (spiel.AuswaertsQuellSpielId is null &&
            await QuelleAsync(spiel.AuswaertsPlatzhalterName, ziel, ct) is { } auswaertsQuelle)
        {
            spiel.AuswaertsQuellSpielId  = auswaertsQuelle.SpielId;
            spiel.AuswaertsQuellErgebnis = auswaertsQuelle.Typ;
            geaendert = true;
        }

        return geaendert;
    }

    // Die Position im Tableau ist ab Erzeugung der Runde stabil — bewusst NICHT wie bei der Auflösung über
    // Platzierungen (PlatzhalterAufloesung) auf bereits mit Mannschaften besetzte Spiele gefiltert.
    private async Task<(int SpielId, SpielQuellErgebnisTyp Typ)?> QuelleAsync(
        string? platzhalterName, Zielrunde ziel, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(platzhalterName)) return null;
        if (PlatzhalterNamen.KoSpielErkennen(platzhalterName) is not { } bezug) return null;

        var quellRunde = PlatzhalterNamen.KoQuellrunde(bezug, ziel.Runde, ziel.AlleRunden);
        if (quellRunde is null) return null;

        var quellSpiele = await EntscheidungsspieleAsync(quellRunde, ziel, ct);
        if (bezug.SpielNummer < 1 || bezug.SpielNummer > quellSpiele.Count) return null;

        return (quellSpiele[bezug.SpielNummer - 1].Id,
            bezug.Verlierer ? SpielQuellErgebnisTyp.Verlierer : SpielQuellErgebnisTyp.Sieger);
    }

    /// <summary>Issue #346: "Sieger &lt;Runde&gt; N" meint die N-te Paarung — bei Hin-/Rückspiel deren Rückspiel,
    /// das die Gesamtwertung trägt.</summary>
    private async Task<List<Spiel>> EntscheidungsspieleAsync(TurnierRunde quellRunde, Zielrunde ziel, CancellationToken ct)
    {
        if (ziel.EntscheidungsspieleJeQuellrunde.TryGetValue(quellRunde.Id, out var gemerkt)) return gemerkt;

        var spiele = KoPaarungen.EntscheidungsspieleJePaarung(await spielRepository.AlleNachRundeAsync(quellRunde.Id, ct));
        ziel.EntscheidungsspieleJeQuellrunde[quellRunde.Id] = spiele;
        return spiele;
    }
}
