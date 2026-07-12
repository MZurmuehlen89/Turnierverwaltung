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
    private readonly INavigationService    _navigationService;

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
    [NotifyPropertyChangedFor(nameof(ZeigeAnsichtUmschalter))]
    [NotifyPropertyChangedFor(nameof(AnsichtUmschalterZweiterTabText))]
    [NotifyPropertyChangedFor(nameof(ZeigtHinRueckrundenTabs))]
    [NotifyPropertyChangedFor(nameof(IstGruppenAnsicht))]
    [NotifyPropertyChangedFor(nameof(IstLigaAnsicht))]
    [NotifyPropertyChangedFor(nameof(IstSpielplanAnsicht))]
    [NotifyPropertyChangedFor(nameof(IstGruppenFokusAnsicht))]
    [NotifyPropertyChangedFor(nameof(IstGruppenUebersichtAnsicht))]
    private TurnierRunde? _ausgewaehlteRunde;

    public bool HatRunde => AusgewaehlteRunde is not null;

    // ── Gruppen-Ansicht ───────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstGruppenAnsicht))]
    [NotifyPropertyChangedFor(nameof(IstLigaAnsicht))]
    [NotifyPropertyChangedFor(nameof(IstSpielplanAnsicht))]
    [NotifyPropertyChangedFor(nameof(IstGruppenFokusAnsicht))]
    [NotifyPropertyChangedFor(nameof(IstGruppenUebersichtAnsicht))]
    private bool _gruppenAnsichtAktiv;

    public bool IstGruppenphase => AusgewaehlteRunde?.Rundenart == Rundenart.Gruppenphase;
    public bool IstLigaRunde    => AusgewaehlteRunde?.Rundenart == Rundenart.Liga;
    public bool ZeigeAnsichtUmschalter => IstGruppenphase || IstLigaRunde;
    public string AnsichtUmschalterZweiterTabText => IstLigaRunde ? "Tabelle" : "Gruppen";
    public bool IstGruppenAnsicht => GruppenAnsichtAktiv && IstGruppenphase;
    public bool IstLigaAnsicht    => GruppenAnsichtAktiv && IstLigaRunde;
    public bool IstSpielplanAnsicht => !IstGruppenAnsicht && !IstLigaAnsicht;

    public ObservableCollection<GruppenTabelle> GruppenTabellen { get; } = [];
    public bool HatGruppenTabellen => GruppenTabellen.Count > 0;

    public ObservableCollection<TabellenEintrag> LigaTabelle { get; } = [];
    public bool HatLigaTabelle => LigaTabelle.Count > 0;
    public bool ZeigtHinRueckrundenTabs => AusgewaehlteRunde?.MitRueckspiel == true;

    [ObservableProperty] private TabellenAnsicht _ausgewaehlteLigaAnsicht = TabellenAnsicht.Gesamt;

    // Historischer Tabellenstand nach Spieltag (Issue #111, Phase 2 von #47). Null = aktueller Stand.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LigaBisSpieltagAuswahl))]
    private int? _ligaBisSpieltag;

    /// <summary>Spieltage, für die in der Liga-Runde bereits Spiele existieren (aufsteigend, ohne Duplikate).</summary>
    public IReadOnlyList<int> LigaVerfuegbareSpieltage =>
        Spiele.Where(s => s.Spieltag.HasValue).Select(s => s.Spieltag!.Value).Distinct().OrderBy(sp => sp).ToList();

    /// <summary>Auswahloptionen für den Spieltag-Filter der Liga-Tabelle: 0 = "Aktuell" (Sentinel statt null, siehe
    /// LigaBisSpieltagAuswahl) + alle vorhandenen Spieltage. Echte Spieltage sind stets &gt;= 1.</summary>
    public IReadOnlyList<int> LigaSpieltagOptionen =>
        new[] { 0 }.Concat(LigaVerfuegbareSpieltage).ToList();

    /// <summary>UI-Wrapper um <see cref="LigaBisSpieltag"/> für das ComboBox-Binding: WPF behandelt einen echten
    /// null-Eintrag in ItemsSource bei SelectedItem-Bindings als "keine Auswahl" statt als auswählbares Element,
    /// daher hier 0 als Sentinel für "Aktuell" statt null.</summary>
    public int LigaBisSpieltagAuswahl
    {
        get => LigaBisSpieltag ?? 0;
        set => LigaBisSpieltag = value == 0 ? null : value;
    }

    // Historischer Tabellenstand nach Spieltag für die Gruppenphase (Issue #112, Phase 2 von #47).
    // In Gruppen-Übersicht UND Gruppen-Fokus-Ansicht wählbar (geteilter Zustand). Bewusst als eigener
    // Selektor von der Spieltag-Navigation in der Fokus-Ansicht getrennt, die die Simulation steuert.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GruppenBisSpieltagAuswahl))]
    private int? _gruppenBisSpieltag;

    /// <summary>Auswahloptionen für den Spieltag-Filter der Gruppentabellen (dieselbe Spieltag-Liste wie in der
    /// Liga-Ansicht, da beide aus derselben <see cref="Spiele"/>-Collection der Runde abgeleitet werden).</summary>
    public IReadOnlyList<int> GruppenSpieltagOptionen => LigaSpieltagOptionen;

    /// <summary>UI-Wrapper um <see cref="GruppenBisSpieltag"/>, siehe <see cref="LigaBisSpieltagAuswahl"/>.</summary>
    public int GruppenBisSpieltagAuswahl
    {
        get => GruppenBisSpieltag ?? 0;
        set => GruppenBisSpieltag = value == 0 ? null : value;
    }

    private readonly RelayCommand _zuSpielplanWechselnBefehl;
    private readonly RelayCommand _zuGruppenWechselnBefehl;
    private readonly RelayCommand<TabellenAnsicht?> _ligaAnsichtWaehlenBefehl;
    public ICommand ZuSpielplanWechselnBefehl  => _zuSpielplanWechselnBefehl;
    public ICommand ZuGruppenWechselnBefehl    => _zuGruppenWechselnBefehl;
    public ICommand LigaAnsichtWaehlenBefehl   => _ligaAnsichtWaehlenBefehl;

    // ── Gruppen-Fokus ─────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstGruppenFokusAnsicht))]
    [NotifyPropertyChangedFor(nameof(IstGruppenUebersichtAnsicht))]
    [NotifyPropertyChangedFor(nameof(FokusSpiele))]
    private GruppenTabelle? _ausgewaehlteGruppe;

    public bool IstGruppenFokusAnsicht     => IstGruppenAnsicht && AusgewaehlteGruppe is not null;
    public bool IstGruppenUebersichtAnsicht => IstGruppenAnsicht && AusgewaehlteGruppe is null;

    public ObservableCollection<int> FokusSpieltage { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FokusSpiele))]
    private bool _fokusAlleAktiv = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FokusSpiele))]
    [NotifyPropertyChangedFor(nameof(FokusSpieltagnummer))]
    private int _fokusAktiverSpieltag;

    public string FokusSpieltagnummer  => $"Spieltag {FokusAktiverSpieltag}";
    public bool   HatMehrereSpieltage  => FokusSpieltage.Count > 1;
    public bool   KannFokusZurueck     => !FokusAlleAktiv && FokusSpieltage.Count > 0 && FokusAktiverSpieltag > FokusSpieltage[0];
    public bool   KannFokusVor         => FokusSpieltage.Count > 1 && (FokusAlleAktiv || FokusAktiverSpieltag < FokusSpieltage[^1]);

    public IReadOnlyList<SpielZeile> FokusSpiele => BerechneFokusSpiele();

    private readonly RelayCommand<GruppenTabelle> _gruppeFokussierenBefehl;
    private readonly RelayCommand                 _zurueckZurUebersichtBefehl;
    private readonly RelayCommand                 _fokusVorigerSpieltag;
    private readonly RelayCommand                 _fokusNaechsterSpieltag;
    private readonly RelayCommand                 _fokusAlleSpieltageBefehl;

    public ICommand GruppeFokussierenBefehl    => _gruppeFokussierenBefehl;
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
    [ObservableProperty] private string? _simulationsStatus;
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

    // ── IRibbonAware ──────────────────────────────────────────────────────────

    public string RibbonTabTitel => "Spielplan";
    public ICommand NeuBefehl      => _neuBefehl;
    public ICommand SpeichernBefehl => _speichernBefehl;
    public ICommand AbbrechenBefehl => _abbrechenBefehl;
    public ICommand LoeschenBefehl  => _loeschenBefehl;

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
        SimulationsService simulationsService,
        INavigationService navigationService)
    {
        _spielService       = spielService;
        _instanzService     = instanzService;
        _spielplanService   = spielplanService;
        _auslosungService   = auslosungService;
        _baumService        = baumService;
        _regelwerkService   = regelwerkService;
        _tabellenService    = tabellenService;
        _simulationsService = simulationsService;
        _navigationService  = navigationService;

        AusgewaehlterSimulationsModus = SimulationsModusOptionen[0];

        _neuBefehl                 = new AsyncRelayCommand(SpielplanNeuGenerierenAsync,   () => HatRunde);
        _speichernBefehl           = new AsyncRelayCommand(SpeichernAsync,                () => FormAktiv && IsDirty);
        _abbrechenBefehl           = new RelayCommand(FormZuruecksetzen,                  () => FormAktiv && IsDirty);
        _loeschenBefehl            = new AsyncRelayCommand(ErgebnisLoeschenAsync,         () => FormAktiv && FormHatErgebnis);
        _platzhalterBefehl         = new AsyncRelayCommand(PlatzhalterAktualisierenAsync, () => HatRunde && HatPlatzhalterSpiele);
        _zuSpielplanWechselnBefehl = new RelayCommand(() => GruppenAnsichtAktiv = false,  () => IstGruppenphase || IstLigaRunde);
        _zuGruppenWechselnBefehl   = new RelayCommand(() => GruppenAnsichtAktiv = true,
            () => (IstGruppenphase && HatGruppenTabellen) || (IstLigaRunde && HatLigaTabelle));
        _ligaAnsichtWaehlenBefehl  = new RelayCommand<TabellenAnsicht?>(a => LigaAnsichtWaehlen(a ?? TabellenAnsicht.Gesamt));

        _gruppeFokussierenBefehl    = new RelayCommand<GruppenTabelle>(g => AusgewaehlteGruppe = g);
        _zurueckZurUebersichtBefehl = new RelayCommand(() => AusgewaehlteGruppe = null);
        _fokusVorigerSpieltag       = new RelayCommand(FokusEinSpieltag_Vorig, () => KannFokusZurueck);
        _fokusNaechsterSpieltag     = new RelayCommand(FokusEinSpieltag_Vor,   () => KannFokusVor);
        _fokusAlleSpieltageBefehl   = new RelayCommand(() => FokusAlleAktiv = true, () => !FokusAlleAktiv);

        _spielSimulierenBefehl        = new AsyncRelayCommand(SpielSimulierenAsync,         CanSpielSimulieren);
        _rundeSimulierenBefehl        = new AsyncRelayCommand(RundeSimulierenAsync,         CanRundeSimulieren);
        _gruppeSimulierenBefehl       = new AsyncRelayCommand(GruppeSimulierenAsync,        CanGruppeSimulieren);
        _spieltagSimulierenBefehl     = new AsyncRelayCommand(SpieltagSimulierenAsync,      CanSpieltagSimulieren);
        _ausfuehrlichSimulierenBefehl = new AsyncRelayCommand(AusfuehrlichSimulierenAsync,  CanAusfuehrlichSimulieren);
    }

    // ── Initialisierung ───────────────────────────────────────────────────────

    public override Task AktivierenAsync() => Task.CompletedTask;

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
        GruppenAnsichtAktiv  = false;
        AusgewaehlteGruppe   = null;
        GruppenTabellen.Clear();
        LigaTabelle.Clear();
        AusgewaehlteLigaAnsicht = TabellenAnsicht.Gesamt;
        // Backing-Felder direkt setzen (nicht ueber die Properties) - TabellenNeuLadenAsync unten laedt
        // die Tabellen ohnehin gleich neu; ueber die Properties wuerden OnLigaBisSpieltagChanged/
        // OnGruppenBisSpieltagChanged einen zusaetzlichen, mit diesem Reload konkurrierenden
        // DbContext-Zugriff ausloesen.
        _ligaBisSpieltag = null;
        _gruppenBisSpieltag = null;
        _teamGruppeMap = [];
        OnPropertyChanged(nameof(HatGruppenTabellen));
        OnPropertyChanged(nameof(HatLigaTabelle));
        AktualisiereBefehle();
        _zuSpielplanWechselnBefehl.NotifyCanExecuteChanged();
        _zuGruppenWechselnBefehl.NotifyCanExecuteChanged();

        if (AusgewaehlteRunde is null) return;

        await TabellenNeuLadenAsync(AusgewaehlteRunde.Id);

        var liste = await _spielService.AlleNachRundeAsync(AusgewaehlteRunde.Id);
        foreach (var (s, i) in liste.Select((x, i) => (x, i)))
            Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap));

        OnPropertyChanged(nameof(HatPlatzhalterSpiele));
        OnPropertyChanged(nameof(LigaVerfuegbareSpieltage));
        OnPropertyChanged(nameof(LigaSpieltagOptionen));
        OnPropertyChanged(nameof(LigaBisSpieltag));
        OnPropertyChanged(nameof(LigaBisSpieltagAuswahl));
        OnPropertyChanged(nameof(GruppenSpieltagOptionen));
        OnPropertyChanged(nameof(GruppenBisSpieltag));
        OnPropertyChanged(nameof(GruppenBisSpieltagAuswahl));
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

    private async Task GruppenTabellenLadenAsync(int rundeId)
    {
        GruppenTabellen.Clear();
        _teamGruppeMap = [];
        try
        {
            var tabellen = await _tabellenService.GruppenTabellenBerechnenAsync(rundeId, GruppenBisSpieltag);
            foreach (var gt in tabellen)
            {
                GruppenTabellen.Add(gt);
                foreach (var te in gt.Tabelle)
                    _teamGruppeMap[te.Mannschaft.Id] = gt.GruppenNummer;
            }
        }
        catch { /* Gruppenstand konnte nicht berechnet werden — kein Fehler */ }
        OnPropertyChanged(nameof(HatGruppenTabellen));
        _zuGruppenWechselnBefehl.NotifyCanExecuteChanged();

        // Bei aktiver Gruppen-Fokus-Ansicht (z.B. Spieltag-Snapshot dort umgeschaltet, Issue #112) zeigt
        // AusgewaehlteGruppe sonst weiter auf die alte, gerade aus GruppenTabellen entfernte Instanz.
        if (AusgewaehlteGruppe is { } aktuelleGruppe)
            AusgewaehlteGruppe = GruppenTabellen.FirstOrDefault(g => g.GruppenNummer == aktuelleGruppe.GruppenNummer);
    }

    private async Task LigaTabelleLadenAsync(int rundeId)
    {
        LigaTabelle.Clear();
        try
        {
            var tabelle = await _tabellenService.TabelleBerechnenAsync(rundeId, AusgewaehlteLigaAnsicht, LigaBisSpieltag);
            foreach (var eintrag in tabelle)
                LigaTabelle.Add(eintrag);
        }
        catch { /* Tabelle konnte nicht berechnet werden — kein Fehler */ }
        OnPropertyChanged(nameof(HatLigaTabelle));
        _zuGruppenWechselnBefehl.NotifyCanExecuteChanged();
    }

    // Laedt Gruppen- bzw. Liga-Tabelle passend zur aktuellen Rundenart nach.
    private async Task TabellenNeuLadenAsync(int rundeId)
    {
        if (AusgewaehlteRunde?.Rundenart == Rundenart.Gruppenphase)
            await GruppenTabellenLadenAsync(rundeId);
        else if (AusgewaehlteRunde?.Rundenart == Rundenart.Liga)
            await LigaTabelleLadenAsync(rundeId);

        if (SpielstandGeaendertAsync is not null)
            await SpielstandGeaendertAsync();
    }

    // Bewusst kein event (fire-and-forget wuerde parallel zum naechsten DbContext-Zugriff
    // im Aufrufer laufen und "A second operation was started on this context..." ausloesen) —
    // stattdessen ein awaitbares Delegate fuer die Turnier-Fortschrittsanzeige.
    public Func<Task>? SpielstandGeaendertAsync { get; set; }

    private void LigaAnsichtWaehlen(TabellenAnsicht ansicht)
    {
        if (AusgewaehlteLigaAnsicht == ansicht) return;
        AusgewaehlteLigaAnsicht = ansicht;
        if (AusgewaehlteRunde is { } runde)
            _ = LigaTabelleLadenAsync(runde.Id);
    }

    partial void OnLigaBisSpieltagChanged(int? value)
    {
        if (AusgewaehlteRunde is { } runde)
            _ = LigaTabelleLadenAsync(runde.Id);
    }

    partial void OnGruppenBisSpieltagChanged(int? value)
    {
        if (AusgewaehlteRunde is { } runde)
            _ = GruppenTabellenLadenAsync(runde.Id);
    }

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
            OnPropertyChanged(nameof(LigaVerfuegbareSpieltage));
            OnPropertyChanged(nameof(LigaSpieltagOptionen));
            OnPropertyChanged(nameof(GruppenSpieltagOptionen));
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
            OnPropertyChanged(nameof(LigaVerfuegbareSpieltage));
            OnPropertyChanged(nameof(LigaSpieltagOptionen));
            OnPropertyChanged(nameof(GruppenSpieltagOptionen));
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
        var spielId   = AusgewaehlteSpiel.Id;
        var rundeId   = AusgewaehlteRunde.Id;
        Fehlermeldung = null;
        try
        {
            await _spielService.ErgebnisLoeschenAsync(spielId);

            // Siehe ErgebnisSpeichernAsync — ein zuvor automatisch generiertes, noch nicht gespieltes
            // Finale muss wieder entfernt werden, wenn das Halbfinale dadurch nicht mehr entschieden ist.
            await _spielplanService.FinaleAutomatischGenerierenWennBereitAsync(AusgewaehlteRunde.TurnierInstanzId);

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
        _zuSpielplanWechselnBefehl.NotifyCanExecuteChanged();
        _zuGruppenWechselnBefehl.NotifyCanExecuteChanged();
        _fokusVorigerSpieltag.NotifyCanExecuteChanged();
        _fokusNaechsterSpieltag.NotifyCanExecuteChanged();
        _fokusAlleSpieltageBefehl.NotifyCanExecuteChanged();
        _spielSimulierenBefehl.NotifyCanExecuteChanged();
        _rundeSimulierenBefehl.NotifyCanExecuteChanged();
        _gruppeSimulierenBefehl.NotifyCanExecuteChanged();
        _spieltagSimulierenBefehl.NotifyCanExecuteChanged();
        _ausfuehrlichSimulierenBefehl.NotifyCanExecuteChanged();
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

    private void BeiGruppeFokusAenderung()
    {
        FokusSpieltage.Clear();
        FokusAlleAktiv       = true;
        FokusAktiverSpieltag = 0;
        _gruppeSimulierenBefehl.NotifyCanExecuteChanged();
        _spieltagSimulierenBefehl.NotifyCanExecuteChanged();

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

        OnPropertyChanged(nameof(HatMehrereSpieltage));
        _fokusVorigerSpieltag.NotifyCanExecuteChanged();
        _fokusNaechsterSpieltag.NotifyCanExecuteChanged();
        _fokusAlleSpieltageBefehl.NotifyCanExecuteChanged();
    }

    // ── Simulation: CanExecute ─────────────────────────────────────────────────

    private bool CanSpielSimulieren()    =>
        HatRunde && AusgewaehlteSpiel is { IstPlatzhalterSpiel: false } && !IstSimulierend;

    private bool CanAusfuehrlichSimulieren() =>
        HatRunde && AusgewaehlteSpiel is { IstPlatzhalterSpiel: false } && AusgewaehlteInstanz is not null && !IstSimulierend;

    private bool CanRundeSimulieren()    => HatRunde && !IstSimulierend;

    private bool CanGruppeSimulieren()   => IstGruppenFokusAnsicht && AusgewaehlteGruppe is not null && !IstSimulierend;

    private bool CanSpieltagSimulieren() =>
        IstGruppenFokusAnsicht && AusgewaehlteGruppe is not null && !FokusAlleAktiv && !IstSimulierend;

    // ── Simulation: Aktionen ───────────────────────────────────────────────────

    private async Task SpielSimulierenAsync()
    {
        if (AusgewaehlteSpiel is null || AusgewaehlteRunde is null) return;
        int spielId = AusgewaehlteSpiel.Id;
        int rundeId = AusgewaehlteRunde.Id;
        SimulationsStatus = null;
        Fehlermeldung     = null;
        IstSimulierend    = true;
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
            SimulationsStatus = "Spiel simuliert.";
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

    private async Task AusfuehrlichSimulierenAsync()
    {
        if (AusgewaehlteSpiel is null || AusgewaehlteRunde is null || AusgewaehlteInstanz is null)
            return;

        var vorauswahl = new VorausgewaehlteSimulation(
            AusgewaehlteInstanz.Id,
            AusgewaehlteRunde.Id,
            AusgewaehlteSpiel.Id);

        await _navigationService.NavigiereZuAsync<SimulationsViewModel>(vm => vm.Vorauswahl = vorauswahl);
    }

    private async Task RundeSimulierenAsync()
    {
        if (AusgewaehlteRunde is null) return;
        int rundeId = AusgewaehlteRunde.Id;
        SimulationsStatus = null;
        Fehlermeldung     = null;
        IstSimulierend    = true;
        try
        {
            int anzahl = await _simulationsService.RundeSimulierenAsync(
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

            SimulationsStatus = anzahl == 0
                ? "Keine offenen Spiele in dieser Runde."
                : $"{anzahl} Spiel(e) simuliert.";
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
        if (AusgewaehlteGruppe is null || AusgewaehlteRunde is null) return;
        int rundeId = AusgewaehlteRunde.Id;
        var teamIds = AusgewaehlteGruppe.Tabelle.Select(t => t.Mannschaft.Id).ToList();
        SimulationsStatus = null;
        Fehlermeldung     = null;
        IstSimulierend    = true;
        try
        {
            int anzahl = await _simulationsService.GruppeSimulierenAsync(
                rundeId, teamIds, AusgewaehlterSimulationsModus.Wert);

            await GruppenTabellenLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i)))
                Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap));
            OnPropertyChanged(nameof(FokusSpiele));
            AusgewaehlteSpiel = null;

            SimulationsStatus = anzahl == 0
                ? "Keine offenen Spiele in dieser Gruppe."
                : $"{anzahl} Spiel(e) simuliert.";
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
        if (AusgewaehlteGruppe is null || AusgewaehlteRunde is null || FokusAlleAktiv) return;
        int rundeId  = AusgewaehlteRunde.Id;
        int spieltag = FokusAktiverSpieltag;
        var teamIds  = AusgewaehlteGruppe.Tabelle.Select(t => t.Mannschaft.Id).ToList();
        SimulationsStatus = null;
        Fehlermeldung     = null;
        IstSimulierend    = true;
        try
        {
            int anzahl = await _simulationsService.SpieltagInGruppeSimulierenAsync(
                rundeId, spieltag, teamIds, AusgewaehlterSimulationsModus.Wert);

            await GruppenTabellenLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i)))
                Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap));
            OnPropertyChanged(nameof(FokusSpiele));
            AusgewaehlteSpiel = null;

            SimulationsStatus = anzahl == 0
                ? $"Keine offenen Spiele in Spieltag {spieltag} dieser Gruppe."
                : $"{anzahl} Spiel(e) in Spieltag {spieltag} simuliert.";
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
