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
        VorbelegungViewModel vorbelegungVm,
        AusgewaehlteTurnierInstanzService turnierAuswahlService)
    {
        TurnierkopfVm = turnierkopfVm;
        RundenVm = rundenVm;
        RundenUebergaengeVm = rundenUebergaengeVm;
        VorbelegungVm = vorbelegungVm;

        // Geteilter Kontext-Service (#299) — hält die Auswahl über den Wechsel zu "Turnierdurchführung"
        // hinweg, da beide ViewModels per Transient-Scope bei jeder Navigation neu erzeugt werden.
        TabKoordinator = new TurnierTabKoordinator(service, [TurnierkopfVm, RundenVm, RundenUebergaengeVm, VorbelegungVm], turnierAuswahlService);

        TabKoordinator.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TurnierTabKoordinator.AusgewaehlteInstanz))
                OnPropertyChanged(nameof(RundenTabAktiv));
        };

        // Die ausgewählte Instanz wird direkt an alle Kind-ViewModels durchgereicht, NACHEINANDER (nicht per
        // Fire-and-Forget parallel) — alle drei Kind-ViewModels teilen sich denselben Scoped-DbContext über
        // ihre Services, ein gleichzeitiger Start mehrerer DB-Abfragen auf derselben Kontext-Instanz wirft
        // "A second operation was started on this context instance before a previous operation completed"
        // (Fund aus #297-Test, siehe auch feedback_dbcontext_concurrency in Memory). Zusätzlich überlappen
        // sich bei schnellem Durchsteppen der Instanzliste (z.B. Pfeiltasten) auch ZWEI dieser Fire-and-
        // Forget-Aufrufe selbst, mit demselben Symptom — TabKoordinator.SequenziellMitDbContextSchutzAsync
        // serialisiert das (#189-Nebenfund, gemeinsame Basis mit TurnierdurchfuehrungViewModel, siehe #317).
        TabKoordinator.AusgewaehlteInstanzGesetzt += instanz => _ = InstanzAnKindViewModelsWeiterreichenAsync(instanz);

        TabKoordinator.AktivesTabZustandGeaendert += (_, _) => RibbonEigenschaftenGeaendertMelden();

        // Nach Speichern/Löschen/Statuswechsel im Turnierkopf-Tab die geteilte Instanzliste neu laden und die
        // (ggf. neue) Instanz erneut auswählen — löst über AusgewaehlteInstanzGesetzt automatisch den Reload
        // der übrigen Tabs mit aus.
        TurnierkopfVm.InstanzGeaendert += instanz => _ = TabKoordinator.InstanzenNeuLadenAsync(instanz?.Id);

        // Nach Speichern im Runden-Tab liest VorbelegungVm (PaarungFestVorgegeben, Rundenstruktur für
        // "RUNDEN MIT FESTER PAARUNG") unabhängig frisch aus der DB — ohne diesen Reload blieb der
        // Vorbelegung-Tab nach dem Setzen von "Paarung fest vorgegeben" leer (Fund aus #297-Test).
        RundenVm.RundenGespeichert += instanz => _ = VorbelegungVm.InstanzSetzenAsync(instanz);
    }

    private Task InstanzAnKindViewModelsWeiterreichenAsync(TurnierInstanz? instanz) =>
        TabKoordinator.SequenziellMitDbContextSchutzAsync(
            async () =>
            {
                await TurnierkopfVm.InstanzSetzenAsync(instanz);
                await RundenVm.InstanzSetzenAsync(instanz);
                await VorbelegungVm.InstanzSetzenAsync(instanz);
            },
            ex =>
            {
                // Auf dem gerade aktiven Tab anzeigen, analog dem lokalen Fehlerpfad der drei Kind-ViewModels.
                switch (TabKoordinator.AktivesTab)
                {
                    case TurnierkopfViewModel t: t.Fehlermeldung = ex.Message; break;
                    case RundenViewModel r: r.Fehlermeldung = ex.Message; break;
                    case VorbelegungViewModel v: v.Fehlermeldung = ex.Message; break;
                }
            });

    public override async Task AktivierenAsync()
    {
        await TurnierkopfVm.StammdatenLadenAsync();
        await TabKoordinator.AktivierenAsync();

        // TabKoordinator.AktivierenAsync() löst den initialen Tab-Reload nur als Fire-and-Forget über
        // AusgewaehlteInstanzGesetzt aus (Event-Handler können nicht awaitet werden) — ohne diesen
        // expliziten Await hier gilt die Seite als "aktiv", bevor der erste Reload tatsächlich
        // abgeschlossen ist. Läuft über denselben Lock wie der Event-Aufruf, wartet also einfach mit,
        // statt ihn zu duplizieren (Precedent: TurnierdurchfuehrungViewModel.AktivierenAsync).
        await InstanzAnKindViewModelsWeiterreichenAsync(TabKoordinator.AusgewaehlteInstanz);
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

    // ── IRibbonAware — Wert-Delegation an das aktive Tab-Kind (analog WPF-Original TurnierDefinitionViewModel) ──
    public string RibbonTabTitel => "Turniere";
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
