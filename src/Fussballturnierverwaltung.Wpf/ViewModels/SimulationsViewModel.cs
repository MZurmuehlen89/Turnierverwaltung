using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
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

public partial class SimulationsViewModel : ViewModelBase, IRibbonAware
{
    private readonly SimulationsService    _simulationsService;
    private readonly SpielErgebnisService  _spielService;
    private readonly TurnierInstanzService _instanzService;
    private readonly MannschaftService     _mannschaftService;
    private readonly RegelwerkService      _regelwerkService;
    private readonly VerbandService        _verbandService;
    private readonly AusgewaehlteTurnierInstanzService _turnierAuswahlService;

    private int _ladeUnterdrückt;

    // ── Editor-Modus ──────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstTurnierModus))]
    [NotifyPropertyChangedFor(nameof(IstEinzelspielModus))]
    [NotifyPropertyChangedFor(nameof(ZeigeVorschauKarte))]
    [NotifyPropertyChangedFor(nameof(ZeigeLeerZustand))]
    [NotifyPropertyChangedFor(nameof(KannTurnierSpielSimulieren))]
    [NotifyPropertyChangedFor(nameof(KannTurnierErgebnisZuruecksetzen))]
    [NotifyPropertyChangedFor(nameof(EinzelspielKannSimuliert))]
    private int _modusIndex = 0;

    public bool IstTurnierModus     => ModusIndex == 0;
    public bool IstEinzelspielModus => ModusIndex == 1;

    // ── Linke Spalte: Turnierspiel-Modus ─────────────────────────────────────

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

    // Issue #164: "Simulieren" ist jetzt immer verfügbar (auch mit vorhandenem Ergebnis) — fragt bei
    // Bedarf per Dialog nach Überschreiben, statt eine exklusiv geschaltete zweite Aktion zu
    // brauchen (siehe TurnierSpielSimulierenAsync). IstTurnierModus explizit geprüft, vormals nur
    // implizit durch Verschachtelung in der Turnier-Modus-Breadcrumb-Leiste sichergestellt.
    public bool KannTurnierSpielSimulieren =>
        IstTurnierModus && AusgewaehlteSpiel is { IstPlatzhalterSpiel: false } && !IstLadend;

    public bool KannTurnierErgebnisZuruecksetzen =>
        IstTurnierModus && AusgewaehlteSpiel is { IstPlatzhalterSpiel: false } && !IstLadend
        && AusgewaehltesSpielAbgeschlossen;

    public bool AusgewaehltesSpielAbgeschlossen => AusgewaehlteSpiel?.HatErgebnis == true;

    public bool ZeigeVorschauKarte =>
        IstTurnierModus && AusgewaehlteSpiel is { IstPlatzhalterSpiel: false } && !AusgewaehltesSpielAbgeschlossen;

    public bool ZeigeLeerZustand => !HatProtokoll && !ZeigeVorschauKarte;

    // ── Linke Spalte: Einzelspiel-Modus ──────────────────────────────────────

    [ObservableProperty] private ObservableCollection<Mannschaft> _alleMannschaften = [];
    [ObservableProperty] private ObservableCollection<Regelwerk>  _alleRegelwerke   = [];
    [ObservableProperty] private ObservableCollection<Verband>    _alleVerbaende    = [];

    private static readonly VerbandFilterOption AllesVerband = new("— Alle —", null);

