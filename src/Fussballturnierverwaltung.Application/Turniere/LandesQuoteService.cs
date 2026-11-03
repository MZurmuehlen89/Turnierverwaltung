using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Turniere;

public class LandesQuoteService(
    ILandesQuoteRepository repository,
    ILandRepository landRepository,
    LandesQuoteValidator validator,
    ILogger<LandesQuoteService> logger)
{
    public Task<IReadOnlyList<LandesQuote>> AlleNachTurnierInstanzIdAsync(int turnierInstanzId, CancellationToken ct = default)
        => repository.AlleNachTurnierInstanzIdAsync(turnierInstanzId, ct);

    public async Task<IReadOnlyList<LandesQuote>> QuotenErsetzenAsync(
        int turnierInstanzId, IReadOnlyList<LandesQuoteZeileEingabe> zeilen, int? eingangsKapazitaet = null, CancellationToken ct = default)
    {
        var laender = await landRepository.AlleFindenMitVerbandAsync(ct);
        ValidationException.WerfenWennUngueltig(validator.Validieren(zeilen, laender, eingangsKapazitaet));
        var neueQuoten = zeilen.Select(z => new LandesQuote
        {
            TurnierInstanzId = turnierInstanzId, LandId = z.LandId, Anzahl = z.Anzahl,
            Spielklasse = z.Spielklasse, AbPlatz = z.GespeicherterAbPlatz, Pokalsieger = z.Pokalsieger
        }).ToList();
        var gespeichert = await repository.QuotenErsetzenAsync(turnierInstanzId, neueQuoten, ct);
        logger.LogInformation("Landes-Quoten für TurnierInstanz {Id} ersetzt ({Anzahl} Einträge).", turnierInstanzId, zeilen.Count);
        return gespeichert;
    }
}
