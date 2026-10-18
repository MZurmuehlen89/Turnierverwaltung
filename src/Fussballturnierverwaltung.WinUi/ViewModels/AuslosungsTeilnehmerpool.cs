using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Woher die Teilnehmer einer Auslosung in der Turnierdurchführung kommen: Platzhalter aus den Vorrunden
/// oder, für eine Runde ohne Vorgänger, die Mannschaften nach dem Turnierfilter. Gegenstück zum
/// <see cref="VorbelegungsTeilnehmerpool"/>; eine Instanz je <see cref="AuslosungViewModel"/>.</summary>
public class AuslosungsTeilnehmerpool(
    MannschaftService mannschaftService,
    VerbandService verbandService,
    UebergangsregelService uebergangsregelService)
{
    private IReadOnlyDictionary<int, string>? _verbandKurznamen;

    /// <summary>Turnierwechsel: die je Turnier gemerkten Verbandsnamen verwerfen.</summary>
    public void Zuruecksetzen() => _verbandKurznamen = null;

    /// <summary>Alle Teilnehmer, die für die Runde der Auslosung in Frage kommen — Platzhalter aus den
    /// eingehenden Übergangsregeln, sonst aus der Vorgängerrunde, in der ersten Runde die Mannschaften nach
    /// dem Turnierfilter. Bereits einem Lostopf zugeordnete sind noch enthalten.</summary>
    public async Task<IReadOnlyList<TeilnehmerAnzeige>> ErmittelnAsync(
        Auslosung detail, TurnierInstanz? instanz, IReadOnlyList<TurnierRunde> instanzRunden)
    {
        var aktuelleRunde = instanzRunden.FirstOrDefault(r => r.Id == detail.TurnierRundeId);
        if (aktuelleRunde is null) return await MannschaftenNachTurnierfilterAsync(instanz);

        var eingehendeRegeln = await uebergangsregelService.AlleNachZielRundeIdAsync(aktuelleRunde.Id);
        return TeilnehmerPoolHilfsmittel.PlatzhalterAusVorrunden(instanzRunden, aktuelleRunde, eingehendeRegeln)
               ?? await MannschaftenNachTurnierfilterAsync(instanz);
    }

    private async Task<IReadOnlyList<TeilnehmerAnzeige>> MannschaftenNachTurnierfilterAsync(TurnierInstanz? instanz)
    {
        var teams = await mannschaftService.AlleNachTurnierfilterAsync(
            instanz?.VerbandId,
            instanz?.LandId,
            instanz?.MannschaftsartFilter ?? MannschaftsartFilter.Alle);
        var verbandKurznamen = await VerbandKurznamenAsync();
        return teams
            .Select(TeilnehmerAnzeige.AusMannschaft)
            .Select(t => t with { VerbandName = verbandKurznamen.GetValueOrDefault(t.VerbandId ?? 0) })
            .ToList();
    }

    /// <summary>Kurznamen der Verbände für den Verbandsfilter der verfügbaren Teams (#421). Je Turnier einmal
    /// geladen: der Teilnehmer-Pool wird nach jeder Topf-Änderung neu ermittelt, und die Mannschaftsabfrage lädt
    /// den Verband bewusst nicht mit (sein Logo käme sonst in jeder Mannschaftszeile mit).</summary>
    private async Task<IReadOnlyDictionary<int, string>> VerbandKurznamenAsync() =>
        _verbandKurznamen ??= (await verbandService.AlleAbrufenAsync()).ToDictionary(v => v.Id, v => v.Kurzname);
}
