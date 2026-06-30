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

public partial class SpielplanViewModel : ViewModelBase, IRibbonAware
{
    private readonly SpielErgebnisService _spielService;
    private readonly TurnierInstanzService _instanzService;
    private readonly SpielplanService _spielplanService;
    private readonly AuslosungService _auslosungService;
    private readonly TurnierBaumService _baumService;
    private readonly RegelwerkService _regelwerkService;
    private readonly TabellenService _tabellenService;
    private readonly SimulationsService _simulationsService;

    private int _populiertGerade;
    private Dictionary<int, int> _teamGruppeMap = [];
    private List<VerfahrenArt> _erlaubteVerfahren = [];

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
    [NotifyPropertyChangedFor(nameof(ZeigeAnsichtUmschalter))]
    [NotifyPropertyChangedFor(nameof(IstGruppenAnsicht))]
    [NotifyPropertyChangedFor(nameof(IstSpielplanAnsicht))]
    [NotifyPropertyChangedFor(nameof(IstGruppenFokusAnsicht))]
    [NotifyPropertyChangedFor(nameof(IstGruppenUebersichtAnsicht))]
    private TurnierRunde? _ausgewaehlteRunde;

    public bool HatRunde => AusgewaehlteRunde is not null;

    // ── Gruppen-Ansicht ───────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstGruppenAnsicht))]
    [NotifyPropertyChangedFor(nameof(IstSpielplanAnsicht))]
    [NotifyPropertyChangedFor(nameof(IstGruppenFokusAnsicht))]
    [NotifyPropertyChangedFor(nameof(IstGruppenUebersichtAnsicht))]
    private bool _gruppenAnsichtAktiv;

    public bool IstGruppenphase => AusgewaehlteRunde?.Rundenart == Rundenart.Gruppenphase;
    public bool ZeigeAnsichtUmschalter => IstGruppenphase;
    public bool IstGruppenAnsicht => GruppenAnsichtAktiv && IstGruppenphase;
    public bool IstSpielplanAnsicht => !IstGruppenAnsicht;

    public ObservableCollection<GruppenTabelle> GruppenTabellen { get; } = [];
    public bool HatGruppenTabellen => GruppenTabellen.Count > 0;

    private readonly RelayCommand _zuSpielplanWechselnBefehl;
    private readonly RelayCommand _zuGruppenWechselnBefehl;
    public ICommand ZuSpielplanWechselnBefehl => _zuSpielplanWechselnBefehl;
    public ICommand ZuGruppenWechselnBefehl   => _zuGruppenWechselnBefehl;

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
        ParseInt(HeimToreRegulaer) == ParseInt(AuswaertsToreRegulaer);

