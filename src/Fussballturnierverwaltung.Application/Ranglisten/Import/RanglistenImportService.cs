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
    /// Baut Eintraege-Eingaben aus der Vorschau plus der manuellen Zuordnung nicht aufgelöster
    /// Zeilen (Schlüssel = RanglistenRohZeile.Index, Wert = zugeordnete Mannschaft oder null
    /// zum Überspringen). Die Reihenfolge in der Quelltabelle (Index) bleibt erhalten — Rang
    /// ergibt sich ausschließlich aus der resultierenden Position, analog zu RanglisteViewModel.
    /// </summary>
    public IReadOnlyList<RanglistenEintragEingabe> EintraegeErstellen(
        RanglistenImportVorschau vorschau,
        IReadOnlyDictionary<int, Mannschaft?> manuelleZuordnung)
    {
        var zeilen = vorschau.Aufgeloest
            .Select(z => (z.Quelle.Index, Mannschaft: (Mannschaft?)z.Mannschaft, z.Quelle.Punkte))
            .Concat(vorschau.Unaufgeloest
                .Where(z => manuelleZuordnung.TryGetValue(z.Index, out var m) && m is not null)
                .Select(z => (z.Index, Mannschaft: manuelleZuordnung[z.Index], z.Punkte)))
            .OrderBy(z => z.Index)
            .ToList();

        return zeilen
            .Select((z, index) => new RanglistenEintragEingabe(z.Mannschaft!.Id, index + 1, z.Punkte))
            .ToList();
    }

    private static string Normalisieren(string name) => name.Trim().ToUpperInvariant();
}
