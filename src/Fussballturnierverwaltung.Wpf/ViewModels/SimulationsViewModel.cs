using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Wpf.Navigation;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

public record SimulationsModusOption(string Anzeige, SimulationsModus Wert);

public record EinzelspielEreignisZeile(
    string Minute, string Seite, string Art, bool IstHeimSeite,
    bool IstTrenner = false, string? TrennerText = null)
{
    public static EinzelspielEreignisZeile Trenner(string text) =>
        new("", "", "", false, IstTrenner: true, TrennerText: text);
}

/// <summary>
/// Turnierspiel-Simulation innerhalb des Turnier-Bereichs (Simulation-Tab). Die freie Einzelspiel-
/// Simulation wurde in Issue #174 als eigenständiger Menüpunkt ausgelagert, siehe
/// <see cref="EinzelspielSimulationsViewModel"/>.
/// </summary>
public partial class SimulationsViewModel : ViewModelBase, IRibbonAware
{
    private readonly SimulationsService    _simulationsService;
    private readonly SpielErgebnisService  _spielService;
    private readonly TurnierInstanzService _instanzService;
    private readonly AusgewaehlteTurnierInstanzService _turnierAuswahlService;

    private int _ladeUnterdrückt;

    // ── Turnier/Runde/Spiel-Auswahl ──────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<TurnierInstanz> _instanzen = [];
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

    public ObservableCollection<string>                   ProtokollZeilen    { get; } = [];
    public ObservableCollection<EinzelspielEreignisZeile> ProtokollEreignisse { get; } = [];

    public bool HatProtokoll          => ProtokollZeilen.Count > 0;
    public bool HatProtokollEreignisse => ProtokollEreignisse.Count > 0;

    // ── Status ────────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KannTurnierSpielSimulieren))]
    [NotifyPropertyChangedFor(nameof(KannTurnierErgebnisZuruecksetzen))]
    private bool    _istLadend;

    [ObservableProperty] private string? _fehlermeldung;

    // ── Vorauswahl (gesetzt vom SpielplanViewModel vor AktivierenAsync) ───────

    public VorausgewaehlteSimulation? Vorauswahl { get; set; }

    // ── Befehle ───────────────────────────────────────────────────────────────

    public ICommand TurnierSpielSimulierenBefehl             { get; }
    public ICommand TurnierSpielAusfuehrlichSimulierenBefehl { get; }
    public ICommand ErgebnisZuruecksetzenBefehl              { get; }

