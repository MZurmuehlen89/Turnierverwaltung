using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.WinUi.Helpers;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

public record TitelverteidigerOption(string Anzeige, int? Wert);

/// <summary>Gastgeber-Zeile (Issue #352). Ein <c>null</c> aus der ComboBox kommt nur transient (Auswahl vor
/// <c>Loaded</c> gesetzt, siehe WinUi-CLAUDE.md) und wird ignoriert — entfernt wird eine Zeile über ✕.</summary>
public partial class GastgeberZeile : ObservableObject
{
    private MannschaftAuswahl? _mannschaft;
    public MannschaftAuswahl? Mannschaft
    {
        get => _mannschaft;
        set { if (value is not null) SetProperty(ref _mannschaft, value); else OnPropertyChanged(); }
    }
}

/// <summary>Bereich „Titelverteidiger &amp; Gastgeber“ des Turnierkopfs (#434): Auswahl des Titelverteidigers
/// und der Gastgeber samt Kopfzeile des aufklappbaren Bereichs. Beide Auswahllisten sind nach dem
/// Auslosungsfilter des Turniers vorgefiltert; den Filter reicht <see cref="TurnierkopfViewModel"/> mit jedem
/// Aufruf herein.</summary>
public partial class TitelGastgeberViewModel : ObservableObject
{
    private readonly MannschaftService _mannschaftService;
    private readonly ILogger<TitelGastgeberViewModel> _logger;
    private int _populiertGerade;
    private Auslosungsfilter _filter = Auslosungsfilter.Keiner;

    // Alle Mannschaften nach Name sortiert — Grundlage beider Auswahllisten.
    private IReadOnlyList<MannschaftAuswahl> _alleMannschaften = [];

    private static readonly TitelverteidigerOption _keinTitelverteidiger = new("— keiner —", null);

    /// <summary>Der Nutzer hat den Titelverteidiger oder die Gastgeber geändert — oder ein Filterwechsel hat
    /// eine nicht mehr passende Auswahl abgeräumt.</summary>
    public event Action? Geaendert;

    // ── Titelverteidiger (Issue #349) ─────────────────────────────────────────
    // Optional; kommt bei Runden mit "Titelverteidiger in Topf 1" unabhängig vom Ranglistenplatz in Topf 1.
    // Die Auswahl ist nach dem Auslosungsfilter (Verband/Land/Mannschaftsart) vorgefiltert — gleiche Semantik
    // wie MannschaftRepository.AlleNachTurnierfilterAsync, FIFA = weltweit. Stabile Collection-Instanz mit
    // inkrementellem Abgleich statt neuer ItemsSource je Filterwechsel (WinUI3-Fallstrick, siehe
    // SammlungsAbgleich.TeilfolgeAbgleichen / #335).
    [ObservableProperty] private TitelverteidigerOption _titelverteidiger = _keinTitelverteidiger;
    public ObservableCollection<TitelverteidigerOption> TitelverteidigerOptionenGefiltert { get; } = [_keinTitelverteidiger];

    // Je Mannschaft genau eine Option (Referenzabgleich der ComboBox), Reihenfolge wie _alleMannschaften.
    private IReadOnlyList<(TitelverteidigerOption Option, MannschaftAuswahl Mannschaft)> _titelverteidigerKandidaten = [];

    public int? TitelverteidigerId => Titelverteidiger?.Wert;

    partial void OnTitelverteidigerChanged(TitelverteidigerOption value)
    {
        if (value is null) Titelverteidiger = _keinTitelverteidiger;
    }

    private TitelverteidigerOption TitelverteidigerOptionFuer(int? mannschaftId) =>
        _titelverteidigerKandidaten.FirstOrDefault(k => k.Option.Wert == mannschaftId && mannschaftId is not null).Option
        ?? _keinTitelverteidiger;

    /// <summary>Gleicht die Auswahlliste mit dem Auslosungsfilter ab. Die aktuelle Auswahl (und optional
    /// <paramref name="zusaetzlich"/>, bevor sie gesetzt wird) bleibt immer enthalten — sonst fände die ComboBox
    /// sie nicht und schriebe null zurück.</summary>
    private void TitelverteidigerOptionenSynchronisieren(TitelverteidigerOption? zusaetzlich = null)
    {
        IReadOnlyList<TitelverteidigerOption> soll =
        [
            _keinTitelverteidiger,
            .. _titelverteidigerKandidaten
                .Where(k => k.Option == Titelverteidiger || k.Option == zusaetzlich || _filter.Passt(k.Mannschaft))
                .Select(k => k.Option)
        ];
        SammlungsAbgleich.TeilfolgeAbgleichen(TitelverteidigerOptionenGefiltert, soll);
    }

