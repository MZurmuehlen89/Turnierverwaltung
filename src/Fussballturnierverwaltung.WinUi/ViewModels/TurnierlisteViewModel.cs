using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using Microsoft.Extensions.Logging;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>
/// "Turniere"-Seite (Issue #297) — die EINE vereinheitlichte Turnier-View, Composite aus Turnierkopf-,
/// Runden- (inkl. Übergangsregeln, #348) und Vorbelegungs-Tab unter einer geteilten Instanzliste (Status-Filter
/// Turniere/Vorlagen). Ersetzt sowohl die bisherige "Turniervorlagen"-Seite (#284) als auch die Platzhalter
/// "Turnierkopf"/"Turnierdefinition" — eine Vorlage ist seit #294 einfach eine <c>TurnierInstanz</c> mit
/// Status <c>Vorlage</c>, die je nach Status unterschiedliche Interaktionen freischaltet, statt zwei
/// parallele View-Bäume zu pflegen.
/// </summary>
public class TurnierlisteViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen, IVerlassenBestaetigen
{
    private readonly ILogger<TurnierlisteViewModel> _logger;

    public TurnierkopfViewModel TurnierkopfVm { get; }
    public RundenViewModel RundenVm { get; }
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
        VorbelegungViewModel vorbelegungVm,
        AusgewaehlteTurnierInstanzService turnierAuswahlService,
        ILogger<TurnierlisteViewModel> logger)
    {
        _logger = logger;
        TurnierkopfVm = turnierkopfVm;
        RundenVm = rundenVm;
        VorbelegungVm = vorbelegungVm;

        // Geteilter Kontext-Service (#299) — hält die Auswahl über den Wechsel zu "Turnierdurchführung"
        // hinweg, da beide ViewModels per Transient-Scope bei jeder Navigation neu erzeugt werden.
        TabKoordinator = new TurnierTabKoordinator(service, [TurnierkopfVm, RundenVm, VorbelegungVm], turnierAuswahlService);

        // Aktionen aller Reiter reihen sich hinter einen Turnierwechsel ein und umgekehrt (#416, analog #413/#415).
        TurnierkopfVm.DbContextSchutz = TabKoordinator.DbContextSchutz;
        RundenVm.DbContextSchutz = TabKoordinator.DbContextSchutz;
        VorbelegungVm.DbContextSchutz = TabKoordinator.DbContextSchutz;

        TabKoordinator.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TurnierTabKoordinator.AusgewaehlteInstanz))
                OnPropertyChanged(nameof(RundenTabAktiv));
            // "Neu" legt an, was die Liste gerade zeigt (#382).
            if (e.PropertyName == nameof(TurnierTabKoordinator.NurVorlagenAnzeigen))
                TurnierkopfVm.StatusFuerNeueInstanz = TabKoordinator.NurVorlagenAnzeigen ? TurnierStatus.Vorlage : TurnierStatus.Geplant;
        };

        // Die ausgewählte Instanz wird direkt an alle Kind-ViewModels durchgereicht, NACHEINANDER (nicht per
        // Fire-and-Forget parallel) — alle drei Kind-ViewModels teilen sich denselben Scoped-DbContext über
        // ihre Services, ein gleichzeitiger Start mehrerer DB-Abfragen auf derselben Kontext-Instanz wirft
        // "A second operation was started on this context instance before a previous operation completed"
        // (Fund aus #297-Test, siehe auch feedback_dbcontext_concurrency in Memory). Zusätzlich überlappen
        // sich bei schnellem Durchsteppen der Instanzliste (z.B. Pfeiltasten) auch ZWEI dieser Fire-and-
        // Forget-Aufrufe selbst, mit demselben Symptom — TabKoordinator.SequenziellMitDbContextSchutzAsync
        // serialisiert das (#189-Nebenfund, gemeinsame Basis mit TurnierdurchfuehrungViewModel, siehe #317).
        TabKoordinator.AusgewaehlteInstanzGesetzt += instanz => _ = InstanzAnKindViewModelsWeiterreichenAsync(instanz, "Turnierwechsel");

        TabKoordinator.AktivesTabZustandGeaendert += (_, _) => RibbonEigenschaftenGeaendertMelden();

        // Eine begonnene Vorbelegung überlebt den Reiterwechsel (kein Reload), nicht aber den Wechsel des
        // Turniers — davor nachfragen. Dasselbe gilt für Speichern/Statuswechsel/"Aus Vorlage erstellen" in
        // den beiden anderen Reitern: sie laden das Turnier in allen Reitern neu (siehe unten).
        TabKoordinator.InstanzVerlassenBestaetigen = VorbelegungVm.VerwerfenBestaetigtAsync;
        TurnierkopfVm.VorAenderungBestaetigen = VorbelegungVm.VerwerfenBestaetigtAsync;
        RundenVm.VorAenderungBestaetigen = VorbelegungVm.VerwerfenBestaetigtAsync;

        // Nach Speichern/Löschen/Statuswechsel im Turnierkopf-Tab die geteilte Instanzliste neu laden und die
        // (ggf. neue) Instanz erneut auswählen — löst über AusgewaehlteInstanzGesetzt automatisch den Reload
        // der übrigen Tabs mit aus. Über denselben TabKoordinator-Lock wie die Weiterreichung selbst (#338)
        // — sonst könnte dieser direkte DB-Zugriff einen parallel laufenden, gesperrten Reload treffen
        // (z.B. Speichern im Turnierkopf während gleichzeitig per Pfeiltaste die Instanz gewechselt wird).
        TurnierkopfVm.InstanzGeaendert += instanz => _ = TabKoordinator.SequenziellMitDbContextSchutzAsync(
            async () =>
            {
                using var vorgang = _logger.Vorgang($"Turniere: Turnierliste neu laden (Instanz {instanz?.Id}, nach Änderung im Turnierkopf)");
                await TabKoordinator.InstanzenNeuLadenAsync(instanz?.Id);
            },
            ex =>
            {
                _logger.LogError(ex, "Turnierliste neu laden fehlgeschlagen (Instanz {InstanzId}).", instanz?.Id);
                TurnierkopfVm.Fehlermeldung = ex.Message;
            });

        // Nach Speichern im Runden-Tab liest VorbelegungVm (PaarungFestVorgegeben, Rundenstruktur für
        // "RUNDEN MIT FESTER PAARUNG") unabhängig frisch aus der DB — ohne diesen Reload blieb der
        // Vorbelegung-Tab nach dem Setzen von "Paarung fest vorgegeben" leer (Fund aus #297-Test). Ebenfalls
        // über den TabKoordinator-Lock (#338) statt direktem Fire-and-Forget-Zugriff auf denselben Grund.
        RundenVm.RundenGespeichert += instanz => _ = TabKoordinator.SequenziellMitDbContextSchutzAsync(
            async () =>
            {
                using var vorgang = _logger.Vorgang($"Turniere: Vorbelegung neu laden (Instanz {instanz.Id}, nach Speichern der Runden)");
                await VorbelegungVm.InstanzSetzenAsync(instanz);
            },
            ex =>
            {
                _logger.LogError(ex, "Vorbelegung neu laden fehlgeschlagen (Instanz {InstanzId}).", instanz.Id);
                VorbelegungVm.Fehlermeldung = ex.Message;
            });
    }

    private Task InstanzAnKindViewModelsWeiterreichenAsync(TurnierInstanz? instanz, string ausloeser) =>
        TabKoordinator.SequenziellMitDbContextSchutzAsync(
            async () =>
            {
                using var vorgang = _logger.Vorgang($"Turniere: alle Reiter laden (Instanz {instanz?.Id}, {ausloeser})");
                await TurnierkopfVm.InstanzSetzenAsync(instanz);
                await RundenVm.InstanzSetzenAsync(instanz);
                await VorbelegungVm.InstanzSetzenAsync(instanz);
            },
            ex =>
            {
                // Auf dem gerade aktiven Tab anzeigen, analog dem lokalen Fehlerpfad der drei Kind-ViewModels.
                _logger.LogError(ex, "Reiter laden fehlgeschlagen (Instanz {InstanzId}, {Ausloeser}).", instanz?.Id, ausloeser);
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

        // TabKoordinator.AktivierenAsync() löst den initialen Tab-Reload bewusst NICHT selbst über
        // AusgewaehlteInstanzGesetzt aus (siehe dortige Doku) — sonst gälte die Seite als "aktiv", bevor
        // der erste Reload abgeschlossen ist, UND der hier folgende explizite Aufruf würde denselben
        // Reload ein zweites Mal ausführen statt nur mitzuwarten (Root Cause von #336: der Event-Invoke
        // lief als eigener Fire-and-Forget-Durchlauf, der SequenziellMitDbContextSchutzAsync-Lock hat die
        // beiden Läufe nur nacheinander statt zusammengeführt ausgeführt — Vorbelegung baute sich danach
        // nicht mehr auf). Dieser Aufruf hier ist daher der EINZIGE initiale Reload.
        await InstanzAnKindViewModelsWeiterreichenAsync(TabKoordinator.AusgewaehlteInstanz, "Seite geöffnet");
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

    /// <summary>Unabhängig vom aktiven Reiter — die Vorbelegung bleibt beim Reiterwechsel im Speicher.</summary>
    public Task<bool> VerlassenBestaetigenAsync() => VorbelegungVm.VerwerfenBestaetigtAsync();
}
