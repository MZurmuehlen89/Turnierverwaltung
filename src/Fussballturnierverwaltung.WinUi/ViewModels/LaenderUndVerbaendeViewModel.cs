using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Seite mit den Reitern Länder und Verbände; jeder Reiter ist ein eigener Editor (#606).</summary>
public partial class LaenderUndVerbaendeViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly RanglisteService _ranglisteService;

    public LandEditorViewModel LandEditor { get; }
    public VerbandEditorViewModel VerbandEditor { get; }

    // ── Aktiver Tab ───────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeuBefehl))]
    [NotifyPropertyChangedFor(nameof(SpeichernBefehl))]
    [NotifyPropertyChangedFor(nameof(AbbrechenBefehl))]
    [NotifyPropertyChangedFor(nameof(LoeschenBefehl))]
    private int _aktiverTabIndex;

    [ObservableProperty] private bool _istLadend;

    // ── IRibbonAware ──────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Länder & Verbände";
    public ICommand? NeuBefehl => AktiverTabIndex == 0 ? LandEditor.NeuCommand : VerbandEditor.NeuCommand;
    public ICommand? SpeichernBefehl => AktiverTabIndex == 0 ? LandEditor.SpeichernCommand : VerbandEditor.SpeichernCommand;
    public ICommand? AbbrechenBefehl => AktiverTabIndex == 0 ? LandEditor.AbbrechenCommand : VerbandEditor.AbbrechenCommand;
    public ICommand? LoeschenBefehl => AktiverTabIndex == 0 ? LandEditor.LoeschenCommand : VerbandEditor.LoeschenCommand;

    public LaenderUndVerbaendeViewModel(
        LandEditorViewModel landEditor, VerbandEditorViewModel verbandEditor, RanglisteService ranglisteService)
    {
        LandEditor = landEditor;
        VerbandEditor = verbandEditor;
        _ranglisteService = ranglisteService;
        LandEditor.NeuLadenUeber(LadenAsync);
        VerbandEditor.NeuLadenUeber(LadenAsync);
    }

    public override async Task AktivierenAsync() => await LadenAsync();

    private async Task LadenAsync()
    {
        IstLadend = true;
        try
        {
            var ranglisten = await _ranglisteService.AlleOhneEintraegeAbrufenAsync();
            IReadOnlyList<RanglistenOption> ranglistenOptionen =
                [RanglistenOption.Keine, .. ranglisten.OrderBy(r => r.Name).Select(r => new RanglistenOption(r.Name, r.Id))];

            await VerbandEditor.LadenAsync(ranglistenOptionen);
            await LandEditor.LadenAsync(VerbandEditor.Verbaende, ranglistenOptionen);
        }
        finally
        {
            IstLadend = false;
        }
    }

    // Beide Reiter zeigten früher dieselbe Fehlermeldung – sie gehört zum Reiter, in dem sie entstand (#559)
    partial void OnAktiverTabIndexChanged(int value)
    {
        LandEditor.MeldungVerwerfen();
        VerbandEditor.MeldungVerwerfen();
    }

    // Unabhaengig vom aktiven Tab: auch das inaktive Formular wuerde bei Navigation verloren gehen.
    public bool HatUngespeicherteAenderungen =>
        LandEditor.HatUngespeicherteAenderungen || VerbandEditor.HatUngespeicherteAenderungen;
}