    /// <summary>Nach einer Filteränderung durch den Nutzer: passt der gewählte Titelverteidiger nicht mehr
    /// zum Filter, wird er zurückgesetzt (Prävention statt Speicher-Validierung), danach die Liste abgeglichen.</summary>
    private void TitelverteidigerFilterAnwenden()
    {
        var aktuell = _titelverteidigerKandidaten.FirstOrDefault(k => k.Option == Titelverteidiger);
        if (aktuell.Mannschaft is not null && !_filter.Passt(aktuell.Mannschaft))
            Titelverteidiger = _keinTitelverteidiger;
        TitelverteidigerOptionenSynchronisieren();
    }

    /// <summary>Befüllt die Auswahl beim Laden eines Turniers; meldet keine Änderung.</summary>
    public void TitelverteidigerSetzen(Auslosungsfilter filter, int? mannschaftId)
    {
        _filter = filter;
        _populiertGerade++;
        try
        {
            // Erst die Liste abgleichen, dann die Auswahl setzen — eine Auswahl, die die ComboBox in ihrer
            // Liste nicht findet, schreibt sie als null zurück.
            var titelverteidiger = TitelverteidigerOptionFuer(mannschaftId);
            TitelverteidigerOptionenSynchronisieren(titelverteidiger);
            Titelverteidiger = titelverteidiger;
            TitelverteidigerOptionenSynchronisieren();
        }
        finally
        {
            _populiertGerade--;
        }
    }

    // ── Gastgeber (Issue #352) ────────────────────────────────────────────────
    // Behalten in Runden mit neutralem Spielort ihren Heimvorteil. Auswahl wie beim Titelverteidiger nach dem
    // Auslosungsfilter vorgefiltert; gewählte Gastgeber bleiben immer enthalten, sonst fände die ComboBox ihre
    // Auswahl nicht. Stabile Collection-Instanzen mit inkrementellem Abgleich (WinUI3-Fallstrick #335); die
    // ComboBox findet ihre Auswahl über dieselben Einträge.
    public ObservableCollection<GastgeberZeile> GastgeberZeilen { get; } = [];
    public ObservableCollection<MannschaftAuswahl> GastgeberKandidaten { get; } = [];


    [RelayCommand]
    private void GastgeberHinzufuegen() => GastgeberZeilen.Add(new GastgeberZeile());

    [RelayCommand]
    private void GastgeberEntfernen(GastgeberZeile zeile) => GastgeberZeilen.Remove(zeile);

