using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Helpers;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly ThemeService _themeService;

    [ObservableProperty]
    private bool _istDunkelModus;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatRibbonBereich))]
    [NotifyPropertyChangedFor(nameof(RibbonTabTitel))]
    [NotifyPropertyChangedFor(nameof(RibbonNeuBefehl))]
    [NotifyPropertyChangedFor(nameof(RibbonSpeichernBefehl))]
    [NotifyPropertyChangedFor(nameof(RibbonAbbrechenBefehl))]
    [NotifyPropertyChangedFor(nameof(RibbonLoeschenBefehl))]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    [NotifyPropertyChangedFor(nameof(RibbonNeuUnterpunkte))]
    [NotifyPropertyChangedFor(nameof(HatStandardAktionen))]
    [NotifyPropertyChangedFor(nameof(HatLoeschenAktion))]
    private ViewModelBase? _aktuellesViewModel;

    public bool HatRibbonBereich => AktuellesViewModel is IRibbonAware ribbonAware
        && (ribbonAware.HatStandardAktionen || ribbonAware.HatLoeschenAktion || ribbonAware.RibbonZusatzGruppen.Count > 0);
    public string RibbonTabTitel => (AktuellesViewModel as IRibbonAware)?.RibbonTabTitel ?? string.Empty;
    public ICommand? RibbonNeuBefehl => (AktuellesViewModel as IRibbonAware)?.NeuBefehl;
    public ICommand? RibbonSpeichernBefehl => (AktuellesViewModel as IRibbonAware)?.SpeichernBefehl;
    public ICommand? RibbonAbbrechenBefehl => (AktuellesViewModel as IRibbonAware)?.AbbrechenBefehl;
    public ICommand? RibbonLoeschenBefehl => (AktuellesViewModel as IRibbonAware)?.LoeschenBefehl;
    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen => (AktuellesViewModel as IRibbonAware)?.RibbonZusatzGruppen ?? [];
    public IReadOnlyList<RibbonAktion> RibbonNeuUnterpunkte => (AktuellesViewModel as IRibbonAware)?.NeuUnterpunkte ?? [];
    public bool HatStandardAktionen => (AktuellesViewModel as IRibbonAware)?.HatStandardAktionen ?? false;
    public bool HatLoeschenAktion   => (AktuellesViewModel as IRibbonAware)?.HatLoeschenAktion ?? false;

    public ObservableCollection<NavigationsGruppe> NavigationsGruppen { get; } = [];

    public MainViewModel(INavigationService navigation, ThemeService themeService)
    {
        _navigation = navigation;
        _themeService = themeService;
        IstDunkelModus = _themeService.AktuellerModus == ThemeModus.Dunkel;

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
                EintragErstellen("Länder & Verbände", () => _navigation.NavigiereZuAsync<LaenderUndVerbaendeViewModel>()),
                EintragErstellen("Regelwerke",        () => _navigation.NavigiereZuAsync<RegelwerkeViewModel>()),
                EintragErstellen("Ranglisten",        () => _navigation.NavigiereZuAsync<RanglistenViewModel>()),
            ]
        });

        NavigationsGruppen.Add(new NavigationsGruppe
        {
            Titel = "Turniere",
            Eintraege =
            [
                EintragErstellen("Turniere",               () => _navigation.NavigiereZuAsync<TurnierlisteViewModel>()),
                EintragErstellen("Turnierdurchführung",    () => _navigation.NavigiereZuAsync<TurnierdurchfuehrungViewModel>()),
                EintragErstellen("Einzelspiel-Simulation", () => _navigation.NavigiereZuAsync<EinzelspielSimulationsViewModel>()),
            ]
        });

        NavigationsGruppen.Add(new NavigationsGruppe
        {
            Titel = "Import / Export",
            Eintraege =
            [
                EintragErstellen("Import / Export", () => _navigation.NavigiereZuAsync<ImportExportViewModel>()),
            ]
        });

        NavigationsGruppen.Add(new NavigationsGruppe
        {
            Titel = "Wartung",
            Eintraege =
            [
                EintragErstellen("Datenbankwartung", () => _navigation.NavigiereZuAsync<DatenbankwartungViewModel>()),
            ]
        });
    }

    [RelayCommand]
    private void ThemeUmschalten()
    {
        _themeService.Umschalten();
        IstDunkelModus = _themeService.AktuellerModus == ThemeModus.Dunkel;
    }

    private void OnAktuellesViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IRibbonAware.RibbonTabTitel) or
                               nameof(IRibbonAware.NeuBefehl) or
                               nameof(IRibbonAware.SpeichernBefehl) or
                               nameof(IRibbonAware.AbbrechenBefehl) or
                               nameof(IRibbonAware.LoeschenBefehl) or
                               nameof(IRibbonAware.RibbonZusatzGruppen) or
                               nameof(IRibbonAware.NeuUnterpunkte) or
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
            OnPropertyChanged(nameof(RibbonNeuUnterpunkte));
            OnPropertyChanged(nameof(HatStandardAktionen));
            OnPropertyChanged(nameof(HatLoeschenAktion));
        }
    }

    private NavigationsEintrag EintragErstellen(string titel, Func<Task> aktion)
        => new() { Titel = titel, Befehl = new AsyncRelayCommand(aktion) };
}
