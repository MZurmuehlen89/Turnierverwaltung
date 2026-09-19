using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Turniere;

public class LandesQuoteService(
    ILandesQuoteRepository repository,
    LandesQuoteValidator validator,
    ILogger<LandesQuoteService> logger)
{
    public Task<IReadOnlyList<LandesQuote>> AlleNachTurnierInstanzIdAsync(int turnierInstanzId, CancellationToken ct = default)
        => repository.AlleNachTurnierInstanzIdAsync(turnierInstanzId, ct);

    public async Task<LandesQuote> ErstellenAsync(LandesQuoteEingabe eingabe, CancellationToken ct = default)
    {
        var bestehende = await repository.AlleNachTurnierInstanzIdAsync(eingabe.TurnierInstanzId, ct);
        ValidationException.WerfenWennUngueltig(validator.Validieren(eingabe, bestehende));

        var quote = new LandesQuote
        {
            TurnierInstanzId = eingabe.TurnierInstanzId,
            LandId           = eingabe.LandId,
            Anzahl           = eingabe.Anzahl
        };

        await repository.HinzufuegenAsync(quote, ct);
        logger.LogInformation(
            "Landes-Quote angelegt: TurnierInstanz {TurnierInstanzId}, Land {LandId}, Anzahl {Anzahl}.",
            eingabe.TurnierInstanzId, eingabe.LandId, eingabe.Anzahl);
        return quote;
    }

    public async Task AktualisierenAsync(int id, LandesQuoteEingabe eingabe, CancellationToken ct = default)
    {
        var quote = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Landes-Quote mit ID {id} nicht gefunden.");

        var bestehende = await repository.AlleNachTurnierInstanzIdAsync(eingabe.TurnierInstanzId, ct);
        ValidationException.WerfenWennUngueltig(validator.Validieren(eingabe, bestehende, id));

        quote.LandId = eingabe.LandId;
        quote.Anzahl = eingabe.Anzahl;

        await repository.AktualisierenAsync(quote, ct);
        logger.LogInformation("Landes-Quote {Id} aktualisiert.", id);
    }

    public async Task LoeschenAsync(int id, CancellationToken ct = default)
    {
        var quote = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Landes-Quote mit ID {id} nicht gefunden.");
        await repository.LoeschenAsync(quote, ct);
        logger.LogInformation("Landes-Quote {Id} gelöscht.", id);
    }

    public async Task<IReadOnlyList<LandesQuote>> QuotenErsetzenAsync(
        int turnierInstanzId, IReadOnlyList<LandesQuoteZeileEingabe> zeilen, int? eingangsKapazitaet = null, CancellationToken ct = default)
    {
        ValidationException.WerfenWennUngueltig(validator.Validieren(zeilen, eingangsKapazitaet));
        var neueQuoten = zeilen.Select(z => new LandesQuote
        {
            TurnierInstanzId = turnierInstanzId, LandId = z.LandId,
            Anzahl = z.Anzahl
        }).ToList();
        var gespeichert = await repository.QuotenErsetzenAsync(turnierInstanzId, neueQuoten, ct);
        logger.LogInformation("Landes-Quoten für TurnierInstanz {Id} ersetzt ({Anzahl} Einträge).", turnierInstanzId, zeilen.Count);
        return gespeichert;
    }
}
