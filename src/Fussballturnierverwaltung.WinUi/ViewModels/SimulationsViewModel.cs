using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
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

    private TurnierInstanz? _instanz;
    private int _ladeUnterdrueckt;

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

    public ObservableCollection<ProtokollErgebnisZeile>   ProtokollZeilen    { get; } = [];
    public ObservableCollection<EinzelspielEreignisZeile> ProtokollEreignisse { get; } = [];

    public bool HatProtokoll           => ProtokollZeilen.Count > 0;
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
        IDialogService         dialogService)
    {
        _simulationsService = simulationsService;
        _spielService       = spielService;
        _instanzService     = instanzService;
        _dialogService      = dialogService;

        TurnierSpielSimulierenBefehl = new AsyncRelayCommand(TurnierSpielSimulierenAsync, () => KannTurnierSpielSimulieren);
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
        if (Vorauswahl is { } v)
        {
            Vorauswahl = null;
            await VorauswahlLadenUndSimulierenAsync(instanz, v);
            return;
        }

        _instanz = instanz;

        _ladeUnterdrueckt++;
        AusgewaehlteInstanz = instanz;
        _ladeUnterdrueckt--;

        await BeiInstanzAenderungAsync();
    }

    // ── Property-Reaktionen ───────────────────────────────────────────────────

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (_ladeUnterdrueckt > 0) return;

        switch (e.PropertyName)
        {
            case nameof(AusgewaehlteRunde):
                _ = BeiRundeAenderungAsync();
                break;
            case nameof(AusgewaehlteSpiel):
                _ = BeiSpielAuswahlAsync();
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

        _ladeUnterdrueckt++;
        try
        {
            AusgewaehlteRunde = wiederhergestellteRunde;
        }
        finally
        {
            _ladeUnterdrueckt--;
        }

        await SpiellisteAktualisierenAsync();
    }

    private async Task RundeUndSpielZuruecksetzenAsync()
    {
        _ladeUnterdrueckt++;
        try
        {
            AusgewaehlteRunde = null;
        }
        finally
        {
            _ladeUnterdrueckt--;
        }

        await BeiRundeAenderungAsync();
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
        foreach (var (s, i) in liste.Select((x, i) => (x, i)))
            Spiele.Add(MapSpielZeile(s, i));

        _ladeUnterdrueckt++;
        try
        {
            AusgewaehlteSpiel = spielId.HasValue ? Spiele.FirstOrDefault(s => s.Id == spielId) : null;
        }
        finally
        {
            _ladeUnterdrueckt--;
        }

        await BeiSpielAuswahlAsync();
        AktualisiereBefehle();
    }

    // Wird beim manuellen Auswaehlen eines Spiels (Breadcrumb-Combobox) ausgeloest — laedt das
    // bestehende Protokoll bei bereits abgeschlossenen Spielen, sonst wird die Vorschau-Karte
    // gezeigt (ZeigeVorschauKarte). Bei programmatischen Neuzuweisungen (Simulieren/Ueberschreiben/
    // Zuruecksetzen/Vorauswahl) wird das per _ladeUnterdrueckt unterdrueckt, da diese Pfade das
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
            await _simulationsService.SpielSimulierenAsync(
                spielId, SimulationsModus.AusfuehrlicheSimulation, ueberschreiben: ueberschreiben);

            if (rundeId.HasValue)
            {
                var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId.Value);
                Spiele.Clear();
                foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i))) Spiele.Add(MapSpielZeile(s, i));
                _ladeUnterdrueckt++;
                AusgewaehlteSpiel = Spiele.FirstOrDefault(s => s.Id == spielId);
                _ladeUnterdrueckt--;
            }

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
        if (AusgewaehlteSpiel is null) return;
        int spielId  = AusgewaehlteSpiel.Id;
        int? rundeId = AusgewaehlteRunde?.Id;
        Fehlermeldung = null;
        IstLadend     = true;
        try
        {
            await _spielService.ErgebnisLoeschenAsync(spielId);

            if (rundeId.HasValue)
            {
                var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId.Value);
                Spiele.Clear();
                foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i))) Spiele.Add(MapSpielZeile(s, i));
                _ladeUnterdrueckt++;
                AusgewaehlteSpiel = Spiele.FirstOrDefault(s => s.Id == spielId);
                _ladeUnterdrueckt--;
            }

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

    // ── Vorauswahl: auto-laden und simulieren ─────────────────────────────────

    private async Task VorauswahlLadenUndSimulierenAsync(TurnierInstanz? instanz, VorausgewaehlteSimulation v)
    {
        if (instanz is null || instanz.Id != v.InstanzId) return;

        _instanz = instanz;
        _ladeUnterdrueckt++;
        AusgewaehlteInstanz = instanz;
        _ladeUnterdrueckt--;

        var instanzMitRunden = await _instanzService.FindenNachIdMitRundenAsync(v.InstanzId);
        if (instanzMitRunden is null) return;

        Runden.Clear();
        foreach (var r in instanzMitRunden.Runden.OrderBy(r => r.Reihenfolge))
            Runden.Add(r);

        var runde = Runden.FirstOrDefault(r => r.Id == v.RundeId);
        if (runde is null) return;

        _ladeUnterdrueckt++;
        AusgewaehlteRunde = runde;
        _ladeUnterdrueckt--;

        var liste = await _spielService.AlleNachRundeAsync(v.RundeId);
        Spiele.Clear();
        foreach (var (s, i) in liste.Select((x, i) => (x, i)))
            Spiele.Add(MapSpielZeile(s, i));

        var spiel = Spiele.FirstOrDefault(s => s.Id == v.SpielId);
        if (spiel is null) return;

        _ladeUnterdrueckt++;
        AusgewaehlteSpiel = spiel;
        _ladeUnterdrueckt--;

        // Issue #240: reine Ansicht eines bereits simulierten Spiels — kein Neu-Simulieren/
        // Überschreiben, nur das vorhandene Protokoll laden. Ohne Ergebnis (z. B. Zurücksetzen
        // zwischen Auslösen und Verarbeiten der Navigation) bleibt es beim Simulieren als Fallback.
        if (v.NurAnzeigen && spiel.HatErgebnis)
            await LadeProtokollAsync(spiel.Id);
        else
            await TurnierSpielSimulierenAsync();
    }

    // ── Protokoll ─────────────────────────────────────────────────────────────

    private async Task LadeProtokollAsync(int spielId)
    {
        var spiel = await _spielService.FindenNachIdMitEreignissenAsync(spielId);
        if (spiel is null) return;

        string heim      = spiel.HeimMannschaft?.Name ?? spiel.HeimPlatzhalterName ?? "?";
        string auswaerts = spiel.AuswaertsMannschaft?.Name ?? spiel.AuswaertsPlatzhalterName ?? "?";

        ProtokollSpielInfo = $"{heim} vs. {auswaerts}";

        if (spiel.Ergebnis is { } e)
        {
            string detailText = SpielErgebnisFormatierer.DetailText(
                e.EntscheidungArt,
                e.HeimToreRegulaer, e.AuswaertsToreRegulaer,
                e.HeimToreVerlaengerung, e.AuswaertsToreVerlaengerung,
                e.HeimElfmetertore, e.AuswaertsElfmetertore);
            string detail = detailText.Length > 0 ? $" ({detailText})" : "";
            byte[]? heimWappen      = SimulationsProtokollHelfer.MannschaftsIcon(spiel.HeimMannschaft);
            byte[]? auswaertsWappen = SimulationsProtokollHelfer.MannschaftsIcon(spiel.AuswaertsMannschaft);
            ProtokollZeilen.Add(new ProtokollErgebnisZeile(
                null,
                heim, heimWappen,
                $"{e.HeimToreGesamt}:{e.AuswaertsToreGesamt}",
                auswaerts, auswaertsWappen,
                detail));

            SimulationsProtokollHelfer.ZeitleisteHinzufuegen(
                ProtokollEreignisse,
                spiel.Ereignisse.Select(ev => (ev.Minute, ev.EreignisArt, ev.MannschaftId == spiel.HeimMannschaftId)),
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
        ProtokollZeilen.Clear();
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

    private SpielZeile MapSpielZeile(Spiel s, int positionInRunde = 0)
    {
        string text   = "-:-";
        string detail = "";

        if (s.Ergebnis is { } e)
        {
            text = $"{e.HeimToreGesamt}:{e.AuswaertsToreGesamt}";
            detail = SpielErgebnisFormatierer.DetailText(
                e.EntscheidungArt,
                e.HeimToreRegulaer, e.AuswaertsToreRegulaer,
                e.HeimToreVerlaengerung, e.AuswaertsToreVerlaengerung,
                e.HeimElfmetertore, e.AuswaertsElfmetertore);
        }

        return new SpielZeile
        {
            Id             = s.Id,
            Spielnummer    = s.Spielnummer,
            Spieltag       = s.Spieltag,
            KoLabel        = SpielplanViewModel.KoPositionsLabel(s, AusgewaehlteRunde, positionInRunde),
            HeimName       = s.HeimMannschaft?.Name ?? s.HeimPlatzhalterName ?? "?",
            AuswaertsName  = s.AuswaertsMannschaft?.Name ?? s.AuswaertsPlatzhalterName ?? "?",
            HeimWappen      = SimulationsProtokollHelfer.MannschaftsIcon(s.HeimMannschaft),
            AuswaertsWappen = SimulationsProtokollHelfer.MannschaftsIcon(s.AuswaertsMannschaft),
            Status         = s.Status,
            HatErgebnis    = s.Ergebnis is not null,
            ErgebnisText   = text,
            ErgebnisDetail = detail,
            IstRueckspiel  = s.IstRueckspiel,
            IstPlatzhalterSpiel = !s.HeimMannschaftId.HasValue || !s.AuswaertsMannschaftId.HasValue,
        };
    }
}
