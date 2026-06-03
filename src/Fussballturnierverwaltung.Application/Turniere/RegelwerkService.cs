using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Turniere;

public class RegelwerkService(
    IRegelwerkRepository repository,
    IRankingRegelSetRepository rankingRegelSetRepository,
    RegelwerkValidator validator,
    ILogger<RegelwerkService> logger)
{
    public Task<IReadOnlyList<Regelwerk>> AlleAbrufenAsync(CancellationToken ct = default)
        => repository.AlleFindenMitRegelnAsync(ct);

    public Task<Regelwerk?> FindenNachIdAsync(int id, CancellationToken ct = default)
        => repository.FindenNachIdMitAllenRegelnAsync(id, ct);

    public Task<IReadOnlyList<RankingRegelSet>> AlleRankingRegelSetsAbrufenAsync(CancellationToken ct = default)
        => rankingRegelSetRepository.AlleFindenMitEintraegenAsync(ct);

    public async Task<Regelwerk> ErstellenAsync(RegelwerkErstellenEingabe eingabe, CancellationToken ct = default)
    {
        ValidationException.WerfenWennUngueltig(validator.Validieren(eingabe));

        var regelwerk = new Regelwerk
        {
            Name          = eingabe.Name.Trim(),
            Beschreibung  = eingabe.Beschreibung.Trim(),
            GueltigVon    = eingabe.GueltigVon,
            GueltigBis    = eingabe.GueltigBis,
            RankingRegelSetId = eingabe.RankingRegelSetId
        };

        regelwerk.PunkteRegeln.Add(new PunkteRegel
        {
            PunkteFuerSieg          = eingabe.PunkteFuerSieg,
            PunkteFuerUnentschieden = eingabe.PunkteFuerUnentschieden,
            PunkteFuerNiederlage    = eingabe.PunkteFuerNiederlage
        });

        for (int i = 0; i < eingabe.EntscheidungsVerfahrenReihenfolge.Count; i++)
        {
            regelwerk.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            {
                Reihenfolge  = i + 1,
                VerfahrenArt = eingabe.EntscheidungsVerfahrenReihenfolge[i]
            });
        }

        await repository.HinzufuegenAsync(regelwerk, ct);
        logger.LogInformation("Regelwerk '{Name}' angelegt (ID {Id}).", regelwerk.Name, regelwerk.Id);
        return regelwerk;
    }

    public async Task LoeschenAsync(int id, CancellationToken ct = default)
    {
        var regelwerk = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Regelwerk mit ID {id} nicht gefunden.");
        await repository.LoeschenAsync(regelwerk, ct);
        logger.LogInformation("Regelwerk '{Name}' (ID {Id}) gelöscht.", regelwerk.Name, regelwerk.Id);
    }
}
