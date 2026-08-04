using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Wpf.Navigation;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

/// <summary>
/// "Turnierdurchführung"-Seite (Issue #172) — Auslosung, Spielplan &amp; Baum, Simulation als
/// Tabs unter einem gemeinsamen Turnier-Header. Generische Tab-Container-Mechanik ausgelagert
/// in TurnierTabKoordinator.
/// </summary>
public class TurnierDurchfuehrungViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    public AuslosungViewModel      AuslosungVm        { get; }
    public TurnierBereichViewModel SpielplanUndBaumVm { get; }
    public SimulationsViewModel    SimulationVm       { get; }

    public TurnierTabKoordinator TabKoordinator { get; }

    public TurnierDurchfuehrungViewModel(
        TurnierInstanzService instanzService,
        AusgewaehlteTurnierInstanzService turnierAuswahlService,
        AuslosungViewModel auslosungVm,
        TurnierBereichViewModel spielplanUndBaumVm,
        SimulationsViewModel simulationVm)
    {
        AuslosungVm        = auslosungVm;
        SpielplanUndBaumVm = spielplanUndBaumVm;
        SimulationVm       = simulationVm;

        TabKoordinator = new TurnierTabKoordinator(
            instanzService, turnierAuswahlService, [AuslosungVm, SpielplanUndBaumVm, SimulationVm]);

        TabKoordinator.AktivesTabZustandGeaendert += (_, _) => RibbonEigenschaftenGeaendertMelden();

        // Nachbesserung zu #163: "Ausführliche Simulation" im Spielplan-Tab wechselt nur noch
        // auf den Simulation-Tab derselben, bereits offenen Turnierdurchführung-Seite statt eine
        // eigene Navigation zur Simulations-Vollansicht auszulösen (siehe SpielplanViewModel).
        SpielplanUndBaumVm.AusfuehrlicheSimulationAngefordert += (_, vorauswahl) =>
        {
            SimulationVm.Vorauswahl = vorauswahl;
            TabKoordinator.AktiverTabIndex = Array.IndexOf(TabKoordinator.Tabs, SimulationVm);
        };
    }

    public override Task AktivierenAsync() => TabKoordinator.AktivierenAsync();

    private void RibbonEigenschaftenGeaendertMelden()
    {
        OnPropertyChanged(nameof(NeuBefehl));
        OnPropertyChanged(nameof(SpeichernBefehl));
        OnPropertyChanged(nameof(AbbrechenBefehl));
        OnPropertyChanged(nameof(LoeschenBefehl));
        OnPropertyChanged(nameof(RibbonZusatzGruppen));
        OnPropertyChanged(nameof(HatStandardAktionen));
        OnPropertyChanged(nameof(HatLoeschenAktion));
        OnPropertyChanged(nameof(HatUngespeicherteAenderungen));
    }

    // ── IRibbonAware — an das aktive Tab-Kind delegiert (Wert-Delegation, nicht Typ-Check,
    //    siehe Nachbesserung zu #173/#164) ──
    public string RibbonTabTitel => "Turnierdurchführung";
    public ICommand? NeuBefehl       => (TabKoordinator.AktivesTab as IRibbonAware)?.NeuBefehl;
    public ICommand? SpeichernBefehl => (TabKoordinator.AktivesTab as IRibbonAware)?.SpeichernBefehl;
    public ICommand? AbbrechenBefehl => (TabKoordinator.AktivesTab as IRibbonAware)?.AbbrechenBefehl;
    public ICommand? LoeschenBefehl  => (TabKoordinator.AktivesTab as IRibbonAware)?.LoeschenBefehl;
    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen
        => (TabKoordinator.AktivesTab as IRibbonAware)?.RibbonZusatzGruppen ?? [];
    public bool HatStandardAktionen => (TabKoordinator.AktivesTab as IRibbonAware)?.HatStandardAktionen ?? false;
    public bool HatLoeschenAktion   => (TabKoordinator.AktivesTab as IRibbonAware)?.HatLoeschenAktion ?? false;

    public bool HatUngespeicherteAenderungen
        => (TabKoordinator.AktivesTab as IHatUngespeicherteAenderungen)?.HatUngespeicherteAenderungen ?? false;
}
