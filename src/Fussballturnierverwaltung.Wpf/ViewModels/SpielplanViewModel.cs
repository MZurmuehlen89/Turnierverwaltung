using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Spiele.Eingaben;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Wpf.Converters;
using Fussballturnierverwaltung.Wpf.Navigation;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

// ── Hilfsklassen ──────────────────────────────────────────────────────────────

public class SpielZeile
{
    public int Id { get; init; }
    public int Spielnummer { get; init; }
    public int? Spieltag { get; init; }
    public string? KoLabel { get; init; }
    public string SpieltagAnzeige => KoLabel ?? (Spieltag.HasValue ? $"ST {Spieltag}" : "-");
    public string HeimName { get; init; } = "";
    public string AuswaertsName { get; init; } = "";
    public SpielStatus Status { get; init; }
    public bool HatErgebnis { get; init; }
    public string ErgebnisText { get; init; } = "-:-";
    public string ErgebnisDetail { get; init; } = "";
    public bool IstRueckspiel { get; init; }
    public bool IstPlatzhalterSpiel { get; init; }
    public int? GruppeNummer { get; init; }
    public string GruppeAnzeige => GruppeNummer.HasValue ? $"Gr.{GruppeNummer}" : "";
    public decimal? HeimRating { get; init; }
    public decimal? AuswaertsRating { get; init; }
}

// ── ViewModel ─────────────────────────────────────────────────────────────────

