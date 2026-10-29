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
    /// <summary>Landesquoten zählen nur bei Vereinswettbewerben, wie in der Quotenanzeige der Auslosung (#189).</summary>
    public async Task<Kontingentvorgabe> LadenAsync(TurnierInstanz instanz, CancellationToken ct = default)
    {
        var landesquoten = instanz.MannschaftsartFilter == MannschaftsartFilter.Verein
            ? await landesQuoteRepository.AlleNachTurnierInstanzIdAsync(instanz.Id, ct)
            : [];
        var verbandsquoten = await verbandsQuoteRepository.AlleNachTurnierInstanzIdAsync(instanz.Id, ct);

        var ranglisten = await RanglistenAsync(
            landesquoten.Select(q => q.Land?.RanglisteId).Concat(verbandsquoten.Select(q => q.Verband?.RanglisteId)), ct);

        return new Kontingentvorgabe(
            [.. landesquoten.Select(q => new LandesKontingent(
                q.LandId, q.Land?.Name ?? $"Land {q.LandId}", q.Anzahl, q.Spielklasse, q.AbPlatz,
                ranglisten.GetValueOrDefault(q.Land?.RanglisteId ?? 0)))],
            [.. verbandsquoten.Select(q => new VerbandsKontingent(
                q.VerbandId, q.Verband?.Name ?? $"Verband {q.VerbandId}", q.Anzahl,
                ranglisten.GetValueOrDefault(q.Verband?.RanglisteId ?? 0)))]);
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
