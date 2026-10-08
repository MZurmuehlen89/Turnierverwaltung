using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Quoten-Spalte der geöffneten Auslosung (#423): wie viele Mannschaften je Verband bzw. Land schon in
/// den Lostöpfen liegen, gemessen an der Quote des Turniers.</summary>
public partial class QuotenAnzeigeViewModel : ObservableObject
{
    private readonly VerbandsQuoteService _verbandsQuoteService;
    private readonly LandesQuoteService _landesQuoteService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigeVerbandsQuoten))]
    private ObservableCollection<VerbandsQuoteAnzeigeViewModel> _verbandsQuotenAnzeige = [];

    public bool ZeigeVerbandsQuoten => VerbandsQuotenAnzeige.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigeLandesQuoten))]
    private ObservableCollection<LandesQuoteAnzeigeViewModel> _landesQuotenAnzeige = [];

    public bool ZeigeLandesQuoten => LandesQuotenAnzeige.Count > 0;

    public QuotenAnzeigeViewModel(VerbandsQuoteService verbandsQuoteService, LandesQuoteService landesQuoteService)
    {
        _verbandsQuoteService = verbandsQuoteService;
        _landesQuoteService = landesQuoteService;
    }

    public async Task ZeigenAsync(Auslosung detail, TurnierInstanz? instanz, IReadOnlyList<TurnierRunde> instanzRunden)
    {
        await AktualisiereVerbandsQuotenAnzeigeAsync(detail, instanz, instanzRunden);
        await AktualisiereLandesQuotenAnzeigeAsync(detail, instanz, instanzRunden);
    }

    private async Task AktualisiereVerbandsQuotenAnzeigeAsync(
        Auslosung detail, TurnierInstanz? instanz, IReadOnlyList<TurnierRunde> instanzRunden)
    {
        if (instanz is null) { VerbandsQuotenAnzeige = []; return; }

        // Verbandsquoten gelten laut Domänen-Annahme nur für die initiale Turnierrunde (Zulassung),
        // nicht kumulativ für Folgerunden mit eigenem Losvorgang — siehe [[project_verbandsquote_scope]].
        bool istErsteRunde = instanzRunden.Count > 0 && instanzRunden[0].Id == detail.TurnierRundeId;
        if (!istErsteRunde) { VerbandsQuotenAnzeige = []; return; }

        var quoten = await _verbandsQuoteService.AlleNachTurnierInstanzIdAsync(instanz.Id);
        if (quoten.Count == 0) { VerbandsQuotenAnzeige = []; return; }

        var anzahlProVerband = detail.Lostoepfe
            .SelectMany(l => l.Eintraege)
            .Where(e => e.Mannschaft?.Land is not null)
            .GroupBy(e => e.Mannschaft!.Land!.VerbandId)
            .ToDictionary(g => g.Key, g => g.Count());

        VerbandsQuotenAnzeige = new ObservableCollection<VerbandsQuoteAnzeigeViewModel>(
            quoten.Select(q => new VerbandsQuoteAnzeigeViewModel
            {
                VerbandName = q.Verband?.Name ?? $"Verband {q.VerbandId}",
                Aktuell     = anzahlProVerband.GetValueOrDefault(q.VerbandId),
                Anzahl      = q.Anzahl
            }));
    }

    /// <summary>Landes-Quoten-Pendant zu <see cref="AktualisiereVerbandsQuotenAnzeigeAsync"/> (Issue #189)
    /// — dieselbe "nur erste Runde"-Domänenregel, zusätzlich nur für Vereinswettbewerbe sichtbar
    /// (Landesquoten sind bei Nationalmannschaften trivial, siehe TurnierkopfViewModel.LandesQuotenSichtbar).
    /// Gruppierung über Mannschaft.LandId direkt statt der Verband-Indirektion über Land.VerbandId.</summary>
    private async Task AktualisiereLandesQuotenAnzeigeAsync(
        Auslosung detail, TurnierInstanz? instanz, IReadOnlyList<TurnierRunde> instanzRunden)
    {
        if (instanz is null || instanz.MannschaftsartFilter != MannschaftsartFilter.Verein) { LandesQuotenAnzeige = []; return; }

        bool istErsteRunde = instanzRunden.Count > 0 && instanzRunden[0].Id == detail.TurnierRundeId;
        if (!istErsteRunde) { LandesQuotenAnzeige = []; return; }

        var quoten = await _landesQuoteService.AlleNachTurnierInstanzIdAsync(instanz.Id);
        if (quoten.Count == 0) { LandesQuotenAnzeige = []; return; }

        var anzahlProLand = detail.Lostoepfe
            .SelectMany(l => l.Eintraege)
            .Where(e => e.Mannschaft is not null)
            .GroupBy(e => e.Mannschaft!.LandId)
            .ToDictionary(g => g.Key, g => g.Count());

        LandesQuotenAnzeige = new ObservableCollection<LandesQuoteAnzeigeViewModel>(
            quoten.Select(q => new LandesQuoteAnzeigeViewModel
            {
                LandName = q.Land?.Name ?? $"Land {q.LandId}",
                Aktuell  = anzahlProLand.GetValueOrDefault(q.LandId),
                Anzahl   = q.Anzahl
            }));
    }
}
