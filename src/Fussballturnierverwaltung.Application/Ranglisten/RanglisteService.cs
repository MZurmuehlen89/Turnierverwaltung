using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Ranglisten;

public class RanglisteService(
    IRanglisteRepository repository,
    RanglisteValidator validator,
    ILogger<RanglisteService> logger)
{
    public Task<IReadOnlyList<Rangliste>> AlleAbrufenAsync(CancellationToken ct = default)
        => repository.AlleFindenMitEintraegenAsync(ct);

    /// <summary>Alle Ranglisten ohne Einträge — für Auswahllisten (Standard-Rangliste einer Runde, #393).</summary>
    public Task<IReadOnlyList<Rangliste>> AlleOhneEintraegeAbrufenAsync(CancellationToken ct = default)
        => repository.AlleFindenAsync(ct);

    public Task<Rangliste?> FindenNachIdAsync(int id, CancellationToken ct = default)
        => repository.FindenNachIdMitEintraegenAsync(id, ct);

    public async Task<Rangliste> ErstellenAsync(RanglisteErstellenEingabe eingabe, CancellationToken ct = default)
    {
        ValidationException.WerfenWennUngueltig(validator.Validieren(eingabe));

        var rangliste = new Rangliste
        {
            Name   = eingabe.Name.Trim(),
            Datum  = eingabe.Datum,
            Quelle = eingabe.Quelle.Trim(),
            RanglistenQuelleId = eingabe.RanglistenQuelleId
        };

        foreach (var e in eingabe.Eintraege)
        {
            rangliste.Eintraege.Add(new RanglistenEintrag
            {
                MannschaftId = e.MannschaftId,
                Rang         = e.Rang,
                Punkte       = e.Punkte
            });
        }

        await repository.HinzufuegenAsync(rangliste, ct);
        logger.LogInformation("Rangliste '{Name}' angelegt (ID {Id}).", rangliste.Name, rangliste.Id);
        return rangliste;
    }

    public async Task AktualisierenAsync(int id, RanglisteAktualisierenEingabe eingabe, CancellationToken ct = default)
    {
        var rangliste = await repository.FindenNachIdMitEintraegenAsync(id, ct)
            ?? throw new InvalidOperationException($"Rangliste mit ID {id} nicht gefunden.");

        ValidationException.WerfenWennUngueltig(validator.Validieren(eingabe));

        rangliste.Name   = eingabe.Name.Trim();
        rangliste.Datum  = eingabe.Datum;
        rangliste.Quelle = eingabe.Quelle.Trim();
        // Nur ein Import (mit Quelle) setzt die Verknüpfung neu; manuelles Bearbeiten lässt sie stehen (#349).
        if (eingabe.RanglistenQuelleId is int quelleId)
            rangliste.RanglistenQuelleId = quelleId;

        rangliste.Eintraege.Clear();
        foreach (var e in eingabe.Eintraege)
        {
            rangliste.Eintraege.Add(new RanglistenEintrag
            {
                MannschaftId = e.MannschaftId,
                Rang         = e.Rang,
                Punkte       = e.Punkte
            });
        }

        await repository.SpeichernAenderungenAsync(ct);
        logger.LogInformation("Rangliste '{Name}' (ID {Id}) aktualisiert.", rangliste.Name, rangliste.Id);
    }

    public async Task LoeschenAsync(int id, CancellationToken ct = default)
    {
        var rangliste = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Rangliste mit ID {id} nicht gefunden.");
        await repository.LoeschenAsync(rangliste, ct);
        logger.LogInformation("Rangliste '{Name}' (ID {Id}) gelöscht.", rangliste.Name, rangliste.Id);
    }
}
