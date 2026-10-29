using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Turniere;

/// <summary>Klont eine TurnierInstanz strukturell (Issue #294, löst #46 "Turnier klonen") — seit der
/// Vorlage/Instanz-Vereinheitlichung ist "Instanz aus Vorlage erzeugen" derselbe Vorgang wie "Turnier für
/// eine neue Saison duplizieren": beides ist ein reiner Kopiervorgang ohne persistente Rückreferenz auf
/// die Quelle. Kopiert Kopf-Felder, Runden mit Tabellenbereichen (Issue #420), Übergangsregeln, Verbands- und Landesquoten sowie fest vorgegebene
/// Platzhalter-Paarungen (Issue #372) — NIE Spiele, echte Auslosungen oder SimulationsLaeufe, auch nicht von einer bereits gespielten Quelle (eine Vorlage ist ein
/// Musterturnier ohne Teams/Ergebnisse, ein Klon startet immer bei null). Titelverteidiger und Gastgeber (Issue #352) sind saisonspezifisch und
/// werden ebenfalls nicht kopiert.</summary>
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
        if (zielStatus == TurnierStatus.Vorlage)
        {
            var vorlagen = await repository.AlleNachStatusFindenAsync(TurnierStatus.Vorlage, ct);
            ValidationException.WerfenWennUngueltig(
                TurnierValidator.VorlagenEindeutigkeitValidieren(neuerName, quelle.Variante, null, vorlagen));
        }

        var neu = new TurnierInstanz
        {
            Name                  = neuerName.Trim(),
            SaisonOderJahr        = neueSaisonOderJahr.Trim(),
            Status                = zielStatus,
            Beschreibung          = quelle.Beschreibung,
            Variante              = quelle.Variante,
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
            var neueRunde = RundeKopieren(runde);
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

        QuotenKopieren(quelle, neu);

        foreach (var auslosung in quelle.Auslosungen.Where(IstFesteVorbelegung))
            neu.Auslosungen.Add(FesteVorbelegungKopieren(auslosung, neu, rundenNachQuellId[auslosung.TurnierRundeId]));

        await repository.HinzufuegenAsync(neu, ct);
        logger.LogInformation("TurnierInstanz {QuellId} als '{Name}' geklont (neue ID {NeueId}).", quellId, neu.Name, neu.Id);
        return neu;
    }

    private static TurnierRunde RundeKopieren(TurnierRunde runde) =>
        new()
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
            NeutralerSpielort         = runde.NeutralerSpielort,
            Ueberraschungsfaktor      = runde.Ueberraschungsfaktor,
            KonfigurationJson         = runde.KonfigurationJson,
            EingangUeberUebergangsregeln = runde.EingangUeberUebergangsregeln,
            PaarungFestVorgegeben     = runde.PaarungFestVorgegeben,
            VorgegebeneAuslosungsart  = runde.VorgegebeneAuslosungsart,
            VorgegebeneAuslosungsregeln = [.. runde.VorgegebeneAuslosungsregeln],
            VorgegebeneGruppenposition = runde.VorgegebeneGruppenposition,
            GastgeberGruppen = [.. runde.GastgeberGruppen],
            TitelverteidigerGruppe = runde.TitelverteidigerGruppe,
            KombinationstabelleJson   = runde.KombinationstabelleJson,
            StandardRanglisteId = runde.StandardRanglisteId,
            TitelverteidigerInTopf1   = runde.TitelverteidigerInTopf1,
            GegnerJeTopf              = runde.GegnerJeTopf,
            TabellenBereiche          = [.. runde.TabellenBereiche.Select(b => new TabellenBereich
            {
                PlatzVon = b.PlatzVon, PlatzBis = b.PlatzBis, Art = b.Art, Bezeichnung = b.Bezeichnung
            })]
        };

    private static void QuotenKopieren(TurnierInstanz quelle, TurnierInstanz neu)
    {
        foreach (var quote in quelle.VerbandsQuoten)
            neu.VerbandsQuoten.Add(new VerbandsQuote { VerbandId = quote.VerbandId, Anzahl = quote.Anzahl });

        foreach (var quote in quelle.LandesQuoten)
            neu.LandesQuoten.Add(new LandesQuote
            {
                LandId = quote.LandId, Anzahl = quote.Anzahl, Spielklasse = quote.Spielklasse, AbPlatz = quote.AbPlatz
            });
    }

    // ── Feste Vorbelegung ──────────────────────────────────────────────────────

    // Issue #372: die fest vorgegebenen Paarungen einer Runde (PaarungFestVorgegeben + abgeschlossene
    // Manuell-Auslosung, z.B. "1. Gruppe B – 3. Gruppe A/D/E/F") gehören zur Turnierstruktur, nicht zu
    // einem konkreten Turnierverlauf — ohne sie kam ein aus einer Vorlage erzeugtes Turnier mit gesetztem
    // Flag, aber ohne Paarungen an. Nur reine Platzhalter-Vorbelegungen: echte Mannschaften sind
    // saisonspezifisch und werden wie Spiele nie mitgeklont.

    private static bool IstFesteVorbelegung(Auslosung auslosung) =>
        auslosung.Auslosungsart == Auslosungsart.Manuell
        && auslosung.Status == AuslosungsStatus.Abgeschlossen
        && auslosung.TurnierRunde.PaarungFestVorgegeben
        && auslosung.Ergebnisse.Count > 0
        && auslosung.Ergebnisse.All(e => e.HeimMannschaftId is null && e.AuswaertsMannschaftId is null);

    // Bleibt abgeschlossen: die Spiele erzeugt die Platzhalter-Kaskade (leere Runde mit abgeschlossener
    // Auslosung → neu generieren), sobald im neuen Turnier die erste Auslosung ausgeführt wird.
    private static Auslosung FesteVorbelegungKopieren(Auslosung quelle, TurnierInstanz ziel, TurnierRunde zielRunde)
    {
        var kopie = new Auslosung
        {
            TurnierInstanz = ziel,
            TurnierRunde   = zielRunde,
            Name           = quelle.Name,
            Auslosungsart  = quelle.Auslosungsart,
            Gruppenposition = quelle.Gruppenposition,
            Status         = quelle.Status,
            IstAutomatisch = quelle.IstAutomatisch
        };
        foreach (var e in quelle.Ergebnisse.OrderBy(e => e.PaarungsNummer).ThenBy(e => e.Id))
        {
            kopie.Ergebnisse.Add(new AuslosungsErgebnis
            {
                PaarungsNummer           = e.PaarungsNummer,
                HeimPlatzhalterName      = e.HeimPlatzhalterName,
                AuswaertsPlatzhalterName = e.AuswaertsPlatzhalterName,
                IstFreilos               = e.IstFreilos
            });
        }
        return kopie;
    }
}
