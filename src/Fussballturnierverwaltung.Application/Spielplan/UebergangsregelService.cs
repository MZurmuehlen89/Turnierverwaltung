using Fussballturnierverwaltung.Application.Spielplan.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Spielplan;

public class UebergangsregelService(
    IUebergangsregelRepository repository,
    ITurnierRundeRepository rundeRepository,
    UebergangsregelValidator validator,
    SpielplanService spielplanService,
    ILogger<UebergangsregelService> logger)
{
    public Task<IReadOnlyList<Uebergangsregel>> AlleNachQuellRundeIdAsync(int quellRundeId, CancellationToken ct = default)
        => repository.AlleNachQuellRundeIdAsync(quellRundeId, ct);

    public Task<IReadOnlyList<Uebergangsregel>> AlleNachZielRundeIdAsync(int zielRundeId, CancellationToken ct = default)
        => repository.AlleNachZielRundeIdAsync(zielRundeId, ct);

    public Task<IReadOnlyList<Uebergangsregel>> AlleNachTurnierInstanzIdAsync(int turnierInstanzId, CancellationToken ct = default)
        => repository.AlleNachTurnierInstanzIdAsync(turnierInstanzId, ct);

    public async Task<Uebergangsregel> ErstellenAsync(UebergangsregelEingabe eingabe, CancellationToken ct = default)
    {
        var quellRunde = await QuellRundeLadenAsync(eingabe.QuellRundeId, ct);
        var zielRunde = await ZielRundeLadenAsync(eingabe.ZielRundeId, ct);
        var alleRegeln = await repository.AlleFindenAsync(ct);
        ValidationException.WerfenWennUngueltig(validator.Validieren(eingabe, quellRunde, zielRunde, alleRegeln));

        var regel = new Uebergangsregel
        {
            QuellRundeId    = eingabe.QuellRundeId,
            ZielRundeId     = eingabe.ZielRundeId,
            GruppeIndex     = eingabe.GruppeIndex,
            PlatzierungVon  = eingabe.PlatzierungVon,
            PlatzierungBis  = eingabe.PlatzierungBis
        };

        await repository.HinzufuegenAsync(regel, ct);
        logger.LogInformation(
            "Übergangsregel angelegt: Runde '{QuellRunde}' → Runde '{ZielRunde}' (Platz {Von}-{Bis}).",
            quellRunde.Name, eingabe.ZielRundeId, eingabe.PlatzierungVon, eingabe.PlatzierungBis);
        return regel;
    }

    public async Task AktualisierenAsync(int id, UebergangsregelEingabe eingabe, CancellationToken ct = default)
    {
        var regel = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Übergangsregel mit ID {id} nicht gefunden.");

        var quellRunde = await QuellRundeLadenAsync(eingabe.QuellRundeId, ct);
        var zielRunde = await ZielRundeLadenAsync(eingabe.ZielRundeId, ct);
        var alleRegeln = await repository.AlleFindenAsync(ct);
        ValidationException.WerfenWennUngueltig(validator.Validieren(eingabe, quellRunde, zielRunde, alleRegeln, bearbeiteteRegelId: id));

        regel.QuellRundeId   = eingabe.QuellRundeId;
        regel.ZielRundeId    = eingabe.ZielRundeId;
        regel.GruppeIndex    = eingabe.GruppeIndex;
        regel.PlatzierungVon = eingabe.PlatzierungVon;
        regel.PlatzierungBis = eingabe.PlatzierungBis;

        await repository.AktualisierenAsync(regel, ct);
        logger.LogInformation("Übergangsregel {Id} aktualisiert.", id);
    }

    /// <summary>Speichert einen gesamten Übergangsregel-Block (alle Zeilen einer Quellrunde → Zielrunde) in einem
    /// Zug: validiert zuerst den vollständigen beabsichtigten Endzustand, erst danach werden Schreibzugriffe
    /// ausgeführt — kein Halbzustand mehr bei einem Validierungsfehler in der Mitte eines Blocks (Issue #180).</summary>
    public async Task<IReadOnlyList<Uebergangsregel>> BlockSpeichernAsync(
        int quellRundeId, int zielRundeId,
        IReadOnlyList<UebergangsregelZeileEingabe> zeilen,
        IReadOnlyList<int> zuLoeschendeIds,
        CancellationToken ct = default)
    {
        var quellRunde = await QuellRundeLadenAsync(quellRundeId, ct);
        var zielRunde  = await ZielRundeLadenAsync(zielRundeId, ct);
        var alleRegeln = await repository.AlleFindenAsync(ct);

        var eingaben = zeilen
            .Select(z => new UebergangsregelEingabe(quellRundeId, zielRundeId, z.GruppeIndex, z.PlatzierungVon, z.PlatzierungBis))
            .ToList();
        ValidationException.WerfenWennUngueltig(validator.Validieren(eingaben, quellRunde, zielRunde, alleRegeln));

        foreach (var id in zuLoeschendeIds)
        {
            var regel = await repository.FindenNachIdAsync(id, ct);
            if (regel is not null)
                await repository.LoeschenAsync(regel, ct);
        }

        var ergebnis = new List<Uebergangsregel>();
        foreach (var (zeile, eingabe) in zeilen.Zip(eingaben))
        {
            if (zeile.Id is null)
            {
                var regel = new Uebergangsregel
                {
                    QuellRundeId   = quellRundeId,
                    ZielRundeId    = zielRundeId,
                    GruppeIndex    = eingabe.GruppeIndex,
                    PlatzierungVon = eingabe.PlatzierungVon,
                    PlatzierungBis = eingabe.PlatzierungBis
                };
                await repository.HinzufuegenAsync(regel, ct);
                ergebnis.Add(regel);
            }
            else
            {
                var regel = await repository.FindenNachIdAsync(zeile.Id.Value, ct)
                    ?? throw new InvalidOperationException($"Übergangsregel mit ID {zeile.Id} nicht gefunden.");
                regel.GruppeIndex    = eingabe.GruppeIndex;
                regel.PlatzierungVon = eingabe.PlatzierungVon;
                regel.PlatzierungBis = eingabe.PlatzierungBis;
                await repository.AktualisierenAsync(regel, ct);
                ergebnis.Add(regel);
            }
        }

        logger.LogInformation(
            "Übergangsregel-Block '{QuellRunde}' → '{ZielRunde}' gespeichert ({Anzahl} Zeilen, {Geloescht} gelöscht).",
            quellRunde.Name, zielRunde.Name, zeilen.Count, zuLoeschendeIds.Count);
        return ergebnis;
    }

    public async Task LoeschenAsync(int id, CancellationToken ct = default)
    {
        var regel = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Übergangsregel mit ID {id} nicht gefunden.");
        await repository.LoeschenAsync(regel, ct);
        logger.LogInformation("Übergangsregel {Id} gelöscht.", id);
    }

    /// <summary>Löst alle Platzierungen einer Regel auf. Werte sind <c>null</c>, solange die Quellrunde für die
    /// betreffende Platzierung noch nicht (vollständig) entschieden ist.</summary>
    public async Task<IReadOnlyDictionary<int, int?>> QualifikantenErmittelnAsync(int uebergangsregelId, CancellationToken ct = default)
    {
        var regel = await repository.FindenNachIdMitRundenAsync(uebergangsregelId, ct)
            ?? throw new InvalidOperationException($"Übergangsregel mit ID {uebergangsregelId} nicht gefunden.");

        return await spielplanService.UebergangsregelQualifikantenAsync(regel, ct);
    }

    private async Task<TurnierRunde> QuellRundeLadenAsync(int quellRundeId, CancellationToken ct)
        => await rundeRepository.FindenNachIdMitRegelwerkAsync(quellRundeId, ct)
            ?? throw new InvalidOperationException($"Quellrunde mit ID {quellRundeId} nicht gefunden.");

    private async Task<TurnierRunde> ZielRundeLadenAsync(int zielRundeId, CancellationToken ct)
        => await rundeRepository.FindenNachIdAsync(zielRundeId, ct)
            ?? throw new InvalidOperationException($"Zielrunde mit ID {zielRundeId} nicht gefunden.");
}
