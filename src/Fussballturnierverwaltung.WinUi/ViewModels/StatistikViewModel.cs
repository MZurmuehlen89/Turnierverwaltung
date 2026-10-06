using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Application.Statistiken;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>
/// "Statistik"-Reiter der Turnierdurchführung (#327): links die Liste der Auswertungen, rechts die Rangliste
/// über alle Mannschaften des Turniers. Der Reiter liest nur — er hat keine eigenen Aktionen und deshalb
/// keinen Ribbon-Bereich. Die Instanz kommt wie bei den anderen Reitern über <see cref="InstanzSetzenAsync"/>.
/// </summary>
public partial class StatistikViewModel : ViewModelBase
{
    private const string GRUND_NEUTRALER_SPIELORT = "Alle Spiele fanden auf neutralem Platz statt.";
    private const string HINWEIS_KEINE_SPIELE = "Noch keine abgeschlossenen Spiele. Die Statistik erscheint, sobald Ergebnisse vorliegen.";

    private readonly StatistikService _service;
    private readonly ILogger<StatistikViewModel> _logger;

    private IReadOnlyList<MannschaftsSpiel> _spiele = [];
    private int? _geladeneInstanzId;

    public IReadOnlyList<StatistikAuswertung> Auswertungen { get; } =
    [
        new(StatistikAuswertungsart.Gesamt, "Gesamt"),
        new(StatistikAuswertungsart.Heim, "Heim", GRUND_NEUTRALER_SPIELORT),
        new(StatistikAuswertungsart.Auswaerts, "Auswärts", GRUND_NEUTRALER_SPIELORT),
        new(StatistikAuswertungsart.Form, "Form")
    ];

    private StatistikAuswertung _ausgewaehlteAuswertung;

    // Die ListView schreibt beim Auf- und Abbau der Ansicht vorübergehend null zurück — die Auswahl bleibt dann.
    public StatistikAuswertung AusgewaehlteAuswertung
    {
        get => _ausgewaehlteAuswertung;
        set
        {
            if (value is null || !SetProperty(ref _ausgewaehlteAuswertung, value)) return;
            TabelleAktualisieren();
        }
    }

    [ObservableProperty] private StatistikTabelle? _tabelle;
    [ObservableProperty] private string? _hinweis;
    [ObservableProperty] private string? _fehlermeldung;

    public StatistikViewModel(StatistikService service, ILogger<StatistikViewModel> logger)
    {
        _service = service;
        _logger = logger;
        _ausgewaehlteAuswertung = Auswertungen[0];
    }

    /// <summary>Von <see cref="TurnierdurchfuehrungViewModel"/> bei Turnier- und Reiterwechsel aufgerufen.
    /// Geladen wird nur, solange der Reiter sichtbar ist (<paramref name="istAktiv"/>) — sonst liefe die Abfrage
    /// aller Spiele samt Ereignissen bei jedem Wechsel in den anderen Reitern mit. Der Wechsel auf diesen Reiter
    /// löst ohnehin einen neuen Aufruf aus.</summary>
    public async Task InstanzSetzenAsync(TurnierInstanz? instanz, bool istAktiv)
    {
        Fehlermeldung = null;

        if (instanz is null || (!istAktiv && instanz.Id != _geladeneInstanzId))
        {
            SpieleUebernehmen([], instanzId: null);
            return;
        }
        if (!istAktiv) return;

        using var vorgang = _logger.Vorgang($"Statistik: Turnier setzen (Instanz {instanz.Id})");
        SpieleUebernehmen(await _service.MannschaftsSpieleLadenAsync(instanz.Id), instanz.Id);
    }

    private void SpieleUebernehmen(IReadOnlyList<MannschaftsSpiel> spiele, int? instanzId)
    {
        _spiele = spiele;
        _geladeneInstanzId = instanzId;

        bool hatHeimUndAuswaerts = spiele.Count == 0 || StatistikBerechnung.HatHeimUndAuswaertsSpiele(spiele);
        foreach (var auswertung in Auswertungen.Where(a => a.Art is StatistikAuswertungsart.Heim or StatistikAuswertungsart.Auswaerts))
            auswertung.IstVerfuegbar = hatHeimUndAuswaerts;

        TabelleAktualisieren();
    }

    private void TabelleAktualisieren()
    {
        var tabelle = _geladeneInstanzId is null || !AusgewaehlteAuswertung.IstVerfuegbar
            ? null
            : RanglistenTabellenBuilder.Erstellen(AusgewaehlteAuswertung.Art, _spiele);

        Tabelle = tabelle is { HatZeilen: true } ? tabelle : null;
        Hinweis = HinweisErmitteln(tabelle);
    }

    private string? HinweisErmitteln(StatistikTabelle? tabelle)
    {
        if (_geladeneInstanzId is null) return null;
        if (!AusgewaehlteAuswertung.IstVerfuegbar) return AusgewaehlteAuswertung.Hinweis;
        return tabelle is { HatZeilen: true } ? null : HINWEIS_KEINE_SPIELE;
    }
}
