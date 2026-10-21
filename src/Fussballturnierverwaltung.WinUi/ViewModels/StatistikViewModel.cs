using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Application.Statistiken;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>
/// "Statistik"-Reiter der Turnierdurchführung (#327): links die Liste der Auswertungen, rechts die Rangliste
/// über alle Mannschaften des Turniers, im Bereich „Turnier“ (#432) Kennzahlen und Bestwerte des ganzen Turniers
/// (<see cref="TurnierSeite"/>) bzw. im Bereich „Mannschaft“ (#324) die Seite einer einzelnen Mannschaft
/// (<see cref="Mannschaft"/>), im Bereich „Tabellenverlauf“ (#480) Platzverlauf und Tabellen einer Runde zu einem
/// wählbaren Spieltag (<see cref="TabellenVerlauf"/>). Der Reiter liest nur — er hat keine eigenen Aktionen und deshalb keinen Ribbon-Bereich.
/// Die Instanz kommt wie bei den anderen Reitern über <see cref="InstanzSetzenAsync"/>.
/// </summary>
public partial class StatistikViewModel : ViewModelBase, IMannschaftsAnzeige
{
    private const string GRUND_NEUTRALER_SPIELORT = "Alle Spiele fanden auf neutralem Platz statt.";
    private const string GRUND_KEINE_TABELLE = "Das Turnier hat keine Runde mit Tabelle (Liga oder Gruppenphase).";
    private const string HINWEIS_KEINE_SPIELE = "Noch keine abgeschlossenen Spiele. Die Statistik erscheint, sobald Ergebnisse vorliegen.";

    private readonly StatistikService _service;
    private readonly ILogger<StatistikViewModel> _logger;

    private IReadOnlyList<MannschaftsSpiel> _spiele = [];
    private int? _geladeneInstanzId;
    private int? _vorauswahlMannschaftId;

    public IReadOnlyList<StatistikAuswertung> Auswertungen { get; } =
    [
        new(StatistikAuswertungsart.Turnier, "Turnier"),
        new(StatistikAuswertungsart.Mannschaft, "Mannschaft"),
        new(StatistikAuswertungsart.Tabellenverlauf, "Tabellenverlauf", GRUND_KEINE_TABELLE),
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

    public TabellenVerlaufViewModel TabellenVerlauf { get; }

    private StatistikAuswertung _ausgewaehlteAuswertung;

    // Die ListView schreibt beim Auf- und Abbau der Ansicht vorübergehend null zurück — die Auswahl bleibt dann.
    public StatistikAuswertung AusgewaehlteAuswertung
    {
        get => _ausgewaehlteAuswertung;
        set
        {
            if (value is null || !SetProperty(ref _ausgewaehlteAuswertung, value)) return;
            OnPropertyChanged(nameof(IstMannschaftsbereich));
            OnPropertyChanged(nameof(IstTurnierbereich));
            OnPropertyChanged(nameof(IstTabellenverlaufsbereich));
            TabelleAktualisieren();
            if (IstTabellenverlaufsbereich && AusgewaehlteAuswertung.IstVerfuegbar) TabellenVerlauf.Anzeigen();
        }
    }

    public bool IstMannschaftsbereich => AusgewaehlteAuswertung.Art == StatistikAuswertungsart.Mannschaft;
    public bool IstTurnierbereich => AusgewaehlteAuswertung.Art == StatistikAuswertungsart.Turnier;

    public bool IstTabellenverlaufsbereich =>
        AusgewaehlteAuswertung is { Art: StatistikAuswertungsart.Tabellenverlauf, IstVerfuegbar: true };

    // Ranglisten zeigen eine Tabelle, die Bereiche eine eigene Seite.
    private bool IstBereich => IstMannschaftsbereich || IstTurnierbereich || IstTabellenverlaufsbereich;

    [ObservableProperty] private StatistikTabelle? _tabelle;
    [ObservableProperty] private TurnierSeite? _turnierSeite;
    [ObservableProperty] private string? _hinweis;
    [ObservableProperty] private string? _fehlermeldung;

    /// <summary>„Alle Spiele des Teams anzeigen“ aus diesem Reiter — der Wechsel in den Spielplan ist Sache der
    /// Turnierdurchführung.</summary>
    public event EventHandler<int>? MannschaftsSpieleAngefordert;

    public StatistikViewModel(
        StatistikService service, TabellenVerlaufViewModel tabellenVerlauf, ILogger<StatistikViewModel> logger)
    {
        _service = service;
        _logger = logger;
        TabellenVerlauf = tabellenVerlauf;
        TabellenVerlauf.FehlerGemeldet += meldung => Fehlermeldung = meldung;
        _ausgewaehlteAuswertung = Auswertung(StatistikAuswertungsart.Gesamt);
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
            TabellenVerlaufUebernehmen(null);
            SpieleUebernehmen([], instanzId: null);
            return;
        }
        if (!istAktiv) return;

        using var vorgang = _logger.Vorgang($"Statistik: Turnier setzen (Instanz {instanz.Id})");
        TabellenVerlaufUebernehmen(instanz);
        SpieleUebernehmen(await _service.MannschaftsSpieleLadenAsync(instanz.Id), instanz.Id);
        if (IstTabellenverlaufsbereich) await TabellenVerlauf.AnzeigenAsync();
    }

