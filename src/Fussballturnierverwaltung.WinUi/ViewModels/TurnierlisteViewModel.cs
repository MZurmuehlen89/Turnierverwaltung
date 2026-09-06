using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>
/// "Turniere"-Seite (Issue #297) — die EINE vereinheitlichte Turnier-View, Composite aus Turnierkopf-,
/// Runden-, Rundenübergänge- und Vorbelegungs-Tab unter einer geteilten Instanzliste (Status-Filter
/// Turniere/Vorlagen). Ersetzt sowohl die bisherige "Turniervorlagen"-Seite (#284) als auch die Platzhalter
/// "Turnierkopf"/"Turnierdefinition" — eine Vorlage ist seit #294 einfach eine <c>TurnierInstanz</c> mit
/// Status <c>Vorlage</c>, die je nach Status unterschiedliche Interaktionen freischaltet, statt zwei
/// parallele View-Bäume zu pflegen.
/// </summary>
public class TurnierlisteViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    public TurnierkopfViewModel TurnierkopfVm { get; }
    public RundenViewModel RundenVm { get; }
    public RundenUebergaengeViewModel RundenUebergaengeVm { get; }
    public VorbelegungViewModel VorbelegungVm { get; }

    public TurnierTabKoordinator TabKoordinator { get; }

    /// <summary>Runden/Übergänge/Vorbelegung dürfen erst bearbeitet werden, sobald ein Turnier gewählt UND
    /// gespeichert ist (eine neu angelegte, noch nicht gespeicherte Instanz hat keine Id, Runden können ihr
    /// noch nicht zugeordnet werden). Das feinere Status-Gating (Geplant/Vorlage vs. Auslosung/Laufend/…)
    /// sitzt auf Formular-Ebene in <see cref="RundenViewModel.RundenBearbeitbar"/>.</summary>
    public bool RundenTabAktiv => TabKoordinator.AusgewaehlteInstanz is not null;

    public TurnierlisteViewModel(
        TurnierInstanzService service,
        TurnierkopfViewModel turnierkopfVm,
        RundenViewModel rundenVm,
        RundenUebergaengeViewModel rundenUebergaengeVm,
        VorbelegungViewModel vorbelegungVm)
    {
        TurnierkopfVm = turnierkopfVm;
        RundenVm = rundenVm;
        RundenUebergaengeVm = rundenUebergaengeVm;
        VorbelegungVm = vorbelegungVm;

        TabKoordinator = new TurnierTabKoordinator(service, [TurnierkopfVm, RundenVm, RundenUebergaengeVm, VorbelegungVm]);

        TabKoordinator.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TurnierTabKoordinator.AusgewaehlteInstanz))
                OnPropertyChanged(nameof(RundenTabAktiv));
        };

        // Kein geteilter Kontext-Service (anders als beim WPF-Instanz-Vorbild) — die ausgewählte Instanz wird
        // direkt an alle Kind-ViewModels durchgereicht. NACHEINANDER (nicht per Fire-and-Forget parallel) —
        // alle drei Kind-ViewModels teilen sich denselben Scoped-DbContext über ihre Services, ein
        // gleichzeitiger Start mehrerer DB-Abfragen auf derselben Kontext-Instanz wirft "A second operation
        // was started on this context instance before a previous operation completed" (Fund aus #297-Test,
        // siehe auch feedback_dbcontext_concurrency in Memory).
        TabKoordinator.AusgewaehlteInstanzGesetzt += instanz => _ = InstanzAnKindViewModelsWeiterreichenAsync(instanz);

        TabKoordinator.AktivesTabZustandGeaendert += (_, _) => RibbonEigenschaftenGeaendertMelden();

        // Nach Speichern/Löschen/Statuswechsel im Turnierkopf-Tab die geteilte Instanzliste neu laden und die
        // (ggf. neue) Instanz erneut auswählen — löst über AusgewaehlteInstanzGesetzt automatisch den Reload
        // der übrigen Tabs mit aus.
        TurnierkopfVm.InstanzGeaendert += instanz => _ = TabKoordinator.InstanzenNeuLadenAsync(instanz?.Id);
    }

    private async Task InstanzAnKindViewModelsWeiterreichenAsync(TurnierInstanz? instanz)
    {
        await TurnierkopfVm.InstanzSetzenAsync(instanz);
        await RundenVm.InstanzSetzenAsync(instanz);
        await VorbelegungVm.InstanzSetzenAsync(instanz);
    }

    public override async Task AktivierenAsync()
    {
        await TurnierkopfVm.StammdatenLadenAsync();
        await TabKoordinator.AktivierenAsync();
    }

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

    // ── IRibbonAware — Wert-Delegation an das aktive Tab-Kind (analog WPF-Original TurnierDefinitionViewModel) ──
    public string RibbonTabTitel => "Turniere";
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
