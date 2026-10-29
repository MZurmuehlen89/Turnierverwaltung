using Fussballturnierverwaltung.Application.Stammdaten.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Stammdaten;

public class LandService(ILandRepository repository, LandValidator validator, ILogger<LandService> logger)
{
    public Task<IReadOnlyList<Land>> AlleAbrufenAsync(CancellationToken ct = default)
        => repository.AlleFindenAsync(ct);

    public Task<IReadOnlyList<Land>> AlleAbrufenMitVerbandAsync(CancellationToken ct = default)
        => repository.AlleFindenMitVerbandAsync(ct);

    public async Task<Land?> FindenNachIdAsync(int id, CancellationToken ct = default)
        => await repository.FindenNachIdAsync(id, ct);

    public async Task<Land> ErstellenAsync(LandErstellenEingabe eingabe, CancellationToken ct = default)
    {
        ValidationException.WerfenWennUngueltig(validator.Validieren(eingabe));
        var land = new Land
        {
            Name = eingabe.Name.Trim(),
            Kurzname = eingabe.Kurzname.Trim(),
            FifaCode = eingabe.FifaCode.Trim().ToUpperInvariant(),
            Kontinent = eingabe.Kontinent,
            VerbandId = eingabe.VerbandId,
            Flagge = eingabe.Flagge,
            RanglisteId = eingabe.RanglisteId
        };
        await repository.HinzufuegenAsync(land, ct);
        logger.LogInformation("Land '{Name}' angelegt (ID {Id}).", land.Name, land.Id);
        return land;
    }

    public async Task AktualisierenAsync(int id, LandAktualisierenEingabe eingabe, CancellationToken ct = default)
    {
        var land = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Land mit ID {id} nicht gefunden.");
        ValidationException.WerfenWennUngueltig(validator.Validieren(eingabe));
        land.Name = eingabe.Name.Trim();
        land.Kurzname = eingabe.Kurzname.Trim();
        land.FifaCode = eingabe.FifaCode.Trim().ToUpperInvariant();
        land.Kontinent = eingabe.Kontinent;
        land.VerbandId = eingabe.VerbandId;
        land.Flagge = eingabe.Flagge;
        land.RanglisteId = eingabe.RanglisteId;
        await repository.AktualisierenAsync(land, ct);
    }

    public async Task LoeschenAsync(int id, CancellationToken ct = default)
    {
        var land = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Land mit ID {id} nicht gefunden.");
        await repository.LoeschenAsync(land, ct);
    }
}
