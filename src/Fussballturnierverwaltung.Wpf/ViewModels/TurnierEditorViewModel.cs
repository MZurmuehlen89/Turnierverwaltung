using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Wpf.Navigation;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

/// <summary>
/// Äußere Komposition der 6 Bewegungsdaten-Editoren zu einem gemeinsamen "Turnier-Bereich"
/// mit geteiltem Turnier-Header und Sub-Navigation als Tabs (Issue #152). Die bestehende
/// TurnierBereichViewModel (Spielplan+Baum) bleibt unverändert und wird als Tab 4 eingebettet.
/// </summary>
public partial class TurnierEditorViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly TurnierInstanzService _instanzService;
    private readonly AusgewaehlteTurnierInstanzService _turnierAuswahlService;

    public RundenEditorViewModel   RundenVm         { get; }
    public AufstiegsregelViewModel AufstiegsregelnVm { get; }
    public VerbandsQuoteViewModel  VerbandsQuotenVm { get; }
    public AuslosungViewModel      AuslosungVm      { get; }
    public TurnierBereichViewModel SpielplanUndBaumVm { get; }
    public SimulationsViewModel    SimulationVm     { get; }

    private readonly ViewModelBase[] _tabs;

    [ObservableProperty] private ObservableCollection<TurnierInstanz> _instanzen = [];

    [ObservableProperty] private ObservableCollection<TurnierInstanz> _instanzenGefiltert = [];

    [ObservableProperty] private string? _instanzSuchbegriff;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RundenTabAktiv))]
    private TurnierInstanz? _ausgewaehlteInstanz;

    [ObservableProperty] private int _aktiverTabIndex;

    /// <summary>Runden dürfen nur für noch nicht gestartete Turniere bearbeitet werden — der
    /// Runden-Tab wird deaktiviert, sobald ein Turnier mit anderem Status gewählt ist.</summary>
    public bool RundenTabAktiv => AusgewaehlteInstanz is null || AusgewaehlteInstanz.Status == TurnierStatus.Geplant;

    public TurnierEditorViewModel(
        TurnierInstanzService instanzService,
        AusgewaehlteTurnierInstanzService turnierAuswahlService,
        RundenEditorViewModel rundenVm,
        AufstiegsregelViewModel aufstiegsregelnVm,
        VerbandsQuoteViewModel verbandsQuotenVm,
        AuslosungViewModel auslosungVm,
        TurnierBereichViewModel spielplanUndBaumVm,
        SimulationsViewModel simulationVm)
    {
        _instanzService = instanzService;
        _turnierAuswahlService = turnierAuswahlService;

        RundenVm           = rundenVm;
        AufstiegsregelnVm  = aufstiegsregelnVm;
        VerbandsQuotenVm   = verbandsQuotenVm;
        AuslosungVm        = auslosungVm;
        SpielplanUndBaumVm = spielplanUndBaumVm;
        SimulationVm       = simulationVm;

        _tabs = [RundenVm, AufstiegsregelnVm, VerbandsQuotenVm, AuslosungVm, SpielplanUndBaumVm, SimulationVm];

        // Nachbesserung zu #163: "Ausführliche Simulation" im Spielplan-Tab wechselt jetzt nur noch
        // auf den Simulation-Tab desselben, bereits offenen Turnier-Bereichs statt eine eigene
        // Navigation zur Simulations-Vollansicht auszulösen (siehe SpielplanViewModel).
        SpielplanUndBaumVm.AusfuehrlicheSimulationAngefordert += (_, vorauswahl) =>
        {
            SimulationVm.Vorauswahl = vorauswahl;
            AktiverTabIndex = Array.IndexOf(_tabs, SimulationVm);
        };
    }

    private bool _initialisierungAktiv;

    public override async Task AktivierenAsync()
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

        KorrigiereAktivenTabFallsUngueltig();

        _initialisierungAktiv = false;

        AktiviereAktivenTab(vorherigesTab: null);
        await AktivesTabAktivierenAsync();
    }

    partial void OnAusgewaehlteInstanzChanged(TurnierInstanz? value)
    {
        _turnierAuswahlService.TurnierInstanzId = value?.Id;
        if (_initialisierungAktiv) return;

        if (!KorrigiereAktivenTabFallsUngueltig())
            _ = AktivesTabAktivierenAsync();
    }

    partial void OnAktiverTabIndexChanged(int value)
    {
        if (_initialisierungAktiv) return;
        AktiviereAktivenTab(vorherigesTab: _vorherigesTab);
        _ = AktivesTabAktivierenAsync();
    }

    /// <summary>Springt vom Runden-Tab weg, falls das gewählte Turnier dort nicht mehr
    /// bearbeitbar ist (Statuswechsel). Gibt zurück, ob ein Tab-Wechsel ausgelöst wurde.</summary>
    private bool KorrigiereAktivenTabFallsUngueltig()
    {
        if (AktiverTabIndex != 0 || RundenTabAktiv) return false;
        AktiverTabIndex = 1;
        return true;
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

        _vorherigesTab = _tabs[AktiverTabIndex];
        _vorherigesTab.PropertyChanged += OnAktivemTabPropertyChanged;

        RibbonEigenschaftenGeaendertMelden();
    }

    private async Task AktivesTabAktivierenAsync() => await _tabs[AktiverTabIndex].AktivierenAsync();

    private void OnAktivemTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IRibbonAware.RibbonTabTitel) or
                               nameof(IRibbonAware.NeuBefehl) or
                               nameof(IRibbonAware.SpeichernBefehl) or
                               nameof(IRibbonAware.AbbrechenBefehl) or
                               nameof(IRibbonAware.LoeschenBefehl) or
                               nameof(IRibbonAware.RibbonZusatzGruppen) or
                               nameof(IHatUngespeicherteAenderungen.HatUngespeicherteAenderungen))
        {
            RibbonEigenschaftenGeaendertMelden();
        }
    }

    private void RibbonEigenschaftenGeaendertMelden()
    {
        OnPropertyChanged(nameof(NeuBefehl));
        OnPropertyChanged(nameof(SpeichernBefehl));
        OnPropertyChanged(nameof(AbbrechenBefehl));
        OnPropertyChanged(nameof(LoeschenBefehl));
        OnPropertyChanged(nameof(RibbonZusatzGruppen));
        OnPropertyChanged(nameof(HatStandardAktionen));
        OnPropertyChanged(nameof(HatLoeschenAktion));
        OnPropertyChanged(nameof(HatUngespeicherteAenderungen));
    }

    // ── IRibbonAware — an das aktive Tab-Kind delegiert; statischer Tab-Titel unabhängig
    //    vom aktiven Tab, da einzelne Kinder (z. B. Simulation) keinen eigenen Titel liefern ──
    public string RibbonTabTitel => "Turnier-Bereich";
    public ICommand? NeuBefehl       => (_tabs[AktiverTabIndex] as IRibbonAware)?.NeuBefehl;
    public ICommand? SpeichernBefehl => (_tabs[AktiverTabIndex] as IRibbonAware)?.SpeichernBefehl;
    public ICommand? AbbrechenBefehl => (_tabs[AktiverTabIndex] as IRibbonAware)?.AbbrechenBefehl;
    public ICommand? LoeschenBefehl  => (_tabs[AktiverTabIndex] as IRibbonAware)?.LoeschenBefehl;
    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen
        => (_tabs[AktiverTabIndex] as IRibbonAware)?.RibbonZusatzGruppen ?? [];
    // Nachbesserung zu #173/#164: reiner Typ-Check (_tabs[...] is IRibbonAware) ignorierte den
    // tatsächlichen Wert der Kind-Eigenschaft — sobald ein Kind IRibbonAware implementiert, aber
    // bewusst HatStandardAktionen=false liefert (z.B. SimulationsViewModel), muss das hier
    // durchgereicht werden statt pauschal true zu liefern.
    public bool HatStandardAktionen => (_tabs[AktiverTabIndex] as IRibbonAware)?.HatStandardAktionen ?? false;
    public bool HatLoeschenAktion   => (_tabs[AktiverTabIndex] as IRibbonAware)?.HatLoeschenAktion ?? false;

    public bool HatUngespeicherteAenderungen
        => (_tabs[AktiverTabIndex] as IHatUngespeicherteAenderungen)?.HatUngespeicherteAenderungen ?? false;
}
