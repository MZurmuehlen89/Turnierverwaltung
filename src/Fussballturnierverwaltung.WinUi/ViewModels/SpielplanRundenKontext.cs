using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Runden-Kontext des Spielplans (#490, aus <see cref="SpielplanViewModel"/> herausgelöst):
/// Gruppentabellen, Team→Gruppe-Zuordnung und erlaubte Entscheidungsverfahren der Runde, mit der Formular und
/// Spielzeilen arbeiten — in der Rundenansicht die gewählte Runde, in der Mannschaftsansicht die Runde des
/// gewählten Spiels. Welche Runde das ist, entscheidet der Spielplan.</summary>
public sealed class SpielplanRundenKontext(TabellenService tabellenService, RegelwerkService regelwerkService)
{
    private readonly List<GruppenTabelle> _gruppenTabellen = [];
    private readonly Dictionary<int, int> _teamGruppen = [];

    public IReadOnlyList<GruppenTabelle> GruppenTabellen => _gruppenTabellen;

    /// <summary>Mannschafts-Id → Gruppennummer der geladenen Gruppentabellen.</summary>
    public IReadOnlyDictionary<int, int> TeamGruppen => _teamGruppen;

    public GruppenTabelle? Gruppe(int gruppenNummer) =>
        _gruppenTabellen.FirstOrDefault(g => g.GruppenNummer == gruppenNummer);

    public void Leeren()
    {
        _gruppenTabellen.Clear();
        _teamGruppen.Clear();
    }

    public async Task GruppenTabellenLadenAsync(int rundeId)
    {
        Leeren();
        try
        {
            var tabellen = await tabellenService.GruppenTabellenBerechnenAsync(rundeId);
            foreach (var gt in tabellen)
            {
                _gruppenTabellen.Add(gt);
                foreach (var te in gt.Tabelle)
                    _teamGruppen[te.Mannschaft.Id] = gt.GruppenNummer;
            }
        }
        catch { /* Gruppenstand konnte nicht berechnet werden — kein Fehler */ }
    }

    /// <summary>Die Entscheidungsverfahren aus dem Regelwerk der Runde, ohne eigenes das des Turniers.</summary>
    public async Task<IEnumerable<VerfahrenArt>> ErlaubteVerfahrenAsync(TurnierRunde runde, int? standardRegelwerkId)
    {
        var regelwerkId = runde.RegelwerkId ?? standardRegelwerkId;
        var regelwerk = regelwerkId is { } rid ? await regelwerkService.FindenNachIdAsync(rid) : null;

        return regelwerk?.Entscheidungsverfahren
            .OrderBy(v => v.Reihenfolge)
            .Select(v => v.VerfahrenArt) ?? [];
    }
}
