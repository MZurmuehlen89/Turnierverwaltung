using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Wpf.Navigation;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatRibbonBereich))]
    [NotifyPropertyChangedFor(nameof(RibbonTabTitel))]
    [NotifyPropertyChangedFor(nameof(RibbonNeuBefehl))]
    [NotifyPropertyChangedFor(nameof(RibbonSpeichernBefehl))]
    [NotifyPropertyChangedFor(nameof(RibbonAbbrechenBefehl))]
    [NotifyPropertyChangedFor(nameof(RibbonLoeschenBefehl))]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    [NotifyPropertyChangedFor(nameof(HatStandardAktionen))]
    [NotifyPropertyChangedFor(nameof(HatLoeschenAktion))]
    private ViewModelBase? _aktuellesViewModel;

    // Issue #173: Ribbon-Seitentab bleibt nur sichtbar, wenn es tatsächlich etwas anzuzeigen gibt —
    // sonst (z.B. Simulation-Tab in TurnierEditorViewModel, weder Standardaktionen noch
    // Zusatzgruppen) ein leerer Tab-Reiter ohne jeden Button, verwirrender als gar kein Seitentab.
    public bool HatRibbonBereich => AktuellesViewModel is IRibbonAware ribbonAware
        && (ribbonAware.HatStandardAktionen || ribbonAware.HatLoeschenAktion || ribbonAware.RibbonZusatzGruppen.Count > 0);
    public string RibbonTabTitel => (AktuellesViewModel as IRibbonAware)?.RibbonTabTitel ?? string.Empty;
    public ICommand? RibbonNeuBefehl => (AktuellesViewModel as IRibbonAware)?.NeuBefehl;
    public ICommand? RibbonSpeichernBefehl => (AktuellesViewModel as IRibbonAware)?.SpeichernBefehl;
    public ICommand? RibbonAbbrechenBefehl => (AktuellesViewModel as IRibbonAware)?.AbbrechenBefehl;
    public ICommand? RibbonLoeschenBefehl => (AktuellesViewModel as IRibbonAware)?.LoeschenBefehl;
    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen => (AktuellesViewModel as IRibbonAware)?.RibbonZusatzGruppen ?? [];
    public bool HatStandardAktionen => (AktuellesViewModel as IRibbonAware)?.HatStandardAktionen ?? false;
    public bool HatLoeschenAktion   => (AktuellesViewModel as IRibbonAware)?.HatLoeschenAktion ?? false;

    public ObservableCollection<NavigationsGruppe> NavigationsGruppen { get; } = [];

    public MainViewModel(INavigationService navigation)
    {
        _navigation = navigation;
        _navigation.AktuellesViewModelGeaendert += (_, _) =>
        {
            if (AktuellesViewModel is not null)
                AktuellesViewModel.PropertyChanged -= OnAktuellesViewModelPropertyChanged;
            AktuellesViewModel = _navigation.AktuellesViewModel;
            if (AktuellesViewModel is not null)
                AktuellesViewModel.PropertyChanged += OnAktuellesViewModelPropertyChanged;
        };

        NavigationsGruppen.Add(new NavigationsGruppe
        {
            Titel = "Stammdaten",
            Eintraege =
            [
                EintragErstellen("Mannschaften",      () => _navigation.NavigiereZuAsync<MannschaftenViewModel>()),
                EintragErstellen("Länder & Verbände", () => _navigation.NavigiereZuAsync<LandUndVerbandViewModel>()),
                EintragErstellen("Regelwerke",        () => _navigation.NavigiereZuAsync<RegelwerkViewModel>()),
                EintragErstellen("Turniervorlagen",   () => _navigation.NavigiereZuAsync<TurnierVorlageViewModel>()),
                EintragErstellen("Ranglisten",        () => _navigation.NavigiereZuAsync<RanglisteViewModel>()),
            ]
        });

        NavigationsGruppen.Add(new NavigationsGruppe
        {
            Titel = "Bewegungsdaten",
            Eintraege =
            [
                EintragErstellen("Turniere",              () => _navigation.NavigiereZuAsync<TurnierInstanzViewModel>()),
                EintragErstellen("Turnier-Bereich",       () => _navigation.NavigiereZuAsync<TurnierEditorViewModel>()),
                EintragErstellen("Einzelspiel-Simulation", () => _navigation.NavigiereZuAsync<EinzelspielSimulationsViewModel>()),
            ]
        });

        NavigationsGruppen.Add(new NavigationsGruppe
        {
            Titel = "Import / Export",
            Eintraege =
            [
                EintragErstellen("Import / Export",   () => _navigation.NavigiereZuAsync<ImportExportViewModel>()),
            ]
        });
    }

    private void OnAktuellesViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IRibbonAware.RibbonTabTitel) or
                               nameof(IRibbonAware.NeuBefehl) or
                               nameof(IRibbonAware.SpeichernBefehl) or
                               nameof(IRibbonAware.AbbrechenBefehl) or
                               nameof(IRibbonAware.LoeschenBefehl) or
                               nameof(IRibbonAware.RibbonZusatzGruppen) or
                               nameof(IRibbonAware.HatStandardAktionen) or
                               nameof(IRibbonAware.HatLoeschenAktion))
        {
            OnPropertyChanged(nameof(HatRibbonBereich));
            OnPropertyChanged(nameof(RibbonTabTitel));
            OnPropertyChanged(nameof(RibbonNeuBefehl));
            OnPropertyChanged(nameof(RibbonSpeichernBefehl));
            OnPropertyChanged(nameof(RibbonAbbrechenBefehl));
            OnPropertyChanged(nameof(RibbonLoeschenBefehl));
            OnPropertyChanged(nameof(RibbonZusatzGruppen));
            OnPropertyChanged(nameof(HatStandardAktionen));
            OnPropertyChanged(nameof(HatLoeschenAktion));
        }
    }

    private NavigationsEintrag EintragErstellen(string titel, Func<Task> aktion)
        => new() { Titel = titel, Befehl = new AsyncRelayCommand(aktion) };

    private async Task NavigiereZuPlatzhalterAsync(string titel)
    {
        await _navigation.NavigiereZuAsync<PlatzhalterViewModel>();
        if (_navigation.AktuellesViewModel is PlatzhalterViewModel ph)
            ph.SeitenTitel = titel;
    }
}
