using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.WinUi.Ribbon;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Spike (#278): minimaler Nachbau des WPF-MainViewModel-Proxy-Musters (CommandBar-Bindings gegen das
/// jeweils aktive ViewModel). Da der Spike nur eine einzige View testet, gibt es keine echte Navigation mit
/// DI-Scopes wie im WPF-Original — AktuellesViewModel wird einmalig gesetzt, das Proxy-Bindungsmuster selbst
/// ist aber identisch zum Original nachgebaut.</summary>
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private ViewModelBase _aktuellesViewModel;

    /// <summary>x:Bind ist stark typisiert und kann AktuellesViewModel (ViewModelBase) nicht direkt casten —
    /// da der Spike nur eine einzige View testet, reicht diese zusätzliche typed Property statt eines echten
    /// ViewModel-zu-View-Locators wie im WPF-Original.</summary>
    public TurnierVorlageViewModel TurnierVorlage { get; }

    public MainViewModel(TurnierVorlageViewModel turnierVorlageViewModel)
    {
        _aktuellesViewModel = turnierVorlageViewModel;
        TurnierVorlage = turnierVorlageViewModel;
        _aktuellesViewModel.PropertyChanged += OnAktuellesViewModelPropertyChanged;
        _ = _aktuellesViewModel.AktivierenAsync();
    }

    private void OnAktuellesViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HatRibbonBereich));
        OnPropertyChanged(nameof(RibbonTabTitel));
        OnPropertyChanged(nameof(RibbonNeuBefehl));
        OnPropertyChanged(nameof(RibbonSpeichernBefehl));
        OnPropertyChanged(nameof(RibbonAbbrechenBefehl));
        OnPropertyChanged(nameof(RibbonLoeschenBefehl));
        OnPropertyChanged(nameof(HatStandardAktionen));
        OnPropertyChanged(nameof(HatLoeschenAktion));
    }

    public bool HatRibbonBereich => AktuellesViewModel is IRibbonAware ribbon
        && (ribbon.HatStandardAktionen || ribbon.HatLoeschenAktion);

    public string RibbonTabTitel => (AktuellesViewModel as IRibbonAware)?.RibbonTabTitel ?? string.Empty;
    public ICommand? RibbonNeuBefehl => (AktuellesViewModel as IRibbonAware)?.NeuBefehl;
    public ICommand? RibbonSpeichernBefehl => (AktuellesViewModel as IRibbonAware)?.SpeichernBefehl;
    public ICommand? RibbonAbbrechenBefehl => (AktuellesViewModel as IRibbonAware)?.AbbrechenBefehl;
    public ICommand? RibbonLoeschenBefehl => (AktuellesViewModel as IRibbonAware)?.LoeschenBefehl;
    public bool HatStandardAktionen => (AktuellesViewModel as IRibbonAware)?.HatStandardAktionen ?? false;
    public bool HatLoeschenAktion => (AktuellesViewModel as IRibbonAware)?.HatLoeschenAktion ?? false;
}
