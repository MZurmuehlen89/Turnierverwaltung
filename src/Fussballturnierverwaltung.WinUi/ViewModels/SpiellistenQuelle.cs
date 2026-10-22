using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Listenquelle des Spielplans (#490, aus <see cref="SpielplanViewModel"/> herausgelöst): welche Spiele
/// die Liste zeigt — in der Mannschaftsansicht (#479) die der Mannschaft über alle Runden, sonst die der Runde,
/// bei aktiver Platzhalter-Vorschau (#208) mit den voraussichtlichen Teams.</summary>
public sealed class SpiellistenQuelle(SpielErgebnisService spielService, PlatzhalterAufloesung platzhalterAufloesung)
{
    private IReadOnlyDictionary<int, (string? Heim, string? Auswaerts)> _platzhalterVorschau = KeineVorschau();

    public void VorschauVerwerfen() => _platzhalterVorschau = KeineVorschau();

    /// <summary>Löst die Platzhalter der Runde für die folgenden <see cref="ZeilenAsync"/>-Aufrufe auf.</summary>
    public async Task VorschauLadenAsync(int rundeId) =>
        _platzhalterVorschau = await platzhalterAufloesung.VorschauAsync(rundeId);

    /// <summary>Die Zeilen der Liste frisch aus der Datenbank; <c>null</c>, wenn es nichts zu zeigen gibt (weder
    /// Mannschaft noch Runde gewählt).</summary>
    /// <param name="teamGruppen">Gruppe je Mannschaft der Runde (<see cref="SpielplanRundenKontext.TeamGruppen"/>).</param>
    public async Task<IReadOnlyList<SpielZeile>?> ZeilenAsync(
        int instanzId, int? mannschaftId, TurnierRunde? runde, IReadOnlyDictionary<int, int> teamGruppen)
    {
        if (mannschaftId is { } id)
            return SpielZeilenMapper.MannschaftsZeilen(await spielService.AlleNachMannschaftAsync(instanzId, id));
        if (runde is null) return null;

        var spiele = await spielService.AlleNachRundeAsync(runde.Id);
        return SpielZeilenMapper.SpielplanZeilen(spiele, runde, teamGruppen, _platzhalterVorschau);
    }

    private static Dictionary<int, (string?, string?)> KeineVorschau() => [];
}
