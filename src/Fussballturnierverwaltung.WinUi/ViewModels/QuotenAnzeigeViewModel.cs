using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Quoten-Spalte der geöffneten Auslosung (#423): wie viele Mannschaften je Verband bzw. Land schon in
/// den Lostöpfen liegen, gemessen an der Quote des Turniers. Gezählt wird in <see cref="QuotenBelegung"/>.
/// Ein Klick auf eine Quote meldet <see cref="LandGewaehlt"/> bzw. <see cref="VerbandGewaehlt"/>; welche Quote
/// hervorgehoben ist, sagt der Filter der Verfügbar-Liste über <see cref="Hervorheben"/> (#573).</summary>
public partial class QuotenAnzeigeViewModel : ObservableObject
{
    private readonly VerbandsQuoteService _verbandsQuoteService;
    private readonly LandesQuoteService _landesQuoteService;

    private IReadOnlyList<QuotenStand> _verbandsStaende = [];
    private IReadOnlyList<QuotenStand> _landesStaende = [];
    private int? _aktiverVerband;
    private int? _aktivesLand;

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

    public event Action<int>? LandGewaehlt;

    public event Action<int>? VerbandGewaehlt;

    public async Task ZeigenAsync(Auslosung detail, TurnierInstanz? instanz, IReadOnlyList<TurnierRunde> instanzRunden)
    {
        if (instanz is null || !QuotenBelegung.GeltenFuer(detail.TurnierRundeId, instanzRunden))
        {
            _verbandsStaende = [];
            _landesStaende = [];
            ZeilenAktualisieren();
            return;
        }

        int? eingang = instanzRunden[0].EingangsMannschaftsanzahl;
        _verbandsStaende = MitFreienPlaetzen(QuotenBelegung.JeVerband(
            await _verbandsQuoteService.AlleNachTurnierInstanzIdAsync(instanz.Id), detail.Lostoepfe), detail, eingang);

        // Nur für Vereinswettbewerbe — Landesquoten sind bei Nationalmannschaften trivial (siehe
        // TurnierkopfViewModel.LandesQuotenSichtbar, #189).
        _landesStaende = instanz.MannschaftsartFilter == MannschaftsartFilter.Verein
            ? MitFreienPlaetzen(QuotenBelegung.JeLand(
                await _landesQuoteService.AlleNachTurnierInstanzIdAsync(instanz.Id), detail.Lostoepfe), detail, eingang)
            : [];
        ZeilenAktualisieren();
    }

    /// <summary>Der Filterstand der Verfügbar-Liste: die Quote seines Verbands bzw. Landes wird hervorgehoben.</summary>
    public void Hervorheben(int? verbandId, int? landId)
    {
        if (_aktiverVerband == verbandId && _aktivesLand == landId) return;
        _aktiverVerband = verbandId;
        _aktivesLand = landId;
        ZeilenAktualisieren();
    }

    private void ZeilenAktualisieren()
    {
        Verbandsquoten = Zeilen(_verbandsStaende, _aktiverVerband, id => VerbandGewaehlt?.Invoke(id));
        Landesquoten = Zeilen(_landesStaende, _aktivesLand, id => LandGewaehlt?.Invoke(id));
    }

    /// <summary>Die Quoten und dahinter die freien Plätze (#572), sofern es Quoten gibt und die Runde ihren Eingang kennt.</summary>
    private static IReadOnlyList<QuotenStand> MitFreienPlaetzen(IReadOnlyList<QuotenStand> staende, Auslosung detail, int? eingang) =>
        staende.Count > 0 && eingang is { } anzahl
            ? [.. staende, QuotenBelegung.FreiePlaetze(staende, detail.Lostoepfe, anzahl)]
            : staende;

    private static IReadOnlyList<QuoteAnzeige> Zeilen(IReadOnlyList<QuotenStand> staende, int? aktiv, Action<int> gewaehlt) =>
        [.. staende.Select(s => new QuoteAnzeige(
            s.Name,
            $"{s.Aktuell} / {s.Anzahl}",
            s.Schluessel is int id ? new RelayCommand(() => gewaehlt(id)) : null,
            s.Schluessel is not null && s.Schluessel == aktiv))];
}
