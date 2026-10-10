using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Woher die Teilnehmer einer Runde in der Vorbelegung kommen: Platzhalter aus den Vorrunden oder, für eine
/// Runde ohne Vorgänger, die Teilnehmer des Turniers. Eine Instanz je Turnier, erzeugt vom
/// <see cref="VorbelegungViewModel"/> und an jeden <see cref="VorbelegungWorkspace"/> weitergereicht.</summary>
public class VorbelegungsTeilnehmerpool
{
    private readonly MannschaftService _mannschaftService;
    private readonly UebergangsregelService _uebergangsregelService;
    private readonly TurnierInstanz _instanz;
    private readonly IReadOnlyList<TurnierRunde> _instanzRunden;

    public VorbelegungsTeilnehmerpool(
        MannschaftService mannschaftService,
        UebergangsregelService uebergangsregelService,
        TurnierInstanz instanz,
        IReadOnlyList<TurnierRunde> instanzRunden)
    {
        _mannschaftService = mannschaftService;
        _uebergangsregelService = uebergangsregelService;
        _instanz = instanz;
        _instanzRunden = instanzRunden;
    }

    /// <summary>Die Platzhalter einer Runde, die auf andere Runden folgt — <c>null</c> für eine Runde ohne
    /// Vorgänger (siehe <see cref="TeilnehmerOhneVorgaengerAsync"/>).</summary>
    public async Task<IReadOnlyList<TeilnehmerAnzeige>?> PlatzhalterAusVorrundenAsync(TurnierRunde runde)
    {
        var eingehendeRegeln = await _uebergangsregelService.AlleNachZielRundeIdAsync(runde.Id);
        return TeilnehmerPoolHilfsmittel.PlatzhalterAusVorrunden(
            _instanzRunden, runde, eingehendeRegeln, Kombinationstabelle.AusJson(runde.KombinationstabelleJson));
    }

    /// <summary>Teilnehmer einer Runde ohne Vorgänger. Issue #297: "echte Auslosung mit Mannschaften nur bei
    /// Status != Vorlage" — eine Vorlage hat noch keine echten Teilnehmer, daher generische Platzhalter statt einer
    /// fachlich unpassenden echten Mannschaftsliste.</summary>
    public async Task<IReadOnlyList<TeilnehmerAnzeige>> TeilnehmerOhneVorgaengerAsync(TurnierRunde runde)
    {
        if (_instanz.Status == TurnierStatus.Vorlage)
            return Enumerable.Range(1, runde.EingangsMannschaftsanzahl)
                .Select(i => TeilnehmerAnzeige.AusPlatzhalter($"Team {i}")).ToList();

        var teams = await _mannschaftService.AlleNachTurnierfilterAsync(
            _instanz.VerbandId, _instanz.LandId, _instanz.MannschaftsartFilter);
        return teams.Select(TeilnehmerAnzeige.AusMannschaft).ToList();
    }
}
