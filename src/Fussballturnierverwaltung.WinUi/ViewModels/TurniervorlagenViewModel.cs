using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>
/// "Turniervorlagen"-Seite (#284) — Composite aus Turnierkopf-, Runden- und Rundenübergänge-Tab unter einer
/// geteilten Vorlagenliste, analog zum WPF-Original <c>TurnierDefinitionViewModel</c>/<c>TurnierTabKoordinator</c>
/// für Turnier-Instanzen. Runden und Rundenübergänge waren ursprünglich ein gemeinsamer Tab, wurden aber nach
/// Nutzer-Feedback getrennt (reine Rundendaten vs. Perlenkette/Übergangsregeln) — beide teilen sich weiterhin
/// dieselbe Datenhaltung in <see cref="VorlagenRundenViewModel"/> (siehe dort). Ein vierter Tab "Paarungen"
/// (feste Paarungen auf Platzhalterbasis, Issue #277) ist bewusst nicht Teil dieser Etappe —
/// <see cref="VorlagenTabKoordinator.Tabs"/> ist als Array angelegt, damit er später ohne Strukturänderung
/// ergänzt werden kann.
/// </summary>
public class TurniervorlagenViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    public TurnierkopfViewModel TurnierkopfVm { get; }
    public VorlagenRundenViewModel RundenVm { get; }
    public VorlagenRundenUebergaengeViewModel RundenUebergaengeVm { get; }

    public VorlagenTabKoordinator TabKoordinator { get; }

    /// <summary>Runden und Rundenübergänge dürfen erst bearbeitet werden, sobald eine Vorlage gewählt UND
    /// gespeichert ist (eine neu angelegte, noch nicht gespeicherte Vorlage hat keine Id, <c>VorlagenRunde</c>n
    /// können ihr noch nicht zugeordnet werden).</summary>
    public bool RundenTabAktiv => TabKoordinator.AusgewaehlteVorlage is not null;

    public TurniervorlagenViewModel(
        TurnierVorlageService service,
        TurnierkopfViewModel turnierkopfVm,
        VorlagenRundenViewModel rundenVm,
        VorlagenRundenUebergaengeViewModel rundenUebergaengeVm)
    {
        TurnierkopfVm = turnierkopfVm;
        RundenVm = rundenVm;
        RundenUebergaengeVm = rundenUebergaengeVm;

        TabKoordinator = new VorlagenTabKoordinator(service, [TurnierkopfVm, RundenVm, RundenUebergaengeVm]);

        TabKoordinator.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(VorlagenTabKoordinator.AusgewaehlteVorlage))
                OnPropertyChanged(nameof(RundenTabAktiv));
        };

        // Kein geteilter Kontext-Service (anders als beim WPF-Instanz-Vorbild) — die ausgewählte Vorlage wird
        // direkt an beide Kind-ViewModels durchgereicht.
        TabKoordinator.AusgewaehlteVorlageGesetzt += vorlage =>
        {
            TurnierkopfVm.VorlageSetzen(vorlage);
            _ = RundenVm.VorlageSetzenAsync(vorlage);
        };

        TabKoordinator.AktivesTabZustandGeaendert += (_, _) => RibbonEigenschaftenGeaendertMelden();

        // Nach Speichern/Löschen im Turnierkopf-Tab die geteilte Vorlagenliste neu laden und die
        // (ggf. neue) Vorlage erneut auswählen — löst über AusgewaehlteVorlageGesetzt automatisch den
        // Reload des Runden-Tabs mit aus.
        TurnierkopfVm.VorlageGeaendert += vorlage => _ = TabKoordinator.VorlagenNeuLadenAsync(vorlage?.Id);
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

    // ── IRibbonAware — Wert-Delegation an das aktive Tab-Kind (analog TurnierDefinitionViewModel) ──
    public string RibbonTabTitel => "Turniervorlagen";
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
