using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>
/// Automatische Generierung ohne Auslosung (Issue #113): hält Zielrunden mit genau zwei Eingangsmannschaften
/// synchron mit dem Stand ihrer(n) Quellrunde(n) — ohne dass für die Zielrunde eine eigene Auslosung durchlaufen
/// werden muss (bei nur zwei möglichen Teilnehmern gibt es ohnehin keinen Freiheitsgrad, wer gegen wen antritt).
/// </summary>
public class FinaleAutomatik(
    ITurnierRundeRepository rundeRepository,
    IUebergangsregelRepository uebergangsregelRepository,
    IAuslosungRepository auslosungRepository,
    ISpielRepository spielRepository,
    PlatzhalterAufloesung aufloesung,
    SpielplanService spielplanService,
    ILogger<FinaleAutomatik> logger)
{
    /// <summary>
    /// Generiert das Spiel, sobald die Quellrunde(n) vollständig entschieden sind, und entfernt/ersetzt ein noch
    /// nicht gespieltes Spiel wieder, falls sich der Quellrunden-Stand nachträglich ändert (Ergebnis zurückgesetzt
    /// oder neu simuliert). Eine bereits gespielte Zielrunde (mit Ergebnis) wird nie automatisch verändert.
    /// Zwei Wege werden unterstützt: (1) explizite <see cref="Uebergangsregel"/>n, die zusammen genau zwei
    /// Qualifikanten für die Zielrunde liefern (Issue #29/#130); (2) als Fallback für Turniere ohne solche Regeln
    /// die ursprüngliche Heuristik aus Issue #113 (nächstgelegene einbeinige K.-o.-Vorrunde mit zwei Ausgangsteams).
    /// Aggregat-Sieger-Ermittlung über zwei Spielbeine ist bewusst nicht Teil dieser Phase (weder für die
    /// Quellrunde noch für die Zielrunde).
    /// Aufrufpunkt: direkt nach dem Speichern/Zurücksetzen/Simulieren eines Ergebnisses (siehe SpielplanViewModel),
    /// da die Zielrunde vor der automatischen Generierung noch keine Spiele hat und der reguläre
    /// "Platzhalter aktualisieren"-Button dafür nicht sichtbar/erreichbar ist.
    /// </summary>
    public async Task AbgleichenAsync(int turnierInstanzId, CancellationToken ct = default)
    {
        using var vorgang = logger.Vorgang($"Finale-Generierung prüfen (Instanz {turnierInstanzId})");

        var alleRunden = await rundeRepository.AlleNachInstanzAsync(turnierInstanzId, ct);
        var alleRegeln = await uebergangsregelRepository.AlleNachTurnierInstanzIdAsync(turnierInstanzId, ct);

        foreach (var ziel in alleRunden.Where(r => r.EingangsMannschaftsanzahl == 2).ToList())
            await ZielrundeAbgleichenAsync(ziel, alleRunden, alleRegeln, ct);
    }

    private async Task ZielrundeAbgleichenAsync(
        TurnierRunde ziel, IReadOnlyList<TurnierRunde> alleRunden, IReadOnlyList<Uebergangsregel> alleRegeln,
        CancellationToken ct)
    {
        // Zielrunden mit eigener Auslosung gehören ausschließlich der breiteren #88-Kaskade
        // (PlatzhalterKaskadeService) — sonst würde diese Heuristik eine gerade frisch aus der Auslosung
        // regenerierte Platzhalter-Paarung sofort wieder entfernen, da sie den Auslosung-Zustand nicht kennt
        // (nur Ergebnis-Vorhandensein prüft).
        if (await auslosungRepository.FindenAbgeschlosseneNachRundeAsync(ziel.TurnierInstanzId, ziel.Id, ct) is not null)
            return;

        var zielSpiele = await spielRepository.AlleNachRundeAsync(ziel.Id, ct);

        // Bereits gespielt oder unerwartete Struktur (z.B. mehr als ein Spiel) - nie automatisch anfassen.
        if (zielSpiele.Count > 1 || zielSpiele.Any(s => s.Ergebnis is not null))
            return;

        var regelnFuerZiel = alleRegeln.Where(r => r.ZielRundeId == ziel.Id).ToList();
        var (sollTeilnehmer, quelleBeschreibung) = regelnFuerZiel.Count > 0
            ? await QualifikantenAusRegelnAsync(regelnFuerZiel, ct)
            : await QualifikantenAusFinaleHeuristikAsync(ziel, alleRunden, ct);

        if (zielSpiele.Count == 0)
        {
            if (sollTeilnehmer is null) return; // Quellrunde(n) noch nicht (vollständig) entschieden
            await spielplanService.GenerierenAsync(ziel.Id, sollTeilnehmer, ct);
            logger.LogInformation(
                "Runde '{Runde}' automatisch ohne Auslosung generiert (Teilnehmer aus {Quelle}).",
                ziel.Name, quelleBeschreibung);
            return;
        }

        // Genau ein noch nicht gespieltes Spiel vorhanden - prüfen ob es noch zum Quellrunden-Stand passt.
        if (BestreitenGenau(zielSpiele[0], sollTeilnehmer)) return;

        await spielplanService.RundenSpielplanOhneKaskadeLoeschenAsync(ziel.Id, ct);
        logger.LogInformation(
            "Automatisch generiertes, noch nicht gespieltes Spiel in Runde '{Runde}' entfernt " +
            "(Quellrunden-Stand hat sich geändert).", ziel.Name);

        if (sollTeilnehmer is null) return;
        await spielplanService.GenerierenAsync(ziel.Id, sollTeilnehmer, ct);
        logger.LogInformation(
            "Runde '{Runde}' automatisch neu generiert (Teilnehmer aus {Quelle}).",
            ziel.Name, quelleBeschreibung);
    }

    private static bool BestreitenGenau(Spiel spiel, IReadOnlyCollection<int>? sollTeilnehmer)
    {
        if (sollTeilnehmer is null) return false;

        var aktuelleTeilnehmer = new[] { spiel.HeimMannschaftId, spiel.AuswaertsMannschaftId }
            .Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
        return aktuelleTeilnehmer.SetEquals(sollTeilnehmer);
    }

    /// <summary>Ermittelt die beiden Qualifikanten für eine Zielrunde aus expliziten <see cref="Uebergangsregel"/>n.
    /// Liefert <c>null</c>, solange nicht alle beteiligten Quellrunden vollständig entschieden sind.</summary>
    private async Task<(List<int>? Teilnehmer, string Beschreibung)> QualifikantenAusRegelnAsync(
        IReadOnlyList<Uebergangsregel> regeln, CancellationToken ct)
    {
        var teilnehmer = new List<int>();
        foreach (var regel in regeln)
        {
            var qualifikanten = await aufloesung.UebergangsregelQualifikantenAsync(regel, ct);
            if (qualifikanten.Values.Any(teamId => teamId is null)) return (null, "");
            teilnehmer.AddRange(qualifikanten.OrderBy(q => q.Key).Select(q => q.Value!.Value));
        }

        var beschreibung = string.Join(", ", regeln.Select(r => $"'{r.QuellRunde.Name}'").Distinct());
        return teilnehmer.Count == 2 ? (teilnehmer, beschreibung) : (null, beschreibung);
    }

    /// <summary>Fallback für Turniere ohne explizite <see cref="Uebergangsregel"/>n: sucht die nächstgelegene
    /// einbeinige K.-o.-Vorrunde mit zwei Ausgangsteams und ermittelt deren beide Qualifikanten (Issue #113) —
    /// bei <paramref name="ziel"/> = <see cref="Rundenart.SpielUmPlatzDrei"/> die Verlierer statt der Sieger
    /// (Issue #199).</summary>
    private async Task<(List<int>? Teilnehmer, string Beschreibung)> QualifikantenAusFinaleHeuristikAsync(
        TurnierRunde ziel, IReadOnlyList<TurnierRunde> alleRunden, CancellationToken ct)
    {
        var vorRunde = alleRunden
            .Where(r => r.Reihenfolge < ziel.Reihenfolge
                     && r.Rundenart == Rundenart.KoRunde
                     && r.AusgangsMannschaftsanzahl == 2
                     && !r.MitRueckspiel)
            .OrderByDescending(r => r.Reihenfolge)
            .FirstOrDefault();

        if (vorRunde is null) return (null, "");

        var vorRundenSpiele = await spielRepository.AlleNachRundeAsync(vorRunde.Id, ct);
        if (vorRundenSpiele.Count == 0 || vorRundenSpiele.Any(s => s.Ergebnis?.SiegerMannschaftId is null))
            return (null, $"'{vorRunde.Name}'"); // noch nicht (vollständig) entschieden

        int platzierung = ziel.Rundenart == Rundenart.SpielUmPlatzDrei ? 2 : 1;
        var qualifikanten = vorRundenSpiele
            .OrderBy(s => s.Spielnummer)
            .Select(s => KoPaarungen.SiegerOderVerliererId(s, platzierung))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        return qualifikanten.Count == 2 ? (qualifikanten, $"'{vorRunde.Name}'") : (null, $"'{vorRunde.Name}'");
    }
}
