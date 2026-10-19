using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Application.Statistiken;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>
/// "Statistik"-Reiter der Turnierdurchführung (#327): links die Liste der Auswertungen, rechts die Rangliste
/// über alle Mannschaften des Turniers bzw. im Bereich „Mannschaft“ (#324) die Seite einer einzelnen Mannschaft
/// (<see cref="Mannschaft"/>). Der Reiter liest nur — er hat keine eigenen Aktionen und deshalb keinen Ribbon-Bereich.
/// Die Instanz kommt wie bei den anderen Reitern über <see cref="InstanzSetzenAsync"/>.
/// </summary>
public partial class StatistikViewModel : ViewModelBase, IMannschaftsAnzeige
{
    private const string GRUND_NEUTRALER_SPIELORT = "Alle Spiele fanden auf neutralem Platz statt.";
    private const string HINWEIS_KEINE_SPIELE = "Noch keine abgeschlossenen Spiele. Die Statistik erscheint, sobald Ergebnisse vorliegen.";

    private readonly StatistikService _service;
    private readonly ILogger<StatistikViewModel> _logger;

    private IReadOnlyList<MannschaftsSpiel> _spiele = [];
    private int? _geladeneInstanzId;
    private int? _vorauswahlMannschaftId;

    public IReadOnlyList<StatistikAuswertung> Auswertungen { get; } =
    [
        new(StatistikAuswertungsart.Mannschaft, "Mannschaft"),
        new(StatistikAuswertungsart.Gesamt, "Gesamt"),
        new(StatistikAuswertungsart.Heim, "Heim", GRUND_NEUTRALER_SPIELORT),
        new(StatistikAuswertungsart.Auswaerts, "Auswärts", GRUND_NEUTRALER_SPIELORT),
        new(StatistikAuswertungsart.Form, "Form"),
        new(StatistikAuswertungsart.Halbzeit, "Halbzeit"),
        new(StatistikAuswertungsart.Serien, "Serien"),
        new(StatistikAuswertungsart.Tore, "Tore und Zu-Null"),
        new(StatistikAuswertungsart.Fairplay, "Fairplay"),
        new(StatistikAuswertungsart.Verbaende, "Verbände")
    ];

    public MannschaftsStatistikViewModel Mannschaft { get; } = new();

    private StatistikAuswertung _ausgewaehlteAuswertung;

    // Die ListView schreibt beim Auf- und Abbau der Ansicht vorübergehend null zurück — die Auswahl bleibt dann.
    public StatistikAuswertung AusgewaehlteAuswertung
    {
        get => _ausgewaehlteAuswertung;
        set
        {
            if (value is null || !SetProperty(ref _ausgewaehlteAuswertung, value)) return;
            OnPropertyChanged(nameof(IstMannschaftsbereich));
            TabelleAktualisieren();
        }
    }

    public bool IstMannschaftsbereich => AusgewaehlteAuswertung.Art == StatistikAuswertungsart.Mannschaft;

    [ObservableProperty] private StatistikTabelle? _tabelle;
    [ObservableProperty] private string? _hinweis;
    [ObservableProperty] private string? _fehlermeldung;

    /// <summary>„Alle Spiele des Teams anzeigen“ aus diesem Reiter — der Wechsel in den Spielplan ist Sache der
    /// Turnierdurchführung.</summary>
    public event EventHandler<int>? MannschaftsSpieleAngefordert;

    public StatistikViewModel(StatistikService service, ILogger<StatistikViewModel> logger)
    {
        _service = service;
        _logger = logger;
        _ausgewaehlteAuswertung = Auswertungen[1];
        Mannschaft.AlleSpieleAngefordert += (_, mannschaftId) => MannschaftsSpieleAngefordert?.Invoke(this, mannschaftId);
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

    /// <summary>Kontextmenü „Statistik anzeigen“ aus einem anderen Reiter (#324): Bereich „Mannschaft“ wählen; die
    /// Mannschaft folgt, sobald der anschließende Reiterwechsel die Spiele geladen hat.</summary>
    public void MannschaftVorauswaehlen(int mannschaftId)
    {
        AusgewaehlteAuswertung = Auswertungen[0];
        _vorauswahlMannschaftId = mannschaftId;
    }

    // ── IMannschaftsAnzeige (Klick und Kontextmenü in diesem Reiter) ──────────

    public void MannschaftsStatistikAnzeigen(int mannschaftId)
    {
        AusgewaehlteAuswertung = Auswertungen[0];
        Mannschaft.Zeigen(mannschaftId);
    }

    public Task MannschaftsSpieleAnzeigenAsync(int mannschaftId)
    {
        MannschaftsSpieleAngefordert?.Invoke(this, mannschaftId);
        return Task.CompletedTask;
    }

    // ── Laden ─────────────────────────────────────────────────────────────

    private void SpieleUebernehmen(IReadOnlyList<MannschaftsSpiel> spiele, int? instanzId)
    {
        bool turnierGewechselt = instanzId != _geladeneInstanzId;
        _spiele = spiele;
        _geladeneInstanzId = instanzId;

        bool hatHeimUndAuswaerts = spiele.Count == 0 || StatistikBerechnung.HatHeimUndAuswaertsSpiele(spiele);
        foreach (var auswertung in Auswertungen.Where(a => a.Art is StatistikAuswertungsart.Heim or StatistikAuswertungsart.Auswaerts))
            auswertung.IstVerfuegbar = hatHeimUndAuswaerts;

        MannschaftUebernehmen(spiele, turnierGewechselt);
        TabelleAktualisieren();
    }

    // Ein anderes Turnier beginnt ohne gewählte Mannschaft — es sei denn, das Kontextmenü hat gerade eine vorgegeben.
    private void MannschaftUebernehmen(IReadOnlyList<MannschaftsSpiel> spiele, bool turnierGewechselt)
    {
        if (turnierGewechselt) Mannschaft.Zeigen(null);
        Mannschaft.SpieleUebernehmen(spiele);

        if (_vorauswahlMannschaftId is not int vorauswahl || _geladeneInstanzId is null) return;
        Mannschaft.Zeigen(vorauswahl);
        _vorauswahlMannschaftId = null;
    }

    private void TabelleAktualisieren()
    {
        var tabelle = _geladeneInstanzId is null || IstMannschaftsbereich || !AusgewaehlteAuswertung.IstVerfuegbar
            ? null
            : RanglistenTabellenBuilder.Erstellen(AusgewaehlteAuswertung.Art, _spiele);

        Tabelle = tabelle is { HatZeilen: true } ? tabelle : null;
        Hinweis = HinweisErmitteln(tabelle);
    }

    private string? HinweisErmitteln(StatistikTabelle? tabelle)
    {
        if (_geladeneInstanzId is null) return null;
        if (!AusgewaehlteAuswertung.IstVerfuegbar) return AusgewaehlteAuswertung.Hinweis;
        if (IstMannschaftsbereich) return _spiele.Count == 0 ? HINWEIS_KEINE_SPIELE : null;
        return tabelle is { HatZeilen: true } ? null : HINWEIS_KEINE_SPIELE;
    }
}
