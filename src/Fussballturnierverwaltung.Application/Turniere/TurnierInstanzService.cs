using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Turniere;

public class TurnierInstanzService(
    ITurnierInstanzRepository repository,
    ITurnierRundeRepository rundeRepository,
    ISpielRepository spielRepository,
    TurnierValidator validator,
    ILogger<TurnierInstanzService> logger)
{
    public Task<IReadOnlyList<TurnierInstanz>> AlleAbrufenAsync(CancellationToken ct = default)
        => repository.AlleFindenMitVorlageAsync(ct);

    public Task<TurnierInstanz?> FindenNachIdMitRundenAsync(int id, CancellationToken ct = default)
        => repository.FindenNachIdMitRundenAsync(id, ct);

    // Eine Runde gilt als abgeschlossen, wenn ihr Spielplan generiert ist und alle Spiele
    // den Status Abgeschlossen haben.
    public async Task<TurnierFortschritt> FortschrittAbrufenAsync(int turnierInstanzId, CancellationToken ct = default)
    {
        var runden = await rundeRepository.AlleNachInstanzAsync(turnierInstanzId, ct);
        var spiele = await spielRepository.AlleNachInstanzAsync(turnierInstanzId, ct);
        var spieleNachRunde = spiele.ToLookup(s => s.TurnierRundeId);

        int rundenAbgeschlossen = runden.Count(r =>
        {
            var rundenSpiele = spieleNachRunde[r.Id];
            return rundenSpiele.Any() && rundenSpiele.All(s => s.Status == SpielStatus.Abgeschlossen);
        });

        return new TurnierFortschritt(
            RundenAbgeschlossen: rundenAbgeschlossen,
            RundenGesamt: runden.Count,
            SpieleGespielt: spiele.Count(s => s.Status == SpielStatus.Abgeschlossen),
            SpieleGesamt: spiele.Count);
    }

    /// <summary>Legt eine TurnierInstanz ohne Runden an (Runden werden später im Runden-Editor gepflegt).</summary>
    public async Task<TurnierInstanz> ErstellenAsync(
        TurnierInstanzErstellenEingabe instanzEingabe,
        CancellationToken ct = default)
    {
        ValidationException.WerfenWennUngueltig(validator.TurnierInstanzValidieren(new TurnierInstanz
        {
            Name           = instanzEingabe.Name.Trim(),
            SaisonOderJahr = instanzEingabe.SaisonOderJahr.Trim(),
            Startdatum     = instanzEingabe.Startdatum,
            Enddatum       = instanzEingabe.Enddatum
        }));

        var instanz = new TurnierInstanz
        {
            Name                  = instanzEingabe.Name.Trim(),
            SaisonOderJahr        = instanzEingabe.SaisonOderJahr.Trim(),
            TurnierVorlageId      = instanzEingabe.TurnierVorlageId,
            Startdatum            = instanzEingabe.Startdatum,
            Enddatum              = instanzEingabe.Enddatum,
            Status                = TurnierStatus.Geplant,
            VerbandId             = instanzEingabe.VerbandId,
            LandId                = instanzEingabe.LandId,
            MannschaftsartFilter  = instanzEingabe.MannschaftsartFilter,
            StandardRegelwerkId   = instanzEingabe.StandardRegelwerkId
        };

        await repository.HinzufuegenAsync(instanz, ct);
        logger.LogInformation("TurnierInstanz '{Name}' ({Saison}) angelegt.", instanz.Name, instanz.SaisonOderJahr);
        return instanz;
    }

    /// <summary>Legt eine TurnierInstanz mit ihren Runden an. Validiert die Rundenübergänge.</summary>
    public async Task<TurnierInstanz> ErstellenMitRundenAsync(
        TurnierInstanzErstellenEingabe instanzEingabe,
        IReadOnlyList<TurnierRundeEingabe> rundenEingaben,
        CancellationToken ct = default)
    {
        ValidationException.WerfenWennUngueltig(validator.RundenValidieren(rundenEingaben));

        var instanz = new TurnierInstanz
        {
            Name                  = instanzEingabe.Name.Trim(),
            SaisonOderJahr        = instanzEingabe.SaisonOderJahr.Trim(),
            TurnierVorlageId      = instanzEingabe.TurnierVorlageId,
            Startdatum            = instanzEingabe.Startdatum,
            Enddatum              = instanzEingabe.Enddatum,
            Status                = TurnierStatus.Geplant,
            VerbandId             = instanzEingabe.VerbandId,
            LandId                = instanzEingabe.LandId,
            MannschaftsartFilter  = instanzEingabe.MannschaftsartFilter,
            StandardRegelwerkId   = instanzEingabe.StandardRegelwerkId
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
                GruppenAnzahl              = re.GruppenAnzahl,
                RegelwerkId                = re.RegelwerkId,
                MitRueckspiel              = re.MitRueckspiel,
                AuswaertstorRegelAktiv     = re.AuswaertstorRegelAktiv,
                KonfigurationJson          = re.KonfigurationJson,
                EingangUeberUebergangsregeln = re.EingangUeberUebergangsregeln
            });
        }

        await repository.HinzufuegenAsync(instanz, ct);
        logger.LogInformation("TurnierInstanz '{Name}' ({Saison}) mit {Runden} Runde(n) angelegt.",
            instanz.Name, instanz.SaisonOderJahr, instanz.Runden.Count);
        return instanz;
    }

    public async Task VollAktualisierenAsync(int id, TurnierInstanzVollAktualisierenEingabe eingabe, CancellationToken ct = default)
    {
        var instanz = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"TurnierInstanz mit ID {id} nicht gefunden.");
        instanz.Name                 = eingabe.Name.Trim();
        instanz.SaisonOderJahr       = eingabe.SaisonOderJahr.Trim();
        instanz.TurnierVorlageId     = eingabe.TurnierVorlageId;
        instanz.Startdatum           = eingabe.Startdatum;
        instanz.Enddatum             = eingabe.Enddatum;
        instanz.Status               = eingabe.Status;
        instanz.VerbandId            = eingabe.VerbandId;
        instanz.LandId               = eingabe.LandId;
        instanz.MannschaftsartFilter = eingabe.MannschaftsartFilter;
        instanz.StandardRegelwerkId  = eingabe.StandardRegelwerkId;
        await repository.AktualisierenAsync(instanz, ct);
        logger.LogInformation("TurnierInstanz '{Name}' (ID {Id}) aktualisiert.", instanz.Name, id);
    }

    public async Task AktualisierenAsync(int id, TurnierAktualisierenEingabe eingabe, CancellationToken ct = default)
    {
        var instanz = await repository.FindenNachIdAsync(id, ct)
            ?? throw new InvalidOperationException($"TurnierInstanz mit ID {id} nicht gefunden.");
        instanz.Name           = eingabe.Name.Trim();
        instanz.SaisonOderJahr = eingabe.SaisonOderJahr.Trim();
        await repository.AktualisierenAsync(instanz, ct);
        logger.LogInformation("TurnierInstanz '{Name}' (ID {Id}) aktualisiert.", instanz.Name, id);
    }

    public async Task RundeAktualisierenAsync(int rundeId, TurnierRundeAktualisierenEingabe eingabe, CancellationToken ct = default)
    {
        var runde = await rundeRepository.FindenNachIdAsync(rundeId, ct)
            ?? throw new InvalidOperationException($"TurnierRunde mit ID {rundeId} nicht gefunden.");
        runde.Name                   = eingabe.Name.Trim();
        runde.RegelwerkId            = eingabe.RegelwerkId;
        runde.AuswaertstorRegelAktiv = eingabe.AuswaertstorRegelAktiv;
        await rundeRepository.AktualisierenAsync(runde, ct);
        logger.LogInformation("TurnierRunde '{Name}' (ID {Id}) aktualisiert.", runde.Name, rundeId);
    }

    public async Task<IReadOnlyList<TurnierRunde>> RundenErsetzenAsync(int id, IReadOnlyList<TurnierRundeEingabe> eingaben, CancellationToken ct = default)
    {
        ValidationException.WerfenWennUngueltig(validator.RundenValidieren(eingaben));

        var neueRunden = eingaben.Select((e, i) => new TurnierRunde
        {
            TurnierInstanzId          = id,
            Name                      = e.Name.Trim(),
            Reihenfolge               = i + 1,
            Rundenart                 = e.Rundenart,
            EingangsMannschaftsanzahl = e.EingangsMannschaftsanzahl,
            AusgangsMannschaftsanzahl = e.AusgangsMannschaftsanzahl,
            GruppenAnzahl             = e.GruppenAnzahl,
            RegelwerkId               = e.RegelwerkId,
            MitRueckspiel             = e.MitRueckspiel,
            AuswaertstorRegelAktiv    = e.AuswaertstorRegelAktiv,
            KonfigurationJson         = e.KonfigurationJson,
            EingangUeberUebergangsregeln = e.EingangUeberUebergangsregeln,
            PaarungFestVorgegeben     = e.PaarungFestVorgegeben
        }).ToList();

        var ergebnis = await repository.RundenErsetzenAsync(id, neueRunden, ct);
        logger.LogInformation("TurnierInstanz {Id}: {Anzahl} Runde(n) ersetzt.", id, neueRunden.Count);
        return ergebnis;
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
            var runde = runden[i];

            if (runde.AuswaertstorRegelAktiv && !runde.MitRueckspiel)
                fehler.Add($"Runde {i + 1} ('{runde.Name}'): Auswärtstorregel ist nur bei Hin-/Rückspiel-Runden gültig.");

            if (runde.EingangUeberUebergangsregeln)
                continue; // Eingang wird über Uebergangsregeln definiert (separat geprüft, siehe UebergangsregelValidator)

            // K.o.-Geschwisterpaar (Finale + Spiel um Platz 3) speist sich beide aus derselben Vorrunde,
            // siehe TurnierValidator.IstKoGeschwisterPaar (Issue #178).
            int vorrundeIndex = i >= 2 && IstKoGeschwisterPaar(runde.Rundenart, runden[i - 1].Rundenart)
                ? i - 2
                : i - 1;
            var vorrunde = runden[vorrundeIndex];

            if (runde.EingangsMannschaftsanzahl != vorrunde.AusgangsMannschaftsanzahl)
                fehler.Add($"Runde {i + 1} ('{runde.Name}') erwartet {runde.EingangsMannschaftsanzahl} " +
                           $"Mannschaften, Runde {vorrundeIndex + 1} ('{vorrunde.Name}') liefert aber {vorrunde.AusgangsMannschaftsanzahl}.");
        }

        foreach (var runde in runden)
        {
            if (runde.AusgangsMannschaftsanzahl >= runde.EingangsMannschaftsanzahl && runde.Rundenart != Rundenart.Liga)
                fehler.Add($"Runde '{runde.Name}': Ausgangsteams ({runde.AusgangsMannschaftsanzahl}) " +
                           $"dürfen bei K.-o.-Runden nicht ≥ Eingangsteams ({runde.EingangsMannschaftsanzahl}) sein.");
        }

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }

    /// <summary>Finale und Spiel um Platz 3 speisen sich strukturell immer aus derselben Vorrunde
    /// (Sieger- bzw. Verliereranzahl einer K.-o.-Runde sind immer gleich groß) — unabhängig davon,
    /// in welcher Reihenfolge sie in der Rundenliste stehen. Dupliziert TurnierValidator.IstKoGeschwisterPaar,
    /// da diese Methode statisch ist und ohne DI-Instanz aus dem interaktiven Console-Rundenbau aufgerufen wird.</summary>
    private static bool IstKoGeschwisterPaar(Rundenart a, Rundenart b) =>
        (a == Rundenart.Finale && b == Rundenart.SpielUmPlatzDrei) ||
        (a == Rundenart.SpielUmPlatzDrei && b == Rundenart.Finale);
}
