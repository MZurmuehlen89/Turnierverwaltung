using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Turniere;

public class TurnierVorlageService(ITurnierVorlageRepository repository, ILogger<TurnierVorlageService> logger)
{
    public Task<IReadOnlyList<TurnierVorlage>> AlleAbrufenAsync(CancellationToken ct = default)
        => repository.AlleFindenAsync(ct);

    public Task<IReadOnlyList<TurnierVorlage>> NachKategorieAbrufenAsync(VorlagenKategorie kategorie, CancellationToken ct = default)
        => repository.AlleNachKategorieFindenAsync(kategorie, ct);

    public async Task<TurnierVorlage?> FindenNachIdAsync(int id, CancellationToken ct = default)
        => await repository.FindenNachIdAsync(id, ct);

    public async Task<TurnierVorlage> ErstellenAsync(TurnierVorlageErstellenEingabe eingabe, CancellationToken ct = default)
    {
        if (eingabe.ErforderlicheMannschaftsanzahl < 2)
            throw new ArgumentException("Eine Turniervorlage muss mindestens 2 Mannschaften erfordern.");

        var vorlage = new TurnierVorlage
        {
            Name                       = eingabe.Name.Trim(),
            Beschreibung               = eingabe.Beschreibung.Trim(),
            ErforderlicheMannschaftsanzahl = eingabe.ErforderlicheMannschaftsanzahl,
            VorlagenKategorie          = eingabe.VorlagenKategorie,
            IstHistorisch              = eingabe.IstHistorisch,
            GueltigVon                 = eingabe.GueltigVon,
            GueltigBis                 = eingabe.GueltigBis
        };

        await repository.HinzufuegenAsync(vorlage, ct);
        logger.LogInformation("Turniervorlage '{Name}' angelegt (ID {Id}).", vorlage.Name, vorlage.Id);
        return vorlage;
    }

    public async Task<TurnierVorlage> AktualisierenAsync(int id, TurnierVorlageAktualisierenEingabe eingabe, CancellationToken ct = default)
    {
        var vorlage = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Turniervorlage mit ID {id} nicht gefunden.");
        if (eingabe.ErforderlicheMannschaftsanzahl < 2)
            throw new ArgumentException("Eine Turniervorlage muss mindestens 2 Mannschaften erfordern.");
        vorlage.Name                           = eingabe.Name.Trim();
        vorlage.Beschreibung                   = eingabe.Beschreibung.Trim();
        vorlage.ErforderlicheMannschaftsanzahl = eingabe.ErforderlicheMannschaftsanzahl;
        vorlage.VorlagenKategorie              = eingabe.VorlagenKategorie;
        vorlage.IstHistorisch                  = eingabe.IstHistorisch;
        vorlage.GueltigVon                     = eingabe.GueltigVon;
        vorlage.GueltigBis                     = eingabe.GueltigBis;
        await repository.AktualisierenAsync(vorlage, ct);
        logger.LogInformation("Turniervorlage '{Name}' (ID {Id}) aktualisiert.", vorlage.Name, vorlage.Id);
        return vorlage;
    }

    public async Task LoeschenAsync(int id, CancellationToken ct = default)
    {
        var vorlage = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Turniervorlage mit ID {id} nicht gefunden.");
        await repository.LoeschenAsync(vorlage, ct);
        logger.LogInformation("Turniervorlage '{Name}' (ID {Id}) gelöscht.", vorlage.Name, vorlage.Id);
    }
}
