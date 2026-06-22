using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

public partial class TurnierBereichViewModel : ViewModelBase, IRibbonAware
{
    public SpielplanViewModel   SpielplanVm   { get; }
    public TurnierBaumViewModel TurnierBaumVm { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RibbonTabTitel))]
    [NotifyPropertyChangedFor(nameof(NeuBefehl))]
    [NotifyPropertyChangedFor(nameof(SpeichernBefehl))]
    [NotifyPropertyChangedFor(nameof(AbbrechenBefehl))]
    [NotifyPropertyChangedFor(nameof(LoeschenBefehl))]
    private int _aktiverTabIndex;

    public TurnierBereichViewModel(
        SpielplanViewModel   spielplanVm,
        TurnierBaumViewModel turnierBaumVm)
    {
        SpielplanVm   = spielplanVm;
        TurnierBaumVm = turnierBaumVm;
    }

    public override async Task AktivierenAsync()
    {
        await SpielplanVm.AktivierenAsync();
        await TurnierBaumVm.AktivierenAsync();
    }

    private IRibbonAware? AktivesRibbonVm() => AktiverTabIndex switch
    {
        1 => TurnierBaumVm,
        _ => SpielplanVm,
    };

    // ── IRibbonAware ──────────────────────────────────────────────────────────
    public string  RibbonTabTitel    => AktivesRibbonVm()?.RibbonTabTitel ?? "Turnier";
    public ICommand? NeuBefehl       => AktivesRibbonVm()?.NeuBefehl;
    public ICommand? SpeichernBefehl => AktivesRibbonVm()?.SpeichernBefehl;
    public ICommand? AbbrechenBefehl => AktivesRibbonVm()?.AbbrechenBefehl;
    public ICommand? LoeschenBefehl  => AktivesRibbonVm()?.LoeschenBefehl;
}
