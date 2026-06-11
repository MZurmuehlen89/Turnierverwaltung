using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

public record SimulationsModusOption(string Anzeige, SimulationsModus Wert);

public partial class SimulationsViewModel : ViewModelBase, IRibbonAware
{
    private readonly SimulationsService    _simulationsService;
    private readonly SpielErgebnisService  _spielService;
    private readonly TurnierInstanzService _instanzService;

    // ── Linke Spalte ──────────────────────────────────────────────────────────

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

    // ── Simulationsoptionen ───────────────────────────────────────────────────

    [ObservableProperty] private SimulationsModusOption _ausgewaehlterModus = null!;
    [ObservableProperty] private string _seedEingabe = "";

    public List<SimulationsModusOption> ModusOptionen { get; } =
    [
        new("Schnellsimulation",      SimulationsModus.SchnellSimulation),
        new("Ausführliche Simulation", SimulationsModus.AusfuehrlicheSimulation),
    ];

    // ── Status ────────────────────────────────────────────────────────────────

    [ObservableProperty] private bool    _istLadend;
    [ObservableProperty] private string? _fehlermeldung;
    [ObservableProperty] private string? _statusMeldung;

    // ── Zusatz-Befehl (Runde simulieren) ─────────────────────────────────────

    public AsyncRelayCommand RundeSimulierenBefehl { get; }

    // ── IRibbonAware ──────────────────────────────────────────────────────────

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
        TurnierInstanzService instanzService)
    {
        _simulationsService = simulationsService;
        _spielService       = spielService;
        _instanzService     = instanzService;

        AusgewaehlterModus = ModusOptionen[0];

        _neuBefehl       = new AsyncRelayCommand(TurnierSimulierenAsync,    () => AusgewaehlteInstanz != null && !IstLadend);
        _speichernBefehl = new AsyncRelayCommand(EinzelspielSimulierenAsync, () => AusgewaehlteSpiel  != null && !IstLadend);
        _abbrechenBefehl = new RelayCommand(() => { }, () => false);
        _loeschenBefehl  = new RelayCommand(() => { }, () => false);

        RundeSimulierenBefehl = new AsyncRelayCommand(RundeSimulierenAsync, () => AusgewaehlteRunde != null && !IstLadend);
    }

    // ── Initialisierung ───────────────────────────────────────────────────────

    public override async Task AktivierenAsync()
    {
        var liste = await _instanzService.AlleAbrufenAsync();
        Instanzen.Clear();
        foreach (var i in liste)
            Instanzen.Add(i);
    }

    // ── Property-Reaktionen ───────────────────────────────────────────────────

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        switch (e.PropertyName)
        {
            case nameof(AusgewaehlteInstanz):
                _ = BeiInstanzAenderungAsync();
                break;
            case nameof(AusgewaehlteRunde):
                _ = BeiRundeAenderungAsync();
                break;
            case nameof(AusgewaehlteSpiel):
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
        foreach (var s in liste)
            Spiele.Add(MapSpielZeile(s));
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
            var seed = ParseSeed();
            var lauf = await _simulationsService.TurnierSimulierenAsync(
                turnierInstanzId, AusgewaehlterModus.Wert, seed);

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

    // ── Ribbon: Einzelspiel simulieren ────────────────────────────────────────

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
            var seed = ParseSeed();
            var rng  = seed.HasValue ? new Random(seed.Value) : null;
            await _simulationsService.SpielSimulierenAsync(spielId, AusgewaehlterModus.Wert, rng);

            StatusMeldung = "Spiel simuliert.";

            if (rundeId.HasValue)
            {
                var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId.Value);
                Spiele.Clear();
                foreach (var s in neueSpiele) Spiele.Add(MapSpielZeile(s));
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
            var seed   = ParseSeed();
            int anzahl = await _simulationsService.RundeSimulierenAsync(
                rundeId, AusgewaehlterModus.Wert, seed);

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

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    private int? ParseSeed()
    {
        if (string.IsNullOrWhiteSpace(SeedEingabe)) return null;
        return int.TryParse(SeedEingabe, out var v) ? v : null;
    }

    private void AktualisiereBefehle()
    {
        _neuBefehl.NotifyCanExecuteChanged();
        _speichernBefehl.NotifyCanExecuteChanged();
        RundeSimulierenBefehl.NotifyCanExecuteChanged();
    }

    private static SpielZeile MapSpielZeile(Spiel s)
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
            HeimName       = s.HeimMannschaft?.Name ?? "?",
            AuswaertsName  = s.AuswaertsMannschaft?.Name ?? "?",
            Status         = s.Status,
            HatErgebnis    = s.Ergebnis is not null,
            ErgebnisText   = text,
            ErgebnisDetail = detail,
            IstRueckspiel  = s.IstRueckspiel
        };
    }
}
