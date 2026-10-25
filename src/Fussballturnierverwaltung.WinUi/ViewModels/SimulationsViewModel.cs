using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>
/// "Simulation"-Tab der Turnierdurchführung (#302, ersetzt den Platzhalter aus #299) — 1:1 aus
/// Wpf/ViewModels/SimulationsViewModel.cs portiert. Wichtigste WinUi-Anpassung: anders als das WPF-
/// Original (eigenständiger Menüpunkt mit eigener Instanzliste/-auswahl) ist dieser Tab Teil des
/// <see cref="TurnierdurchfuehrungViewModel"/>-Composite — die Instanz kommt daher über
/// <see cref="InstanzSetzenAsync"/> von außen (Precedent: <see cref="SpielplanViewModel.InstanzSetzenAsync"/>),
/// Runde/Spiel-Auswahl bleiben wie im Original eigene lokale Comboboxen. <see cref="MessageBox"/> →
/// <see cref="IDialogService.BestaetigenAsync"/> (kein synchroner Dialog in WinUI 3).
/// </summary>
public partial class SimulationsViewModel : ViewModelBase, IRibbonAware
{
    private readonly SimulationsService   _simulationsService;
    private readonly SpielErgebnisService _spielService;
    private readonly TurnierInstanzService _instanzService;
    private readonly IDialogService       _dialogService;
    private readonly RanglisteService     _ranglisteService;
    private readonly ILogger<SimulationsViewModel> _logger;

    private TurnierInstanz? _instanz;
    private readonly WechselUnterdrueckung _ladeUnterdrueckung = new();

    /// <summary>Reiht Runde-/Spiel-Wechsel (#338), die beiden Aktionen und — über den vom
    /// <see cref="TurnierdurchfuehrungViewModel"/> gesetzten gemeinsamen Schutz — auch Turnier-/Reiterwechsel
    /// hintereinander (#415, siehe <see cref="SpielplanViewModel.DbContextSchutz"/>). Nicht
    /// wiedereintrittsfähig — <see cref="InstanzSetzenAsync"/> läuft bereits unter dem Schutz.</summary>
    public DbContextReentrancySchutz DbContextSchutz { get; set; } = new();

    // Deckt Vorschau-Karte/Protokoll komplett ab, solange der automatische Vorauswahl-Pfad
    // (VorauswahlLadenUndSimulierenAsync, "Ausführlich simulieren"/"Spielverlauf ansehen" aus dem
    // Spielplan) läuft — ohne dieses Flag blitzten dort nacheinander das alte Protokoll des zuvor
    // betrachteten Spiels, dann kurz die Vorschau-Karte des neuen (noch unsimulierten) Spiels auf,
    // bevor das fertige Ergebnis erscheint (Nutzer-Fund beim #317-Test). Bewusst getrennt von
    // IstLadend, das eigenständig (und verschachtelt) in TurnierSpielSimulierenAsync gesetzt wird.
    [ObservableProperty] private bool _istVorauswahlLadend;

    // ── Runde/Spiel-Auswahl ───────────────────────────────────────────────────

