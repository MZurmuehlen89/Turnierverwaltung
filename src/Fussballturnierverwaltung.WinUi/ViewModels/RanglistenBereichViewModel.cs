using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Seite „Ranglisten“ (#477) mit den Reitern „Ranglisten“, „Import“ und „Quellen“. Jeder Reiter behält seinen
/// Zustand beim Reiterwechsel; die Befehlsleiste gehört dem aktiven Reiter (wie Turnierliste). Die Seite verbindet die
/// Reiter: Import → Quelle bearbeiten, Rangliste → aus Quelle aktualisieren, Import fertig → Rangliste anzeigen.</summary>
public partial class RanglistenBereichViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private const int RANGLISTEN_TAB = 0;
    private const int IMPORT_TAB = 1;
    private const int QUELLEN_TAB = 2;

    private static readonly HashSet<string> _ribbonEigenschaften =
    [
        nameof(IRibbonAware.NeuBefehl), nameof(IRibbonAware.SpeichernBefehl), nameof(IRibbonAware.AbbrechenBefehl),
        nameof(IRibbonAware.LoeschenBefehl), nameof(IRibbonAware.RibbonZusatzGruppen),
        nameof(IRibbonAware.HatStandardAktionen), nameof(IRibbonAware.HatLoeschenAktion)
    ];

    private readonly IDialogService _dialogService;

    public RanglistenViewModel Ranglisten { get; }
    public RanglistenImportViewModel Import { get; }
    public RanglistenQuellenViewModel Quellen { get; }

    [ObservableProperty] private int _aktiverTabIndex;

    private IRibbonAware AktivesTab => AktiverTabIndex switch
    {
        IMPORT_TAB => Import,
        QUELLEN_TAB => Quellen,
        _ => Ranglisten
    };

    public RanglistenBereichViewModel(
        RanglistenViewModel ranglisten,
        RanglistenImportViewModel import,
        RanglistenQuellenViewModel quellen,
        IDialogService dialogService)
    {
        Ranglisten = ranglisten;
        Import = import;
        Quellen = quellen;
        _dialogService = dialogService;
        Ranglisten.PropertyChanged += KindEigenschaftGeaendert;
        Import.PropertyChanged += KindEigenschaftGeaendert;
        Quellen.PropertyChanged += KindEigenschaftGeaendert;

        Import.QuelleBearbeitenAngefordert += async (_, quelleId) => await QuelleBearbeitenAsync(quelleId);
        Import.Importiert += async (_, ranglisteId) => await ImportiertAsync(ranglisteId);
        Ranglisten.AusQuelleAktualisierenAngefordert += async (_, rangliste) => await AusQuelleAktualisierenAsync(rangliste);
    }

    // Nacheinander: alle Reiter teilen sich denselben Scoped-DbContext.
    public override async Task AktivierenAsync()
    {
        await Ranglisten.AktivierenAsync();
        await Quellen.LadenAsync();
        Import.QuellenSetzen(Quellen.Quellen);
    }

    private void KindEigenschaftGeaendert(object? sender, PropertyChangedEventArgs e)
    {
        if (ReferenceEquals(sender, AktivesTab) && e.PropertyName is not null && _ribbonEigenschaften.Contains(e.PropertyName))
            OnPropertyChanged(e.PropertyName);
    }

    partial void OnAktiverTabIndexChanged(int value)
    {
        // Im Reiter „Quellen“ angelegte oder gelöschte Quellen erscheinen so sofort im Import.
        if (value == IMPORT_TAB) Import.QuellenSetzen(Quellen.Quellen);
        foreach (var eigenschaft in _ribbonEigenschaften)
            OnPropertyChanged(eigenschaft);
    }

    // ── Verbindungen zwischen den Reitern ────────────────────────────────────

    private async Task QuelleBearbeitenAsync(int quelleId)
    {
        var andereQuelleOffen = Quellen.Ausgewaehlte?.Id != quelleId;
        if (andereQuelleOffen && Quellen.HatUngespeicherteAenderungen
            && !await _dialogService.BestaetigenAsync("Quelle bearbeiten",
                "Im Reiter „Quellen“ gibt es ungespeicherte Änderungen. Verwerfen und diese Quelle öffnen?"))
            return;

        if (andereQuelleOffen) Quellen.Auswaehlen(quelleId);
        AktiverTabIndex = QUELLEN_TAB;
    }

    private async Task AusQuelleAktualisierenAsync(Rangliste rangliste)
    {
        if (Import.HatUngespeicherteAenderungen
            && !await _dialogService.BestaetigenAsync("Aus Quelle aktualisieren",
                "Im Reiter „Import“ liegt eine abgerufene, nicht importierte Vorschau. Verwerfen?"))
            return;

        AktiverTabIndex = IMPORT_TAB;
        await Import.FuerRanglisteAbrufenAsync(rangliste);
    }

    private async Task ImportiertAsync(int ranglisteId)
    {
        if (Ranglisten.HatUngespeicherteAenderungen
            && !await _dialogService.BestaetigenAsync("Importierte Rangliste anzeigen",
                "Im Reiter „Ranglisten“ gibt es ungespeicherte Änderungen. Verwerfen und die importierte Rangliste anzeigen?"))
            return;

        await Ranglisten.AnzeigenAsync(ranglisteId);
        AktiverTabIndex = RANGLISTEN_TAB;
    }

    // ── IRibbonAware — Delegation an den aktiven Reiter ───────────────────────
    public string RibbonTabTitel => "Ranglisten";
    public ICommand? NeuBefehl => AktivesTab.NeuBefehl;
    public ICommand? SpeichernBefehl => AktivesTab.SpeichernBefehl;
    public ICommand? AbbrechenBefehl => AktivesTab.AbbrechenBefehl;
    public ICommand? LoeschenBefehl => AktivesTab.LoeschenBefehl;
    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen => AktivesTab.RibbonZusatzGruppen;
    public bool HatStandardAktionen => AktivesTab.HatStandardAktionen;
    public bool HatLoeschenAktion => AktivesTab.HatLoeschenAktion;

    // Über alle Reiter, da jeder seinen Stand beim Reiterwechsel behält.
    public bool HatUngespeicherteAenderungen =>
        Ranglisten.HatUngespeicherteAenderungen || Import.HatUngespeicherteAenderungen || Quellen.HatUngespeicherteAenderungen;
}
