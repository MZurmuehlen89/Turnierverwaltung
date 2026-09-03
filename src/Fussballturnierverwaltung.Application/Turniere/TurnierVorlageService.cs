using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Turniere;

public class TurnierVorlageService(
    ITurnierVorlageRepository repository,
    VorlagenUebergangsregelValidator uebergangsregelValidator,
    VorlagenPaarungValidator paarungValidator,
    TurnierVorlageValidator vorlageValidator,
    ILogger<TurnierVorlageService> logger)
{
    public Task<IReadOnlyList<TurnierVorlage>> AlleAbrufenAsync(CancellationToken ct = default)
        => repository.AlleFindenAsync(ct);

    public Task<IReadOnlyList<TurnierVorlage>> NachKategorieAbrufenAsync(VorlagenKategorie kategorie, CancellationToken ct = default)
        => repository.AlleNachKategorieFindenAsync(kategorie, ct);

    public async Task<TurnierVorlage?> FindenNachIdAsync(int id, CancellationToken ct = default)
        => await repository.FindenNachIdAsync(id, ct);

    public Task<TurnierVorlage?> FindenNachIdMitRundenAsync(int id, CancellationToken ct = default)
        => repository.FindenNachIdMitRundenAsync(id, ct);

    public async Task RundenSynchronisierenAsync(
        int vorlageId,
        IReadOnlyList<VorlagenRundeEingabe> runden,
        IReadOnlyList<VorlagenUebergangsRegelEingabe>? uebergangsregeln = null,
        IReadOnlyList<VorlagenPaarungEingabe>? paarungen = null,
        CancellationToken ct = default)
    {
        var neueRunden = runden.Select((r, i) => new VorlagenRunde
        {
            TurnierVorlageId          = vorlageId,
            Name                      = r.Name.Trim(),
            Reihenfolge               = i + 1,
            Rundenart                 = r.Rundenart,
            EingangsMannschaftsanzahl = r.EingangsMannschaftsanzahl,
            AusgangsMannschaftsanzahl = r.AusgangsMannschaftsanzahl,
            GruppenAnzahl             = r.GruppenAnzahl,
            MitRueckspiel             = r.MitRueckspiel,
            AuswaertstorRegelAktiv    = r.AuswaertstorRegelAktiv,
            RegelwerkId               = r.RegelwerkId,
            PaarungFestVorgegeben     = r.PaarungFestVorgegeben
        }).ToList();

        var regeln = uebergangsregeln ?? [];
        var paarungsListe = paarungen ?? [];
        ValidationException.WerfenWennUngueltig(vorlageValidator.RundenValidieren(runden));
        ValidationException.WerfenWennUngueltig(uebergangsregelValidator.Validieren(runden, regeln));
        ValidationException.WerfenWennUngueltig(paarungValidator.Validieren(runden, regeln, paarungsListe));

        await repository.RundenErsetzenAsync(vorlageId, neueRunden, regeln, paarungsListe, ct);
        logger.LogInformation(
            "Vorlage {Id}: {Anzahl} Runde(n), {AnzahlRegeln} Übergangsregel(n), {AnzahlPaarungen} Paarung(en) synchronisiert.",
            vorlageId, neueRunden.Count, regeln.Count, paarungsListe.Count);
    }

    public async Task<TurnierVorlage> ErstellenAsync(TurnierVorlageErstellenEingabe eingabe, CancellationToken ct = default)
    {
        ValidationException.WerfenWennUngueltig(vorlageValidator.BasisdatenValidieren(
            eingabe.Name, eingabe.ErforderlicheMannschaftsanzahl, eingabe.GueltigVon, eingabe.GueltigBis, eingabe.Logo));

        var vorlage = new TurnierVorlage
        {
            Name                       = eingabe.Name.Trim(),
            Beschreibung               = eingabe.Beschreibung.Trim(),
            ErforderlicheMannschaftsanzahl = eingabe.ErforderlicheMannschaftsanzahl,
            VorlagenKategorie          = eingabe.VorlagenKategorie,
            IstHistorisch              = eingabe.IstHistorisch,
            GueltigVon                 = eingabe.GueltigVon,
            GueltigBis                 = eingabe.GueltigBis,
            Logo                       = eingabe.Logo
        };

        await repository.HinzufuegenAsync(vorlage, ct);
        logger.LogInformation("Turniervorlage '{Name}' angelegt (ID {Id}).", vorlage.Name, vorlage.Id);
        return vorlage;
    }

    public async Task<TurnierVorlage> AktualisierenAsync(int id, TurnierVorlageAktualisierenEingabe eingabe, CancellationToken ct = default)
    {
        var vorlage = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Turniervorlage mit ID {id} nicht gefunden.");
        ValidationException.WerfenWennUngueltig(vorlageValidator.BasisdatenValidieren(
            eingabe.Name, eingabe.ErforderlicheMannschaftsanzahl, eingabe.GueltigVon, eingabe.GueltigBis, eingabe.Logo));
        vorlage.Name                           = eingabe.Name.Trim();
        vorlage.Beschreibung                   = eingabe.Beschreibung.Trim();
        vorlage.ErforderlicheMannschaftsanzahl = eingabe.ErforderlicheMannschaftsanzahl;
        vorlage.VorlagenKategorie              = eingabe.VorlagenKategorie;
        vorlage.IstHistorisch                  = eingabe.IstHistorisch;
        vorlage.GueltigVon                     = eingabe.GueltigVon;
        vorlage.GueltigBis                     = eingabe.GueltigBis;
        vorlage.Logo                           = eingabe.Logo;
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
