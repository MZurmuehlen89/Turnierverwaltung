using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Wpf.Navigation;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

/// <summary>
/// "Turnierdefinition"-Seite — Runden-Editor + Rundenübergänge-Übersicht + Paarungsfestlegung unter
/// einem gemeinsamen Turnier-Header. Generische Turnier-Liste/-Auswahl/-Filterung weiterhin über
/// TurnierTabKoordinator (geteilt mit TurnierDurchfuehrungViewModel). Die frühere Ein-Tab-Hülle (seit
/// Verband-Quoten in den Turnierkopf gewandert sind, Issue #161) bekam mit #249 einen zweiten Tab — eine
/// rein lesende visuelle Übersicht der Rundenübergänge neben dem bestehenden tabellarischen Editor (der
/// weiterhin die einzige Bearbeitungsmöglichkeit für Übergangsregeln bleibt, siehe #169/#186) — und mit
/// #251 einen dritten Tab: die Paarungsfestlegung für Runden mit von vornherein feststehenden Paarungen
/// (Auslosungsart.Manuell), die zuvor Teil von Turnierdurchführung → Auslosung war.
/// </summary>
public class TurnierDefinitionViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    public RundenEditorViewModel RundenVm { get; }
    public RundenUebergaengeViewModel UebergaengeVm { get; }
    public PaarungsfestlegungViewModel PaarungenVm { get; }

    public TurnierTabKoordinator TabKoordinator { get; }

    /// <summary>Runden dürfen nur für noch nicht gestartete Turniere bearbeitet werden — der
    /// Runden-Inhalt wird ausgegraut, sobald ein Turnier mit anderem Status gewählt ist.</summary>
    public bool RundenTabAktiv => TabKoordinator.AusgewaehlteInstanz is null
        || TabKoordinator.AusgewaehlteInstanz.Status == TurnierStatus.Geplant;

    public TurnierDefinitionViewModel(
        TurnierInstanzService instanzService,
        AusgewaehlteTurnierInstanzService turnierAuswahlService,
        RundenEditorViewModel rundenVm,
        RundenUebergaengeViewModel uebergaengeVm,
        PaarungsfestlegungViewModel paarungenVm)
    {
        RundenVm = rundenVm;
        UebergaengeVm = uebergaengeVm;
        PaarungenVm = paarungenVm;

        TabKoordinator = new TurnierTabKoordinator(instanzService, turnierAuswahlService, [RundenVm, UebergaengeVm, PaarungenVm]);

        TabKoordinator.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TurnierTabKoordinator.AusgewaehlteInstanz))
                OnPropertyChanged(nameof(RundenTabAktiv));
        };

        TabKoordinator.AktivesTabZustandGeaendert += (_, _) => RibbonEigenschaftenGeaendertMelden();
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
    public string RibbonTabTitel => "Turnierdefinition";
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
