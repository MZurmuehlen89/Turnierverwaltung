using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Lädt, woraus der Teilnehmerpool einer Auslosung entsteht (#576): Quoten samt Ranglisten, die zugelassenen
/// Mannschaften, die Rangliste der Automatik und die Teams vorab für Topf 1. Den Pool selbst schlägt
/// <see cref="Teilnehmerpool"/> zustandslos vor. Läuft unter dem DbContext-Schutz des Aufrufers.</summary>
public class TeilnehmerpoolVorbelegung(
    TurnierInstanzService instanzService,
    KontingentvorgabeService kontingentvorgabeService,
    RanglisteService ranglisteService)
{
    /// <summary><c>null</c>, wenn die Runde keinen Pool hat — nur die erste Runde eines Turniers mit Quoten hat einen.
    /// <paramref name="teilnehmer"/> sind alle für die Runde zugelassenen Teams, auch die schon eingelegten.</summary>
    public async Task<Teilnehmerpool?> LadenAsync(Auslosung detail, IReadOnlyList<TeilnehmerAnzeige> teilnehmer, int? ranglisteId)
    {
        var instanz = await instanzService.FindenNachIdMitRundenAsync(detail.TurnierInstanzId);
        if (instanz is null) return null;
        var quoten = await kontingentvorgabeService.LadenAsync(instanz, detail.TurnierRundeId);
        if (quoten.Leer) return null;

        return new Teilnehmerpool(
            quoten,
            Kandidaten(teilnehmer),
            detail.TurnierRunde?.EingangsMannschaftsanzahl ?? 0,
            await RangAsync(ranglisteId),
            LostopfBefuellung.VorabInTopf1(detail, instanz));
    }

    /// <summary>Platz je Mannschaft in der Rangliste der Automatik; ohne Rangliste leer.</summary>
    public async Task<IReadOnlyDictionary<int, int>> RangAsync(int? ranglisteId)
    {
        if (ranglisteId is not int id || await ranglisteService.FindenNachIdAsync(id) is not { } rangliste)
            return new Dictionary<int, int>();
        return rangliste.Eintraege
            .GroupBy(e => e.MannschaftId)
            .ToDictionary(g => g.Key, g => g.Min(e => e.Rang));
    }

    /// <summary>Nur echte Mannschaften mit Land — eine erste Runde hat keine Platzhalter, und ohne Land zählt ein Team auf
    /// keine Quote.</summary>
    private static IReadOnlyList<AuswahlKandidat> Kandidaten(IReadOnlyList<TeilnehmerAnzeige> teilnehmer) =>
        [.. teilnehmer
            .Where(t => t.MannschaftId.HasValue && t.LandId.HasValue)
            .Select(t => new AuswahlKandidat(t.MannschaftId!.Value, t.AnzeigeName, t.LandId!.Value, t.VerbandId))];
}