public partial class SpielplanViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly SpielErgebnisService  _spielService;
    private readonly TurnierInstanzService _instanzService;
    private readonly SpielplanService      _spielplanService;
    private readonly AuslosungService      _auslosungService;
    private readonly TurnierBaumService    _baumService;
    private readonly RegelwerkService      _regelwerkService;
    private readonly TabellenService       _tabellenService;
    private readonly SimulationsService    _simulationsService;

    private int _populiertGerade;
    private Dictionary<int, int> _teamGruppeMap = [];
    private List<VerfahrenArt> _erlaubteVerfahren = [];

    // Spiel/Runde, dessen Ergebnis aktuell im Formular steht (für Dirty-Check beim Spielwechsel)
    private int? _formularSpielId;
    private int? _formularRundeId;
    private int  _spielWechselUnterdrueckt;

    private static readonly HashSet<string> _formFelder =
    [
        nameof(HeimToreRegulaer),       nameof(AuswaertsToreRegulaer),
        nameof(HeimToreVerlaengerung),  nameof(AuswaertsToreVerlaengerung),
        nameof(HeimElfmetertore),       nameof(AuswaertsElfmetertore)
    ];

    private static readonly HashSet<string> _scoreFelder =
    [
        nameof(HeimToreRegulaer),       nameof(AuswaertsToreRegulaer),
        nameof(HeimToreVerlaengerung),  nameof(AuswaertsToreVerlaengerung),
    ];

    // ── linke Spalte ──────────────────────────────────────────────────────────

    [ObservableProperty] private TurnierInstanz? _ausgewaehlteInstanz;
    [ObservableProperty] private ObservableCollection<TurnierRunde> _runden = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatRunde))]
    [NotifyPropertyChangedFor(nameof(IstGruppenphase))]
    [NotifyPropertyChangedFor(nameof(IstKoRunde))]
    [NotifyPropertyChangedFor(nameof(IstLigaRunde))]
    private TurnierRunde? _ausgewaehlteRunde;

    public bool HatRunde => AusgewaehlteRunde is not null;

    // ── Gruppen-Filter (Issue #141: Gruppen-/Liga-Tabellendarstellung lebt komplett im rechten
    // Turnierbaum-Canvas-Panel; hier bleibt nur die Spielliste, optional auf eine per Klick im
    // rechten Panel gewählte Gruppe gefiltert) ──────────────────────────────────

    public bool IstGruppenphase => AusgewaehlteRunde?.Rundenart == Rundenart.Gruppenphase;
    public bool IstLigaRunde    => AusgewaehlteRunde?.Rundenart == Rundenart.Liga;

    // GruppenTabellen wird weiterhin intern geladen (nicht mehr direkt angezeigt): liefert die
    // Team→Gruppe-Zuordnung für MapSpielZeile sowie die Team-Ids für "Gruppe simulieren/zurücksetzen".
    public ObservableCollection<GruppenTabelle> GruppenTabellen { get; } = [];

    // ── Gruppen-Fokus ─────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstGefiltertAufGruppe))]
    [NotifyPropertyChangedFor(nameof(FokusSpiele))]
    private GruppenTabelle? _ausgewaehlteGruppe;

    /// <summary>Steuert, ob die Spielliste auf eine Gruppe gefiltert ist (per Klick auf eine
    /// Gruppen-Karte im rechten Panel gesetzt, siehe TurnierBereichViewModel) — ersetzt die frühere
    /// separate Gruppen-Fokus-Ansicht, da es nur noch eine Listenansicht gibt (Issue #141).</summary>
    public bool IstGefiltertAufGruppe => AusgewaehlteGruppe is not null;

    public ObservableCollection<int> FokusSpieltage { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FokusSpiele))]
    private bool _fokusAlleAktiv = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FokusSpiele))]
    [NotifyPropertyChangedFor(nameof(FokusSpieltagnummer))]
    [NotifyPropertyChangedFor(nameof(SpieltagSimulierenMenuText))]
    [NotifyPropertyChangedFor(nameof(SpieltagZuruecksetzenMenuText))]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private int _fokusAktiverSpieltag;

    public string FokusSpieltagnummer  => $"Spieltag {FokusAktiverSpieltag}";
    public bool   HatMehrereSpieltage  => FokusSpieltage.Count > 1;

    // Vorformatierte Menü-Texte fürs "Aktionen ▾"-Dropdown (Issue #144) — MenuItem.Header ist vom
    // Typ object, Binding.StringFormat wird dort nicht zuverlässig angewendet (zeigte nur die
    // rohe Zahl statt des formatierten Texts), daher hier als fertige Strings bereitgestellt.
    // Ohne aktiven expliziten Gruppenfilter (Issue #170, Fallback auf die Spiel-Selektion) bleibt
    // FokusAktiverSpieltag auf 0 — dann bewusst generischer Text statt "Spieltag 0".
    public string SpieltagSimulierenMenuText    => FokusAktiverSpieltag > 0 ? $"{FokusSpieltagnummer} simulieren" : "Spieltag simulieren";
    public string SpieltagZuruecksetzenMenuText => FokusAktiverSpieltag > 0 ? $"{FokusSpieltagnummer} zurücksetzen" : "Spieltag zurücksetzen";
    public bool   KannFokusZurueck     => !FokusAlleAktiv && FokusSpieltage.Count > 0 && FokusAktiverSpieltag > FokusSpieltage[0];
    public bool   KannFokusVor         => FokusSpieltage.Count > 1 && (FokusAlleAktiv || FokusAktiverSpieltag < FokusSpieltage[^1]);

    public IReadOnlyList<SpielZeile> FokusSpiele => BerechneFokusSpiele();

    private readonly RelayCommand                 _zurueckZurUebersichtBefehl;
    private readonly RelayCommand                 _fokusVorigerSpieltag;
    private readonly RelayCommand                 _fokusNaechsterSpieltag;
    private readonly RelayCommand                 _fokusAlleSpieltageBefehl;

    public ICommand ZurueckZurUebersichtBefehl => _zurueckZurUebersichtBefehl;
    public ICommand FokusVorigerSpieltag       => _fokusVorigerSpieltag;
    public ICommand FokusNaechsterSpieltag     => _fokusNaechsterSpieltag;
    public ICommand FokusAlleSpieltageBefehl   => _fokusAlleSpieltageBefehl;

    // ── Ranking-Kriterien ─────────────────────────────────────────────────────

    [ObservableProperty] private string _rankingRegelSetName = "";

    public ObservableCollection<string> RankingKriterien { get; } = [];

    public bool HatRankingKriterien => RankingKriterien.Count > 0;

    // ── Spielliste ────────────────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<SpielZeile> _spiele = [];
    [ObservableProperty] private SpielZeile? _ausgewaehlteSpiel;

    // ── Ergebnis-Formular ─────────────────────────────────────────────────────

    [ObservableProperty] private bool _formAktiv;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private string? _fehlermeldung;

    public bool HatUngespeicherteAenderungen => FormAktiv && IsDirty;

    [ObservableProperty] private string _formHeimName = "";
    [ObservableProperty] private string _formAuswaertsName = "";
    [ObservableProperty] private string _formSpielInfo = "";
    [ObservableProperty] private bool _formHatErgebnis;

    [ObservableProperty] private string _heimToreRegulaer = "0";
    [ObservableProperty] private string _auswaertsToreRegulaer = "0";

    [ObservableProperty]
    private EntscheidungArt _ausgewaehlteEntscheidungArt = EntscheidungArt.RegulaereSpielzeit;

    [ObservableProperty] private string _heimToreVerlaengerung = "";
    [ObservableProperty] private string _auswaertsToreVerlaengerung = "";
    [ObservableProperty] private string _heimElfmetertore = "";
    [ObservableProperty] private string _auswaertsElfmetertore = "";

    private bool IstKoRunde => AusgewaehlteRunde?.Rundenart is
        Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;

    private bool HatVerlaengerungsVerfahren =>
        _erlaubteVerfahren.Any(v => v is VerfahrenArt.VerlaengerungKlassisch or VerfahrenArt.GoldenGoal or VerfahrenArt.SilverGoal);

    private bool HatElfmeterVerfahren =>
        _erlaubteVerfahren.Any(v => v == VerfahrenArt.Elfmeterschiessen);

    public bool VerlaengerungAktiv =>
        IstKoRunde &&
        HatVerlaengerungsVerfahren &&
        ParseIntNullable(HeimToreRegulaer) is { } heimRegVA &&
        ParseIntNullable(AuswaertsToreRegulaer) is { } ausRegVA &&
        heimRegVA == ausRegVA;

    public bool ElfmeterAktiv
    {
        get
        {
            if (!IstKoRunde || !HatElfmeterVerfahren) return false;
            if (ParseIntNullable(HeimToreRegulaer) is not { } heimRegEA ||
                ParseIntNullable(AuswaertsToreRegulaer) is not { } ausRegEA ||
                heimRegEA != ausRegEA) return false;
            if (!HatVerlaengerungsVerfahren) return true;
            int? heimV = ParseIntNullable(HeimToreVerlaengerung);
            int? ausV  = ParseIntNullable(AuswaertsToreVerlaengerung);
            return heimV.HasValue && ausV.HasValue && heimV == ausV;
        }
    }

    // ── Platzhalter ───────────────────────────────────────────────────────────

    public bool HatPlatzhalterSpiele => Spiele.Any(s => s.IstPlatzhalterSpiel);
    public ICommand PlatzhalterBefehl => _platzhalterBefehl;
    private readonly AsyncRelayCommand _platzhalterBefehl;

    // ── Simulation ────────────────────────────────────────────────────────────

    [ObservableProperty] private bool    _istSimulierend;
    [ObservableProperty] private SimulationsModusOption _ausgewaehlterSimulationsModus = null!;

    public List<SimulationsModusOption> SimulationsModusOptionen { get; } =
    [
        new("Schnellsimulation",       SimulationsModus.SchnellSimulation),
        new("Ausführliche Simulation", SimulationsModus.AusfuehrlicheSimulation),
    ];

    private readonly AsyncRelayCommand _spielSimulierenBefehl;
    private readonly AsyncRelayCommand _rundeSimulierenBefehl;
    private readonly AsyncRelayCommand _gruppeSimulierenBefehl;
    private readonly AsyncRelayCommand _spieltagSimulierenBefehl;
    private readonly AsyncRelayCommand _ausfuehrlichSimulierenBefehl;

    public ICommand SpielSimulierenBefehl         => _spielSimulierenBefehl;
    public ICommand RundeSimulierenBefehl         => _rundeSimulierenBefehl;
    public ICommand GruppeSimulierenBefehl        => _gruppeSimulierenBefehl;
    public ICommand SpieltagSimulierenBefehl      => _spieltagSimulierenBefehl;
    public ICommand AusfuehrlichSimulierenBefehl  => _ausfuehrlichSimulierenBefehl;

    // ── Ergebnis-Reset auf Ebene Runde/Gruppe/Spieltag (Issue #123) ─────────────

    private readonly AsyncRelayCommand _rundeZuruecksetzenBefehl;
    private readonly AsyncRelayCommand _gruppeZuruecksetzenBefehl;
    private readonly AsyncRelayCommand _spieltagZuruecksetzenBefehl;

    public ICommand RundeZuruecksetzenBefehl    => _rundeZuruecksetzenBefehl;
    public ICommand GruppeZuruecksetzenBefehl   => _gruppeZuruecksetzenBefehl;
    public ICommand SpieltagZuruecksetzenBefehl => _spieltagZuruecksetzenBefehl;

    // ── Manuelle Multi-Selektion zum Zurücksetzen (Issue #124) ───────────────────
    // Wird von der View per Code-Behind (ListView.SelectionChanged, da ListView.SelectedItems
    // nicht direkt bindbar ist) aktuell gehalten — siehe AktualisiereMehrfachAuswahl.

    private readonly AsyncRelayCommand _mehrfachZuruecksetzenBefehl;
    public ICommand MehrfachZuruecksetzenBefehl => _mehrfachZuruecksetzenBefehl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnzahlMehrfachZuruecksetzbar))]
    [NotifyPropertyChangedFor(nameof(MehrfachZuruecksetzenMenuText))]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private IReadOnlyList<SpielZeile> _mehrfachAusgewaehlteSpiele = [];

    public int    AnzahlMehrfachZuruecksetzbar => MehrfachAusgewaehlteSpiele.Count(s => s.HatErgebnis);
    public string MehrfachZuruecksetzenMenuText => $"Auswahl ({AnzahlMehrfachZuruecksetzbar}) zurücksetzen";

    public void AktualisiereMehrfachAuswahl(IEnumerable<SpielZeile> ausgewaehlt)
    {
        MehrfachAusgewaehlteSpiele = ausgewaehlt.ToList();
        _mehrfachZuruecksetzenBefehl.NotifyCanExecuteChanged();
    }

    // ── IRibbonAware ──────────────────────────────────────────────────────────

    public string RibbonTabTitel => "Spielplan";
    public ICommand NeuBefehl      => _neuBefehl;
    public ICommand SpeichernBefehl => _speichernBefehl;
    public ICommand AbbrechenBefehl => _abbrechenBefehl;
    public ICommand LoeschenBefehl  => _loeschenBefehl;

    // Issue #163: die vormaligen "Aktionen ▾"-ContextMenu-Buttons (Runde-, Gruppe- und
    // Spieltag-Ebene) sowie die Spiel-Simulieren-Buttons aus dem Ergebnis-Formular als eigene
    // Ribbon-Dropdowns/-Buttons — auf Nutzerwunsch als eigenständige Buttons statt weiter
    // zusammengefasst (Gruppe/Spieltag ursprünglich ein gemeinsames Dropdown). Sichtbarkeit/
    // Einschränkung läuft weiterhin rein über die bestehenden CanExecute-Prädikate (z.B.
    // AusgewaehlteGruppe/FokusAlleAktiv) statt über Ein-/Ausblenden der Menüpunkte selbst.
    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen =>
    [
        new RibbonGruppe
        {
            Header = "Spielplan",
            Aktionen =
            [
                new RibbonAktion
                {
                    Header = "Runde",
                    IconGlyph = "",
                    Unteraktionen =
                    [
                        new RibbonAktion { Header = "Runde simulieren", Befehl = RundeSimulierenBefehl, IconGlyph = "" },
                        new RibbonAktion { Header = "Runde zurücksetzen", Befehl = RundeZuruecksetzenBefehl, IconGlyph = "" },
                        new RibbonAktion { Header = MehrfachZuruecksetzenMenuText, Befehl = MehrfachZuruecksetzenBefehl, IconGlyph = "" },
                    ],
                },
                new RibbonAktion
                {
                    Header = "Gruppe",
                    IconGlyph = "",
                    Unteraktionen =
                    [
                        new RibbonAktion { Header = "Gruppe simulieren", Befehl = GruppeSimulierenBefehl, IconGlyph = "" },
                        new RibbonAktion { Header = "Gruppe zurücksetzen", Befehl = GruppeZuruecksetzenBefehl, IconGlyph = "" },
                    ],
                },
                new RibbonAktion
                {
                    Header = "Spieltag",
                    IconGlyph = "",
                    Unteraktionen =
                    [
                        new RibbonAktion { Header = SpieltagSimulierenMenuText, Befehl = SpieltagSimulierenBefehl, IconGlyph = "" },
                        new RibbonAktion { Header = SpieltagZuruecksetzenMenuText, Befehl = SpieltagZuruecksetzenBefehl, IconGlyph = "" },
                    ],
                },
                new RibbonAktion
                {
                    Header = "Spiel simulieren",
                    Befehl = SpielSimulierenBefehl,
                    IconGlyph = "",
                    Unteraktionen =
                    [
                        new RibbonAktion { Header = "Ausführliche Simulation", Befehl = AusfuehrlichSimulierenBefehl, IconVektorDatei = "magnifying_glass.xaml" },
                    ],
                },
            ],
        },
    ];

    private readonly AsyncRelayCommand _neuBefehl;
    private readonly AsyncRelayCommand _speichernBefehl;
    private readonly RelayCommand      _abbrechenBefehl;
    private readonly AsyncRelayCommand _loeschenBefehl;

    // ── Konstruktor ───────────────────────────────────────────────────────────

    public SpielplanViewModel(
        SpielErgebnisService spielService,
        TurnierInstanzService instanzService,
        SpielplanService spielplanService,
        AuslosungService auslosungService,
        TurnierBaumService baumService,
        RegelwerkService regelwerkService,
        TabellenService tabellenService,
        SimulationsService simulationsService)
    {
        _spielService       = spielService;
        _instanzService     = instanzService;
        _spielplanService   = spielplanService;
        _auslosungService   = auslosungService;
        _baumService        = baumService;
        _regelwerkService   = regelwerkService;
        _tabellenService    = tabellenService;
        _simulationsService = simulationsService;

        AusgewaehlterSimulationsModus = SimulationsModusOptionen[0];

        _neuBefehl                 = new AsyncRelayCommand(SpielplanNeuGenerierenAsync,   () => HatRunde);
        _speichernBefehl           = new AsyncRelayCommand(SpeichernAsync,                () => FormAktiv && IsDirty);
        _abbrechenBefehl           = new RelayCommand(FormZuruecksetzen,                  () => FormAktiv && IsDirty);
        _loeschenBefehl            = new AsyncRelayCommand(ErgebnisLoeschenAsync,         () => FormAktiv && FormHatErgebnis);
        _platzhalterBefehl         = new AsyncRelayCommand(PlatzhalterAktualisierenAsync, () => HatRunde && HatPlatzhalterSpiele);

        _zurueckZurUebersichtBefehl = new RelayCommand(() => AusgewaehlteGruppe = null, () => AusgewaehlteGruppe is not null);
        _fokusVorigerSpieltag       = new RelayCommand(FokusEinSpieltag_Vorig, () => KannFokusZurueck);
        _fokusNaechsterSpieltag     = new RelayCommand(FokusEinSpieltag_Vor,   () => KannFokusVor);
        _fokusAlleSpieltageBefehl   = new RelayCommand(() => FokusAlleAktiv = true, () => !FokusAlleAktiv);

        _spielSimulierenBefehl        = new AsyncRelayCommand(SpielSimulierenAsync,         CanSpielSimulieren);
        _rundeSimulierenBefehl        = new AsyncRelayCommand(RundeSimulierenAsync,         CanRundeSimulieren);
        _gruppeSimulierenBefehl       = new AsyncRelayCommand(GruppeSimulierenAsync,        CanGruppeSimulieren);
        _spieltagSimulierenBefehl     = new AsyncRelayCommand(SpieltagSimulierenAsync,      CanSpieltagSimulieren);
        _ausfuehrlichSimulierenBefehl = new AsyncRelayCommand(AusfuehrlichSimulierenAsync,  CanAusfuehrlichSimulieren);

        _rundeZuruecksetzenBefehl    = new AsyncRelayCommand(RundeZuruecksetzenAsync,    CanRundeZuruecksetzen);
        _gruppeZuruecksetzenBefehl   = new AsyncRelayCommand(GruppeZuruecksetzenAsync,   CanGruppeZuruecksetzen);
        _spieltagZuruecksetzenBefehl = new AsyncRelayCommand(SpieltagZuruecksetzenAsync, CanSpieltagZuruecksetzen);
        _mehrfachZuruecksetzenBefehl = new AsyncRelayCommand(MehrfachZuruecksetzenAsync, CanMehrfachZuruecksetzen);
    }

    // ── Initialisierung ───────────────────────────────────────────────────────

    // Issue-Nachbesserung zu #163: vormals ein No-Op — Ergebnisse aus einer Simulation im
    // Simulation-Tab (oder grundsätzlich jede Änderung, die während der Spielplan-Tab inaktiv war)
    // wurden beim Zurückwechseln nicht sichtbar. Bricht bei ungespeicherten Formular-Änderungen
    // bewusst ab, um die laufende Eingabe des Nutzers nicht zu verwerfen — FormAktiv ist bereits
    // dann wahr, wenn nur ein Spiel ausgewählt ist (ohne Bearbeitung), daher zusätzlich IsDirty
    // prüfen statt FormAktiv allein (sonst würde exakt der Rückkehr-von-Simulation-Fall blockiert).
    public override async Task AktivierenAsync()
    {
        if (AusgewaehlteRunde is null || (FormAktiv && IsDirty)) return;
        await SpiellisteAktualisierenAsync();
    }

    private async Task SpiellisteAktualisierenAsync()
    {
        if (AusgewaehlteRunde is null) return;
        int rundeId = AusgewaehlteRunde.Id;
        int? spielId = AusgewaehlteSpiel?.Id;

        await TabellenNeuLadenAsync(rundeId);

        var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
        Spiele.Clear();
        foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i)))
            Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap));
        OnPropertyChanged(nameof(FokusSpiele));
        OnPropertyChanged(nameof(HatPlatzhalterSpiele));
        _platzhalterBefehl.NotifyCanExecuteChanged();

        // Auswahl anhand der Id wiederherstellen, SpielWechselAsync dabei unterdrücken (Spiele
        // wurde neu aufgebaut, die Auswahl "wechselt" technisch die Objektinstanz, fachlich aber
        // nicht) — Formular wird unten explizit einmalig neu geladen, analog SpielSimulierenAsync.
        _spielWechselUnterdrueckt++;
        try
        {
            AusgewaehlteSpiel = spielId.HasValue ? Spiele.FirstOrDefault(s => s.Id == spielId) : null;
        }
        finally
        {
            _spielWechselUnterdrueckt--;
        }

        await TurnierBaumAktualisierenAsync(AusgewaehlteRunde.TurnierInstanzId);
        await BeiSpielAenderungAsync();
        AktualisiereBefehle();
    }

    public async Task LadeInstanzAsync(TurnierInstanz? instanz)
    {
        AusgewaehlteInstanz = instanz;
        await BeiInstanzAenderungAsync();
    }

    // ── Property-Reaktionen ───────────────────────────────────────────────────

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        switch (e.PropertyName)
        {
            case nameof(AusgewaehlteInstanz):
                // Kein fire-and-forget hier — Laden erfolgt sequenziell via LadeInstanzAsync()
                break;
            case nameof(AusgewaehlteRunde):
                _ = BeiRundeAenderungAsync();
                break;
            case nameof(AusgewaehlteSpiel):
                _ = SpielWechselAsync();
                break;
            case nameof(AusgewaehlteGruppe):
                BeiGruppeFokusAenderung();
                break;
            case nameof(FokusAlleAktiv):
            case nameof(FokusAktiverSpieltag):
                _fokusVorigerSpieltag.NotifyCanExecuteChanged();
                _fokusNaechsterSpieltag.NotifyCanExecuteChanged();
                _fokusAlleSpieltageBefehl.NotifyCanExecuteChanged();
                _spieltagSimulierenBefehl.NotifyCanExecuteChanged();
                SpieltagNavigiert?.Invoke();
                break;
        }

        if (_populiertGerade == 0 && e.PropertyName is not null)
        {
            if (_scoreFelder.Contains(e.PropertyName))
                EntscheidungNeuBerechnen();

            if (_formFelder.Contains(e.PropertyName))
            {
                IsDirty = true;
                _speichernBefehl.NotifyCanExecuteChanged();
                _abbrechenBefehl.NotifyCanExecuteChanged();
            }
        }
    }

    // ── Selektions-Handler ────────────────────────────────────────────────────

    private async Task BeiInstanzAenderungAsync()
    {
        Runden.Clear();
        Spiele.Clear();
        AusgewaehlteRunde  = null;
        AusgewaehlteSpiel  = null;
        FormAktiv          = false;

        if (AusgewaehlteInstanz is null) return;

        var instanz = await _instanzService.FindenNachIdMitRundenAsync(AusgewaehlteInstanz.Id);
        if (instanz is null) return;

        foreach (var r in instanz.Runden.OrderBy(r => r.Reihenfolge))
            Runden.Add(r);
    }

    private async Task BeiRundeAenderungAsync()
    {
        Spiele.Clear();
        AusgewaehlteSpiel   = null;
        FormAktiv           = false;
        Fehlermeldung       = null;
        RankingKriterien.Clear();
        OnPropertyChanged(nameof(HatRankingKriterien));
        RankingRegelSetName  = "";
        _erlaubteVerfahren   = [];
        AusgewaehlteGruppe   = null;
        GruppenTabellen.Clear();
        _teamGruppeMap = [];
        AktualisiereBefehle();

        if (AusgewaehlteRunde is null)
        {
            if (RundeGeaendertAsync is not null) await RundeGeaendertAsync(null);
            return;
        }

        await TabellenNeuLadenAsync(AusgewaehlteRunde.Id);

        var liste = await _spielService.AlleNachRundeAsync(AusgewaehlteRunde.Id);
        foreach (var (s, i) in liste.Select((x, i) => (x, i)))
            Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap));

        OnPropertyChanged(nameof(HatPlatzhalterSpiele));
        _platzhalterBefehl.NotifyCanExecuteChanged();

        var regelwerk = await _regelwerkService.FindenNachIdAsync(AusgewaehlteRunde.RegelwerkId);

        _erlaubteVerfahren = regelwerk?.Entscheidungsverfahren
            .OrderBy(v => v.Reihenfolge)
            .Select(v => v.VerfahrenArt)
            .ToList() ?? [];

        if (regelwerk?.RankingRegelSet?.Eintraege is { Count: > 0 } eintraege)
        {
            RankingRegelSetName = regelwerk.RankingRegelSet.Name;
            int pos = 1;
            foreach (var eintrag in eintraege.OrderBy(e => e.Reihenfolge))
                RankingKriterien.Add($"{pos++}. {RegelartZuText(eintrag.RankingRegel.Regelart)}");
            OnPropertyChanged(nameof(HatRankingKriterien));
        }

        // Simulations-/Formularbefehle (u.a. "Runde simulieren") haengen von der jetzt geladenen
        // Runde ab und muessen nach dem Rundenwechsel neu ausgewertet werden, nicht erst beim
        // naechsten Spielfokus.
        AktualisiereBefehle();
    }

    // Laedt die Team→Gruppe-Zuordnung (fuer MapSpielZeile) sowie GruppenTabellen (fuer die
    // Team-Id-Lookups von "Gruppe simulieren/zuruecksetzen") — immer der aktuelle Stand, keine
    // historische Ansicht mehr noetig, da die eigentliche Gruppentabellen-Darstellung komplett im
    // rechten Turnierbaum-Canvas-Panel lebt (Issue #141).
    private async Task GruppenTabellenLadenAsync(int rundeId)
    {
        GruppenTabellen.Clear();
        _teamGruppeMap = [];
        try
        {
            var tabellen = await _tabellenService.GruppenTabellenBerechnenAsync(rundeId);
            foreach (var gt in tabellen)
            {
                GruppenTabellen.Add(gt);
                foreach (var te in gt.Tabelle)
                    _teamGruppeMap[te.Mannschaft.Id] = gt.GruppenNummer;
            }
        }
        catch { /* Gruppenstand konnte nicht berechnet werden — kein Fehler */ }

        // AusgewaehlteGruppe zeigt sonst weiter auf die alte, gerade aus GruppenTabellen entfernte
        // Instanz (z.B. nach Ergebnis-Speichern, wenn eine Gruppe gefiltert ist).
        if (AusgewaehlteGruppe is { } aktuelleGruppe)
            AusgewaehlteGruppe = GruppenTabellen.FirstOrDefault(g => g.GruppenNummer == aktuelleGruppe.GruppenNummer);
    }

    // Laedt die Team→Gruppe-Zuordnung fuer Gruppenphase-Runden nach. Zentrale Stelle fuer alle
    // Aufrufer (Rundenwechsel, Spielplan-Neugenerierung, Ergebnis-Speichern/-Zuruecksetzen, ...) —
    // RundeGeaendertAsync hier statt an jeder einzelnen Aufrufstelle ausgeloest, sonst bleibt die
    // Turnierbaum-Canvas-Anzeige (Issue #137) bei allen anderen Aufrufstellen als dem reinen
    // Rundenwechsel stehen.
    private async Task TabellenNeuLadenAsync(int rundeId)
    {
        if (AusgewaehlteRunde?.Rundenart == Rundenart.Gruppenphase)
            await GruppenTabellenLadenAsync(rundeId);

        if (SpielstandGeaendertAsync is not null)
            await SpielstandGeaendertAsync();

        if (RundeGeaendertAsync is not null)
            await RundeGeaendertAsync(AusgewaehlteRunde);
    }

    // Bewusst kein event (fire-and-forget wuerde parallel zum naechsten DbContext-Zugriff
    // im Aufrufer laufen und "A second operation was started on this context..." ausloesen) —
    // stattdessen ein awaitbares Delegate fuer die Turnier-Fortschrittsanzeige.
    public Func<Task>? SpielstandGeaendertAsync { get; set; }

    // Analog zu SpielstandGeaendertAsync: awaitbares Delegate statt Event, damit der Aufrufer
    // (TurnierBereichViewModel → TurnierBaumViewModel) sequenziell nach dem Laden der Runde
    // reagieren kann, ohne parallel auf demselben (geteilten) DbContext zu arbeiten (Issue #137).
    public Func<TurnierRunde?, Task>? RundeGeaendertAsync { get; set; }

    // Synchron statt awaitbares Delegate: kein DB-Zugriff auf der Empfängerseite
    // (TurnierBaumViewModel.ZeigeAktuellenStand setzt nur einen lokalen Zustand zurück), daher
    // unproblematisch als direkter Callback (Issue #141 — Kopplung Spieltag-Navigation → Tabellen-Snapshot).
    public Action? SpieltagNavigiert { get; set; }

    private async Task SpielWechselAsync()
    {
        // Programmatische Korrektur der Auswahl (siehe unten) — die aufrufende Stelle
        // steuert selbst, ob/wie das Formular neu geladen wird.
        if (_spielWechselUnterdrueckt > 0) return;

        var neuesSpielId = AusgewaehlteSpiel?.Id;

        if (IsDirty &&
            _formularSpielId is { } vorherigeSpielId &&
            _formularRundeId == AusgewaehlteRunde?.Id &&
            vorherigeSpielId != neuesSpielId)
        {
            var vorherigeRundeId = _formularRundeId!.Value;

            var ergebnis = MessageBox.Show(
                "Das aktuelle Spielergebnis wurde geändert, aber nicht gespeichert.\nÄnderungen speichern?",
                "Ungespeicherte Änderungen",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

            // Auswahl erst nach Abschluss des aktuellen SelectionChanged-Zyklus setzen,
            // sonst übernimmt die WPF-ListView die Änderung nicht ins sichtbare Highlight.
            await Task.Yield();

            _spielWechselUnterdrueckt++;
            try
            {
                if (ergebnis == MessageBoxResult.Cancel)
                {
                    // Auswahl zurücksetzen, Formular NICHT neu laden — die ungespeicherte
                    // Eingabe des Nutzers bleibt unverändert stehen.
                    AusgewaehlteSpiel = Spiele.FirstOrDefault(s => s.Id == vorherigeSpielId);
                    return;
                }

                if (ergebnis == MessageBoxResult.Yes)
                    await ErgebnisSpeichernAsync(vorherigeSpielId, vorherigeRundeId);

                // ErgebnisSpeichernAsync baut die Spiele-Liste neu auf (neue Objektinstanzen)
                // und setzt die WPF-Selektion dabei kurzzeitig auf null zurück — Zielspiel erneut
                // anhand der Id auswählen, damit der Fokus tatsächlich wechselt.
                AusgewaehlteSpiel = neuesSpielId.HasValue ? Spiele.FirstOrDefault(s => s.Id == neuesSpielId) : null;
            }
            finally
            {
                _spielWechselUnterdrueckt--;
            }

            // Formular explizit einmalig auf den (nicht) ausgewählten Zielstand laden.
            await BeiSpielAenderungAsync();
            return;
        }

        await BeiSpielAenderungAsync();
    }

    private async Task BeiSpielAenderungAsync()
    {
        FormAktiv     = false;
        Fehlermeldung = null;

        AktualisiereBefehle();

        if (AusgewaehlteSpiel is null) return;

        var spiel = await _spielService.FindenNachIdAsync(AusgewaehlteSpiel.Id);
        if (spiel is null) return;

        FormularAusFuellenMit(spiel);
        FormAktiv = true;
        AktualisiereBefehle();
    }

    /// <summary>Stellt nach einem Gruppen-/Spieltag-Simulieren/Zurücksetzen die vorherige
    /// Spiel-Selektion wieder her (Issue #170-Nachbesserung: die implizite Fokus-Ableitung über
    /// AusgewaehlteSpiel ginge sonst nach jeder Aktion verloren, da die Liste neu aufgebaut wird
    /// und die alte Objektinstanz nicht mehr existiert). SpielWechselAsync wird währenddessen
    /// unterdrückt (reine Objektinstanz-Änderung, kein fachlicher Wechsel), das Formular danach
    /// explizit einmalig neu geladen — analog SpielSimulierenAsync.</summary>
    private async Task SpielAuswahlWiederherstellenAsync(int? spielId)
    {
        _spielWechselUnterdrueckt++;
        try
        {
            AusgewaehlteSpiel = spielId.HasValue ? Spiele.FirstOrDefault(s => s.Id == spielId) : null;
        }
        finally
        {
            _spielWechselUnterdrueckt--;
        }
        await BeiSpielAenderungAsync();
    }

    // ── Formular befüllen ─────────────────────────────────────────────────────

    private void FormularAusFuellenMit(Spiel spiel)
    {
        _formularSpielId = spiel.Id;
        _formularRundeId = AusgewaehlteRunde?.Id;

        _populiertGerade++;
        try
        {
            FormHeimName      = spiel.HeimMannschaft?.Name ?? spiel.HeimPlatzhalterName ?? "";
            FormAuswaertsName = spiel.AuswaertsMannschaft?.Name ?? spiel.AuswaertsPlatzhalterName ?? "";
            FormSpielInfo     = spiel.Spieltag.HasValue
                ? $"Spieltag {spiel.Spieltag} · Spiel #{spiel.Spielnummer}"
                : $"Spiel #{spiel.Spielnummer}";
            FormHatErgebnis = spiel.Ergebnis is not null;

            if (spiel.Ergebnis is { } e)
            {
                HeimToreRegulaer           = e.HeimToreRegulaer.ToString();
                AuswaertsToreRegulaer      = e.AuswaertsToreRegulaer.ToString();
                AusgewaehlteEntscheidungArt = e.EntscheidungArt;
                // Feld zeigt den kumulierten Stand NACH der Verlaengerung (nicht das Verlaengerungs-
                // Delta) — entspricht der ueblichen Ergebnisdarstellung ("2:2 nach Verlaengerung").
                HeimToreVerlaengerung      = e.HeimToreVerlaengerung.HasValue
                    ? (e.HeimToreRegulaer + e.HeimToreVerlaengerung.Value).ToString()
                    : "";
                AuswaertsToreVerlaengerung = e.AuswaertsToreVerlaengerung.HasValue
                    ? (e.AuswaertsToreRegulaer + e.AuswaertsToreVerlaengerung.Value).ToString()
                    : "";
                HeimElfmetertore           = e.HeimElfmetertore?.ToString() ?? "";
                AuswaertsElfmetertore      = e.AuswaertsElfmetertore?.ToString() ?? "";
            }
            else
            {
                HeimToreRegulaer           = "";
                AuswaertsToreRegulaer      = "";
                AusgewaehlteEntscheidungArt = EntscheidungArt.RegulaereSpielzeit;
                HeimToreVerlaengerung      = "";
                AuswaertsToreVerlaengerung = "";
                HeimElfmetertore           = "";
                AuswaertsElfmetertore      = "";
            }
        }
        finally
        {
            _populiertGerade--;
        }

        OnPropertyChanged(nameof(VerlaengerungAktiv));
        OnPropertyChanged(nameof(ElfmeterAktiv));
        IsDirty = false;
        AktualisiereBefehle();
    }

    // ── Ribbon-Aktionen ───────────────────────────────────────────────────────

    private async Task SpielplanNeuGenerierenAsync()
    {
        if (AusgewaehlteRunde is null || AusgewaehlteInstanz is null) return;

        var rundeId   = AusgewaehlteRunde.Id;
        var instanzId = AusgewaehlteInstanz.Id;
        Fehlermeldung = null;

        if (Spiele.Count > 0)
        {
            var antwort = MessageBox.Show(
                $"Die Runde '{AusgewaehlteRunde.Name}' hat bereits {Spiele.Count} Spiel(e). " +
                "Soll der Spielplan gelöscht und neu generiert werden?",
                "Spielplan neu generieren",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (antwort != MessageBoxResult.Yes) return;
        }

        try
        {
            if (Spiele.Count > 0)
                await _spielplanService.LoeschenAsync(rundeId);
            var auslosung = await _auslosungService.FindenAbgeschlosseneNachRundeAsync(instanzId, rundeId);
            if (auslosung is null)
            {
                Fehlermeldung = "Für diese Runde wurde noch keine Auslosung abgeschlossen. " +
                                "Bitte zuerst die Auslosung durchführen.";
                return;
            }

            await _spielplanService.GenerierenAusAuslosungAsync(rundeId, [.. auslosung.Ergebnisse]);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i))) Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap));
            OnPropertyChanged(nameof(HatPlatzhalterSpiele));
            OnPropertyChanged(nameof(FokusSpiele));
            _platzhalterBefehl.NotifyCanExecuteChanged();
            AusgewaehlteSpiel = null;
            await TurnierBaumAktualisierenAsync(AusgewaehlteRunde.TurnierInstanzId);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    private async Task PlatzhalterAktualisierenAsync()
    {
        if (AusgewaehlteRunde is null) return;
        var rundeId   = AusgewaehlteRunde.Id;
        Fehlermeldung = null;
        try
        {
            int aufgeloest = await _spielplanService.PlatzhalterAufloesenAsync(rundeId);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i))) Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap));
            OnPropertyChanged(nameof(HatPlatzhalterSpiele));
            OnPropertyChanged(nameof(FokusSpiele));
            _platzhalterBefehl.NotifyCanExecuteChanged();
            AusgewaehlteSpiel = null;

            if (aufgeloest == 0)
                Fehlermeldung = "Keine Platzhalter konnten aufgelöst werden. " +
                                "Bitte sicherstellen, dass die Vorrunden-Ergebnisse vollständig eingetragen sind.";
            else
                await TurnierBaumAktualisierenAsync(AusgewaehlteRunde.TurnierInstanzId);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    private async Task SpeichernAsync()
    {
        if (AusgewaehlteSpiel is null || AusgewaehlteRunde is null) return;
        var spielId = AusgewaehlteSpiel.Id;

        if (await ErgebnisSpeichernAsync(spielId, AusgewaehlteRunde.Id))
            AusgewaehlteSpiel = Spiele.FirstOrDefault(s => s.Id == spielId);
    }

    private async Task<bool> ErgebnisSpeichernAsync(int spielId, int rundeId)
    {
        if (AusgewaehlteRunde is null || AusgewaehlteRunde.Id != rundeId) return false;
        Fehlermeldung = null;
        try
        {
            await _spielService.ErgebnisErfassenAsync(spielId, BaueEingabe());

            // Falls dieses Ergebnis eine Finale-Runde entscheidungsreif macht, wird sie automatisch ohne
            // Auslosung generiert (Issue #113) — der reguläre "Platzhalter aktualisieren"-Button ist dafür
            // nicht erreichbar, da die Finale-Runde vorher noch keine Spiele hat.
            await _spielplanService.FinaleAutomatischGenerierenWennBereitAsync(AusgewaehlteRunde.TurnierInstanzId);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);

            // Clear()/Add() setzt die WPF-Listenauswahl kurzzeitig auf null zurück — das darf
            // hier keinen (fälschlichen) Dirty-Check-Dialog auslösen (siehe SpielWechselAsync).
            _spielWechselUnterdrueckt++;
            try
            {
                Spiele.Clear();
                foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i))) Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap));
            }
            finally
            {
                _spielWechselUnterdrueckt--;
            }
            OnPropertyChanged(nameof(FokusSpiele));

            return true;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
            return false;
        }
    }

    private void FormZuruecksetzen()
        => _ = BeiSpielAenderungAsync();

    private async Task ErgebnisLoeschenAsync()
    {
        if (AusgewaehlteSpiel is null || !FormHatErgebnis || AusgewaehlteRunde is null) return;

        var ergebnis = MessageBox.Show(
            $"Ergebnis von Spiel #{AusgewaehlteSpiel.Spielnummer} wirklich löschen?",
            "Ergebnis löschen", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ergebnis != MessageBoxResult.Yes) return;

        var spielId   = AusgewaehlteSpiel.Id;
        var rundeId   = AusgewaehlteRunde.Id;
        Fehlermeldung = null;
        try
        {
            // Kaskaden-Check (automatisch generiertes Finale ggf. entfernen/neu generieren) läuft
            // seit Issue #125 zentral in SpielErgebnisService.ErgebnisLoeschenAsync.
            await _spielService.ErgebnisLoeschenAsync(spielId);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i))) Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap));
            OnPropertyChanged(nameof(FokusSpiele));

            AusgewaehlteSpiel = Spiele.FirstOrDefault(s => s.Id == spielId);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    private async Task TurnierBaumAktualisierenAsync(int turnierInstanzId)
    {
        try
        {
            var baum = await _baumService.AbrufenAsync(turnierInstanzId);
            if (baum is not null)
                await _baumService.GenerierenAsync(turnierInstanzId);
        }
        catch { /* TurnierBaum-Rebuild ist optional — nie Fehler an den Benutzer weitergeben */ }
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    private ErgebnisErfassenEingabe BaueEingabe()
    {
        int heimRegulaer      = ParseInt(HeimToreRegulaer);
        int auswaertsRegulaer = ParseInt(AuswaertsToreRegulaer);

        // Das Formular zeigt/erwartet den kumulierten Stand nach der Verlaengerung —
        // fuer die Speicherung auf das intern verwendete Verlaengerungs-Delta zurueckrechnen.
        int? heimVerlaengerungDelta      = ParseIntNullable(HeimToreVerlaengerung)      is { } hv ? hv - heimRegulaer      : null;
        int? auswaertsVerlaengerungDelta = ParseIntNullable(AuswaertsToreVerlaengerung) is { } av ? av - auswaertsRegulaer : null;

        return new(
            heimRegulaer,
            auswaertsRegulaer,
            AusgewaehlteEntscheidungArt,
            heimVerlaengerungDelta,
            auswaertsVerlaengerungDelta,
            ParseIntNullable(HeimElfmetertore),
            ParseIntNullable(AuswaertsElfmetertore));
    }

    private void AktualisiereBefehle()
    {
        _neuBefehl.NotifyCanExecuteChanged();
        _speichernBefehl.NotifyCanExecuteChanged();
        _abbrechenBefehl.NotifyCanExecuteChanged();
        _loeschenBefehl.NotifyCanExecuteChanged();
        _platzhalterBefehl.NotifyCanExecuteChanged();
        _fokusVorigerSpieltag.NotifyCanExecuteChanged();
        _fokusNaechsterSpieltag.NotifyCanExecuteChanged();
        _fokusAlleSpieltageBefehl.NotifyCanExecuteChanged();
        _spielSimulierenBefehl.NotifyCanExecuteChanged();
        _rundeSimulierenBefehl.NotifyCanExecuteChanged();
        _gruppeSimulierenBefehl.NotifyCanExecuteChanged();
        _spieltagSimulierenBefehl.NotifyCanExecuteChanged();
        _ausfuehrlichSimulierenBefehl.NotifyCanExecuteChanged();
        _rundeZuruecksetzenBefehl.NotifyCanExecuteChanged();
        _gruppeZuruecksetzenBefehl.NotifyCanExecuteChanged();
        _spieltagZuruecksetzenBefehl.NotifyCanExecuteChanged();
        _mehrfachZuruecksetzenBefehl.NotifyCanExecuteChanged();
    }

    private static SpielZeile MapSpielZeile(
        Spiel s, TurnierRunde? runde, int positionInRunde,
        IReadOnlyDictionary<int, int>? teamGruppeMap = null)
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

        int? gruppeNummer = null;
        if (teamGruppeMap is not null && s.HeimMannschaftId.HasValue
            && teamGruppeMap.TryGetValue(s.HeimMannschaftId.Value, out var gr))
            gruppeNummer = gr;

        return new SpielZeile
        {
            Id                  = s.Id,
            Spielnummer         = s.Spielnummer,
            Spieltag            = s.Spieltag,
            KoLabel             = KoPositionsLabel(s, runde, positionInRunde),
            HeimName            = s.HeimMannschaft?.Name ?? s.HeimPlatzhalterName ?? "?",
            AuswaertsName       = s.AuswaertsMannschaft?.Name ?? s.AuswaertsPlatzhalterName ?? "?",
            Status              = s.Status,
            HatErgebnis         = s.Ergebnis is not null,
            ErgebnisText        = text,
            ErgebnisDetail      = detail,
            IstRueckspiel       = s.IstRueckspiel,
            IstPlatzhalterSpiel = !s.HeimMannschaftId.HasValue || !s.AuswaertsMannschaftId.HasValue,
            GruppeNummer        = gruppeNummer
        };
    }

    internal static string? KoPositionsLabel(Spiel s, TurnierRunde? runde, int positionInRunde)
    {
        if (runde is null) return null;
        bool mitRueck = runde.MitRueckspiel;
        return runde.Rundenart switch
        {
            Rundenart.Finale           => FormatKoLabel("Finale",   positionInRunde, mitRueck, s.IstRueckspiel),
            Rundenart.SpielUmPlatzDrei => FormatKoLabel("3. Platz", positionInRunde, mitRueck, s.IstRueckspiel),
            Rundenart.KoRunde          => KoKuerzel(runde.EingangsMannschaftsanzahl) is { } k
                                          ? FormatKoLabel(k, positionInRunde, mitRueck, s.IstRueckspiel) : null,
            _                          => null
        };
    }

    private static string FormatKoLabel(string kuerzel, int positionInRunde, bool mitRueckspiel, bool istRueckspiel)
    {
        if (mitRueckspiel)
        {
            int paarNr = (positionInRunde / 2) + 1;
            return $"{kuerzel} {paarNr} {(istRueckspiel ? "R" : "H")}";
        }
        return $"{kuerzel} {positionInRunde + 1}";
    }

    internal static string? KoKuerzel(int eingangTeams) => eingangTeams switch
    {
        4   => "HF",
        8   => "VF",
        16  => "AF",
        32  => "R16",
        64  => "R32",
        128 => "R64",
        _   => eingangTeams > 2 ? $"R{eingangTeams / 2}" : null
    };

    private static int  ParseInt(string s) =>
        int.TryParse(s, out var v) ? v : 0;

    private static int? ParseIntNullable(string s) =>
        string.IsNullOrWhiteSpace(s) ? null : int.TryParse(s, out var v) ? v : null;

    private void EntscheidungNeuBerechnen()
    {
        AusgewaehlteEntscheidungArt = BerechneEntscheidungArt();
        OnPropertyChanged(nameof(VerlaengerungAktiv));
        OnPropertyChanged(nameof(ElfmeterAktiv));
    }

    private EntscheidungArt BerechneEntscheidungArt()
    {
        bool reg90Draw = ParseIntNullable(HeimToreRegulaer) is { } heimReg &&
                         ParseIntNullable(AuswaertsToreRegulaer) is { } ausReg &&
                         heimReg == ausReg;

        // Verlängerung/Elfmeterschießen sind nur in K.-o.-Runden relevant — in Liga/Gruppenphase
        // ist ein Unentschieden immer ein gültiges Endergebnis, unabhängig vom Regelwerk.
        if (!reg90Draw || !IstKoRunde) return EntscheidungArt.RegulaereSpielzeit;

        bool hatVerl = HatVerlaengerungsVerfahren;
        bool hatElf  = HatElfmeterVerfahren;

        if (hatVerl)
        {
            int? heimV = ParseIntNullable(HeimToreVerlaengerung);
            int? ausV  = ParseIntNullable(AuswaertsToreVerlaengerung);

            if (heimV.HasValue && ausV.HasValue)
            {
                if (heimV != ausV)
                {
                    var vart = _erlaubteVerfahren.First(v =>
                        v is VerfahrenArt.VerlaengerungKlassisch or VerfahrenArt.GoldenGoal or VerfahrenArt.SilverGoal);
                    return vart switch
                    {
                        VerfahrenArt.GoldenGoal => EntscheidungArt.GoldenGoal,
                        VerfahrenArt.SilverGoal => EntscheidungArt.SilverGoal,
                        _                       => EntscheidungArt.Verlaengerung,
                    };
                }
                if (hatElf) return EntscheidungArt.Elfmeterschiessen;
            }

            var vartPending = _erlaubteVerfahren.First(v =>
                v is VerfahrenArt.VerlaengerungKlassisch or VerfahrenArt.GoldenGoal or VerfahrenArt.SilverGoal);
            return vartPending switch
            {
                VerfahrenArt.GoldenGoal => EntscheidungArt.GoldenGoal,
                VerfahrenArt.SilverGoal => EntscheidungArt.SilverGoal,
                _                       => EntscheidungArt.Verlaengerung,
            };
        }

        if (hatElf) return EntscheidungArt.Elfmeterschiessen;

        return EntscheidungArt.RegulaereSpielzeit;
    }

    private void FokusEinSpieltag_Vorig()
    {
        if (FokusAlleAktiv) return;
        int idx = FokusSpieltage.IndexOf(FokusAktiverSpieltag);
        if (idx > 0) FokusAktiverSpieltag = FokusSpieltage[idx - 1];
    }

    private void FokusEinSpieltag_Vor()
    {
        if (FokusAlleAktiv)
        {
            FokusAlleAktiv = false; // FokusAktiverSpieltag ist bereits FokusSpieltage[0]
            return;
        }
        int idx = FokusSpieltage.IndexOf(FokusAktiverSpieltag);
        if (idx >= 0 && idx + 1 < FokusSpieltage.Count)
            FokusAktiverSpieltag = FokusSpieltage[idx + 1];
    }

    // Merkt sich die zuletzt fokussierte GruppenNummer, um einen echten Gruppenwechsel von einem
    // bloßen Instanz-Refresh derselben Gruppe zu unterscheiden (Issue #138, Bug 2): GruppenTabellenLadenAsync
    // weist AusgewaehlteGruppe nach jedem Tabellen-Reload eine NEUE GruppenTabelle-Instanz mit
    // derselben GruppenNummer zu (Referenzungleichheit löst OnPropertyChanged aus) — ohne diese
    // Unterscheidung riss das den aktiven Spieltag-Filter bei jeder Aktion (z.B. "Spieltag simulieren")
    // unconditional auf "Alle" zurück.
    private int? _vorherigeFokusGruppenNummer;

    private void BeiGruppeFokusAenderung()
    {
        bool istEchterGruppenwechsel = AusgewaehlteGruppe?.GruppenNummer != _vorherigeFokusGruppenNummer;
        _vorherigeFokusGruppenNummer = AusgewaehlteGruppe?.GruppenNummer;

        _gruppeSimulierenBefehl.NotifyCanExecuteChanged();
        _spieltagSimulierenBefehl.NotifyCanExecuteChanged();
        _zurueckZurUebersichtBefehl.NotifyCanExecuteChanged();

        if (istEchterGruppenwechsel)
        {
            FokusSpieltage.Clear();
            FokusAlleAktiv       = true;
            FokusAktiverSpieltag = 0;

            if (AusgewaehlteGruppe is not null)
            {
                foreach (var st in Spiele
                    .Where(s => s.GruppeNummer == AusgewaehlteGruppe.GruppenNummer && s.Spieltag.HasValue)
                    .Select(s => s.Spieltag!.Value)
                    .Distinct()
                    .OrderBy(t => t))
                {
                    FokusSpieltage.Add(st);
                }
                if (FokusSpieltage.Count > 0)
                    FokusAktiverSpieltag = FokusSpieltage[0];
            }
        }

        OnPropertyChanged(nameof(HatMehrereSpieltage));
        _fokusVorigerSpieltag.NotifyCanExecuteChanged();
        _fokusNaechsterSpieltag.NotifyCanExecuteChanged();
        _fokusAlleSpieltageBefehl.NotifyCanExecuteChanged();
    }

    // ── Effektiver Gruppen-/Spieltag-Fokus (Issue #170) ──────────────────────────
    // Gruppe/Spieltag-Aktionen sollen auch greifen, wenn kein expliziter Gruppenfilter aktiv
    // ist, sondern nur ein einzelnes Spiel in der Liste selektiert wurde — analog dem
    // bestehenden Fallback in TurnierBereichViewModel.AktualisiereGruppenFokus. Expliziter
    // Filter (AusgewaehlteGruppe) hat weiterhin Vorrang.

    private GruppenTabelle? EffektiveGruppe =>
        AusgewaehlteGruppe ?? (AusgewaehlteSpiel?.GruppeNummer is { } gn
            ? GruppenTabellen.FirstOrDefault(g => g.GruppenNummer == gn)
            : null);

    private int? EffektiverSpieltag =>
        AusgewaehlteGruppe is not null
            ? (FokusAlleAktiv ? null : FokusAktiverSpieltag)
            : AusgewaehlteSpiel?.Spieltag;

    // ── Simulation: CanExecute ─────────────────────────────────────────────────

    private bool CanSpielSimulieren()    =>
        HatRunde && AusgewaehlteSpiel is { IstPlatzhalterSpiel: false } && !IstSimulierend && !FormHatErgebnis;

    private bool CanAusfuehrlichSimulieren() =>
        HatRunde && AusgewaehlteSpiel is { IstPlatzhalterSpiel: false } && AusgewaehlteInstanz is not null
        && !IstSimulierend && !FormHatErgebnis;

    private bool CanRundeSimulieren() =>
        HatRunde && !IstSimulierend
        && Spiele.Any(s => s.Status != SpielStatus.Abgeschlossen && !s.IstPlatzhalterSpiel);

    private bool CanGruppeSimulieren() =>
        EffektiveGruppe is { } gruppe && !IstSimulierend
        && Spiele.Any(s => s.GruppeNummer == gruppe.GruppenNummer
                         && s.Status != SpielStatus.Abgeschlossen && !s.IstPlatzhalterSpiel);

    private bool CanSpieltagSimulieren() =>
        EffektiveGruppe is { } gruppe && EffektiverSpieltag is { } spieltag && !IstSimulierend
        && Spiele.Any(s => s.GruppeNummer == gruppe.GruppenNummer
                         && s.Spieltag == spieltag
                         && s.Status != SpielStatus.Abgeschlossen && !s.IstPlatzhalterSpiel);

    // ── Ergebnis-Reset: CanExecute ───────────────────────────────────────────────

    private bool CanRundeZuruecksetzen() =>
        HatRunde && !IstSimulierend && Spiele.Any(s => s.HatErgebnis);

    private bool CanGruppeZuruecksetzen() =>
        EffektiveGruppe is { } gruppe && !IstSimulierend
        && Spiele.Any(s => s.GruppeNummer == gruppe.GruppenNummer && s.HatErgebnis);

    private bool CanSpieltagZuruecksetzen() =>
        EffektiveGruppe is { } gruppe && EffektiverSpieltag is { } spieltag && !IstSimulierend
        && Spiele.Any(s => s.GruppeNummer == gruppe.GruppenNummer
                         && s.Spieltag == spieltag && s.HatErgebnis);

    private bool CanMehrfachZuruecksetzen() =>
        !IstSimulierend && AnzahlMehrfachZuruecksetzbar > 0;

    // ── Simulation: Aktionen ───────────────────────────────────────────────────

    private async Task SpielSimulierenAsync()
    {
        if (AusgewaehlteSpiel is null || AusgewaehlteRunde is null) return;
        int spielId = AusgewaehlteSpiel.Id;
        int rundeId = AusgewaehlteRunde.Id;
        Fehlermeldung  = null;
        IstSimulierend = true;
        try
        {
            await _simulationsService.SpielSimulierenAsync(spielId, AusgewaehlterSimulationsModus.Wert);

            // Siehe ErgebnisSpeichernAsync — auch nach Simulation kann eine Finale-Runde entscheidungsreif werden.
            await _spielplanService.FinaleAutomatischGenerierenWennBereitAsync(AusgewaehlteRunde.TurnierInstanzId);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i)))
                Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap));
            OnPropertyChanged(nameof(FokusSpiele));

            // Property-Change-Trigger (SpielWechselAsync, fire-and-forget) hier unterdrücken,
            // sonst laeuft dessen DB-Zugriff parallel zu TurnierBaumAktualisierenAsync auf
            // demselben DbContext (siehe [[feedback-dbcontext-concurrency]]) — Formular explizit
            // erst danach sequenziell neu laden.
            _spielWechselUnterdrueckt++;
            try
            {
                AusgewaehlteSpiel = Spiele.FirstOrDefault(s => s.Id == spielId);
            }
            finally
            {
                _spielWechselUnterdrueckt--;
            }
            await TurnierBaumAktualisierenAsync(AusgewaehlteRunde.TurnierInstanzId);
            await BeiSpielAenderungAsync();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstSimulierend = false;
            AktualisiereBefehle();
        }
    }

    // Issue-Nachbesserung zu #163: vormals eine eigenständige Navigation zur Simulations-Vollansicht
    // (NavigationService.NavigiereZuAsync<SimulationsViewModel>) — das erzeugte eine zweite, vom
    // Turnier-Bereich losgelöste Ansicht ohne den gemeinsamen Turnier-Header und machte die Rückkehr
    // zum Spielplan unnötig umständlich. Stattdessen wird das Event nach oben durchgereicht
    // (TurnierBereichViewModel → TurnierDurchfuehrungViewModel), das den Simulation-Tab im selben,
    // bereits offenen Turnier-Bereich fokussiert und dort die Vorauswahl setzt.
    public event EventHandler<VorausgewaehlteSimulation>? AusfuehrlicheSimulationAngefordert;

    private Task AusfuehrlichSimulierenAsync()
    {
        if (AusgewaehlteSpiel is null || AusgewaehlteRunde is null || AusgewaehlteInstanz is null)
            return Task.CompletedTask;

        var vorauswahl = new VorausgewaehlteSimulation(
            AusgewaehlteInstanz.Id,
            AusgewaehlteRunde.Id,
            AusgewaehlteSpiel.Id);

        AusfuehrlicheSimulationAngefordert?.Invoke(this, vorauswahl);
        return Task.CompletedTask;
    }

    private async Task RundeSimulierenAsync()
    {
        if (AusgewaehlteRunde is null) return;
        int rundeId = AusgewaehlteRunde.Id;
        Fehlermeldung  = null;
        IstSimulierend = true;
        try
        {
            await _simulationsService.RundeSimulierenAsync(
                rundeId, AusgewaehlterSimulationsModus.Wert);

            // Siehe ErgebnisSpeichernAsync — auch nach Simulation kann eine Finale-Runde entscheidungsreif werden.
            await _spielplanService.FinaleAutomatischGenerierenWennBereitAsync(AusgewaehlteRunde.TurnierInstanzId);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i)))
                Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap));
            OnPropertyChanged(nameof(FokusSpiele));
            AusgewaehlteSpiel = null;
            await TurnierBaumAktualisierenAsync(AusgewaehlteRunde!.TurnierInstanzId);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstSimulierend = false;
            AktualisiereBefehle();
        }
    }

    private async Task GruppeSimulierenAsync()
    {
        if (EffektiveGruppe is not { } gruppe || AusgewaehlteRunde is null) return;
        int rundeId  = AusgewaehlteRunde.Id;
        int? spielId = AusgewaehlteSpiel?.Id;
        var teamIds = gruppe.Tabelle.Select(t => t.Mannschaft.Id).ToList();
        Fehlermeldung  = null;
        IstSimulierend = true;
        try
        {
            await _simulationsService.GruppeSimulierenAsync(
                rundeId, teamIds, AusgewaehlterSimulationsModus.Wert);

            await GruppenTabellenLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i)))
                Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap));
            OnPropertyChanged(nameof(FokusSpiele));
            await SpielAuswahlWiederherstellenAsync(spielId);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstSimulierend = false;
            AktualisiereBefehle();
        }
    }

    private async Task SpieltagSimulierenAsync()
    {
        if (EffektiveGruppe is not { } gruppe || AusgewaehlteRunde is null
            || EffektiverSpieltag is not { } spieltag) return;
        int rundeId  = AusgewaehlteRunde.Id;
        int? spielId = AusgewaehlteSpiel?.Id;
        var teamIds  = gruppe.Tabelle.Select(t => t.Mannschaft.Id).ToList();
        Fehlermeldung  = null;
        IstSimulierend = true;
        try
        {
            await _simulationsService.SpieltagInGruppeSimulierenAsync(
                rundeId, spieltag, teamIds, AusgewaehlterSimulationsModus.Wert);

            await GruppenTabellenLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i)))
                Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap));
            OnPropertyChanged(nameof(FokusSpiele));
            await SpielAuswahlWiederherstellenAsync(spielId);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstSimulierend = false;
            AktualisiereBefehle();
        }
    }

    // ── Ergebnis-Reset: Aktionen (Issue #123) ────────────────────────────────────
    // Kaskaden-Check (automatisch generiertes Finale ggf. entfernen/neu generieren) läuft seit
    // Issue #125 zentral in SpielErgebnisService.MehrereErgebnisseLoeschenAsync.

    private async Task RundeZuruecksetzenAsync()
    {
        if (AusgewaehlteRunde is null) return;
        var spielIds = Spiele.Where(s => s.HatErgebnis).Select(s => s.Id).ToList();
        if (spielIds.Count == 0) return;

        var antwort = MessageBox.Show(
            $"Alle {spielIds.Count} Ergebnis(se) der Runde '{AusgewaehlteRunde.Name}' zurücksetzen?",
            "Ergebnisse zurücksetzen", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (antwort != MessageBoxResult.Yes) return;

        int rundeId = AusgewaehlteRunde.Id;
        Fehlermeldung  = null;
        IstSimulierend = true;
        try
        {
            await _spielService.MehrereErgebnisseLoeschenAsync(spielIds);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i)))
                Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap));
            OnPropertyChanged(nameof(FokusSpiele));
            AusgewaehlteSpiel = null;
            await TurnierBaumAktualisierenAsync(AusgewaehlteRunde!.TurnierInstanzId);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstSimulierend = false;
            AktualisiereBefehle();
        }
    }

    private async Task GruppeZuruecksetzenAsync()
    {
        if (EffektiveGruppe is not { } gruppe || AusgewaehlteRunde is null) return;
        var spielIds = Spiele
            .Where(s => s.GruppeNummer == gruppe.GruppenNummer && s.HatErgebnis)
            .Select(s => s.Id).ToList();
        if (spielIds.Count == 0) return;

        var antwort = MessageBox.Show(
            $"Alle {spielIds.Count} Ergebnis(se) der Gruppe {gruppe.GruppenNummer} zurücksetzen?",
            "Ergebnisse zurücksetzen", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (antwort != MessageBoxResult.Yes) return;

        int rundeId  = AusgewaehlteRunde.Id;
        int? spielId = AusgewaehlteSpiel?.Id;
        Fehlermeldung  = null;
        IstSimulierend = true;
        try
        {
            await _spielService.MehrereErgebnisseLoeschenAsync(spielIds);

            await GruppenTabellenLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i)))
                Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap));
            OnPropertyChanged(nameof(FokusSpiele));
            await SpielAuswahlWiederherstellenAsync(spielId);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstSimulierend = false;
            AktualisiereBefehle();
        }
    }

    private async Task SpieltagZuruecksetzenAsync()
    {
        if (EffektiveGruppe is not { } gruppe || AusgewaehlteRunde is null
            || EffektiverSpieltag is not { } spieltag) return;
        var spielIds = Spiele
            .Where(s => s.GruppeNummer == gruppe.GruppenNummer
                     && s.Spieltag == spieltag && s.HatErgebnis)
            .Select(s => s.Id).ToList();
        if (spielIds.Count == 0) return;

        var antwort = MessageBox.Show(
            $"Alle {spielIds.Count} Ergebnis(se) von Spieltag {spieltag} (Gruppe {gruppe.GruppenNummer}) zurücksetzen?",
            "Ergebnisse zurücksetzen", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (antwort != MessageBoxResult.Yes) return;

        int rundeId  = AusgewaehlteRunde.Id;
        int? spielId = AusgewaehlteSpiel?.Id;
        Fehlermeldung  = null;
        IstSimulierend = true;
        try
        {
            await _spielService.MehrereErgebnisseLoeschenAsync(spielIds);

            await GruppenTabellenLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i)))
                Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap));
            OnPropertyChanged(nameof(FokusSpiele));
            await SpielAuswahlWiederherstellenAsync(spielId);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstSimulierend = false;
            AktualisiereBefehle();
        }
    }

    private async Task MehrfachZuruecksetzenAsync()
    {
        if (AusgewaehlteRunde is null) return;
        var spielIds = MehrfachAusgewaehlteSpiele.Where(s => s.HatErgebnis).Select(s => s.Id).ToList();
        if (spielIds.Count == 0) return;

        var antwort = MessageBox.Show(
            $"{spielIds.Count} Spielergebnis(se) zurücksetzen?",
            "Ergebnisse zurücksetzen", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (antwort != MessageBoxResult.Yes) return;

        int rundeId = AusgewaehlteRunde.Id;
        Fehlermeldung  = null;
        IstSimulierend = true;
        try
        {
            await _spielService.MehrereErgebnisseLoeschenAsync(spielIds);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i)))
                Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap));
            OnPropertyChanged(nameof(FokusSpiele));
            AusgewaehlteSpiel = null;
            MehrfachAusgewaehlteSpiele = [];
            await TurnierBaumAktualisierenAsync(AusgewaehlteRunde!.TurnierInstanzId);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstSimulierend = false;
            AktualisiereBefehle();
        }
    }

    private IReadOnlyList<SpielZeile> BerechneFokusSpiele()
    {
        if (AusgewaehlteGruppe is null) return [];
        var spiele = Spiele.Where(s => s.GruppeNummer == AusgewaehlteGruppe.GruppenNummer);
        if (!FokusAlleAktiv)
            spiele = spiele.Where(s => s.Spieltag == FokusAktiverSpieltag);
        return spiele.ToList();
    }

    private static string RegelartZuText(RankingRegelArt regelart) => regelart switch
    {
        RankingRegelArt.Punkte                         => "Punkte",
        RankingRegelArt.Tordifferenz                   => "Tordifferenz",
        RankingRegelArt.ErzielteTore                   => "Erzielte Tore",
        RankingRegelArt.WenigerGegentore               => "Weniger Gegentore",
        RankingRegelArt.Siege                          => "Siege",
        RankingRegelArt.DirekterVergleichPunkte        => "DV Punkte",
        RankingRegelArt.DirekterVergleichTordifferenz  => "DV Tordiff.",
        RankingRegelArt.DirekterVergleichErzielteTore  => "DV Tore",
        RankingRegelArt.DirekterVergleichAuswaertstore => "DV Auswärtstore",
        RankingRegelArt.FairplayWertung                => "Fairplay",
        RankingRegelArt.Losentscheid                   => "Los",
        _                                              => regelart.ToString()
    };
}
