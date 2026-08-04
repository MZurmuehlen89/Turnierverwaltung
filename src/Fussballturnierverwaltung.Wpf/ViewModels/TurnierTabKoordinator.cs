using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Wpf.Navigation;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

/// <summary>
/// Generische Tab-Container-Mechanik für die beiden Turnier-Composite-Seiten
/// (TurnierDefinitionViewModel/TurnierDurchfuehrungViewModel, Issue #172) — Instanz-
/// Auswahl/-Filterung, aktives-Tab-Tracking, IRibbonAware-Weiterleitung ans aktive Kind.
/// Bewusst eine reine ObservableObject-Helferklasse statt ein weiteres ViewModelBase-
/// Vererbungslevel (Precedent: RanglistenEintragZeile in RanglisteViewModel.cs).
/// </summary>
public partial class TurnierTabKoordinator : ObservableObject, ITurnierAuswahlHost
{
    private readonly TurnierInstanzService _instanzService;
    private readonly AusgewaehlteTurnierInstanzService _turnierAuswahlService;

    public ViewModelBase[] Tabs { get; }

    [ObservableProperty] private ObservableCollection<TurnierInstanz> _instanzen = [];

    [ObservableProperty] private ObservableCollection<TurnierInstanz> _instanzenGefiltert = [];

    [ObservableProperty] private string? _instanzSuchbegriff;

    [ObservableProperty] private TurnierInstanz? _ausgewaehlteInstanz;

    [ObservableProperty] private int _aktiverTabIndex;

    public ViewModelBase AktivesTab => Tabs[AktiverTabIndex];

    /// <summary>
    /// Optionaler Hook für composite-spezifische Korrekturen des aktiven Tabs nach einer
    /// Instanz-/Tab-Auswahl (z.B. Runden-Tab bei TurnierDefinitionViewModel). Gibt zurück,
    /// ob ein Tab-Wechsel ausgelöst wurde. TurnierDurchfuehrungViewModel lässt dies null.
    /// </summary>
    public Func<bool>? NachAuswahlKorrigieren { get; set; }

    /// <summary>Feuert bei jeder Ribbon-relevanten Änderung des aktiven Kinds oder bei
    /// Tab-Wechsel — jede Composite-VM abonniert dies einmal im Konstruktor und reicht ihre
    /// eigenen OnPropertyChanged(...)-Aufrufe durch.</summary>
    public event EventHandler? AktivesTabZustandGeaendert;

    public TurnierTabKoordinator(
        TurnierInstanzService instanzService,
        AusgewaehlteTurnierInstanzService turnierAuswahlService,
        ViewModelBase[] tabs)
    {
        _instanzService = instanzService;
        _turnierAuswahlService = turnierAuswahlService;
        Tabs = tabs;
    }

    private bool _initialisierungAktiv;

    public async Task AktivierenAsync()
    {
        _initialisierungAktiv = true;

        var liste = await _instanzService.AlleAbrufenAsync();
        Instanzen.Clear();
        foreach (var i in liste) Instanzen.Add(i);
        AktualisiereInstanzenGefiltert();

        if (AusgewaehlteInstanz is not null && !Instanzen.Contains(AusgewaehlteInstanz))
            AusgewaehlteInstanz = null;
        else if (AusgewaehlteInstanz is null)
            AusgewaehlteInstanz = Instanzen.FirstOrDefault(
                i => i.Id == _turnierAuswahlService.TurnierInstanzId);

        NachAuswahlKorrigieren?.Invoke();

        _initialisierungAktiv = false;

        AktiviereAktivenTab(vorherigesTab: null);
        await AktivesTabAktivierenAsync();
    }

    partial void OnAusgewaehlteInstanzChanged(TurnierInstanz? value)
    {
        _turnierAuswahlService.TurnierInstanzId = value?.Id;
        if (_initialisierungAktiv) return;

        var korrigiert = NachAuswahlKorrigieren?.Invoke() ?? false;
        if (!korrigiert)
            _ = AktivesTabAktivierenAsync();
    }

    partial void OnAktiverTabIndexChanged(int value)
    {
        if (_initialisierungAktiv) return;
        AktiviereAktivenTab(vorherigesTab: _vorherigesTab);
        _ = AktivesTabAktivierenAsync();
    }

    partial void OnInstanzSuchbegriffChanged(string? value) => AktualisiereInstanzenGefiltert();

    private void AktualisiereInstanzenGefiltert()
    {
        var begriff = InstanzSuchbegriff;
        var treffer = string.IsNullOrWhiteSpace(begriff)
            ? Instanzen
            : Instanzen.Where(i =>
                (i.Name?.Contains(begriff, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (i.SaisonOderJahr?.Contains(begriff, StringComparison.OrdinalIgnoreCase) ?? false));

        InstanzenGefiltert = new ObservableCollection<TurnierInstanz>(treffer);
    }

    private ViewModelBase? _vorherigesTab;

    private void AktiviereAktivenTab(ViewModelBase? vorherigesTab)
    {
        if (vorherigesTab is not null)
            vorherigesTab.PropertyChanged -= OnAktivemTabPropertyChanged;

        _vorherigesTab = Tabs[AktiverTabIndex];
        _vorherigesTab.PropertyChanged += OnAktivemTabPropertyChanged;

        AktivesTabZustandGeaendert?.Invoke(this, EventArgs.Empty);
    }

    private async Task AktivesTabAktivierenAsync() => await Tabs[AktiverTabIndex].AktivierenAsync();

    private void OnAktivemTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IRibbonAware.RibbonTabTitel) or
                               nameof(IRibbonAware.NeuBefehl) or
                               nameof(IRibbonAware.SpeichernBefehl) or
                               nameof(IRibbonAware.AbbrechenBefehl) or
                               nameof(IRibbonAware.LoeschenBefehl) or
                               nameof(IRibbonAware.RibbonZusatzGruppen) or
                               nameof(Navigation.IHatUngespeicherteAenderungen.HatUngespeicherteAenderungen))
        {
            AktivesTabZustandGeaendert?.Invoke(this, EventArgs.Empty);
        }
    }
}
