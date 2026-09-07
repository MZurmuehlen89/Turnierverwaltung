using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using Fussballturnierverwaltung.WinUi.Views;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>
/// "Turnierdurchführung"-Seite (#299) — Composite-Shell analog <see cref="TurnierlisteViewModel"/>, teilt
/// sich die Instanzauswahl aber über <see cref="AusgewaehlteTurnierInstanzService"/> mit "Turniere" (WPF-
/// Pendant: zwei unabhängige Menüpunkte "Turnierdefinition"/"Turnierdurchführung" mit gemeinsamer Auswahl).
/// Anders als die permanente Instanzliste in <see cref="TurnierlisteViewModel"/> (dort Kernnavigation der
/// Seite) nutzt diese Seite eine kompakte Kopfzeile mit Dialog-Auswahl (Precedent: WPF-Original
/// <c>TurnierAuswahlView</c>/<c>TurnierAuswahlDialog</c>) — Nutzerfeedback aus dem #300-Test: die permanente
/// Liste nahm hier unverhältnismäßig viel Platz gegenüber dem eigentlichen Arbeitsbereich ein, und der
/// Vorlagen/Turniere-Umschalter ergibt in der Turnierdurchführung ohnehin keinen Sinn (Vorlagen haben keine
/// Auslosung/Spielplan/Simulation) — <see cref="TurnierTabKoordinator.NurVorlagenAnzeigen"/> bleibt hier
/// daher dauerhaft auf dem Default <c>false</c>, ohne UI dafür.
///
/// Enthält vorerst drei Platzhalter-Tabs — werden in #300 (Auslosung), #301 (Spielplan &amp; Baum) und #302
/// (Simulation) durch die jeweilige Fach-VM ersetzt.
/// </summary>
public partial class TurnierdurchfuehrungViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly IDialogService _dialogService;

    public AuslosungViewModel AuslosungTab { get; }
    public PlatzhalterViewModel SpielplanUndBaumTab { get; } = new() { SeitenTitel = "Spielplan & Baum — folgt in #301" };
    public PlatzhalterViewModel SimulationTab { get; } = new() { SeitenTitel = "Simulation — folgt in #302" };

    public TurnierTabKoordinator TabKoordinator { get; }

    public TurnierdurchfuehrungViewModel(
        TurnierInstanzService service,
        AusgewaehlteTurnierInstanzService turnierAuswahlService,
        AuslosungViewModel auslosungTab,
        IDialogService dialogService)
    {
        _dialogService = dialogService;
        AuslosungTab = auslosungTab;

        TabKoordinator = new TurnierTabKoordinator(service, [AuslosungTab, SpielplanUndBaumTab, SimulationTab], turnierAuswahlService);

        // Nur AuslosungTab kennt InstanzSetzenAsync (Precedent: TurnierlisteViewModel) — die übrigen beiden
        // sind noch Platzhalter (#301/#302) und werden dort um dieselbe Verdrahtung ergänzt.
        TabKoordinator.AusgewaehlteInstanzGesetzt += instanz => _ = AuslosungTab.InstanzSetzenAsync(instanz);

        TabKoordinator.AktivesTabZustandGeaendert += (_, _) => RibbonEigenschaftenGeaendertMelden();
    }

    /// <summary>Öffnet den Turnier-Auswahl-Dialog (Precedent: WPF <c>TurnierAuswahlDialog</c>) — Auswahl
    /// wirkt sofort (analog zur Live-Selektion in jeder anderen Liste dieser App), "Schließen" beendet nur
    /// den Dialog, es gibt keinen separaten Bestätigen/Abbrechen-Schritt.</summary>
    [RelayCommand]
    private async Task TurnierWaehlenAsync()
    {
        var dialog = new TurnierAuswahlDialog(TabKoordinator);
        await _dialogService.AnzeigenAsync(dialog);
    }

    public override async Task AktivierenAsync()
    {
        await AuslosungTab.RanglistenLadenAsync();
        await TabKoordinator.AktivierenAsync();
    }

    private void RibbonEigenschaftenGeaendertMelden()
    {
        OnPropertyChanged(nameof(NeuBefehl));
        OnPropertyChanged(nameof(SpeichernBefehl));
        OnPropertyChanged(nameof(AbbrechenBefehl));
        OnPropertyChanged(nameof(LoeschenBefehl));
        OnPropertyChanged(nameof(RibbonZusatzGruppen));
        OnPropertyChanged(nameof(NeuUnterpunkte));
        OnPropertyChanged(nameof(HatStandardAktionen));
        OnPropertyChanged(nameof(HatLoeschenAktion));
        OnPropertyChanged(nameof(HatUngespeicherteAenderungen));
    }

    // ── IRibbonAware — Wert-Delegation an das aktive Tab-Kind (analog TurnierlisteViewModel) ──
    public string RibbonTabTitel => "Turnierdurchführung";
    public ICommand? NeuBefehl       => (TabKoordinator.AktivesTab as IRibbonAware)?.NeuBefehl;
    public ICommand? SpeichernBefehl => (TabKoordinator.AktivesTab as IRibbonAware)?.SpeichernBefehl;
    public ICommand? AbbrechenBefehl => (TabKoordinator.AktivesTab as IRibbonAware)?.AbbrechenBefehl;
    public ICommand? LoeschenBefehl  => (TabKoordinator.AktivesTab as IRibbonAware)?.LoeschenBefehl;
    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen
        => (TabKoordinator.AktivesTab as IRibbonAware)?.RibbonZusatzGruppen ?? [];
    public IReadOnlyList<RibbonAktion> NeuUnterpunkte
        => (TabKoordinator.AktivesTab as IRibbonAware)?.NeuUnterpunkte ?? [];
    public bool HatStandardAktionen => (TabKoordinator.AktivesTab as IRibbonAware)?.HatStandardAktionen ?? false;
    public bool HatLoeschenAktion   => (TabKoordinator.AktivesTab as IRibbonAware)?.HatLoeschenAktion ?? false;

    public bool HatUngespeicherteAenderungen
        => (TabKoordinator.AktivesTab as IHatUngespeicherteAenderungen)?.HatUngespeicherteAenderungen ?? false;
}
