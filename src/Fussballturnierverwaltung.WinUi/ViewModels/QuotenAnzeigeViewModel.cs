using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Quoten-Spalte der geöffneten Auslosung (#423): wie viele Mannschaften je Verband bzw. Land schon in
/// den Lostöpfen liegen, gemessen an der Quote des Turniers. Gezählt wird in <see cref="QuotenBelegung"/>.</summary>
public partial class QuotenAnzeigeViewModel : ObservableObject
{
    private readonly VerbandsQuoteService _verbandsQuoteService;
    private readonly LandesQuoteService _landesQuoteService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigeVerbandsquoten))]
    private IReadOnlyList<QuoteAnzeige> _verbandsquoten = [];

    public bool ZeigeVerbandsquoten => Verbandsquoten.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigeLandesquoten))]
    private IReadOnlyList<QuoteAnzeige> _landesquoten = [];

    public bool ZeigeLandesquoten => Landesquoten.Count > 0;

    public QuotenAnzeigeViewModel(VerbandsQuoteService verbandsQuoteService, LandesQuoteService landesQuoteService)
    {
        _verbandsQuoteService = verbandsQuoteService;
        _landesQuoteService = landesQuoteService;
    }

    public async Task ZeigenAsync(Auslosung detail, TurnierInstanz? instanz, IReadOnlyList<TurnierRunde> instanzRunden)
    {
        // Quoten gelten laut Domänen-Annahme nur für die initiale Turnierrunde (Zulassung), nicht kumulativ für
        // Folgerunden mit eigenem Losvorgang — siehe [[project_verbandsquote_scope]].
        bool istErsteRunde = instanzRunden.Count > 0 && instanzRunden[0].Id == detail.TurnierRundeId;
        if (instanz is null || !istErsteRunde)
        {
            Verbandsquoten = [];
            Landesquoten = [];
            return;
        }

        int? eingang = instanzRunden[0].EingangsMannschaftsanzahl;
        Verbandsquoten = Anzeige(QuotenBelegung.JeVerband(
            await _verbandsQuoteService.AlleNachTurnierInstanzIdAsync(instanz.Id), detail.Lostoepfe), detail, eingang);

        // Nur für Vereinswettbewerbe — Landesquoten sind bei Nationalmannschaften trivial (siehe
        // TurnierkopfViewModel.LandesQuotenSichtbar, #189).
        Landesquoten = instanz.MannschaftsartFilter == MannschaftsartFilter.Verein
            ? Anzeige(QuotenBelegung.JeLand(
                await _landesQuoteService.AlleNachTurnierInstanzIdAsync(instanz.Id), detail.Lostoepfe), detail, eingang)
            : [];
    }

    /// <summary>Die Quoten und dahinter die freien Plätze (#572), sofern es Quoten gibt und die Runde ihren Eingang kennt.</summary>
    private static IReadOnlyList<QuoteAnzeige> Anzeige(IReadOnlyList<QuotenStand> staende, Auslosung detail, int? eingang)
    {
        var zeilen = staende.ToList();
        if (zeilen.Count > 0 && eingang is { } anzahl)
            zeilen.Add(QuotenBelegung.FreiePlaetze(staende, detail.Lostoepfe, anzahl));
        return zeilen.Select(s => new QuoteAnzeige(s.Name, $"{s.Aktuell} / {s.Anzahl}")).ToList();
    }
}
