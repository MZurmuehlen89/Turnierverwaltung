using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Turniere;

public class TurnierInstanzService(
    ITurnierInstanzRepository repository,
    TurnierValidator validator,
    ILogger<TurnierInstanzService> logger)
{
    public Task<IReadOnlyList<TurnierInstanz>> AlleAbrufenAsync(CancellationToken ct = default)
        => repository.AlleFindenMitVorlageAsync(ct);

    public Task<TurnierInstanz?> FindenNachIdMitRundenAsync(int id, CancellationToken ct = default)
        => repository.FindenNachIdMitRundenAsync(id, ct);

    /// <summary>Legt eine TurnierInstanz mit ihren Runden an. Validiert die Rundenübergänge.</summary>
    public async Task<TurnierInstanz> ErstellenMitRundenAsync(
        TurnierInstanzErstellenEingabe instanzEingabe,
        IReadOnlyList<TurnierRundeEingabe> rundenEingaben,
        CancellationToken ct = default)
    {
        ValidationException.WerfenWennUngueltig(validator.RundenValidieren(rundenEingaben));

        var instanz = new TurnierInstanz
        {
            Name             = instanzEingabe.Name.Trim(),
            SaisonOderJahr   = instanzEingabe.SaisonOderJahr.Trim(),
            TurnierVorlageId = instanzEingabe.TurnierVorlageId,
            Startdatum       = instanzEingabe.Startdatum,
            Enddatum         = instanzEingabe.Enddatum,
            Status           = TurnierStatus.Geplant
        };

        for (int i = 0; i < rundenEingaben.Count; i++)
        {
            var re = rundenEingaben[i];
            instanz.Runden.Add(new TurnierRunde
            {
                Name                       = re.Name.Trim(),
                Reihenfolge                = i + 1,
                Rundenart                  = re.Rundenart,
                EingangsMannschaftsanzahl  = re.EingangsMannschaftsanzahl,
                AusgangsMannschaftsanzahl  = re.AusgangsMannschaftsanzahl,
                RegelwerkId                = re.RegelwerkId,
                AuswaertstorRegelAktiv     = re.AuswaertstorRegelAktiv,
                KonfigurationJson          = re.KonfigurationJson
            });
        }

        await repository.HinzufuegenAsync(instanz, ct);
        logger.LogInformation("TurnierInstanz '{Name}' ({Saison}) mit {Runden} Runde(n) angelegt.",
            instanz.Name, instanz.SaisonOderJahr, instanz.Runden.Count);
        return instanz;
    }

    public async Task StatusAendernAsync(int id, TurnierStatus neuerStatus, CancellationToken ct = default)
    {
        var instanz = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"TurnierInstanz mit ID {id} nicht gefunden.");
        instanz.Status = neuerStatus;
        await repository.AktualisierenAsync(instanz, ct);
    }

    public async Task LoeschenAsync(int id, CancellationToken ct = default)
    {
        if (await repository.FindenNachIdAsync(id, ct) is null)
            throw new InvalidOperationException($"TurnierInstanz mit ID {id} nicht gefunden.");
        await repository.LoeschenMitSpielen(id, ct);
    }

    /// <summary>Prüft ob jede Runde so viele Eingangsteams hat wie die Vorrunde Ausgangsteams liefert.</summary>
    public static ValidationErgebnis RundenUebergaengeValidieren(IReadOnlyList<TurnierRundeEingabe> runden)
    {
        var fehler = new List<string>();

        for (int i = 1; i < runden.Count; i++)
        {
            var vorrunde = runden[i - 1];
            var runde    = runden[i];

            if (runde.EingangsMannschaftsanzahl != vorrunde.AusgangsMannschaftsanzahl)
                fehler.Add($"Runde {i + 1} ('{runde.Name}') erwartet {runde.EingangsMannschaftsanzahl} " +
                           $"Mannschaften, Runde {i} ('{vorrunde.Name}') liefert aber {vorrunde.AusgangsMannschaftsanzahl}.");

            if (runde.AuswaertstorRegelAktiv && runde.Rundenart != Rundenart.KoRundeMitHinUndRueckspiel)
                fehler.Add($"Runde {i + 1} ('{runde.Name}'): Auswärtstorregel ist nur bei Hin-/Rückspiel-Runden gültig.");
        }

        foreach (var runde in runden)
        {
            if (runde.AusgangsMannschaftsanzahl >= runde.EingangsMannschaftsanzahl && runde.Rundenart != Rundenart.Liga)
                fehler.Add($"Runde '{runde.Name}': Ausgangsteams ({runde.AusgangsMannschaftsanzahl}) " +
                           $"dürfen bei K.-o.-Runden nicht ≥ Eingangsteams ({runde.EingangsMannschaftsanzahl}) sein.");
        }

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }
}
