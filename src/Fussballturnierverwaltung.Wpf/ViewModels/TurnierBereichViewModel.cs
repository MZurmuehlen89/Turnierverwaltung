using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Wpf.Navigation;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

public partial class TurnierBereichViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly TurnierInstanzService _instanzService;

    public SpielplanViewModel   SpielplanVm   { get; }
    public TurnierBaumViewModel TurnierBaumVm { get; }

    [ObservableProperty] private ObservableCollection<TurnierInstanz> _instanzen = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatInstanz))]
    private TurnierInstanz? _ausgewaehlteInstanz;

    public bool HatInstanz => AusgewaehlteInstanz is not null;

    // Ein Turnierbaum ergibt nur bei mehreren aufeinanderfolgenden Runden Sinn — bei genau einer
    // Runde (oder keiner) entfaellt die Split-Ansicht komplett zugunsten des vollen Spielplans.
    public bool ZeigeSplitAnsicht => SpielplanVm.Runden.Count > 1;

    [ObservableProperty] private string? _fortschrittAnzeige;

    public TurnierBereichViewModel(
        TurnierInstanzService instanzService,
        SpielplanViewModel   spielplanVm,
        TurnierBaumViewModel turnierBaumVm)
    {
        _instanzService = instanzService;
        SpielplanVm     = spielplanVm;
        TurnierBaumVm   = turnierBaumVm;

        TurnierBaumVm.RundeAngeklickt += (_, rundeId) =>
        {
            var runde = SpielplanVm.Runden.FirstOrDefault(r => r.Id == rundeId);
            if (runde is not null)
                SpielplanVm.AusgewaehlteRunde = runde;
        };

        SpielplanVm.SpielstandGeaendertAsync = FortschrittAktualisierenAsync;
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
        OnPropertyChanged(nameof(ZeigeSplitAnsicht));
        await TurnierBaumVm.LadeInstanzAsync(instanz);
        await FortschrittAktualisierenAsync();
    }

    private async Task FortschrittAktualisierenAsync()
    {
        if (AusgewaehlteInstanz is not { } instanz)
        {
            FortschrittAnzeige = null;
            return;
        }

        var fortschritt = await _instanzService.FortschrittAbrufenAsync(instanz.Id);
        FortschrittAnzeige = fortschritt.RundenGesamt == 0
            ? null
            : $"Runde {Math.Min(fortschritt.RundenAbgeschlossen + 1, fortschritt.RundenGesamt)} von {fortschritt.RundenGesamt} · " +
              $"{fortschritt.SpieleGespielt} / {fortschritt.SpieleGesamt} Spiele gespielt";
    }

    // ── IRibbonAware — immer an SpielplanVm delegiert ────────────────────────
    public string   RibbonTabTitel  => SpielplanVm.RibbonTabTitel;
    public ICommand? NeuBefehl      => SpielplanVm.NeuBefehl;
    public ICommand? SpeichernBefehl => SpielplanVm.SpeichernBefehl;
    public ICommand? AbbrechenBefehl => SpielplanVm.AbbrechenBefehl;
    public ICommand? LoeschenBefehl  => SpielplanVm.LoeschenBefehl;

    public bool HatUngespeicherteAenderungen => SpielplanVm.HatUngespeicherteAenderungen;
}
