using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

public partial class TurnierBereichViewModel : ViewModelBase, IRibbonAware
{
    private readonly TurnierInstanzService _instanzService;

    public SpielplanViewModel   SpielplanVm   { get; }
    public TurnierBaumViewModel TurnierBaumVm { get; }

    [ObservableProperty] private ObservableCollection<TurnierInstanz> _instanzen = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatInstanz))]
    private TurnierInstanz? _ausgewaehlteInstanz;

    public bool HatInstanz => AusgewaehlteInstanz is not null;

    public TurnierBereichViewModel(
        TurnierInstanzService instanzService,
        SpielplanViewModel   spielplanVm,
        TurnierBaumViewModel turnierBaumVm)
    {
        _instanzService = instanzService;
        SpielplanVm     = spielplanVm;
        TurnierBaumVm   = turnierBaumVm;
    }

    public override async Task AktivierenAsync()
    {
        var liste = await _instanzService.AlleAbrufenAsync();
        Instanzen.Clear();
        foreach (var i in liste) Instanzen.Add(i);

        if (AusgewaehlteInstanz is not null && !Instanzen.Contains(AusgewaehlteInstanz))
            AusgewaehlteInstanz = null;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(AusgewaehlteInstanz))
            _ = BeiInstanzAenderungAsync();
    }

    private async Task BeiInstanzAenderungAsync()
    {
        var instanz = AusgewaehlteInstanz;
        await SpielplanVm.LadeInstanzAsync(instanz);
        await TurnierBaumVm.LadeInstanzAsync(instanz);
    }

    // ── IRibbonAware — immer an SpielplanVm delegiert ────────────────────────
    public string   RibbonTabTitel  => SpielplanVm.RibbonTabTitel;
    public ICommand? NeuBefehl      => SpielplanVm.NeuBefehl;
    public ICommand? SpeichernBefehl => SpielplanVm.SpeichernBefehl;
    public ICommand? AbbrechenBefehl => SpielplanVm.AbbrechenBefehl;
    public ICommand? LoeschenBefehl  => SpielplanVm.LoeschenBefehl;
}