    [ObservableProperty] private TurnierInstanz? _ausgewaehlteInstanz;
    [ObservableProperty] private ObservableCollection<TurnierRunde> _runden = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatRunde))]
    private TurnierRunde? _ausgewaehlteRunde;

    public bool HatRunde => AusgewaehlteRunde is not null;

    [ObservableProperty] private ObservableCollection<SpielZeile> _spiele = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KannTurnierSpielSimulieren))]
    [NotifyPropertyChangedFor(nameof(KannTurnierErgebnisZuruecksetzen))]
    [NotifyPropertyChangedFor(nameof(AusgewaehltesSpielAbgeschlossen))]
    [NotifyPropertyChangedFor(nameof(ZeigeVorschauKarte))]
    [NotifyPropertyChangedFor(nameof(ZeigeLeerZustand))]
    private SpielZeile? _ausgewaehlteSpiel;

    // Issue #164: "Simulieren" ist immer verfügbar (auch mit vorhandenem Ergebnis) — fragt bei
    // Bedarf per Dialog nach Überschreiben, statt eine exklusiv geschaltete zweite Aktion zu
    // brauchen (siehe TurnierSpielSimulierenAsync).
    public bool KannTurnierSpielSimulieren =>
        AusgewaehlteSpiel is { IstPlatzhalterSpiel: false } && !IstLadend;

    public bool KannTurnierErgebnisZuruecksetzen =>
        AusgewaehlteSpiel is { IstPlatzhalterSpiel: false } && !IstLadend
        && AusgewaehltesSpielAbgeschlossen;

    public bool AusgewaehltesSpielAbgeschlossen => AusgewaehlteSpiel?.HatErgebnis == true;

    public bool ZeigeVorschauKarte =>
        AusgewaehlteSpiel is { IstPlatzhalterSpiel: false } && !AusgewaehltesSpielAbgeschlossen;

    public bool ZeigeLeerZustand => !HatProtokoll && !ZeigeVorschauKarte;

    // ── Protokoll ─────────────────────────────────────────────────────────────

    [ObservableProperty] private string? _protokollSpielInfo;
    // Issue #341: Spielkopf (kicker-Vorbild) statt der früheren Scoreboard-Zeilenliste
    [ObservableProperty] private SpielKopf? _protokollKopf;
    public ObservableCollection<EinzelspielEreignisZeile> ProtokollEreignisse { get; } = [];

    public bool HatProtokoll           => ProtokollKopf is not null;
    public bool HatProtokollEreignisse => ProtokollEreignisse.Count > 0;

    // ── Status ────────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KannTurnierSpielSimulieren))]
    [NotifyPropertyChangedFor(nameof(KannTurnierErgebnisZuruecksetzen))]
    private bool    _istLadend;

    [ObservableProperty] private string? _fehlermeldung;

    // ── Vorauswahl (von SpielplanViewModel via TurnierdurchfuehrungViewModel gesetzt, vor
    //    InstanzSetzenAsync — siehe Klassenkommentar auf TurnierdurchfuehrungViewModel) ───────────

    public VorausgewaehlteSimulation? Vorauswahl { get; set; }

    // ── Befehle ───────────────────────────────────────────────────────────────

    public ICommand TurnierSpielSimulierenBefehl { get; }
    public ICommand ErgebnisZuruecksetzenBefehl  { get; }

    // ── IRibbonAware ──────────────────────────────────────────────────────────
    // Weder Neu/Speichern/Abbrechen haben hier eine Entsprechung (Simulieren schreibt sofort in die
    // DB, kein separater Save-Schritt). Löschen entspricht sinnvoll "Ergebnis zurücksetzen".
    public string   RibbonTabTitel  => "Simulation";
    public ICommand? NeuBefehl       => null;
    public ICommand? SpeichernBefehl => null;
    public ICommand? AbbrechenBefehl => null;
    public ICommand? LoeschenBefehl  => ErgebnisZuruecksetzenBefehl;
    public bool HatStandardAktionen => false;

    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen =>
    [
        new RibbonGruppe
        {
            Header = "Simulation",
            Aktionen =
            [
                new RibbonAktion
                {
                    Header = "Turnierspiel simulieren",
                    Befehl = TurnierSpielSimulierenBefehl,
                    IconGlyph = "",
                },
            ],
        },
    ];

    // ── Konstruktor ───────────────────────────────────────────────────────────

    public SimulationsViewModel(
        SimulationsService    simulationsService,
        SpielErgebnisService  spielService,
        TurnierInstanzService instanzService,
        IDialogService         dialogService,
        RanglisteService       ranglisteService,
        ILogger<SimulationsViewModel> logger)
    {
        _simulationsService = simulationsService;
        _spielService       = spielService;
        _instanzService     = instanzService;
        _dialogService      = dialogService;
        _ranglisteService   = ranglisteService;
        _logger             = logger;

        TurnierSpielSimulierenBefehl = new AsyncRelayCommand(TurnierSpielSimulierenMitSchutzAsync, () => KannTurnierSpielSimulieren);
        ErgebnisZuruecksetzenBefehl  = new AsyncRelayCommand(ErgebnisZuruecksetzenAsync, () => KannTurnierErgebnisZuruecksetzen);
    }

    // ── Initialisierung ───────────────────────────────────────────────────────

    /// <summary>Von <see cref="TurnierdurchfuehrungViewModel"/> bei Instanzwechsel/Tab-Reaktivierung
    /// aufgerufen (Precedent: <see cref="SpielplanViewModel.InstanzSetzenAsync"/>). Verarbeitet zuerst
    /// eine anstehende <see cref="Vorauswahl"/> (Sprung aus dem Spielplan-Tab über "Spielverlauf
    /// ansehen"/"Ausführlich simulieren") — Precedent WPF-Original <c>AktivierenAsync</c>, hier nur mit
    /// <see cref="InstanzSetzenAsync"/> statt einer echten Navigation als Einstiegspunkt.</summary>
    public async Task InstanzSetzenAsync(TurnierInstanz? instanz)
    {
        using var vorgang = _logger.Vorgang($"Simulation: Turnier setzen (Instanz {instanz?.Id})");

        if (Vorauswahl is { } v)
        {
            Vorauswahl = null;
            await VorauswahlLadenUndSimulierenAsync(instanz, v);
            return;
        }

        _instanz = instanz;

        // #317: Guard umschließt den kompletten Reload, nicht nur die Zuweisung — eine WinUI-ComboBox
        // kann SelectionChanged bei ItemsSource.Clear() über die DispatcherQueue VERZÖGERT statt
        // synchron auslösen. Feuert der Callback erst während der nachfolgenden Abfrage in
        // BeiInstanzAenderungAsync, entsteht sonst ein ungeschützter Parallelzugriff auf denselben
        // DbContext ("second operation"-Exception trotz augenscheinlich striktem Single-Thread-Ablauf,
        // siehe #317 — dort ausschließlich per Retry im Composite-ViewModel abgefangen).
        using (_ladeUnterdrueckung.Betreten())
        {
            AusgewaehlteInstanz = instanz;
            await BeiInstanzAenderungAsync();
        }
    }

    // ── Property-Reaktionen ───────────────────────────────────────────────────

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (_ladeUnterdrueckung.IstAktiv) return;

        switch (e.PropertyName)
        {
            case nameof(AusgewaehlteRunde):
                // #338: zwei echte, schnell aufeinanderfolgende Runde-Auswahländerungen können sonst
                // überlappende Abfragen auf demselben DbContext auslösen ("second operation"-Exception,
                // derselbe Root Cause wie #317, hier innerhalb des Tabs statt beim Instanzwechsel).
                // istNochAktuell verwirft veraltete Zwischenauswahlen aus der Lock-Warteschlange
                // (Ruckel-Fund aus #338-Test).
                var erwarteteRundeId = AusgewaehlteRunde?.Id;
                _ = DbContextSchutz.AusfuehrenAsync(
                    BeiRundeAenderungAsync, ex => Fehlermeldung = ex.Message,
                    istNochAktuell: () => AusgewaehlteRunde?.Id == erwarteteRundeId);
                break;
            case nameof(AusgewaehlteSpiel):
                var erwarteteSpielId = AusgewaehlteSpiel?.Id;
                _ = DbContextSchutz.AusfuehrenAsync(
                    BeiSpielAuswahlAsync, ex => Fehlermeldung = ex.Message,
                    istNochAktuell: () => AusgewaehlteSpiel?.Id == erwarteteSpielId);
                AktualisiereBefehle();
                break;
            case nameof(IstLadend):
                AktualisiereBefehle();
                break;
        }
    }

    // ── Selektions-Handler ────────────────────────────────────────────────────

    private async Task BeiInstanzAenderungAsync()
    {
        int? vorherigeRundeId = AusgewaehlteRunde?.Id;
        Fehlermeldung = null;

        // Runden.Clear() erst NACH der Abfrage (nicht davor, Precedent: SpielplanViewModel) — sonst nullt
        // die ComboBox-Bindung AusgewaehlteRunde bereits beim Clear(), und ein Retry nach #317 (
        // "second operation"-Exception) findet beim neuen Versuch keine vorherigeRundeId mehr vor (#308).
        // Der komplette Methodenkörper läuft jetzt ohnehin innerhalb des _ladeUnterdrueckung-Fensters, das
        // InstanzSetzenAsync um diesen Aufruf legt (#317-Nachbesserung) — Clear() darf ein verzögertes
        // SelectionChanged also auch dann nicht mehr auf einen ungeschützten Fire-and-Forget-Zugriff
        // treffen lassen, wenn dieses Timing-Fenster größer als erwartet ausfällt.
        var instanz = _instanz is null ? null : await _instanzService.FindenNachIdMitRundenAsync(_instanz.Id);

        if (instanz is null)
        {
            Runden.Clear();
            await RundeUndSpielZuruecksetzenAsync();
            return;
        }

        Runden.Clear();
        foreach (var r in instanz.Runden.OrderBy(r => r.Reihenfolge))
            Runden.Add(r);

        // Reiner Tab-Wechsel ohne echten Instanzwechsel (Precedent: SpielplanViewModel.BeiInstanzAenderungAsync,
        // #307): dieselbe Runde existiert nach dem Neuladen noch — Auswahl anhand der Id wiederherstellen statt
        // sie hart zurückzusetzen. Ohne das ging z.B. eine per "Spielverlauf ansehen"/"Ausführlich simulieren"
        // gesetzte Auswahl bei jedem weiteren, redundant ausgelösten Tab-Wechsel sofort wieder verloren (#308).
        var wiederhergestellteRunde = vorherigeRundeId.HasValue
            ? Runden.FirstOrDefault(r => r.Id == vorherigeRundeId)
            : null;

        if (wiederhergestellteRunde is null)
        {
            await RundeUndSpielZuruecksetzenAsync();
            return;
        }

        RundeStillSetzen(wiederhergestellteRunde);

        await SpiellisteAktualisierenAsync();
    }

    private async Task RundeUndSpielZuruecksetzenAsync()
    {
        RundeStillSetzen(null);

        await BeiRundeAenderungAsync();
    }

    /// <summary>Setzt die Rundenauswahl, ohne das Laden der Runde auszulösen.</summary>
    private void RundeStillSetzen(TurnierRunde? runde)
    {
        using (_ladeUnterdrueckung.Betreten())
            AusgewaehlteRunde = runde;
    }

    /// <summary>Setzt die Spielauswahl, ohne das Laden des Protokolls auszulösen — der Aufrufer lädt selbst.</summary>
    private void SpielStillSetzen(SpielZeile? spiel)
    {
        using (_ladeUnterdrueckung.Betreten())
            AusgewaehlteSpiel = spiel;
    }

    private async Task BeiRundeAenderungAsync()
    {
        Spiele.Clear();
        AusgewaehlteSpiel = null;
        Fehlermeldung     = null;
        AktualisiereBefehle();

        if (AusgewaehlteRunde is null) return;
        await SpiellisteAktualisierenAsync();
    }

    // Lädt die Spielliste der aktuellen Runde neu und stellt die zuvor ausgewählte Spiel-Auswahl anhand
    // ihrer Id wieder her, statt sie hart zurückzusetzen (Precedent: SpielplanViewModel.SpiellisteAktualisierenAsync).
    private async Task SpiellisteAktualisierenAsync()
    {
        if (AusgewaehlteRunde is null) return;
        int rundeId = AusgewaehlteRunde.Id;
        int? spielId = AusgewaehlteSpiel?.Id;

        var liste = await _spielService.AlleNachRundeAsync(rundeId);
        Spiele.Clear();
        SpieleHinzufuegen(liste);

        SpielStillSetzen(spielId.HasValue ? Spiele.FirstOrDefault(s => s.Id == spielId) : null);

        await BeiSpielAuswahlAsync();
        AktualisiereBefehle();
    }

    // Wird beim manuellen Auswaehlen eines Spiels (Breadcrumb-Combobox) ausgeloest — laedt das
    // bestehende Protokoll bei bereits abgeschlossenen Spielen, sonst wird die Vorschau-Karte
    // gezeigt (ZeigeVorschauKarte). Bei programmatischen Neuzuweisungen (Simulieren/Ueberschreiben/
    // Zuruecksetzen/Vorauswahl) wird das per _ladeUnterdrueckung unterdrueckt, da diese Pfade das
    // Protokoll bereits selbst sequenziell laden.
    private async Task BeiSpielAuswahlAsync()
    {
        if (AusgewaehlteSpiel is null) { LeerProtokoll(); return; }

        if (AusgewaehlteSpiel.HatErgebnis)
            await LadeProtokollAsync(AusgewaehlteSpiel.Id);
        else
            LeerProtokoll();
    }

    // ── Turnierspiel simulieren ───────────────────────────────────────────────

    // Issue #164: "Ergebnis überschreiben" als eigener Button entfällt — bei bereits vorhandenem
    // Ergebnis fragt "Simulieren" jetzt per Dialog nach, statt eine zweite, exklusiv geschaltete
    // Aktion zu brauchen.
    private async Task TurnierSpielSimulierenMitSchutzAsync()
    {
        using var schutz = await DbContextSchutz.BetretenAsync();
        await TurnierSpielSimulierenAsync();
    }

    /// <summary>Ohne eigenen Schutz: läuft beim Sprung aus dem Spielplan innerhalb von
    /// <see cref="InstanzSetzenAsync"/>, das den Schutz bereits hält.</summary>
    private async Task TurnierSpielSimulierenAsync()
    {
        if (AusgewaehlteSpiel is null) return;

        bool ueberschreiben = AusgewaehltesSpielAbgeschlossen;
        if (ueberschreiben)
        {
            bool bestaetigt = await _dialogService.BestaetigenAsync(
                "Ergebnis überschreiben",
                "Für dieses Spiel liegt bereits ein Ergebnis vor. Neu simulieren und überschreiben?");
            if (!bestaetigt) return;
        }

        int spielId  = AusgewaehlteSpiel.Id;
        int? rundeId = AusgewaehlteRunde?.Id;
        Fehlermeldung = null;
        IstLadend     = true;
        LeerProtokoll();
        try
        {
            using var vorgang = _logger.Vorgang($"Simulation: Turnierspiel simulieren (Spiel {spielId})", "Spiel wird simuliert …");
            await _simulationsService.SpielSimulierenAsync(
                spielId, SimulationsModus.AusfuehrlicheSimulation, ueberschreiben: ueberschreiben);

            if (rundeId.HasValue) await SpieleNeuLadenAsync(rundeId.Value, spielId);

            await LadeProtokollAsync(spielId);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstLadend = false;
            AktualisiereBefehle();
        }
    }

    private async Task ErgebnisZuruecksetzenAsync()
    {
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (AusgewaehlteSpiel is null) return;
        int spielId  = AusgewaehlteSpiel.Id;
        int? rundeId = AusgewaehlteRunde?.Id;
        Fehlermeldung = null;
        IstLadend     = true;
        try
        {
            using var vorgang = _logger.Vorgang($"Simulation: Ergebnis zurücksetzen (Spiel {spielId})", "Ergebnis wird zurückgesetzt …");
            await _spielService.ErgebnisLoeschenAsync(spielId);

            if (rundeId.HasValue) await SpieleNeuLadenAsync(rundeId.Value, spielId);

            LeerProtokoll();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstLadend = false;
            AktualisiereBefehle();
        }
    }

    /// <summary>Spiele der Runde frisch aus der Datenbank, das bearbeitete Spiel bleibt gewählt; das Protokoll
    /// dazu setzt der Aufrufer.</summary>
    private async Task SpieleNeuLadenAsync(int rundeId, int spielId)
    {
        var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
        Spiele.Clear();
        SpieleHinzufuegen(neueSpiele);
        SpielStillSetzen(Spiele.FirstOrDefault(s => s.Id == spielId));
    }

    // ── Vorauswahl: auto-laden und simulieren ─────────────────────────────────

    private async Task VorauswahlLadenUndSimulierenAsync(TurnierInstanz? instanz, VorausgewaehlteSimulation v)
    {
        if (instanz is null || instanz.Id != v.InstanzId) return;

        IstVorauswahlLadend = true;
        try
        {
            var spiel = await VorauswahlDatenLadenAsync(instanz, v);
            if (spiel is null) return;

            // Issue #240: reine Ansicht eines bereits simulierten Spiels — kein Neu-Simulieren/
            // Überschreiben, nur das vorhandene Protokoll laden. Ohne Ergebnis (z. B. Zurücksetzen
            // zwischen Auslösen und Verarbeiten der Navigation) bleibt es beim Simulieren als Fallback.
            if (v.NurAnzeigen && spiel.HatErgebnis)
                await LadeProtokollAsync(spiel.Id);
            else
                await TurnierSpielSimulierenAsync();
        }
        finally
        {
            IstVorauswahlLadend = false;
        }
    }

    // #317: Instanz→Runden→Spiele-Ladevorgang hinter EINEM Suppression-Fenster statt pro Zuweisung
    // einzeln (Precedent: BeiInstanzAenderungAsync/InstanzSetzenAsync) — sonst kann ein durch
    // Runden.Clear()/Spiele.Clear() ausgelöstes, über die WinUI-DispatcherQueue VERZÖGERTES
    // SelectionChanged noch während der nächsten Abfrage dieser Kette einen ungeschützten
    // Fire-and-Forget-Zugriff auf denselben DbContext starten ("second operation"-Exception). Bewusst
    // NICHT bis LadeProtokollAsync/TurnierSpielSimulierenAsync ausgeweitet — die setzen IstLadend und
    // brauchen ihre eigene, ungebremste OnPropertyChanged-Reaktion.
    private async Task<SpielZeile?> VorauswahlDatenLadenAsync(TurnierInstanz instanz, VorausgewaehlteSimulation v)
    {
        using var unterdrueckung = _ladeUnterdrueckung.Betreten();

        _instanz = instanz;
        AusgewaehlteInstanz = instanz;

        var instanzMitRunden = await _instanzService.FindenNachIdMitRundenAsync(v.InstanzId);
        if (instanzMitRunden is null) return null;

        Runden.Clear();
        foreach (var r in instanzMitRunden.Runden.OrderBy(r => r.Reihenfolge))
            Runden.Add(r);

        var runde = Runden.FirstOrDefault(r => r.Id == v.RundeId);
        if (runde is null) return null;

        AusgewaehlteRunde = runde;

        var liste = await _spielService.AlleNachRundeAsync(v.RundeId);
        Spiele.Clear();
        SpieleHinzufuegen(liste);

        var spiel = Spiele.FirstOrDefault(s => s.Id == v.SpielId);
        if (spiel is not null)
            AusgewaehlteSpiel = spiel;

        return spiel;
    }

    // ── Protokoll ─────────────────────────────────────────────────────────────

    private async Task LadeProtokollAsync(int spielId)
    {
        var spiel = await _spielService.FindenNachIdMitEreignissenAsync(spielId);
        if (spiel is null) return;

        // Fügte bisher nur hinzu, ohne vorher zu leeren — beim Wechsel zwischen zwei bereits simulierten
        // Spielen (Breadcrumb-Combobox oder "Spielverlauf ansehen" aus dem Spielplan) hängten sich die
        // Einträge des neuen Spiels an die des vorherigen an, statt sie zu ersetzen.
        ProtokollKopf = null;
        ProtokollEreignisse.Clear();

        string heim      = spiel.HeimMannschaft?.Name ?? spiel.HeimPlatzhalterName ?? "?";
        string auswaerts = spiel.AuswaertsMannschaft?.Name ?? spiel.AuswaertsPlatzhalterName ?? "?";

        ProtokollSpielInfo = $"{heim} vs. {auswaerts}";

        if (spiel.Ergebnis is { } e)
        {
            byte[]? heimWappen      = MannschaftsIcon.Fuer(spiel.HeimMannschaft);
            byte[]? auswaertsWappen = MannschaftsIcon.Fuer(spiel.AuswaertsMannschaft);

            // Kontextzeile wie bei kicker ("Champions League 2025/26, Finale") plus Hin-/Rückspiel-Kennung
            string bein = spiel.IstRueckspiel ? ", Rückspiel" : spiel.TurnierRunde?.MitRueckspiel == true ? ", Hinspiel" : "";
            string ort = spiel.TurnierRunde?.NeutralerSpielort == true ? " (neutraler Platz)" : "";
            // Issue #360: macht nachvollziehbar, warum hier ein Außenseiter öfter gewinnt.
            string ueberraschung = spiel.TurnierRunde?.Ueberraschungsfaktor is int f and > 0 ? $" · Überraschungsfaktor {f} %" : "";
            string kontext = $"{spiel.TurnierInstanz?.Name}, {spiel.TurnierRunde?.Name}{bein}{ort}{ueberraschung}";
            var ergebnis = new SimulationsErgebnis(
                e.HeimToreRegulaer, e.AuswaertsToreRegulaer,
                e.HeimToreVerlaengerung, e.AuswaertsToreVerlaengerung,
                e.HeimElfmetertore, e.AuswaertsElfmetertore,
                e.EntscheidungArt, [],
                e.HalbzeitHeimTore, e.HalbzeitAuswaertsTore,
                (e.EntscheidungArt is EntscheidungArt.Losentscheid or EntscheidungArt.Muenzwurf) && e.SiegerMannschaftId is int siegerId
                    ? siegerId == spiel.HeimMannschaftId
                    : null);
            var untertitel = new UntertitelQuellen(
                m => m.Land?.Verband?.Kurzname, await _ranglisteService.LigazugehoerigkeitAbrufenAsync());
            ProtokollKopf = SimulationsProtokollHelfer.SpielKopfErstellen(
                kontext, spiel.HeimMannschaft, heim, spiel.AuswaertsMannschaft, auswaerts, ergebnis, untertitel);

            SimulationsProtokollHelfer.ZeitleisteHinzufuegen(
                ProtokollEreignisse,
                spiel.Ereignisse.Select(ev => (ev.Minute, ev.EreignisArt, ev.MannschaftId == spiel.HeimMannschaftId, ev.Beschreibung)),
                heim, auswaerts, e.EntscheidungArt,
                e.HeimToreRegulaer, e.AuswaertsToreRegulaer,
                e.HeimToreVerlaengerung, e.AuswaertsToreVerlaengerung,
                e.HeimElfmetertore, e.AuswaertsElfmetertore,
                heimWappen, auswaertsWappen);
        }

        OnPropertyChanged(nameof(HatProtokoll));
        OnPropertyChanged(nameof(HatProtokollEreignisse));
        OnPropertyChanged(nameof(ZeigeLeerZustand));
    }

    private void LeerProtokoll()
    {
        ProtokollSpielInfo = null;
        ProtokollKopf = null;
        ProtokollEreignisse.Clear();
        OnPropertyChanged(nameof(HatProtokoll));
        OnPropertyChanged(nameof(HatProtokollEreignisse));
        OnPropertyChanged(nameof(ZeigeLeerZustand));
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    private void AktualisiereBefehle()
    {
        ((AsyncRelayCommand)TurnierSpielSimulierenBefehl).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)ErgebnisZuruecksetzenBefehl).NotifyCanExecuteChanged();
    }

    private void SpieleHinzufuegen(IReadOnlyList<Spiel> spiele)
    {
        foreach (var zeile in SpielZeilenMapper.Zeilen(spiele, AusgewaehlteRunde))
            Spiele.Add(zeile);
    }
}
