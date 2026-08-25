using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Ranglisten;

public class RanglistenQuelleService(
    IRanglistenQuelleRepository repository,
    RanglistenQuelleValidator validator,
    ILogger<RanglistenQuelleService> logger)
{
    public Task<IReadOnlyList<RanglistenQuelle>> AlleAbrufenAsync(CancellationToken ct = default)
        => repository.AlleFindenAsync(ct);

    public Task<RanglistenQuelle?> FindenNachIdAsync(int id, CancellationToken ct = default)
        => repository.FindenNachIdAsync(id, ct);

    public async Task<RanglistenQuelle> ErstellenAsync(RanglistenQuelleErstellenEingabe eingabe, CancellationToken ct = default)
    {
        ValidationException.WerfenWennUngueltig(validator.Validieren(eingabe));

        var quelle = new RanglistenQuelle
        {
            Name                   = eingabe.Name.Trim(),
            Url                     = eingabe.Url.Trim(),
            TabellenIndex           = eingabe.TabellenIndex,
            TabellenSelektor        = string.IsNullOrWhiteSpace(eingabe.TabellenSelektor) ? null : eingabe.TabellenSelektor.Trim(),
            MannschaftSpalte        = eingabe.MannschaftSpalte,
            PunkteSpalte            = eingabe.PunkteSpalte,
            KopfzeileUeberspringen  = eingabe.KopfzeileUeberspringen
        };

        await repository.HinzufuegenAsync(quelle, ct);
        logger.LogInformation("Ranglisten-Quelle '{Name}' angelegt (ID {Id}).", quelle.Name, quelle.Id);
        return quelle;
    }

    public async Task AktualisierenAsync(int id, RanglistenQuelleAktualisierenEingabe eingabe, CancellationToken ct = default)
    {
        var quelle = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Ranglisten-Quelle mit ID {id} nicht gefunden.");

        ValidationException.WerfenWennUngueltig(validator.Validieren(eingabe));

        quelle.Name                    = eingabe.Name.Trim();
        quelle.Url                     = eingabe.Url.Trim();
        quelle.TabellenIndex           = eingabe.TabellenIndex;
        quelle.TabellenSelektor        = string.IsNullOrWhiteSpace(eingabe.TabellenSelektor) ? null : eingabe.TabellenSelektor.Trim();
        quelle.MannschaftSpalte        = eingabe.MannschaftSpalte;
        quelle.PunkteSpalte            = eingabe.PunkteSpalte;
        quelle.KopfzeileUeberspringen  = eingabe.KopfzeileUeberspringen;

        await repository.AktualisierenAsync(quelle, ct);
        logger.LogInformation("Ranglisten-Quelle '{Name}' (ID {Id}) aktualisiert.", quelle.Name, quelle.Id);
    }

    public async Task LoeschenAsync(int id, CancellationToken ct = default)
    {
        var quelle = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Ranglisten-Quelle mit ID {id} nicht gefunden.");
        await repository.LoeschenAsync(quelle, ct);
        logger.LogInformation("Ranglisten-Quelle '{Name}' (ID {Id}) gelöscht.", quelle.Name, quelle.Id);
    }
}
