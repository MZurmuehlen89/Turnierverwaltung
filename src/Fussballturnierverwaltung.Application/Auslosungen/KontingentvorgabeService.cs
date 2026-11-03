using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Lädt die Quoten eines Turniers samt den Ranglisten ihrer Länder und Verbände (Einträge und Abschnitte) —
/// die Eingabe der <see cref="Teilnehmerauswahl"/> (#573).</summary>
public class KontingentvorgabeService(
    ILandesQuoteRepository landesQuoteRepository,
    IVerbandsQuoteRepository verbandsQuoteRepository,
    IRanglisteRepository ranglisteRepository)
{
    /// <summary>Die Quoten für die Runde <paramref name="turnierRundeId"/> — leer außer in der ersten Runde
    /// (<see cref="QuotenBelegung.GeltenFuer"/>, Runden von <paramref name="instanz"/> mitgeladen). Landesquoten
    /// zählen nur bei Vereinswettbewerben, wie in der Quotenanzeige der Auslosung (#189). Eine Verbandsquote ordnet bei
    /// Vereinswettbewerben die Vereinsrangliste des Verbands, sonst seine Rangliste (#574) — ohne Rückgriff, denn die
    /// Rangliste eines Verbands enthält nur Nationalmannschaften.</summary>
    public async Task<Kontingentvorgabe> LadenAsync(TurnierInstanz instanz, int turnierRundeId, CancellationToken ct = default)
    {
        if (!QuotenBelegung.GeltenFuer(turnierRundeId, instanz.Runden))
            return new Kontingentvorgabe([], []);

        bool vereinswettbewerb = instanz.MannschaftsartFilter == MannschaftsartFilter.Verein;
        var landesquoten = vereinswettbewerb
            ? await landesQuoteRepository.AlleNachTurnierInstanzIdAsync(instanz.Id, ct)
            : [];
        var verbandsquoten = await verbandsQuoteRepository.AlleNachTurnierInstanzIdAsync(instanz.Id, ct);
        int? VerbandsRanglisteId(VerbandsQuote q) => vereinswettbewerb ? q.Verband?.VereinsRanglisteId : q.Verband?.RanglisteId;

        var ranglisten = await RanglistenAsync(
            landesquoten.Select(q => q.Land?.RanglisteId).Concat(verbandsquoten.Select(VerbandsRanglisteId)), ct);

        return new Kontingentvorgabe(
            [.. landesquoten.Select(q => new LandesKontingent(
                q.LandId, q.Land?.Name ?? $"Land {q.LandId}", q.Anzahl, q.Spielklasse, q.AbPlatz,
                ranglisten.GetValueOrDefault(q.Land?.RanglisteId ?? 0), q.Pokalsieger))],
            [.. verbandsquoten.Select(q => new VerbandsKontingent(
                q.VerbandId, q.Verband?.Name ?? $"Verband {q.VerbandId}", q.Anzahl,
                ranglisten.GetValueOrDefault(VerbandsRanglisteId(q) ?? 0)))]);
    }

    /// <summary>Jede Rangliste einmal, auch wenn mehrere Verbände auf dieselbe verweisen (FIFA-Weltrangliste).</summary>
    private async Task<Dictionary<int, Rangliste>> RanglistenAsync(IEnumerable<int?> ranglisteIds, CancellationToken ct)
    {
        var ranglisten = new Dictionary<int, Rangliste>();
        foreach (var id in ranglisteIds.OfType<int>().Distinct())
            if (await ranglisteRepository.MitAbschnittenAsync(id, ct) is { } rangliste)
                ranglisten[id] = rangliste;
        return ranglisten;
    }
}
