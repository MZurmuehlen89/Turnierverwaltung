using Fussballturnierverwaltung.Application.Stammdaten.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Stammdaten;

public class VerbandService(IVerbandRepository repository, VerbandValidator validator, ILogger<VerbandService> logger)
{
    public Task<IReadOnlyList<Verband>> AlleAbrufenAsync(CancellationToken ct = default)
        => repository.AlleFindenAsync(ct);

    public async Task<Verband> ErstellenAsync(VerbandErstellenEingabe eingabe, CancellationToken ct = default)
    {
        ValidationException.WerfenWennUngueltig(validator.Validieren(eingabe));
        var verband = new Verband
        {
            Name = eingabe.Name.Trim(),
            Kurzname = eingabe.Kurzname.Trim(),
            Logo = eingabe.Logo,
            RanglisteId = eingabe.RanglisteId,
            VereinsRanglisteId = eingabe.VereinsRanglisteId
        };
        await repository.HinzufuegenAsync(verband, ct);
        logger.LogInformation("Verband '{Name}' angelegt (ID {Id}).", verband.Name, verband.Id);
        return verband;
    }

    public async Task AktualisierenAsync(int id, VerbandAktualisierenEingabe eingabe, CancellationToken ct = default)
    {
        var verband = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Verband mit ID {id} nicht gefunden.");
        ValidationException.WerfenWennUngueltig(validator.Validieren(eingabe));
        verband.Name = eingabe.Name.Trim();
        verband.Kurzname = eingabe.Kurzname.Trim();
        verband.Logo = eingabe.Logo;
        verband.RanglisteId = eingabe.RanglisteId;
        verband.VereinsRanglisteId = eingabe.VereinsRanglisteId;
        await repository.AktualisierenAsync(verband, ct);
    }

    public async Task LoeschenAsync(int id, CancellationToken ct = default)
    {
        var verband = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Verband mit ID {id} nicht gefunden.");
        await repository.LoeschenAsync(verband, ct);
    }
}
