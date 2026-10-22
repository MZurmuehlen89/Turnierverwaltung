using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Listenquelle des Spielplans (#490, aus <see cref="SpielplanViewModel"/> herausgelöst): welche Spiele
/// die Liste zeigt.</summary>
public sealed class SpiellistenQuelle(SpielErgebnisService spielService, PlatzhalterAufloesung platzhalterAufloesung)
{
    private IReadOnlyDictionary<int, (string? Heim, string? Auswaerts)> _platzhalterVorschau =
        new Dictionary<int, (string?, string?)>();

    public void VorschauVerwerfen() => _platzhalterVorschau = new Dictionary<int, (string?, string?)>();

    public async Task VorschauLadenAsync(int rundeId) =>
        _platzhalterVorschau = await platzhalterAufloesung.VorschauAsync(rundeId);

    public async Task<List<SpielZeile>> RundenZeilenAsync(
        int rundeId, TurnierRunde? runde, IReadOnlyDictionary<int, int> teamGruppen)
    {
        var spiele = await spielService.AlleNachRundeAsync(rundeId);
        return SpielZeilenMapper.SpielplanZeilen(spiele, runde, teamGruppen, _platzhalterVorschau);
    }

    public async Task<List<SpielZeile>> MannschaftsZeilenAsync(int instanzId, int mannschaftId)
    {
        var spiele = await spielService.AlleNachMannschaftAsync(instanzId, mannschaftId);
        return SpielZeilenMapper.MannschaftsZeilen(spiele);
    }
}
