using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Turniere;

public class VerbandsQuoteService(
    IVerbandsQuoteRepository repository,
    VerbandsQuoteValidator validator,
    ILogger<VerbandsQuoteService> logger)
{
    public Task<IReadOnlyList<VerbandsQuote>> AlleNachTurnierInstanzIdAsync(int turnierInstanzId, CancellationToken ct = default)
        => repository.AlleNachTurnierInstanzIdAsync(turnierInstanzId, ct);

    public async Task<VerbandsQuote> ErstellenAsync(VerbandsQuoteEingabe eingabe, CancellationToken ct = default)
    {
        var bestehende = await repository.AlleNachTurnierInstanzIdAsync(eingabe.TurnierInstanzId, ct);
        ValidationException.WerfenWennUngueltig(validator.Validieren(eingabe, bestehende));

        var quote = new VerbandsQuote
        {
            TurnierInstanzId = eingabe.TurnierInstanzId,
            VerbandId        = eingabe.VerbandId,
            MaxAnzahl         = eingabe.MaxAnzahl,
            MinAnzahl         = eingabe.MinAnzahl
        };

        await repository.HinzufuegenAsync(quote, ct);
        logger.LogInformation(
            "Verband-Quote angelegt: TurnierInstanz {TurnierInstanzId}, Verband {VerbandId}, Max {Max}, Min {Min}.",
            eingabe.TurnierInstanzId, eingabe.VerbandId, eingabe.MaxAnzahl, eingabe.MinAnzahl);
        return quote;
    }

    public async Task AktualisierenAsync(int id, VerbandsQuoteEingabe eingabe, CancellationToken ct = default)
    {
        var quote = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Verband-Quote mit ID {id} nicht gefunden.");

        var bestehende = await repository.AlleNachTurnierInstanzIdAsync(eingabe.TurnierInstanzId, ct);
        ValidationException.WerfenWennUngueltig(validator.Validieren(eingabe, bestehende, id));

        quote.VerbandId = eingabe.VerbandId;
        quote.MaxAnzahl  = eingabe.MaxAnzahl;
        quote.MinAnzahl  = eingabe.MinAnzahl;

        await repository.AktualisierenAsync(quote, ct);
        logger.LogInformation("Verband-Quote {Id} aktualisiert.", id);
    }

    public async Task LoeschenAsync(int id, CancellationToken ct = default)
    {
        var quote = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Verband-Quote mit ID {id} nicht gefunden.");
        await repository.LoeschenAsync(quote, ct);
        logger.LogInformation("Verband-Quote {Id} gelöscht.", id);
    }

    public async Task<IReadOnlyList<VerbandsQuote>> QuotenErsetzenAsync(
        int turnierInstanzId, IReadOnlyList<VerbandsQuoteZeileEingabe> zeilen, CancellationToken ct = default)
    {
        ValidationException.WerfenWennUngueltig(validator.Validieren(zeilen));
        var neueQuoten = zeilen.Select(z => new VerbandsQuote
        {
            TurnierInstanzId = turnierInstanzId, VerbandId = z.VerbandId,
            MaxAnzahl = z.MaxAnzahl, MinAnzahl = z.MinAnzahl
        }).ToList();
        var gespeichert = await repository.QuotenErsetzenAsync(turnierInstanzId, neueQuoten, ct);
        logger.LogInformation("Verband-Quoten für TurnierInstanz {Id} ersetzt ({Anzahl} Einträge).", turnierInstanzId, zeilen.Count);
        return gespeichert;
    }
}
