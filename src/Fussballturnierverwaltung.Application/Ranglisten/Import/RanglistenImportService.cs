using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Ranglisten.Import;

public class RanglistenImportService(
    IHtmlHerunterlader htmlHerunterlader,
    IRanglistenHtmlAuswerter htmlAuswerter,
    MannschaftService mannschaftService,
    ILogger<RanglistenImportService> logger)
{
    public async Task<RanglistenImportVorschau> VorschauAbrufenAsync(RanglistenQuelle quelle, CancellationToken ct = default)
    {
        var html = await htmlHerunterlader.HerunterladenAsync(quelle.Url, ct);
        var rohZeilen = htmlAuswerter.Auswerten(html, quelle);

        var mannschaften = await mannschaftService.AlleAbrufenAsync(ct);
        var mannschaftLookup = mannschaften
            .GroupBy(m => Normalisieren(m.Name))
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.Single());

        var aufgeloest = new List<RanglistenImportZeile>();
        var unaufgeloest = new List<RanglistenRohZeile>();

        foreach (var zeile in rohZeilen)
        {
            if (mannschaftLookup.TryGetValue(Normalisieren(zeile.RohName), out var mannschaft))
                aufgeloest.Add(new RanglistenImportZeile(zeile, mannschaft));
            else
                unaufgeloest.Add(zeile);
        }

        logger.LogInformation(
            "Rangliste von '{Url}' abgerufen: {Aufgeloest} aufgelöst, {Unaufgeloest} nicht aufgelöst.",
            quelle.Url, aufgeloest.Count, unaufgeloest.Count);

        return new RanglistenImportVorschau(aufgeloest, unaufgeloest);
    }

    /// <summary>
    /// Baut Eintraege-Eingaben aus der final zusammengestellten, geordneten Liste
    /// (aufgelöste + manuell zugeordnete Zeilen, übersprungene bereits ausgeschlossen).
    /// Rang ergibt sich ausschließlich aus der Position — analog zu RanglisteViewModel.
    /// </summary>
    public IReadOnlyList<RanglistenEintragEingabe> EintraegeAusAufloesungErstellen(
        IReadOnlyList<(Mannschaft Mannschaft, int? Punkte)> geordneteZuordnung)
        => geordneteZuordnung
            .Select((z, index) => new RanglistenEintragEingabe(z.Mannschaft.Id, index + 1, z.Punkte))
            .ToList();

    private static string Normalisieren(string name) => name.Trim().ToUpperInvariant();
}