    public bool ElfmeterAktiv
    {
        get
        {
            if (!IstKoRunde || !HatElfmeterVerfahren) return false;
            if (ParseInt(HeimToreRegulaer) != ParseInt(AuswaertsToreRegulaer)) return false;
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

    public ICommand SpielSimulierenBefehl    => _spielSimulierenBefehl;
    public ICommand RundeSimulierenBefehl    => _rundeSimulierenBefehl;
    public ICommand GruppeSimulierenBefehl   => _gruppeSimulierenBefehl;
    public ICommand SpieltagSimulierenBefehl => _spieltagSimulierenBefehl;

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
        _abbrechenBefehl           = new RelayCommand(FormZuruecksetzen,                  () => FormAktiv);
        _loeschenBefehl            = new AsyncRelayCommand(ErgebnisLoeschenAsync,         () => FormAktiv && FormHatErgebnis);
        _platzhalterBefehl         = new AsyncRelayCommand(PlatzhalterAktualisierenAsync, () => HatRunde && HatPlatzhalterSpiele);
        _zuSpielplanWechselnBefehl = new RelayCommand(() => GruppenAnsichtAktiv = false,  () => IstGruppenphase);
        _zuGruppenWechselnBefehl   = new RelayCommand(() => GruppenAnsichtAktiv = true,   () => IstGruppenphase && HatGruppenTabellen);

        _gruppeFokussierenBefehl    = new RelayCommand<GruppenTabelle>(g => AusgewaehlteGruppe = g);
        _zurueckZurUebersichtBefehl = new RelayCommand(() => AusgewaehlteGruppe = null);
        _fokusVorigerSpieltag       = new RelayCommand(FokusEinSpieltag_Vorig, () => KannFokusZurueck);
        _fokusNaechsterSpieltag     = new RelayCommand(FokusEinSpieltag_Vor,   () => KannFokusVor);
        _fokusAlleSpieltageBefehl   = new RelayCommand(() => FokusAlleAktiv = true, () => !FokusAlleAktiv);

        _spielSimulierenBefehl    = new AsyncRelayCommand(SpielSimulierenAsync,    CanSpielSimulieren);
        _rundeSimulierenBefehl    = new AsyncRelayCommand(RundeSimulierenAsync,    CanRundeSimulieren);
        _gruppeSimulierenBefehl   = new AsyncRelayCommand(GruppeSimulierenAsync,   CanGruppeSimulieren);
        _spieltagSimulierenBefehl = new AsyncRelayCommand(SpieltagSimulierenAsync, CanSpieltagSimulieren);
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
                _ = BeiSpielAenderungAsync();
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
        _teamGruppeMap = [];
        OnPropertyChanged(nameof(HatGruppenTabellen));
        _neuBefehl.NotifyCanExecuteChanged();
        _zuSpielplanWechselnBefehl.NotifyCanExecuteChanged();
        _zuGruppenWechselnBefehl.NotifyCanExecuteChanged();

        if (AusgewaehlteRunde is null) return;

        if (AusgewaehlteRunde.Rundenart == Rundenart.Gruppenphase)
            await GruppenTabellenLadenAsync(AusgewaehlteRunde.Id);

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
    }

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
        OnPropertyChanged(nameof(HatGruppenTabellen));
        _zuGruppenWechselnBefehl.NotifyCanExecuteChanged();
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
                HeimToreVerlaengerung      = e.HeimToreVerlaengerung?.ToString() ?? "";
                AuswaertsToreVerlaengerung = e.AuswaertsToreVerlaengerung?.ToString() ?? "";
                HeimElfmetertore           = e.HeimElfmetertore?.ToString() ?? "";
                AuswaertsElfmetertore      = e.AuswaertsElfmetertore?.ToString() ?? "";
            }
            else
            {
                HeimToreRegulaer           = "0";
                AuswaertsToreRegulaer      = "0";
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

            switch (AusgewaehlteRunde.Rundenart)
            {
                case Rundenart.Gruppenphase:
                {
                    var gruppen = auslosung.Ergebnisse
                        .Where(e => e.HeimMannschaftId.HasValue)
                        .GroupBy(e => e.PaarungsNummer)
                        .OrderBy(g => g.Key)
                        .Select(g => (IReadOnlyList<int>)g
                            .Select(e => e.HeimMannschaftId!.Value).ToList())
                        .ToList();

                    if (gruppen.Any())
                    {
                        await _spielplanService.GenerierenAusGruppenAsync(rundeId, gruppen);
                    }
                    else
                    {
                        // Platzhalter-Gruppenphase: Berger-Round-Robin mit Platzhalternamen
                        var gruppenNamen = auslosung.Ergebnisse
                            .GroupBy(e => e.PaarungsNummer)
                            .OrderBy(g => g.Key)
                            .Select(g => (IReadOnlyList<string>)g
                                .Select(e => e.HeimPlatzhalterName ?? "?")
                                .ToList())
                            .ToList();

                        if (!gruppenNamen.Any() || gruppenNamen.All(g => g.Count < 2))
                        {
                            Fehlermeldung = auslosung.Ergebnisse.Any()
                                ? "Keine gültigen Gruppen-Zuordnungen in der Auslosung gefunden."
                                : "Die Auslosung hat keine Ergebnisse. Bitte die Auslosung zuerst durchführen.";
                            return;
                        }
                        await _spielplanService.GenerierenAusGruppenMitPlatzhalternAsync(rundeId, gruppenNamen);
                    }
                    break;
                }

                case Rundenart.TopfLiga:
                {
                    var paarungen = auslosung.Ergebnisse
                        .Where(e => e.HeimMannschaftId.HasValue && e.AuswaertsMannschaftId.HasValue)
                        .OrderBy(e => e.PaarungsNummer)
                        .Select(e => (e.HeimMannschaftId!.Value, e.AuswaertsMannschaftId!.Value))
                        .ToList<(int, int)>();
                    await _spielplanService.GenerierenAusPaarungenAsync(rundeId, paarungen);
                    break;
                }

                case Rundenart.SchweizerSystem:
                {
                    var sortiertIds = auslosung.Ergebnisse
                        .OrderBy(e => e.PaarungsNummer)
                        .SelectMany(e => new[] { e.HeimMannschaftId, e.AuswaertsMannschaftId })
                        .Where(id => id.HasValue)
                        .Select(id => id!.Value)
                        .Distinct()
                        .ToList();
                    await _spielplanService.SchweizerRundeGenerierenAsync(rundeId, sortiertIds);
                    break;
                }

                default:
                {
                    // Liga, KoRunde, Finale, SpielUmPlatzDrei, Sonderrunde
                    var alleErgebnisse = auslosung.Ergebnisse
                        .OrderBy(e => e.PaarungsNummer)
                        .ToList();

                    bool nurPlatzhalter = alleErgebnisse.Any() &&
                        alleErgebnisse.All(e => !e.HeimMannschaftId.HasValue && !e.AuswaertsMannschaftId.HasValue);

                    if (nurPlatzhalter)
                    {
                        // Platzhalter-Spiele generieren (z.B. "Sieger Gruppe A" vs "Zweiter Gruppe B")
                        var paarungen = alleErgebnisse
                            .Where(e => !e.IstFreilos)
                            .OrderBy(e => e.PaarungsNummer)
                            .Select(e => (
                                Heim: e.HeimPlatzhalterName ?? "?",
                                Auswaerts: e.AuswaertsPlatzhalterName ?? "?"))
                            .ToList<(string Heim, string Auswaerts)>();
                        await _spielplanService.GenerierenAusPlatzhalterPaarungenAsync(rundeId, paarungen);
                    }
                    else
                    {
                        var mannschaftIds = alleErgebnisse
                            .SelectMany(e => new[] { e.HeimMannschaftId, e.AuswaertsMannschaftId })
                            .Where(id => id.HasValue)
                            .Select(id => id!.Value)
                            .Distinct()
                            .ToList();
                        await _spielplanService.GenerierenAsync(rundeId, mannschaftIds);
                    }
                    break;
                }
            }

            if (AusgewaehlteRunde?.Rundenart == Rundenart.Gruppenphase)
                await GruppenTabellenLadenAsync(rundeId);

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

            if (AusgewaehlteRunde?.Rundenart == Rundenart.Gruppenphase)
                await GruppenTabellenLadenAsync(rundeId);

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
        var spielId   = AusgewaehlteSpiel.Id;
        var rundeId   = AusgewaehlteRunde.Id;
        Fehlermeldung = null;
        try
        {
            await _spielService.ErgebnisErfassenAsync(spielId, BaueEingabe());

            if (AusgewaehlteRunde.Rundenart == Rundenart.Gruppenphase)
                await GruppenTabellenLadenAsync(rundeId);

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

            if (AusgewaehlteRunde.Rundenart == Rundenart.Gruppenphase)
                await GruppenTabellenLadenAsync(rundeId);

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

    private ErgebnisErfassenEingabe BaueEingabe() => new(
        ParseInt(HeimToreRegulaer),
        ParseInt(AuswaertsToreRegulaer),
        AusgewaehlteEntscheidungArt,
        ParseIntNullable(HeimToreVerlaengerung),
        ParseIntNullable(AuswaertsToreVerlaengerung),
        ParseIntNullable(HeimElfmetertore),
        ParseIntNullable(AuswaertsElfmetertore));

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
            detail = e.EntscheidungArt switch
            {
                EntscheidungArt.Verlaengerung     => "n.V.",
                EntscheidungArt.GoldenGoal        => "G.G.",
                EntscheidungArt.SilverGoal        => "S.G.",
                EntscheidungArt.Elfmeterschiessen =>
                    $"i.E. ({e.HeimElfmetertore}:{e.AuswaertsElfmetertore})",
                EntscheidungArt.Auswaertstorregel => "A.T.",
                _                                 => ""
            };
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
        bool reg90Draw = ParseInt(HeimToreRegulaer) == ParseInt(AuswaertsToreRegulaer);
        if (!reg90Draw) return EntscheidungArt.RegulaereSpielzeit;

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

            if (AusgewaehlteRunde.Rundenart == Rundenart.Gruppenphase)
                await GruppenTabellenLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i)))
                Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap));
            OnPropertyChanged(nameof(FokusSpiele));
            AusgewaehlteSpiel = Spiele.FirstOrDefault(s => s.Id == spielId);
            await TurnierBaumAktualisierenAsync(AusgewaehlteRunde.TurnierInstanzId);
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

            if (AusgewaehlteRunde?.Rundenart == Rundenart.Gruppenphase)
                await GruppenTabellenLadenAsync(rundeId);

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
