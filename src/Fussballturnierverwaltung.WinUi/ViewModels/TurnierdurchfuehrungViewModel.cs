using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using Fussballturnierverwaltung.WinUi.Views;
using Microsoft.Extensions.Logging;
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
/// Vier Fach-Tabs: #300 (Auslosung), #301 (Spielplan &amp; Baum), #302 (Simulation), #327 (Statistik).
/// </summary>
public partial class TurnierdurchfuehrungViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen, IVerlassenBestaetigen
{
    private readonly IDialogService _dialogService;
    private readonly ILogger<TurnierdurchfuehrungViewModel> _logger;

    public AuslosungViewModel AuslosungTab { get; }
    public SpielplanUndBaumViewModel SpielplanUndBaumTab { get; }
    public SimulationsViewModel SimulationTab { get; }
    public StatistikViewModel StatistikTab { get; }

    public TurnierTabKoordinator TabKoordinator { get; }

    public TurnierdurchfuehrungViewModel(
        TurnierInstanzService service,
        AusgewaehlteTurnierInstanzService turnierAuswahlService,
        AuslosungViewModel auslosungTab,
        SpielplanUndBaumViewModel spielplanUndBaumTab,
        SimulationsViewModel simulationTab,
        StatistikViewModel statistikTab,
        IDialogService dialogService,
        ILogger<TurnierdurchfuehrungViewModel> logger)
    {
        _dialogService = dialogService;
        _logger = logger;
        AuslosungTab = auslosungTab;
        SpielplanUndBaumTab = spielplanUndBaumTab;
        SimulationTab = simulationTab;
        StatistikTab = statistikTab;

        TabKoordinator = new TurnierTabKoordinator(service, [AuslosungTab, SpielplanUndBaumTab, SimulationTab, StatistikTab], turnierAuswahlService);

        // Aktionen aller Reiter reihen sich hinter Turnier-/Reiterwechsel ein und umgekehrt (#413, #415).
        AuslosungTab.DbContextSchutz = TabKoordinator.DbContextSchutz;
        SpielplanUndBaumTab.SpielplanVm.DbContextSchutz = TabKoordinator.DbContextSchutz;
        SpielplanUndBaumTab.TurnierBaumVm.DbContextSchutz = TabKoordinator.DbContextSchutz;
        SimulationTab.DbContextSchutz = TabKoordinator.DbContextSchutz;

        // Alle drei Tabs SEQUENZIELL innerhalb EINES Fire-and-Forget-Aufrufs statt eigenständiger
        // (Testfund: paralleles Fire-and-Forget mit DB-Zugriff auf denselben scoped DbContext löste eine
        // stille "A second operation was started on this context instance"-Exception aus — der Spielplan-Tab
        // brach seinen Runden-Ladevorgang lautlos ab, sobald der Auslosungs-Tab gleichzeitig auf demselben
        // DbContext arbeitete). Nachbesserung #308: das war nur die halbe Miete — auch ZWEI dieser
        // Fire-and-Forget-Ketten selbst können sich überlappen (z.B. schneller Tab-Wechsel während der
        // vorige Durchlauf noch lädt), mit demselben Symptom.
        // TabKoordinator.SequenziellMitDbContextSchutzAsync serialisiert sie (#189-Nebenfund: dieselbe
        // Absicherung wurde für TurnierlisteViewModel gebraucht — beide teilen sich deshalb jetzt diese
        // gemeinsame Basis im Koordinator statt eigener Kopien).
        TabKoordinator.AusgewaehlteInstanzGesetzt += instanz => _ = InstanzAnAlleTabsSequenziellSetzenAsync(instanz, "Turnierwechsel");

        // Ein reiner Tab-Wechsel ist in WinUi (anders als in WPF, siehe TurnierTabKoordinator.AktiverTabGewechselt-
        // Doku) keine eigene Navigation und lädt daher nichts automatisch neu — Daten, die sich in einem anderen
        // Tab geändert haben (z.B. Spielplan-Neugenerierung nach einer Auslosung), blieben beim Zurückwechseln
        // sonst veraltet stehen (Fund aus #301-Testsession). Alle Tabs neu setzen statt nur das Ziel-Tab, analog
        // InstanzAnAlleTabsSequenziellSetzenAsync (vermeidet den bekannten Parallel-DbContext-Bug aus #300/#301).
        TabKoordinator.AktiverTabGewechselt += (_, _) =>
            _ = InstanzAnAlleTabsSequenziellSetzenAsync(TabKoordinator.AusgewaehlteInstanz, "Reiterwechsel");

        TabKoordinator.AktivesTabZustandGeaendert += (_, _) => RibbonEigenschaftenGeaendertMelden();

        // Reiterwechsel weg von einer laufenden TV-Ziehung erst nach Rückfrage (#406).
        TabKoordinator.TabVerlassenBestaetigen = tab =>
            tab == AuslosungTab ? AuslosungTab.ZiehungBeendenNachRueckfrageAsync() : Task.FromResult(true);

        // Nachbesserung zu #163 (WPF-Original TurnierDurchfuehrungViewModel): "Ausführlich simulieren"/
        // "Spielverlauf ansehen" im Spielplan-Tab wechselt nur auf den Simulation-Tab derselben, bereits
        // offenen Turnierdurchführung-Seite statt eine eigene Navigation auszulösen. Die Vorauswahl wird
        // VOR dem Tab-Wechsel gesetzt und von SimulationsViewModel.InstanzSetzenAsync verarbeitet — der
        // Tab-Wechsel selbst löst über AktiverTabGewechselt (oben) ohnehin einen sequenziellen
        // InstanzSetzenAsync-Durchlauf aller Tabs aus, ein zusätzlicher paralleler Aufruf hier würde den
        // bekannten Parallel-DbContext-Bug aus #300/#301 erneut riskieren.
        SpielplanUndBaumTab.AusfuehrlicheSimulationAngefordert += (_, vorauswahl) =>
        {
            SimulationTab.Vorauswahl = vorauswahl;
            TabKoordinator.AktiverTabIndex = Array.IndexOf(TabKoordinator.Tabs, SimulationTab);
        };
    }

