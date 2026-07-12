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
                EintragErstellen("Turniere",           () => _navigation.NavigiereZuAsync<TurnierInstanzViewModel>()),
                EintragErstellen("Runden",             () => _navigation.NavigiereZuAsync<RundenEditorViewModel>()),
                EintragErstellen("Auslosung",          () => _navigation.NavigiereZuAsync<AuslosungViewModel>()),
                EintragErstellen("Spielplan & Baum",   () => _navigation.NavigiereZuAsync<TurnierBereichViewModel>()),
                EintragErstellen("Simulation",         () => _navigation.NavigiereZuAsync<SimulationsViewModel>()),
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
                               nameof(IRibbonAware.LoeschenBefehl))
        {
            OnPropertyChanged(nameof(HatRibbonBereich));
            OnPropertyChanged(nameof(RibbonTabTitel));
            OnPropertyChanged(nameof(RibbonNeuBefehl));
            OnPropertyChanged(nameof(RibbonSpeichernBefehl));
            OnPropertyChanged(nameof(RibbonAbbrechenBefehl));
            OnPropertyChanged(nameof(RibbonLoeschenBefehl));
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
