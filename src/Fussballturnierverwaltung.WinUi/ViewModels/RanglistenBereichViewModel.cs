using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Seite „Ranglisten“ (#477) mit den Reitern „Ranglisten“ und „Quellen“. Jeder Reiter behält seinen
/// Zustand beim Reiterwechsel; die Befehlsleiste gehört dem aktiven Reiter (wie Turnierliste).</summary>
public partial class RanglistenBereichViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private const int QUELLEN_TAB = 1;

    private static readonly HashSet<string> _ribbonEigenschaften =
    [
        nameof(IRibbonAware.NeuBefehl), nameof(IRibbonAware.SpeichernBefehl), nameof(IRibbonAware.AbbrechenBefehl),
        nameof(IRibbonAware.LoeschenBefehl), nameof(IRibbonAware.RibbonZusatzGruppen)
    ];

    public RanglistenViewModel Ranglisten { get; }
    public RanglistenQuellenViewModel Quellen { get; }

    [ObservableProperty] private int _aktiverTabIndex;

    private IRibbonAware AktivesTab => AktiverTabIndex == QUELLEN_TAB ? Quellen : Ranglisten;

    public RanglistenBereichViewModel(RanglistenViewModel ranglisten, RanglistenQuellenViewModel quellen)
    {
        Ranglisten = ranglisten;
        Quellen = quellen;
        Ranglisten.PropertyChanged += KindEigenschaftGeaendert;
        Quellen.PropertyChanged += KindEigenschaftGeaendert;
    }

    // Nacheinander: beide Reiter teilen sich denselben Scoped-DbContext.
    public override async Task AktivierenAsync()
    {
        await Ranglisten.AktivierenAsync();
        await Quellen.LadenAsync();
    }

    private void KindEigenschaftGeaendert(object? sender, PropertyChangedEventArgs e)
    {
        if (ReferenceEquals(sender, AktivesTab) && e.PropertyName is not null && _ribbonEigenschaften.Contains(e.PropertyName))
            OnPropertyChanged(e.PropertyName);
    }

    partial void OnAktiverTabIndexChanged(int value)
    {
        foreach (var eigenschaft in _ribbonEigenschaften)
            OnPropertyChanged(eigenschaft);
    }

    // ── IRibbonAware — Delegation an den aktiven Reiter ───────────────────────
    public string RibbonTabTitel => "Ranglisten";
    public ICommand? NeuBefehl => AktivesTab.NeuBefehl;
    public ICommand? SpeichernBefehl => AktivesTab.SpeichernBefehl;
    public ICommand? AbbrechenBefehl => AktivesTab.AbbrechenBefehl;
    public ICommand? LoeschenBefehl => AktivesTab.LoeschenBefehl;
    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen => AktivesTab.RibbonZusatzGruppen;

    // Über alle Reiter, da jeder seinen Stand beim Reiterwechsel behält.
    public bool HatUngespeicherteAenderungen =>
        Ranglisten.HatUngespeicherteAenderungen || Quellen.HatUngespeicherteAenderungen;
}