    // ── IRibbonAware (Issue #164) ────────────────────────────────────────────
    // Weder Neu/Speichern/Abbrechen haben hier eine Entsprechung (Simulieren schreibt sofort in
    // die DB, kein separater Save-Schritt) — HatStandardAktionen bewusst false (Issue #173). Löschen
    // entspricht aber sinnvoll "Ergebnis zurücksetzen" — HatLoeschenAktion bewusst true, LoeschenBefehl
    // auf ErgebnisZuruecksetzenBefehl verdrahtet, kein eigener Ribbon-Zusatz-Button dafür mehr nötig.
    // "Ergebnis überschreiben" als eigener Button entfällt ebenfalls — TurnierSpielSimulierenAsync
    // fragt bei vorhandenem Ergebnis per Dialog nach. "Simulieren" als SplitButton mit "Ausführliche
    // Simulation" im Dropdown (analog "Spiel simulieren" in #163).
    public string   RibbonTabTitel  => "Simulation";
    public ICommand? NeuBefehl       => null;
    public ICommand? SpeichernBefehl => null;
    public ICommand? AbbrechenBefehl => null;
    public ICommand? LoeschenBefehl  => ErgebnisZuruecksetzenBefehl;
    public bool HatStandardAktionen => false;
    public bool HatLoeschenAktion   => true;

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
                    Unteraktionen =
                    [
                        new RibbonAktion { Header = "Ausführliche Simulation", Befehl = TurnierSpielAusfuehrlichSimulierenBefehl, IconVektorDatei = "magnifying_glass.xaml" },
                    ],
                },
            ],
        },
    ];

    // ── Konstruktor ───────────────────────────────────────────────────────────

    public SimulationsViewModel(
        SimulationsService    simulationsService,
        SpielErgebnisService  spielService,
        TurnierInstanzService instanzService,
        AusgewaehlteTurnierInstanzService turnierAuswahlService)
    {
        _simulationsService  = simulationsService;
        _spielService        = spielService;
        _instanzService      = instanzService;
        _turnierAuswahlService = turnierAuswahlService;

        TurnierSpielSimulierenBefehl             = new AsyncRelayCommand(() => TurnierSpielSimulierenAsync(SimulationsModus.SchnellSimulation), () => KannTurnierSpielSimulieren);
        TurnierSpielAusfuehrlichSimulierenBefehl = new AsyncRelayCommand(() => TurnierSpielSimulierenAsync(SimulationsModus.AusfuehrlicheSimulation), () => KannTurnierSpielSimulieren);
        ErgebnisZuruecksetzenBefehl              = new AsyncRelayCommand(ErgebnisZuruecksetzenAsync, () => KannTurnierErgebnisZuruecksetzen);
    }

    // ── Initialisierung ───────────────────────────────────────────────────────

    public override async Task AktivierenAsync()
    {
        var instanzListe = await _instanzService.AlleAbrufenAsync();

        Instanzen.Clear();
        foreach (var i in instanzListe) Instanzen.Add(i);

        if (Vorauswahl is { } v)
        {
            Vorauswahl = null;
            await VorauswahlLadenUndSimulierenAsync(v);
        }
        else
        {
            AusgewaehlteInstanz = Instanzen.FirstOrDefault(
                i => i.Id == _turnierAuswahlService.TurnierInstanzId);
        }
    }

    // ── Property-Reaktionen ───────────────────────────────────────────────────

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (_ladeUnterdrückt > 0) return;

        switch (e.PropertyName)
        {
            case nameof(AusgewaehlteInstanz):
                _turnierAuswahlService.TurnierInstanzId = AusgewaehlteInstanz?.Id;
                _ = BeiInstanzAenderungAsync();
                break;
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
        Runden.Clear();
        Spiele.Clear();
        AusgewaehlteRunde = null;
        AusgewaehlteSpiel = null;
        Fehlermeldung     = null;
        AktualisiereBefehle();

        if (AusgewaehlteInstanz is null) return;

        var instanz = await _instanzService.FindenNachIdMitRundenAsync(AusgewaehlteInstanz.Id);
        if (instanz is null) return;

        foreach (var r in instanz.Runden.OrderBy(r => r.Reihenfolge))
            Runden.Add(r);
    }

    private async Task BeiRundeAenderungAsync()
    {
        Spiele.Clear();
        AusgewaehlteSpiel = null;
        Fehlermeldung     = null;
        AktualisiereBefehle();

        if (AusgewaehlteRunde is null) return;
        await LadeSpiellisteAsync();
    }

    // Wird beim manuellen Auswaehlen eines Spiels (Breadcrumb-Dropdown) ausgeloest — laedt das
    // bestehende Protokoll bei bereits abgeschlossenen Spielen, sonst wird die Vorschau-Karte
    // gezeigt (ZeigeVorschauKarte). Bei programmatischen Neuzuweisungen (Simulieren/Ueberschreiben/
    // Zuruecksetzen/Vorauswahl) wird das per _ladeUnterdrückt unterdrueckt, da diese Pfade das
    // Protokoll bereits selbst sequenziell laden — siehe [[feedback-dbcontext-concurrency]].
    private async Task BeiSpielAuswahlAsync()
    {
        if (AusgewaehlteSpiel is null) { LeerProtokolll(); return; }

        if (AusgewaehlteSpiel.HatErgebnis)
            await LadeProtokollAsync(AusgewaehlteSpiel.Id);
        else
            LeerProtokolll();
    }

    private async Task LadeSpiellisteAsync()
    {
        if (AusgewaehlteRunde is null) return;

        var liste = await _spielService.AlleNachRundeAsync(AusgewaehlteRunde.Id);
        Spiele.Clear();
        foreach (var (s, i) in liste.Select((x, i) => (x, i)))
            Spiele.Add(MapSpielZeile(s, i));
    }

    // ── Turnierspiel simulieren ───────────────────────────────────────────────

    // Issue #164: "Ergebnis überschreiben" als eigener Button entfällt — bei bereits vorhandenem
    // Ergebnis fragt "Simulieren" jetzt per Dialog nach, statt eine zweite, exklusiv geschaltete
    // Aktion zu brauchen.
    private async Task TurnierSpielSimulierenAsync(SimulationsModus modus)
    {
        if (AusgewaehlteSpiel is null) return;

        bool ueberschreiben = AusgewaehltesSpielAbgeschlossen;
        if (ueberschreiben)
        {
            var antwort = MessageBox.Show(
                "Für dieses Spiel liegt bereits ein Ergebnis vor. Neu simulieren und überschreiben?",
                "Ergebnis überschreiben",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (antwort != MessageBoxResult.Yes) return;
        }

        int spielId  = AusgewaehlteSpiel.Id;
        int? rundeId = AusgewaehlteRunde?.Id;
        Fehlermeldung = null;
        IstLadend     = true;
        LeerProtokolll();
        try
        {
            await _simulationsService.SpielSimulierenAsync(spielId, modus, ueberschreiben: ueberschreiben);

            if (rundeId.HasValue)
            {
                var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId.Value);
                Spiele.Clear();
                foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i))) Spiele.Add(MapSpielZeile(s, i));
                _ladeUnterdrückt++;
                AusgewaehlteSpiel = Spiele.FirstOrDefault(s => s.Id == spielId);
                _ladeUnterdrückt--;
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
                _ladeUnterdrückt++;
                AusgewaehlteSpiel = Spiele.FirstOrDefault(s => s.Id == spielId);
                _ladeUnterdrückt--;
            }

            LeerProtokolll();
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

    private async Task VorauswahlLadenUndSimulierenAsync(VorausgewaehlteSimulation v)
    {
        var instanz = Instanzen.FirstOrDefault(i => i.Id == v.InstanzId);
        if (instanz is null) return;

        // Instanz setzen ohne reaktives Nachladen (das machen wir manuell)
        _ladeUnterdrückt++;
        AusgewaehlteInstanz = instanz;
        _ladeUnterdrückt--;
        _turnierAuswahlService.TurnierInstanzId = instanz.Id;

        var instanzMitRunden = await _instanzService.FindenNachIdMitRundenAsync(v.InstanzId);
        if (instanzMitRunden is null) return;

        Runden.Clear();
        foreach (var r in instanzMitRunden.Runden.OrderBy(r => r.Reihenfolge))
            Runden.Add(r);

        var runde = Runden.FirstOrDefault(r => r.Id == v.RundeId);
        if (runde is null) return;

        _ladeUnterdrückt++;
        AusgewaehlteRunde = runde;
        _ladeUnterdrückt--;

        var liste = await _spielService.AlleNachRundeAsync(v.RundeId);
        Spiele.Clear();
        foreach (var (s, i) in liste.Select((x, i) => (x, i)))
            Spiele.Add(MapSpielZeile(s, i));

        var spiel = Spiele.FirstOrDefault(s => s.Id == v.SpielId);
        if (spiel is null) return;

        _ladeUnterdrückt++;
        AusgewaehlteSpiel  = spiel;
        _ladeUnterdrückt--;

        await TurnierSpielSimulierenAsync(SimulationsModus.AusfuehrlicheSimulation);
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
            string ergebnis = $"{heim}  {e.HeimToreGesamt}:{e.AuswaertsToreGesamt}  {auswaerts}";
            string detailText = SpielErgebnisFormatierer.DetailText(
                e.EntscheidungArt,
                e.HeimToreRegulaer, e.AuswaertsToreRegulaer,
                e.HeimToreVerlaengerung, e.AuswaertsToreVerlaengerung,
                e.HeimElfmetertore, e.AuswaertsElfmetertore);
            string detail = detailText.Length > 0 ? $" ({detailText})" : "";
            ProtokollZeilen.Add(ergebnis + detail);

            SimulationsProtokollHelfer.ZeitleisteHinzufuegen(
                ProtokollEreignisse,
                spiel.Ereignisse.Select(ev => (ev.Minute, ev.EreignisArt, ev.MannschaftId == spiel.HeimMannschaftId)),
                heim, auswaerts, e.EntscheidungArt,
                e.HeimToreRegulaer, e.AuswaertsToreRegulaer,
                e.HeimToreVerlaengerung, e.AuswaertsToreVerlaengerung,
                e.HeimElfmetertore, e.AuswaertsElfmetertore);
        }

        OnPropertyChanged(nameof(HatProtokoll));
        OnPropertyChanged(nameof(HatProtokollEreignisse));
        OnPropertyChanged(nameof(ZeigeLeerZustand));
    }

    private void LeerProtokolll()
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
        ((AsyncRelayCommand)TurnierSpielAusfuehrlichSimulierenBefehl).NotifyCanExecuteChanged();
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
            Status         = s.Status,
            HatErgebnis    = s.Ergebnis is not null,
            ErgebnisText   = text,
            ErgebnisDetail = detail,
            IstRueckspiel  = s.IstRueckspiel,
            IstPlatzhalterSpiel = !s.HeimMannschaftId.HasValue || !s.AuswaertsMannschaftId.HasValue,
            HeimRating      = s.HeimMannschaft?.GesamtRating,
            AuswaertsRating = s.AuswaertsMannschaft?.GesamtRating
        };
    }
}