    /// <summary>Öffnet den Turnier-Auswahl-Dialog (Precedent: WPF <c>TurnierAuswahlDialog</c>) — Auswahl
    /// wirkt sofort (analog zur Live-Selektion in jeder anderen Liste dieser App), "Schließen" beendet nur
    /// den Dialog, es gibt keinen separaten Bestätigen/Abbrechen-Schritt.</summary>
    [RelayCommand]
    private async Task TurnierWaehlenAsync()
    {
        // Vor dem Öffnen fragen, nicht erst bei der Auswahl: der Auswahl-Dialog ist selbst ein ContentDialog,
        // und davon kann nur einer gleichzeitig offen sein (#406).
        if (!await AuslosungTab.ZiehungBeendenNachRueckfrageAsync()) return;

        var dialog = new TurnierAuswahlDialog(TabKoordinator);
        await _dialogService.AnzeigenAsync(dialog);
    }

    public override async Task AktivierenAsync()
    {
        await AuslosungTab.RanglistenLadenAsync();
        await TabKoordinator.AktivierenAsync();

        // TabKoordinator.AktivierenAsync() löst den initialen Tab-Reload bewusst NICHT selbst über
        // AusgewaehlteInstanzGesetzt aus (siehe dortige Doku) — sonst gälte die Seite als "aktiv", bevor
        // der erste Reload abgeschlossen ist, UND der hier folgende explizite Aufruf würde denselben
        // Reload ein zweites Mal ausführen statt nur mitzuwarten (Root Cause von #336, siehe
        // TurnierlisteViewModel.AktivierenAsync für Details). Dieser Aufruf hier ist daher der EINZIGE
        // initiale Reload.
        await InstanzAnAlleTabsSequenziellSetzenAsync(TabKoordinator.AusgewaehlteInstanz, "Seite geöffnet");
    }

    private Task InstanzAnAlleTabsSequenziellSetzenAsync(TurnierInstanz? instanz, string ausloeser) =>
        TabKoordinator.SequenziellMitDbContextSchutzAsync(
            async () =>
            {
                using var vorgang = _logger.Vorgang($"Turnierdurchführung: alle Reiter laden (Instanz {instanz?.Id}, {ausloeser})");
                await AuslosungTab.InstanzSetzenAsync(instanz);
                await SpielplanUndBaumTab.InstanzSetzenAsync(instanz);
                await SimulationTab.InstanzSetzenAsync(instanz);
                await StatistikTab.InstanzSetzenAsync(instanz, istAktiv: TabKoordinator.AktivesTab == StatistikTab);
            },
            ex =>
            {
                // Dieser Aufruf läuft als Fire-and-Forget (_ = ...) ohne Awaiter — eine Exception hier
                // verschwand bisher spurlos (z.B. Runden.Clear() bereits ausgeführt, danach Abbruch mitten
                // im Reload → leere Rundenliste ohne jede Fehlermeldung). Auf dem gerade aktiven Tab
                // anzeigen, analog den lokalen Fehlerpfaden der drei Fach-ViewModels.
                _logger.LogError(ex, "Reiter laden fehlgeschlagen (Instanz {InstanzId}, {Ausloeser}).", instanz?.Id, ausloeser);
                switch (TabKoordinator.AktivesTab)
                {
                    case AuslosungViewModel a: a.Fehlermeldung = ex.Message; break;
                    case SpielplanUndBaumViewModel s: s.SpielplanVm.Fehlermeldung = ex.Message; break;
                    case SimulationsViewModel sim: sim.Fehlermeldung = ex.Message; break;
                    case StatistikViewModel stat: stat.Fehlermeldung = ex.Message; break;
                }
            });

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

    public Task<bool> VerlassenBestaetigenAsync() => AuslosungTab.ZiehungBeendenNachRueckfrageAsync();
}
