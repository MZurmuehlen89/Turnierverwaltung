using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Runden-Kontext des Spielplans (#490, aus <see cref="SpielplanViewModel"/> herausgelöst):
/// Gruppentabellen, Team→Gruppe-Zuordnung und erlaubte Entscheidungsverfahren der Runde, mit der Formular und
/// Spielzeilen arbeiten.</summary>
public sealed class SpielplanRundenKontext(TabellenService tabellenService, RegelwerkService regelwerkService)
{
    public ObservableCollection<GruppenTabelle> GruppenTabellen { get; } = [];

    public Dictionary<int, int> TeamGruppeMap { get; set; } = [];

    public async Task GruppenTabellenLadenAsync(int rundeId)
    {
        GruppenTabellen.Clear();
        TeamGruppeMap = [];
        try
        {
            var tabellen = await tabellenService.GruppenTabellenBerechnenAsync(rundeId);
            foreach (var gt in tabellen)
            {
                GruppenTabellen.Add(gt);
                foreach (var te in gt.Tabelle)
                    TeamGruppeMap[te.Mannschaft.Id] = gt.GruppenNummer;
            }
        }
        catch { /* Gruppenstand konnte nicht berechnet werden — kein Fehler */ }
    }

    public async Task<IEnumerable<VerfahrenArt>> ErlaubteVerfahrenAsync(TurnierRunde runde, int? standardRegelwerkId)
    {
        var regelwerkId = runde.RegelwerkId ?? standardRegelwerkId;
        var regelwerk = regelwerkId is { } rid ? await regelwerkService.FindenNachIdAsync(rid) : null;

        return regelwerk?.Entscheidungsverfahren
            .OrderBy(v => v.Reihenfolge)
            .Select(v => v.VerfahrenArt) ?? [];
    }
}
