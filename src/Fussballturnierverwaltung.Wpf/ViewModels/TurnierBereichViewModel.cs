using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

public partial class TurnierBereichViewModel : ViewModelBase, IRibbonAware
{
    public SpielplanViewModel   SpielplanVm   { get; }
    public TurnierBaumViewModel TurnierBaumVm { get; }
    public SimulationsViewModel SimulationsVm { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RibbonTabTitel))]
    [NotifyPropertyChangedFor(nameof(NeuBefehl))]
    [NotifyPropertyChangedFor(nameof(SpeichernBefehl))]
    [NotifyPropertyChangedFor(nameof(AbbrechenBefehl))]
    [NotifyPropertyChangedFor(nameof(LoeschenBefehl))]
    private int _aktiverTabIndex;

    public TurnierBereichViewModel(
        SpielplanViewModel   spielplanVm,
        TurnierBaumViewModel turnierBaumVm,
        SimulationsViewModel simulationsVm)
    {
        SpielplanVm   = spielplanVm;
        TurnierBaumVm = turnierBaumVm;
        SimulationsVm = simulationsVm;
    }

    public override async Task AktivierenAsync()
    {
        await Task.WhenAll(
            SpielplanVm.AktivierenAsync(),
            TurnierBaumVm.AktivierenAsync(),
            SimulationsVm.AktivierenAsync());
    }

    private IRibbonAware? AktivesRibbonVm() => AktiverTabIndex switch
    {
        1 => TurnierBaumVm,
        2 => SimulationsVm,
        _ => SpielplanVm,
    };

    // ── IRibbonAware ──────────────────────────────────────────────────────────
    public string  RibbonTabTitel   => AktivesRibbonVm()?.RibbonTabTitel ?? "Turnier";
    public ICommand? NeuBefehl      => AktivesRibbonVm()?.NeuBefehl;
    public ICommand? SpeichernBefehl  => AktivesRibbonVm()?.SpeichernBefehl;
    public ICommand? AbbrechenBefehl  => AktivesRibbonVm()?.AbbrechenBefehl;
    public ICommand? LoeschenBefehl   => AktivesRibbonVm()?.LoeschenBefehl;
}