    // Vor den Spielen, damit die Verfügbarkeit steht, wenn die Anzeige danach aktualisiert wird.
    private void TabellenVerlaufUebernehmen(TurnierInstanz? instanz)
    {
        TabellenVerlauf.InstanzSetzen(instanz);
        Auswertung(StatistikAuswertungsart.Tabellenverlauf).IstVerfuegbar = instanz is null || TabellenVerlauf.HatRunden;
        OnPropertyChanged(nameof(IstTabellenverlaufsbereich));
    }

    /// <summary>Kontextmenü „Statistik anzeigen“ aus einem anderen Reiter (#324): Bereich „Mannschaft“ wählen; die
    /// Mannschaft folgt, sobald der anschließende Reiterwechsel die Spiele geladen hat.</summary>
    public void MannschaftVorauswaehlen(int mannschaftId)
    {
        AusgewaehlteAuswertung = Auswertung(StatistikAuswertungsart.Mannschaft);
        _vorauswahlMannschaftId = mannschaftId;
    }

    // ── IMannschaftsAnzeige (Klick und Kontextmenü in diesem Reiter) ──────────

    public void MannschaftsStatistikAnzeigen(int mannschaftId)
    {
        AusgewaehlteAuswertung = Auswertung(StatistikAuswertungsart.Mannschaft);
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
        TurnierSeite = spiele.Count > 0 ? TurnierSeitenBuilder.Erstellen(spiele) : null;
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
        var tabelle = _geladeneInstanzId is null || IstBereich || !AusgewaehlteAuswertung.IstVerfuegbar
            ? null
            : RanglistenTabellenBuilder.Erstellen(AusgewaehlteAuswertung.Art, _spiele);

        Tabelle = tabelle is { HatZeilen: true } ? tabelle : null;
        Hinweis = HinweisErmitteln(tabelle);
    }

    private string? HinweisErmitteln(StatistikTabelle? tabelle)
    {
        if (_geladeneInstanzId is null) return null;
        if (!AusgewaehlteAuswertung.IstVerfuegbar) return AusgewaehlteAuswertung.Hinweis;
        if (IstTabellenverlaufsbereich) return null;
        if (IstBereich) return _spiele.Count == 0 ? HINWEIS_KEINE_SPIELE : null;
        return tabelle is { HatZeilen: true } ? null : HINWEIS_KEINE_SPIELE;
    }

    private StatistikAuswertung Auswertung(StatistikAuswertungsart art) => Auswertungen.First(a => a.Art == art);
}
