using Fussballturnierverwaltung.Application.Stammdaten.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Stammdaten;

public class MannschaftService(
    IMannschaftRepository repository,
    MannschaftValidator validator,
    ILogger<MannschaftService> logger)
{
    public Task<IReadOnlyList<Mannschaft>> AlleAbrufenAsync(CancellationToken ct = default)
        => repository.AlleFindenAsync(ct);

    public Task<IReadOnlyList<Mannschaft>> AlleAbrufenMitLandAsync(CancellationToken ct = default)
        => repository.AlleFindenMitLandAsync(ct);

    public Task<IReadOnlyList<MannschaftAuswahl>> AlleAlsAuswahlAbrufenAsync(CancellationToken ct = default)
        => repository.AlleAlsAuswahlFindenAsync(ct);

    public Task<IReadOnlyList<Mannschaft>> SuchenAsync(string suchbegriff, CancellationToken ct = default)
        => repository.SuchenAsync(suchbegriff, ct);

    public Task<IReadOnlyList<Mannschaft>> AlleNachArtAbrufenAsync(Mannschaftsart art, CancellationToken ct = default)
        => repository.AlleNachArtFindenAsync(art, ct);

    public Task<IReadOnlyList<Mannschaft>> AlleNachTurnierfilterAsync(int? verbandId, int? landId, MannschaftsartFilter filter, CancellationToken ct = default)
        => repository.AlleNachTurnierfilterAsync(verbandId, landId, filter, ct);

    public async Task<Mannschaft?> FindenNachIdAsync(int id, CancellationToken ct = default)
        => await repository.FindenNachIdMitLandAsync(id, ct);

    public async Task<Mannschaft> ErstellenAsync(MannschaftErstellenEingabe eingabe, CancellationToken ct = default)
    {
        ValidationException.WerfenWennUngueltig(validator.Validieren(eingabe));

        var mannschaft = new Mannschaft
        {
            Name             = eingabe.Name.Trim(),
            Kurzname         = eingabe.Kurzname.Trim().ToUpperInvariant(),
            Mannschaftsart   = eingabe.Mannschaftsart,
            LandId           = eingabe.LandId,
            OffensivRating   = eingabe.OffensivRating,
            MittelfeldRating = eingabe.MittelfeldRating,
            DefensivRating   = eingabe.DefensivRating,
            Heimstaerke      = eingabe.Heimstaerke,
            Auswaertsstaerke = eingabe.Auswaertsstaerke,
            Wappen           = eingabe.Wappen
        };

        await repository.HinzufuegenAsync(mannschaft, ct);
        logger.LogInformation("Mannschaft '{Name}' angelegt (ID {Id}).", mannschaft.Name, mannschaft.Id);
        return mannschaft;
    }

    public async Task AktualisierenAsync(int id, MannschaftAktualisierenEingabe eingabe, CancellationToken ct = default)
    {
        var mannschaft = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Mannschaft mit ID {id} nicht gefunden.");

        ValidationException.WerfenWennUngueltig(validator.Validieren(eingabe));

        mannschaft.Name             = eingabe.Name.Trim();
        mannschaft.Kurzname         = eingabe.Kurzname.Trim().ToUpperInvariant();
        mannschaft.LandId           = eingabe.LandId;
        mannschaft.OffensivRating   = eingabe.OffensivRating;
        mannschaft.MittelfeldRating = eingabe.MittelfeldRating;
        mannschaft.DefensivRating   = eingabe.DefensivRating;
        mannschaft.Heimstaerke      = eingabe.Heimstaerke;
        mannschaft.Auswaertsstaerke = eingabe.Auswaertsstaerke;
        mannschaft.Wappen           = eingabe.Wappen;

        await repository.AktualisierenAsync(mannschaft, ct);
        logger.LogInformation("Mannschaft '{Name}' (ID {Id}) aktualisiert.", mannschaft.Name, mannschaft.Id);
    }

    public async Task LoeschenAsync(int id, CancellationToken ct = default)
    {
        var mannschaft = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Mannschaft mit ID {id} nicht gefunden.");

        await repository.LoeschenAsync(mannschaft, ct);
        logger.LogInformation("Mannschaft '{Name}' (ID {Id}) gelöscht.", mannschaft.Name, mannschaft.Id);
    }

}