    private void GastgeberZeilenBeobachten()
    {
        GastgeberZeilen.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null)
                foreach (GastgeberZeile zeile in e.NewItems)
                    zeile.PropertyChanged += (_, _) => GastgeberAenderungMelden();
            GastgeberAenderungMelden();
        };
    }

    private void GastgeberAenderungMelden()
    {
        OnPropertyChanged(nameof(Kopf));
        if (_populiertGerade > 0) return;
        Geaendert?.Invoke();
        // Eine zuvor nur wegen der Auswahl enthaltene Mannschaft fällt wieder heraus.
        SammlungsAbgleich.Verzoegert(() => GastgeberOptionenSynchronisieren());
    }

    /// <summary>Befüllt die Gastgeber beim Laden eines Turniers; meldet keine Änderung.</summary>
    public void GastgeberSetzen(Auslosungsfilter filter, IEnumerable<int> mannschaftIds)
    {
        _filter = filter;
        _populiertGerade++;
        try
        {
            var gastgeber = _alleMannschaften.Where(m => mannschaftIds.Contains(m.Id)).ToList();
            GastgeberZeilen.Clear();
            // Erst die Liste abgleichen, dann die Zeilen anlegen (siehe TitelverteidigerSetzen).
            GastgeberOptionenSynchronisieren(gastgeber);
            foreach (var m in gastgeber)
                GastgeberZeilen.Add(new GastgeberZeile { Mannschaft = m });
        }
        finally
        {
            _populiertGerade--;
        }
    }

    /// <summary>Gleicht die Auswahlliste mit dem Auslosungsfilter ab. Gewählte Gastgeber (und optional
    /// <paramref name="zusaetzlich"/>, bevor sie gesetzt werden) bleiben immer enthalten.</summary>
    private void GastgeberOptionenSynchronisieren(IReadOnlyCollection<MannschaftAuswahl>? zusaetzlich = null)
    {
        var gewaehlt = GewaehlteGastgeber().ToHashSet();
        if (zusaetzlich is not null) gewaehlt.UnionWith(zusaetzlich);
        IReadOnlyList<MannschaftAuswahl> soll = _alleMannschaften
            .Where(m => gewaehlt.Contains(m) || _filter.Passt(m))
            .ToList();
        SammlungsAbgleich.TeilfolgeAbgleichen(GastgeberKandidaten, soll);
    }

    /// <summary>Nach einer Filteränderung durch den Nutzer: Gastgeber, die nicht mehr zum Filter passen, werden
    /// entfernt (Prävention statt Speicher-Validierung, analog <see cref="TitelverteidigerFilterAnwenden"/>),
    /// danach die Liste abgeglichen.</summary>
    private void GastgeberFilterAnwenden()
    {
        foreach (var zeile in GastgeberZeilen.Where(z => z.Mannschaft is not null && !_filter.Passt(z.Mannschaft)).ToList())
            GastgeberZeilen.Remove(zeile);
        GastgeberOptionenSynchronisieren();
    }

    public List<int> GastgeberIds() => GewaehlteGastgeber().Select(m => m.Id).Distinct().ToList();

    // Zeilen ohne gewählte Mannschaft zählen nicht.
    private IEnumerable<MannschaftAuswahl> GewaehlteGastgeber() =>
        GastgeberZeilen.Select(z => z.Mannschaft).OfType<MannschaftAuswahl>();

    // ── Aufklappbarer Bereich (Issue #399) ────────────────────────────────────
    [ObservableProperty] private bool _offen;

    public AbschnittKopf Kopf => AbschnittKopf.Aus("TITELVERTEIDIGER & GASTGEBER",
    [
        Titelverteidiger?.Wert is not null ? $"Titelverteidiger: {Titelverteidiger.Anzeige}" : null,
        GastgeberKopfTeil()
    ]);

    // Bis zu drei Gastgeber stehen mit Namen in der Kopfzeile, darüber nur ihre Anzahl.
    private string? GastgeberKopfTeil()
    {
        var gastgeber = GewaehlteGastgeber().Distinct().ToList();
        return gastgeber.Count switch
        {
            0    => null,
            <= 3 => $"Gastgeber: {string.Join(", ", gastgeber.Select(m => m.Name))}",
            _    => $"Gastgeber: {gastgeber.Count}"
        };
    }

    public void AufklappzustandSetzen() => Offen = Titelverteidiger?.Wert is not null || GastgeberZeilen.Count > 0;

    public TitelGastgeberViewModel(MannschaftService mannschaftService, ILogger<TitelGastgeberViewModel> logger)
    {
        _mannschaftService = mannschaftService;
        _logger = logger;
        GastgeberZeilenBeobachten();
    }

    /// <summary>Kandidaten für „Gastgeber“ und „Titelverteidiger“. Eigener Vorgang im Ablaufprotokoll (#419):
    /// der Anteil der Mannschaftsabfrage am Öffnen der Seite ist nur so vom Rest der Stammdaten zu trennen.</summary>
    public async Task LadenAsync()
    {
        IReadOnlyList<MannschaftAuswahl> mannschaften;
        using (_logger.Vorgang("Turnierkopf: Mannschaften für Gastgeber/Titelverteidiger abfragen"))
            mannschaften = await _mannschaftService.AlleAlsAuswahlAbrufenAsync();
        _alleMannschaften = mannschaften.OrderBy(m => m.Name).ToList();
        GastgeberOptionenSynchronisieren();
        _titelverteidigerKandidaten = _alleMannschaften
            .Select(m => (new TitelverteidigerOption(m.Name, m.Id), m))
            .ToList();
        TitelverteidigerOptionenSynchronisieren();
    }

    /// <summary>Der Nutzer hat den Auslosungsfilter geändert: nicht mehr passende Auswahl abräumen, beide
    /// Auswahllisten abgleichen.</summary>
    public void FilterAnwenden(Auslosungsfilter filter)
    {
        _filter = filter;
        TitelverteidigerFilterAnwenden();
        GastgeberFilterAnwenden();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName != nameof(Titelverteidiger)) return;
        OnPropertyChanged(nameof(Kopf));
        if (_populiertGerade == 0) Geaendert?.Invoke();
    }
}