    public IReadOnlyList<VerbandFilterOption> VerbandFilterOptionen =>
        [AllesVerband, ..AlleVerbaende.Select(v => new VerbandFilterOption(v.Name, v))];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EinzelspielKannSimuliert))]
    private Mannschaft? _einzelspielHeim;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EinzelspielKannSimuliert))]
    private Mannschaft? _einzelspielAuswaerts;

    [ObservableProperty] private Regelwerk? _einzelspielRegelwerk;
    [ObservableProperty] private bool       _einzelspielMitRueckspiel;

    // Verband-Filter + Suche je Seite — Heim und Auswaerts unabhaengig voneinander filterbar.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeimMannschaftenGefiltert))]
    private VerbandFilterOption _heimVerbandFilter = AllesVerband;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeimMannschaftenGefiltert))]
    private string _heimSuchbegriff = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AuswaertsMannschaftenGefiltert))]
    private VerbandFilterOption _auswaertsVerbandFilter = AllesVerband;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AuswaertsMannschaftenGefiltert))]
    private string _auswaertsSuchbegriff = "";

    public IEnumerable<Mannschaft> HeimMannschaftenGefiltert => AlleMannschaften
        .Where(m => HeimVerbandFilter.Wert is null || m.Land?.VerbandId == HeimVerbandFilter.Wert.Id)
        .Where(m => string.IsNullOrWhiteSpace(HeimSuchbegriff)
            || m.Name.Contains(HeimSuchbegriff, StringComparison.OrdinalIgnoreCase)
            || m.Kurzname.Contains(HeimSuchbegriff, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<Mannschaft> AuswaertsMannschaftenGefiltert => AlleMannschaften
        .Where(m => AuswaertsVerbandFilter.Wert is null || m.Land?.VerbandId == AuswaertsVerbandFilter.Wert.Id)
        .Where(m => string.IsNullOrWhiteSpace(AuswaertsSuchbegriff)
            || m.Name.Contains(AuswaertsSuchbegriff, StringComparison.OrdinalIgnoreCase)
            || m.Kurzname.Contains(AuswaertsSuchbegriff, StringComparison.OrdinalIgnoreCase));

    public bool EinzelspielKannSimuliert =>
        IstEinzelspielModus &&
        EinzelspielHeim is not null &&
        EinzelspielAuswaerts is not null &&
        EinzelspielHeim != EinzelspielAuswaerts &&
        !IstLadend;

    // ── Protokoll (beide Modi) ────────────────────────────────────────────────

    [ObservableProperty] private string? _protokollSpielInfo;

    public ObservableCollection<string>                   ProtokollZeilen    { get; } = [];
    public ObservableCollection<EinzelspielEreignisZeile> ProtokollEreignisse { get; } = [];

    public bool HatProtokoll          => ProtokollZeilen.Count > 0;
    public bool HatProtokollEreignisse => ProtokollEreignisse.Count > 0;

    // ── Status ────────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KannTurnierSpielSimulieren))]
    [NotifyPropertyChangedFor(nameof(KannTurnierErgebnisZuruecksetzen))]
    [NotifyPropertyChangedFor(nameof(EinzelspielKannSimuliert))]
    private bool    _istLadend;

    [ObservableProperty] private string? _fehlermeldung;

    // ── Vorauswahl (gesetzt vom SpielplanViewModel vor AktivierenAsync) ───────

    public VorausgewaehlteSimulation? Vorauswahl { get; set; }

    // ── Befehle ───────────────────────────────────────────────────────────────

    public ICommand TurnierSpielSimulierenBefehl             { get; }
    public ICommand TurnierSpielAusfuehrlichSimulierenBefehl { get; }
    public ICommand EinzelspielFreiBefehl                    { get; }
    public ICommand EinzelspielFreiAusfuehrlichBefehl        { get; }
    public ICommand ErgebnisZuruecksetzenBefehl              { get; }

    // ── IRibbonAware (Issue #164) ────────────────────────────────────────────
    // Weder Neu/Speichern/Abbrechen haben hier eine Entsprechung (Simulieren schreibt sofort in
    // die DB, kein separater Save-Schritt) — HatStandardAktionen bewusst false (Issue #173). Löschen
    // entspricht aber sinnvoll "Ergebnis zurücksetzen" — HatLoeschenAktion bewusst true, LoeschenBefehl
    // auf ErgebnisZuruecksetzenBefehl verdrahtet, kein eigener Ribbon-Zusatz-Button dafür mehr nötig.
    // "Ergebnis überschreiben" als eigener Button entfällt ebenfalls — TurnierSpielSimulierenAsync
    // fragt bei vorhandenem Ergebnis per Dialog nach. Beide "…simulieren"-Aktionen als SplitButton
    // mit "Ausführliche Simulation" im Dropdown (analog "Spiel simulieren" in #163).
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
                new RibbonAktion
                {
                    Header = "Einzelspiel simulieren",
                    Befehl = EinzelspielFreiBefehl,
                    IconGlyph = "",
                    Unteraktionen =
                    [
                        new RibbonAktion { Header = "Ausführliche Simulation", Befehl = EinzelspielFreiAusfuehrlichBefehl, IconVektorDatei = "magnifying_glass.xaml" },
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
        MannschaftService     mannschaftService,
        RegelwerkService      regelwerkService,
        VerbandService        verbandService,
        AusgewaehlteTurnierInstanzService turnierAuswahlService)
    {
        _simulationsService  = simulationsService;
        _spielService        = spielService;
        _instanzService      = instanzService;
        _mannschaftService   = mannschaftService;
        _regelwerkService    = regelwerkService;
        _verbandService      = verbandService;
        _turnierAuswahlService = turnierAuswahlService;

        TurnierSpielSimulierenBefehl             = new AsyncRelayCommand(() => TurnierSpielSimulierenAsync(SimulationsModus.SchnellSimulation), () => KannTurnierSpielSimulieren);
        TurnierSpielAusfuehrlichSimulierenBefehl = new AsyncRelayCommand(() => TurnierSpielSimulierenAsync(SimulationsModus.AusfuehrlicheSimulation), () => KannTurnierSpielSimulieren);
        EinzelspielFreiBefehl                    = new AsyncRelayCommand(() => EinzelspielFreiSimulierenAsync(SimulationsModus.SchnellSimulation), () => EinzelspielKannSimuliert);
        EinzelspielFreiAusfuehrlichBefehl        = new AsyncRelayCommand(() => EinzelspielFreiSimulierenAsync(SimulationsModus.AusfuehrlicheSimulation), () => EinzelspielKannSimuliert);
        ErgebnisZuruecksetzenBefehl              = new AsyncRelayCommand(ErgebnisZuruecksetzenAsync, () => KannTurnierErgebnisZuruecksetzen);
    }

    // ── Initialisierung ───────────────────────────────────────────────────────

    public override async Task AktivierenAsync()
    {
        var instanzListe     = await _instanzService.AlleAbrufenAsync();
        var mannschaftsListe = await _mannschaftService.AlleAbrufenMitLandAsync();
        var regelwerkListe   = await _regelwerkService.AlleAbrufenAsync();
        var verbandListe     = await _verbandService.AlleAbrufenAsync();

        Instanzen.Clear();
        foreach (var i in instanzListe) Instanzen.Add(i);

        AlleMannschaften.Clear();
        foreach (var m in mannschaftsListe.OrderBy(m => m.Name)) AlleMannschaften.Add(m);
        OnPropertyChanged(nameof(HeimMannschaftenGefiltert));
        OnPropertyChanged(nameof(AuswaertsMannschaftenGefiltert));

        AlleRegelwerke.Clear();
        foreach (var r in regelwerkListe) AlleRegelwerke.Add(r);

        AlleVerbaende.Clear();
        foreach (var verband in verbandListe.OrderBy(x => x.Name)) AlleVerbaende.Add(verband);
        OnPropertyChanged(nameof(VerbandFilterOptionen));

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
            case nameof(ModusIndex):
                Fehlermeldung = null;
                AktualisiereBefehle();
                break;
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
            case nameof(EinzelspielHeim):
            case nameof(EinzelspielAuswaerts):
                AktualisiereBefehle();
                break;
        }
    }

    // ── Selektions-Handler (Turnier-Modus) ────────────────────────────────────

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
    // Aktion zu brauchen (KannTurnierSpielSimulieren prüft daher auch nicht mehr
    // AusgewaehltesSpielAbgeschlossen).
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

    // ── Einzelspiel-Modus: Freie Simulation ──────────────────────────────────

    private async Task EinzelspielFreiSimulierenAsync(SimulationsModus modus)
    {
        if (EinzelspielHeim is null || EinzelspielAuswaerts is null) return;

        var heim      = EinzelspielHeim;
        var auswaerts = EinzelspielAuswaerts;
        var verfahren = EinzelspielRegelwerk?.Entscheidungsverfahren.ToList()
                        ?? (IReadOnlyList<EntscheidungsVerfahren>)[];

        Fehlermeldung = null;
        IstLadend     = true;
        LeerProtokolll();

        await Task.Yield();

        try
        {
            if (EinzelspielMitRueckspiel)
            {
                var rng  = new Random();
                var erg1 = _simulationsService.EinzelspielSimulieren(heim, auswaerts, verfahren, modus, rng);
                var erg2 = _simulationsService.EinzelspielSimulieren(auswaerts, heim, verfahren, modus, rng);

                int heimGesamt      = (erg1.HeimToreRegulaer     + (erg1.HeimToreVerlaengerung ?? 0))
                                    + (erg2.AuswaertsToreRegulaer + (erg2.AuswaertsToreVerlaengerung ?? 0));
                int auswaertsGesamt = (erg1.AuswaertsToreRegulaer + (erg1.AuswaertsToreVerlaengerung ?? 0))
                                    + (erg2.HeimToreRegulaer      + (erg2.HeimToreVerlaengerung ?? 0));

                string weiter = heimGesamt > auswaertsGesamt ? heim.Name
                              : heimGesamt < auswaertsGesamt ? auswaerts.Name
                              : "Unentschieden";

                ProtokollSpielInfo = $"{heim.Name} vs. {auswaerts.Name} (Hin & Rück)";
                ProtokollZeilen.Add($"Hinspiel:   {FormatErgebnisZeile(erg1, heim.Name, auswaerts.Name)}");
                ProtokollZeilen.Add($"Rückspiel:  {FormatErgebnisZeile(erg2, auswaerts.Name, heim.Name)}");
                ProtokollZeilen.Add($"Gesamt:     {heim.Name}  {heimGesamt}:{auswaertsGesamt}  {auswaerts.Name}");
                ProtokollZeilen.Add($"Weiter:     {weiter}");

                if (modus == SimulationsModus.AusfuehrlicheSimulation)
                {
                    ZeitleisteHinzufuegen(
                        erg1.Ereignisse.Select(ev => (ev.Minute, ev.Art, ev.IstHeim)),
                        heim.Name, auswaerts.Name, erg1.EntscheidungArt,
                        erg1.HeimToreRegulaer, erg1.AuswaertsToreRegulaer,
                        erg1.HeimToreVerlaengerung, erg1.AuswaertsToreVerlaengerung,
                        erg1.HeimElfmetertore, erg1.AuswaertsElfmetertore,
                        minutenPraefix: "Hin");

                    // erg2 wurde mit vertauschten Rollen simuliert (Auswärts spielt zu Hause) —
                    // Seiten und Tore auf die feste Team-Identität (heim/auswaerts) zurückspiegeln.
                    ZeitleisteHinzufuegen(
                        erg2.Ereignisse.Select(ev => (ev.Minute, ev.Art, !ev.IstHeim)),
                        heim.Name, auswaerts.Name, erg2.EntscheidungArt,
                        erg2.AuswaertsToreRegulaer, erg2.HeimToreRegulaer,
                        erg2.AuswaertsToreVerlaengerung, erg2.HeimToreVerlaengerung,
                        erg2.AuswaertsElfmetertore, erg2.HeimElfmetertore,
                        minutenPraefix: "Rück");
                }
            }
            else
            {
                var erg = _simulationsService.EinzelspielSimulieren(heim, auswaerts, verfahren, modus);
                ProtokollSpielInfo = $"{heim.Name} vs. {auswaerts.Name}";
                ProtokollZeilen.Add(FormatErgebnisZeile(erg, heim.Name, auswaerts.Name));

                if (modus == SimulationsModus.AusfuehrlicheSimulation)
                {
                    ZeitleisteHinzufuegen(
                        erg.Ereignisse.Select(ev => (ev.Minute, ev.Art, ev.IstHeim)),
                        heim.Name, auswaerts.Name, erg.EntscheidungArt,
                        erg.HeimToreRegulaer, erg.AuswaertsToreRegulaer,
                        erg.HeimToreVerlaengerung, erg.AuswaertsToreVerlaengerung,
                        erg.HeimElfmetertore, erg.AuswaertsElfmetertore);
                }
            }
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstLadend = false;
            AktualisiereBefehle();
            OnPropertyChanged(nameof(HatProtokoll));
            OnPropertyChanged(nameof(HatProtokollEreignisse));
            OnPropertyChanged(nameof(ZeigeLeerZustand));
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

        // In Turnierspiel-Modus wechseln
        _ladeUnterdrückt++;
        ModusIndex = 0;
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

            ZeitleisteHinzufuegen(
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
        ((AsyncRelayCommand)EinzelspielFreiBefehl).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)EinzelspielFreiAusfuehrlichBefehl).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)ErgebnisZuruecksetzenBefehl).NotifyCanExecuteChanged();
    }

    private static string FormatErgebnisZeile(SimulationsErgebnis e, string heimName, string auswaertsName)
    {
        int heim = e.HeimToreRegulaer + (e.HeimToreVerlaengerung ?? 0);
        int gast = e.AuswaertsToreRegulaer + (e.AuswaertsToreVerlaengerung ?? 0);

        string detail = e.EntscheidungArt switch
        {
            EntscheidungArt.Verlaengerung     => " (n.V.)",
            EntscheidungArt.GoldenGoal        => " (G.G.)",
            EntscheidungArt.SilverGoal        => " (S.G.)",
            EntscheidungArt.Elfmeterschiessen => $" (i.E. {e.HeimElfmetertore}:{e.AuswaertsElfmetertore})",
            EntscheidungArt.Auswaertstorregel => " (A.T.)",
            EntscheidungArt.Losentscheid      => " (Los)",
            EntscheidungArt.Muenzwurf         => " (Münze)",
            _                                 => ""
        };

        return $"{heimName}  {heim}:{gast}  {auswaertsName}{detail}";
    }

    // Baut die Ereigniszeitleiste inkl. Abschnittstrenner (Halbzeit, 90 Minuten,
    // Halbzeit/Ende Verlängerung, Elfmeterschießen) für ProtokollEreignisse auf.
    private void ZeitleisteHinzufuegen(
        IEnumerable<(int Minute, EreignisArt Art, bool IstHeimSeite)> ereignisse,
        string heimName, string auswaertsName,
        EntscheidungArt entscheidungArt,
        int heimToreRegulaer, int auswaertsToreRegulaer,
        int? heimToreVerlaengerung, int? auswaertsToreVerlaengerung,
        int? heimElfmetertore, int? auswaertsElfmetertore,
        string? minutenPraefix = null)
    {
        // Schnellsimulation persistiert keine Einzelereignisse (siehe SimulationsService.SpeichernAsync,
        // sim.Ereignisse.Any()-Gate) — ohne diesen fruehen Ausstieg wuerde SchwellenPruefen(int.MaxValue)
        // unten trotzdem einen "HALBZEIT"-Trenner einfuegen und eine leere Zeitleiste vortaeuschen.
        var ereignisListe = ereignisse.ToList();
        if (ereignisListe.Count == 0) return;

        // Verlaengerung fand statt, sobald die Verlaengerungs-Torfelder gesetzt sind — unabhaengig
        // von der finalen EntscheidungArt (auch bei Elfmeterschiessen ging dem immer eine
        // vollstaendige Verlaengerung voraus, siehe BerechneEntscheidungArt in SpielplanViewModel).
        bool hatVerlaengerung = heimToreVerlaengerung.HasValue && auswaertsToreVerlaengerung.HasValue;
        bool hatElfmeter      = entscheidungArt == EntscheidungArt.Elfmeterschiessen;

        bool halbzeitEingefuegt = false, neunzigEingefuegt = false, vzHalbzeitEingefuegt = false;
        int heimStand = 0, auswaertsStand = 0;

        void SchwellenPruefen(int minute)
        {
            if (!halbzeitEingefuegt && minute > 45)
            {
                ProtokollEreignisse.Add(EinzelspielEreignisZeile.Trenner($"HALBZEIT ({heimStand}:{auswaertsStand})"));
                halbzeitEingefuegt = true;
            }
            if (!neunzigEingefuegt && (hatVerlaengerung || hatElfmeter) && minute > 90)
            {
                ProtokollEreignisse.Add(EinzelspielEreignisZeile.Trenner($"90 MINUTEN ({heimToreRegulaer}:{auswaertsToreRegulaer})"));
                neunzigEingefuegt = true;
            }
            if (!vzHalbzeitEingefuegt && hatVerlaengerung && minute > 105)
            {
                ProtokollEreignisse.Add(EinzelspielEreignisZeile.Trenner("HALBZEIT VERLÄNGERUNG"));
                vzHalbzeitEingefuegt = true;
            }
        }

        static string ArtText(EreignisArt art) => art switch
        {
            EreignisArt.Tor        => "Tor",
            EreignisArt.GelbeKarte => "Gelbe Karte",
            EreignisArt.RoteKarte  => "Rote Karte",
            EreignisArt.GoldenGoal => "Golden Goal",
            EreignisArt.SilverGoal => "Silver Goal",
            _                      => art.ToString()
        };

        foreach (var (minute, art, istHeimSeite) in ereignisListe.OrderBy(ev => ev.Minute))
        {
            SchwellenPruefen(minute);

            if (art is EreignisArt.Tor or EreignisArt.GoldenGoal or EreignisArt.SilverGoal)
            {
                if (istHeimSeite) heimStand++; else auswaertsStand++;
            }

            string minuteText = minutenPraefix is null ? $"{minute}'" : $"{minutenPraefix} {minute}'";
            ProtokollEreignisse.Add(new EinzelspielEreignisZeile(
                minuteText, istHeimSeite ? heimName : auswaertsName, ArtText(art), istHeimSeite));
        }

        // Noch nicht erreichte Schwellen (z. B. Halbzeit ohne Tor, Spielende ohne Ereignis danach) nachtragen
        SchwellenPruefen(int.MaxValue);

        if (hatVerlaengerung)
        {
            int heimGesamt      = heimToreRegulaer      + (heimToreVerlaengerung ?? 0);
            int auswaertsGesamt = auswaertsToreRegulaer + (auswaertsToreVerlaengerung ?? 0);
            ProtokollEreignisse.Add(EinzelspielEreignisZeile.Trenner($"ENDE VERLÄNGERUNG ({heimGesamt}:{auswaertsGesamt})"));
        }

        if (hatElfmeter)
            ProtokollEreignisse.Add(EinzelspielEreignisZeile.Trenner($"ELFMETERSCHIESSEN ({heimElfmetertore}:{auswaertsElfmetertore})"));
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
