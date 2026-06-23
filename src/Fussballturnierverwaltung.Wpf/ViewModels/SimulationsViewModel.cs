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
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

public record SimulationsModusOption(string Anzeige, SimulationsModus Wert);
public record EinzelspielEreignisZeile(string Minute, string Seite, string Art);

public partial class SimulationsViewModel : ViewModelBase, IRibbonAware
{
    private readonly SimulationsService    _simulationsService;
    private readonly SpielErgebnisService  _spielService;
    private readonly TurnierInstanzService _instanzService;
    private readonly MannschaftService     _mannschaftService;
    private readonly RegelwerkService      _regelwerkService;

    // ── Editor-Modus ──────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstTurnierModus))]
    [NotifyPropertyChangedFor(nameof(IstEinzelspielModus))]
    private int _modusIndex = 0;

    public bool IstTurnierModus     => ModusIndex == 0;
    public bool IstEinzelspielModus => ModusIndex == 1;

    // ── Linke Spalte: Turnier ────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<TurnierInstanz> _instanzen = [];
    [ObservableProperty] private TurnierInstanz? _ausgewaehlteInstanz;
    [ObservableProperty] private ObservableCollection<TurnierRunde> _runden = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatRunde))]
    private TurnierRunde? _ausgewaehlteRunde;

    public bool HatRunde => AusgewaehlteRunde is not null;

    // ── Spielliste ────────────────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<SpielZeile> _spiele = [];
    [ObservableProperty] private SpielZeile? _ausgewaehlteSpiel;

    // ── Einzelspiel: Stammdaten ───────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<Mannschaft> _alleMannschaften = [];
    [ObservableProperty] private ObservableCollection<Regelwerk>  _alleRegelwerke   = [];

    // ── Einzelspiel: Auswahl ─────────────────────────────────────────────────

    [ObservableProperty] private Mannschaft? _einzelspielHeim;
    [ObservableProperty] private Mannschaft? _einzelspielAuswaerts;
    [ObservableProperty] private Regelwerk?  _einzelspielRegelwerk;
    [ObservableProperty] private bool        _einzelspielMitRueckspiel;

    public bool EinzelspielKannSimuliert =>
        EinzelspielHeim     is not null &&
        EinzelspielAuswaerts is not null &&
        EinzelspielHeim     != EinzelspielAuswaerts &&
        !IstLadend;

    // ── Einzelspiel: Ergebnis ────────────────────────────────────────────────

    public ObservableCollection<string>                   EinzelspielErgebnisZeilen { get; } = [];
    public ObservableCollection<EinzelspielEreignisZeile> EinzelspielEreignisse     { get; } = [];

    public bool HatEinzelspielErgebnis   => EinzelspielErgebnisZeilen.Count > 0;
    public bool HatEinzelspielEreignisse => EinzelspielEreignisse.Count > 0;

    // ── Simulationsoptionen ───────────────────────────────────────────────────

    [ObservableProperty] private SimulationsModusOption _ausgewaehlterModus = null!;

    public List<SimulationsModusOption> ModusOptionen { get; } =
    [
        new("Schnellsimulation",       SimulationsModus.SchnellSimulation),
        new("Ausführliche Simulation", SimulationsModus.AusfuehrlicheSimulation),
    ];

    // ── Status ────────────────────────────────────────────────────────────────

    [ObservableProperty] private bool    _istLadend;
    [ObservableProperty] private string? _fehlermeldung;
    [ObservableProperty] private string? _statusMeldung;

    // ── Befehle ───────────────────────────────────────────────────────────────

    public AsyncRelayCommand RundeSimulierenBefehl  { get; }
    public AsyncRelayCommand EinzelspielFreiBefehl  { get; }

    public string   RibbonTabTitel  => "Simulation";
    public ICommand NeuBefehl       => _neuBefehl;
    public ICommand SpeichernBefehl => _speichernBefehl;
    public ICommand AbbrechenBefehl => _abbrechenBefehl;
    public ICommand LoeschenBefehl  => _loeschenBefehl;

    private readonly AsyncRelayCommand _neuBefehl;
    private readonly AsyncRelayCommand _speichernBefehl;
    private readonly RelayCommand      _abbrechenBefehl;
    private readonly RelayCommand      _loeschenBefehl;

    // ── Konstruktor ───────────────────────────────────────────────────────────

    public SimulationsViewModel(
        SimulationsService    simulationsService,
        SpielErgebnisService  spielService,
        TurnierInstanzService instanzService,
        MannschaftService     mannschaftService,
        RegelwerkService      regelwerkService)
    {
        _simulationsService  = simulationsService;
        _spielService        = spielService;
        _instanzService      = instanzService;
        _mannschaftService   = mannschaftService;
        _regelwerkService    = regelwerkService;

        AusgewaehlterModus = ModusOptionen[0];

        _neuBefehl       = new AsyncRelayCommand(TurnierSimulierenAsync,    () => AusgewaehlteInstanz != null && !IstLadend);
        _speichernBefehl = new AsyncRelayCommand(EinzelspielSimulierenAsync, () => AusgewaehlteSpiel  != null && !IstLadend);
        _abbrechenBefehl = new RelayCommand(() => { }, () => false);
        _loeschenBefehl  = new RelayCommand(() => { }, () => false);

        RundeSimulierenBefehl = new AsyncRelayCommand(RundeSimulierenAsync,           () => AusgewaehlteRunde != null && !IstLadend);
        EinzelspielFreiBefehl = new AsyncRelayCommand(EinzelspielFreiSimulierenAsync, () => EinzelspielKannSimuliert);
    }

    // ── Initialisierung ───────────────────────────────────────────────────────

    public override async Task AktivierenAsync()
    {
        var instanzListe     = await _instanzService.AlleAbrufenAsync();
        var mannschaftsListe = await _mannschaftService.AlleAbrufenAsync();
        var regelwerkListe   = await _regelwerkService.AlleAbrufenAsync();

        Instanzen.Clear();
        foreach (var i in instanzListe) Instanzen.Add(i);

        AlleMannschaften.Clear();
        foreach (var m in mannschaftsListe.OrderBy(m => m.Name)) AlleMannschaften.Add(m);

        AlleRegelwerke.Clear();
        foreach (var r in regelwerkListe) AlleRegelwerke.Add(r);
    }

    // ── Property-Reaktionen ───────────────────────────────────────────────────

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        switch (e.PropertyName)
        {
            case nameof(ModusIndex):
                StatusMeldung = null;
                Fehlermeldung = null;
                break;
            case nameof(AusgewaehlteInstanz):
                _ = BeiInstanzAenderungAsync();
                break;
            case nameof(AusgewaehlteRunde):
                _ = BeiRundeAenderungAsync();
                break;
            case nameof(AusgewaehlteSpiel):
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
        StatusMeldung     = null;
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
        StatusMeldung     = null;
        AktualisiereBefehle();

        if (AusgewaehlteRunde is null) return;
        await LadeSpiellisteAsync();
    }

    private async Task LadeSpiellisteAsync()
    {
        if (AusgewaehlteRunde is null) return;

        var liste = await _spielService.AlleNachRundeAsync(AusgewaehlteRunde.Id);
        Spiele.Clear();
        foreach (var (s, i) in liste.Select((x, i) => (x, i)))
            Spiele.Add(MapSpielZeile(s, i));
    }

    // ── Ribbon: Turnier simulieren ────────────────────────────────────────────

    private async Task TurnierSimulierenAsync()
    {
        if (AusgewaehlteInstanz is null) return;
        int turnierInstanzId = AusgewaehlteInstanz.Id;
        Fehlermeldung = null;
        StatusMeldung = null;
        IstLadend     = true;
        try
        {
            var lauf = await _simulationsService.TurnierSimulierenAsync(
                turnierInstanzId, AusgewaehlterModus.Wert, null);

            StatusMeldung = $"Turnier simuliert — Seed: {lauf.ZufallsSeed}.";
            await LadeSpiellisteAsync();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstLadend = false;
        }
    }

    // ── Ribbon: Einzelspiel simulieren (Turnier-Kontext) ─────────────────────

    private async Task EinzelspielSimulierenAsync()
    {
        if (AusgewaehlteSpiel is null) return;
        int spielId  = AusgewaehlteSpiel.Id;
        int? rundeId = AusgewaehlteRunde?.Id;
        Fehlermeldung = null;
        StatusMeldung = null;
        IstLadend     = true;
        try
        {
            await _simulationsService.SpielSimulierenAsync(spielId, AusgewaehlterModus.Wert, null);

            StatusMeldung = "Spiel simuliert.";

            if (rundeId.HasValue)
            {
                var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId.Value);
                Spiele.Clear();
                foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i))) Spiele.Add(MapSpielZeile(s, i));
                AusgewaehlteSpiel = Spiele.FirstOrDefault(s => s.Id == spielId);
            }
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstLadend = false;
        }
    }

    // ── Panel: Runde simulieren ───────────────────────────────────────────────

    private async Task RundeSimulierenAsync()
    {
        if (AusgewaehlteRunde is null) return;
        int rundeId   = AusgewaehlteRunde.Id;
        Fehlermeldung = null;
        StatusMeldung = null;
        IstLadend     = true;
        try
        {
            int anzahl = await _simulationsService.RundeSimulierenAsync(
                rundeId, AusgewaehlterModus.Wert, null);

            StatusMeldung = anzahl == 0
                ? "Keine offenen Spiele in dieser Runde."
                : $"{anzahl} Spiel(e) simuliert.";

            await LadeSpiellisteAsync();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstLadend = false;
        }
    }

    // ── Einzelspiel-Modus: Freie Simulation ──────────────────────────────────

    private async Task EinzelspielFreiSimulierenAsync()
    {
        if (EinzelspielHeim is null || EinzelspielAuswaerts is null) return;

        var heim      = EinzelspielHeim;
        var auswaerts = EinzelspielAuswaerts;
        var verfahren = EinzelspielRegelwerk?.Entscheidungsverfahren.ToList()
                        ?? (IReadOnlyList<EntscheidungsVerfahren>)[];
        var modus = AusgewaehlterModus.Wert;

        Fehlermeldung = null;
        StatusMeldung = null;
        IstLadend     = true;
        EinzelspielErgebnisZeilen.Clear();
        EinzelspielEreignisse.Clear();
        OnPropertyChanged(nameof(HatEinzelspielErgebnis));
        OnPropertyChanged(nameof(HatEinzelspielEreignisse));

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

                EinzelspielErgebnisZeilen.Add($"Hinspiel:   {FormatErgebnisZeile(erg1, heim.Name, auswaerts.Name)}");
                EinzelspielErgebnisZeilen.Add($"Rückspiel:  {FormatErgebnisZeile(erg2, auswaerts.Name, heim.Name)}");
                EinzelspielErgebnisZeilen.Add($"Gesamt:     {heim.Name}  {heimGesamt}:{auswaertsGesamt}  {auswaerts.Name}");
                EinzelspielErgebnisZeilen.Add($"Weiter:     {weiter}");

                if (modus == SimulationsModus.AusfuehrlicheSimulation)
                {
                    foreach (var e in erg1.Ereignisse)
                        EinzelspielEreignisse.Add(MapEreignis(e, heim.Name, auswaerts.Name, "Hin"));
                    foreach (var e in erg2.Ereignisse)
                        EinzelspielEreignisse.Add(MapEreignis(e, auswaerts.Name, heim.Name, "Rück"));
                }
            }
            else
            {
                var erg = _simulationsService.EinzelspielSimulieren(heim, auswaerts, verfahren, modus);
                EinzelspielErgebnisZeilen.Add(FormatErgebnisZeile(erg, heim.Name, auswaerts.Name));

                if (modus == SimulationsModus.AusfuehrlicheSimulation)
                {
                    foreach (var e in erg.Ereignisse)
                        EinzelspielEreignisse.Add(MapEreignis(e, heim.Name, auswaerts.Name, null));
                }
            }

            StatusMeldung = "Einzelspiel simuliert.";
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstLadend = false;
            OnPropertyChanged(nameof(HatEinzelspielErgebnis));
            OnPropertyChanged(nameof(HatEinzelspielEreignisse));
        }
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    private void AktualisiereBefehle()
    {
        _neuBefehl.NotifyCanExecuteChanged();
        _speichernBefehl.NotifyCanExecuteChanged();
        RundeSimulierenBefehl.NotifyCanExecuteChanged();
        EinzelspielFreiBefehl.NotifyCanExecuteChanged();
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

    private static EinzelspielEreignisZeile MapEreignis(
        SimuliertesEreignis e, string heimName, string auswaertsName, string? spiel)
    {
        string minute = spiel is null ? $"{e.Minute}'" : $"{spiel} {e.Minute}'";
        string seite  = e.IstHeim ? heimName : auswaertsName;
        string art    = e.Art switch
        {
            EreignisArt.Tor        => "Tor",
            EreignisArt.GelbeKarte => "Gelbe Karte",
            EreignisArt.RoteKarte  => "Rote Karte",
            EreignisArt.GoldenGoal => "Golden Goal",
            EreignisArt.SilverGoal => "Silver Goal",
            _                      => e.Art.ToString()
        };
        return new EinzelspielEreignisZeile(minute, seite, art);
    }

    private SpielZeile MapSpielZeile(Spiel s, int positionInRunde = 0)
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
                EntscheidungArt.Elfmeterschiessen => $"i.E. ({e.HeimElfmetertore}:{e.AuswaertsElfmetertore})",
                EntscheidungArt.Auswaertstorregel => "A.T.",
                _                                 => ""
            };
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
            IstRueckspiel  = s.IstRueckspiel
        };
    }
}
