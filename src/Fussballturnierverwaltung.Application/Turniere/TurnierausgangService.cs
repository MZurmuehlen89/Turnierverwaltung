using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Tabelle;

namespace Fussballturnierverwaltung.Application.Turniere;

/// <summary>Lädt, was <see cref="TurnierausgangBerechnung"/> für den Ausgang eines Turniers braucht (#527).</summary>
public class TurnierausgangService(
    ITurnierRundeRepository rundeRepository,
    ISpielRepository spielRepository,
    TabellenService tabellenService)
{
    public async Task<Turnierausgang> ErmittelnAsync(int turnierInstanzId, CancellationToken ct = default)
    {
        var runden = await rundeRepository.AlleNachInstanzAsync(turnierInstanzId, ct);
        if (TurnierausgangBerechnung.Schlussrunde(runden) is not { } schlussrunde) return Turnierausgang.Keiner;

        var spiele = await spielRepository.AlleNachInstanzAsync(turnierInstanzId, ct);
        if (!TurnierausgangBerechnung.EndetMitTabelle(schlussrunde))
            return TurnierausgangBerechnung.AusKoRunden(runden, spiele);

        var tabelle = await tabellenService.LigaTabelleMitSicherheitBerechnenAsync(schlussrunde.Id, ct);
        return TurnierausgangBerechnung.AusTabelle(
            schlussrunde, tabelle, [.. spiele.Where(s => s.TurnierRundeId == schlussrunde.Id)]);
    }
}
