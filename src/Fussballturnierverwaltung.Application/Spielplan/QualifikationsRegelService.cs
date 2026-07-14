using Fussballturnierverwaltung.Application.Spielplan.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Spielplan;

public class QualifikationsRegelService(
    IQualifikationsRegelRepository repository,
    ITurnierRundeRepository rundeRepository,
    QualifikationsRegelValidator validator,
    SpielplanService spielplanService,
    ILogger<QualifikationsRegelService> logger)
{
    public Task<IReadOnlyList<QualifikationsRegel>> AlleNachQuellRundeIdAsync(int quellRundeId, CancellationToken ct = default)
        => repository.AlleNachQuellRundeIdAsync(quellRundeId, ct);

    public Task<IReadOnlyList<QualifikationsRegel>> AlleNachZielRundeIdAsync(int zielRundeId, CancellationToken ct = default)
        => repository.AlleNachZielRundeIdAsync(zielRundeId, ct);

    public async Task<QualifikationsRegel> ErstellenAsync(QualifikationsRegelEingabe eingabe, CancellationToken ct = default)
    {
        var quellRunde = await QuellRundeLadenAsync(eingabe.QuellRundeId, ct);
        ValidationException.WerfenWennUngueltig(validator.Validieren(eingabe, quellRunde));

        var regel = new QualifikationsRegel
        {
            QuellRundeId    = eingabe.QuellRundeId,
            ZielRundeId     = eingabe.ZielRundeId,
            GruppeIndex     = eingabe.GruppeIndex,
            PlatzierungVon  = eingabe.PlatzierungVon,
            PlatzierungBis  = eingabe.PlatzierungBis
        };

        await repository.HinzufuegenAsync(regel, ct);
        logger.LogInformation(
            "Aufstiegsregel angelegt: Runde '{QuellRunde}' → Runde '{ZielRunde}' (Platz {Von}-{Bis}).",
            quellRunde.Name, eingabe.ZielRundeId, eingabe.PlatzierungVon, eingabe.PlatzierungBis);
        return regel;
    }

    public async Task AktualisierenAsync(int id, QualifikationsRegelEingabe eingabe, CancellationToken ct = default)
    {
        var regel = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Aufstiegsregel mit ID {id} nicht gefunden.");

        var quellRunde = await QuellRundeLadenAsync(eingabe.QuellRundeId, ct);
        ValidationException.WerfenWennUngueltig(validator.Validieren(eingabe, quellRunde));

        regel.QuellRundeId   = eingabe.QuellRundeId;
        regel.ZielRundeId    = eingabe.ZielRundeId;
        regel.GruppeIndex    = eingabe.GruppeIndex;
        regel.PlatzierungVon = eingabe.PlatzierungVon;
        regel.PlatzierungBis = eingabe.PlatzierungBis;

        await repository.AktualisierenAsync(regel, ct);
        logger.LogInformation("Aufstiegsregel {Id} aktualisiert.", id);
    }

    public async Task LoeschenAsync(int id, CancellationToken ct = default)
    {
        var regel = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Aufstiegsregel mit ID {id} nicht gefunden.");
        await repository.LoeschenAsync(regel, ct);
        logger.LogInformation("Aufstiegsregel {Id} gelöscht.", id);
    }

    /// <summary>Löst alle Platzierungen einer Regel auf. Werte sind <c>null</c>, solange die Quellrunde für die
    /// betreffende Platzierung noch nicht (vollständig) entschieden ist.</summary>
    public async Task<IReadOnlyDictionary<int, int?>> QualifikantenErmittelnAsync(int qualifikationsRegelId, CancellationToken ct = default)
    {
        var regel = await repository.FindenNachIdMitRundenAsync(qualifikationsRegelId, ct)
            ?? throw new InvalidOperationException($"Aufstiegsregel mit ID {qualifikationsRegelId} nicht gefunden.");

        return await spielplanService.QualifikationsRegelQualifikantenAsync(regel, ct);
    }

    private async Task<TurnierRunde> QuellRundeLadenAsync(int quellRundeId, CancellationToken ct)
        => await rundeRepository.FindenNachIdMitRegelwerkAsync(quellRundeId, ct)
            ?? throw new InvalidOperationException($"Quellrunde mit ID {quellRundeId} nicht gefunden.");
}
