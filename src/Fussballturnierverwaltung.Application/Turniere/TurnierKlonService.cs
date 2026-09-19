using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Turniere;

/// <summary>Klont eine TurnierInstanz strukturell (Issue #294, löst #46 "Turnier klonen") — seit der
/// Vorlage/Instanz-Vereinheitlichung ist "Instanz aus Vorlage erzeugen" derselbe Vorgang wie "Turnier für
/// eine neue Saison duplizieren": beides ist ein reiner Kopiervorgang ohne persistente Rückreferenz auf
/// die Quelle. Kopiert Kopf-Felder, Runden, Übergangsregeln sowie Verbands- und Landesquoten — NIE Spiele, Auslosungen
/// oder SimulationsLaeufe, auch nicht von einer bereits gespielten Quelle (eine Vorlage ist ein
/// Musterturnier ohne Teams/Ergebnisse, ein Klon startet immer bei null).</summary>
public class TurnierKlonService(
    ITurnierInstanzRepository repository,
    ILogger<TurnierKlonService> logger)
{
    public async Task<TurnierInstanz> KlonenAsync(
        int quellId, string neuerName, string neueSaisonOderJahr, TurnierStatus zielStatus,
        CancellationToken ct = default)
    {
        var quelle = await repository.FindenNachIdMitVollstaendigenDatenAsync(quellId, ct)
            ?? throw new InvalidOperationException($"TurnierInstanz mit ID {quellId} nicht gefunden.");

        var neu = new TurnierInstanz
        {
            Name                  = neuerName.Trim(),
            SaisonOderJahr        = neueSaisonOderJahr.Trim(),
            Status                = zielStatus,
            Beschreibung          = quelle.Beschreibung,
            Kategorie             = quelle.Kategorie,
            ZielMannschaftsanzahl = quelle.ZielMannschaftsanzahl,
            Logo                  = quelle.Logo,
            VerbandId             = quelle.VerbandId,
            LandId                = quelle.LandId,
            MannschaftsartFilter  = quelle.MannschaftsartFilter,
            StandardRegelwerkId   = quelle.StandardRegelwerkId
        };

        var rundenNachQuellId = new Dictionary<int, TurnierRunde>();
        foreach (var runde in quelle.Runden.OrderBy(r => r.Reihenfolge))
        {
            var neueRunde = new TurnierRunde
            {
                Name                      = runde.Name,
                Reihenfolge               = runde.Reihenfolge,
                Rundenart                 = runde.Rundenart,
                EingangsMannschaftsanzahl = runde.EingangsMannschaftsanzahl,
                AusgangsMannschaftsanzahl = runde.AusgangsMannschaftsanzahl,
                GruppenAnzahl             = runde.GruppenAnzahl,
                RegelwerkId               = runde.RegelwerkId,
                MitRueckspiel             = runde.MitRueckspiel,
                AuswaertstorRegelAktiv    = runde.AuswaertstorRegelAktiv,
                KonfigurationJson         = runde.KonfigurationJson,
                EingangUeberUebergangsregeln = runde.EingangUeberUebergangsregeln,
                PaarungFestVorgegeben     = runde.PaarungFestVorgegeben
            };
            neu.Runden.Add(neueRunde);
            rundenNachQuellId[runde.Id] = neueRunde;
        }

        foreach (var runde in quelle.Runden)
        foreach (var regel in runde.Uebergangsregeln)
        {
            rundenNachQuellId[regel.QuellRundeId].Uebergangsregeln.Add(new Uebergangsregel
            {
                QuellRunde     = rundenNachQuellId[regel.QuellRundeId],
                ZielRunde      = rundenNachQuellId[regel.ZielRundeId],
                GruppeIndex    = regel.GruppeIndex,
                PlatzierungVon = regel.PlatzierungVon,
                PlatzierungBis = regel.PlatzierungBis,
                AnzahlBeste    = regel.AnzahlBeste
            });
        }

        foreach (var quote in quelle.VerbandsQuoten)
            neu.VerbandsQuoten.Add(new VerbandsQuote { VerbandId = quote.VerbandId, Anzahl = quote.Anzahl });

        foreach (var quote in quelle.LandesQuoten)
            neu.LandesQuoten.Add(new LandesQuote { LandId = quote.LandId, Anzahl = quote.Anzahl });

        await repository.HinzufuegenAsync(neu, ct);
        logger.LogInformation("TurnierInstanz {QuellId} als '{Name}' geklont (neue ID {NeueId}).", quellId, neu.Name, neu.Id);
        return neu;
    }
}
