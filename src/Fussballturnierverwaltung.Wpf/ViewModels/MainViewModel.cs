using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Wpf.Navigation;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
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
    private ViewModelBase? _aktuellesViewModel;

    public bool HatRibbonBereich => AktuellesViewModel is IRibbonAware;
    public string RibbonTabTitel => (AktuellesViewModel as IRibbonAware)?.RibbonTabTitel ?? string.Empty;
    public ICommand? RibbonNeuBefehl => (AktuellesViewModel as IRibbonAware)?.NeuBefehl;
    public ICommand? RibbonSpeichernBefehl => (AktuellesViewModel as IRibbonAware)?.SpeichernBefehl;
    public ICommand? RibbonAbbrechenBefehl => (AktuellesViewModel as IRibbonAware)?.AbbrechenBefehl;
    public ICommand? RibbonLoeschenBefehl => (AktuellesViewModel as IRibbonAware)?.LoeschenBefehl;

    public ObservableCollection<NavigationsGruppe> NavigationsGruppen { get; } = [];

    public MainViewModel(INavigationService navigation)
    {
        _navigation = navigation;
        _navigation.AktuellesViewModelGeaendert += (_, _) =>
            AktuellesViewModel = _navigation.AktuellesViewModel;

        NavigationsGruppen.Add(new NavigationsGruppe
        {
            Titel = "Stammdaten",
            Eintraege =
            [
                EintragErstellen("Mannschaften",      () => _navigation.NavigiereZu<MannschaftenViewModel>()),
                EintragErstellen("Länder & Verbände", () => _navigation.NavigiereZu<LandUndVerbandViewModel>()),
                EintragErstellen("Regelwerke",        () => _navigation.NavigiereZu<RegelwerkViewModel>()),
                EintragErstellen("Turniervorlagen",   () => _navigation.NavigiereZu<TurnierVorlageViewModel>()),
            ]
        });

        NavigationsGruppen.Add(new NavigationsGruppe
        {
            Titel = "Bewegungsdaten",
            Eintraege =
            [
                EintragErstellen("Turniere",          () => _navigation.NavigiereZu<TurnierInstanzViewModel>()),
                EintragErstellen("Auslosung",         () => _navigation.NavigiereZu<AuslosungViewModel>()),
                EintragErstellen("Spielplan",         () => _navigation.NavigiereZu<SpielplanViewModel>()),
                EintragErstellen("Turnierbaum",       () => _navigation.NavigiereZu<TurnierBaumViewModel>()),
                EintragErstellen("Simulation",        () => _navigation.NavigiereZu<SimulationsViewModel>()),
            ]
        });

        NavigationsGruppen.Add(new NavigationsGruppe
        {
            Titel = "Import / Export",
            Eintraege =
            [
                EintragErstellen("Import / Export",   () => _navigation.NavigiereZu<ImportExportViewModel>()),
            ]
        });
    }

    private NavigationsEintrag EintragErstellen(string titel, Action aktion)
        => new() { Titel = titel, Befehl = new RelayCommand(aktion) };

    private void NavigiereZuPlatzhalter(string titel)
    {
        _navigation.NavigiereZu<PlatzhalterViewModel>();
        if (_navigation.AktuellesViewModel is PlatzhalterViewModel ph)
            ph.SeitenTitel = titel;
    }
}
