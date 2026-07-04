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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

public record SimulationsModusOption(string Anzeige, SimulationsModus Wert);
public record EinzelspielEreignisZeile(string Minute, string Seite, string Art);

public partial class SimulationsViewModel : ViewModelBase
{
    private readonly SimulationsService    _simulationsService;
    private readonly SpielErgebnisService  _spielService;
    private readonly TurnierInstanzService _instanzService;
    private readonly MannschaftService     _mannschaftService;
    private readonly RegelwerkService      _regelwerkService;

    private int _ladeUnterdrückt;

    // ── Editor-Modus ──────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstTurnierModus))]
    [NotifyPropertyChangedFor(nameof(IstEinzelspielModus))]
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
    private SpielZeile? _ausgewaehlteSpiel;

    public bool KannTurnierSpielSimulieren =>
        AusgewaehlteSpiel is { IstPlatzhalterSpiel: false } && !IstLadend;

    // ── Linke Spalte: Einzelspiel-Modus ──────────────────────────────────────

    [ObservableProperty] private ObservableCollection<Mannschaft> _alleMannschaften = [];
    [ObservableProperty] private ObservableCollection<Regelwerk>  _alleRegelwerke   = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EinzelspielKannSimuliert))]
    private Mannschaft? _einzelspielHeim;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EinzelspielKannSimuliert))]
    private Mannschaft? _einzelspielAuswaerts;

    [ObservableProperty] private Regelwerk? _einzelspielRegelwerk;
    [ObservableProperty] private bool       _einzelspielMitRueckspiel;

    public bool EinzelspielKannSimuliert =>
        EinzelspielHeim is not null &&
        EinzelspielAuswaerts is not null &&
        EinzelspielHeim != EinzelspielAuswaerts &&
        !IstLadend;

    // ── Simulationsoptionen ───────────────────────────────────────────────────

    [ObservableProperty] private SimulationsModusOption _ausgewaehlterModus = null!;

    public List<SimulationsModusOption> ModusOptionen { get; } =
    [
        new("Schnellsimulation",       SimulationsModus.SchnellSimulation),
        new("Ausführliche Simulation", SimulationsModus.AusfuehrlicheSimulation),
    ];

    // ── Protokoll (beide Modi) ────────────────────────────────────────────────

    [ObservableProperty] private string? _protokollSpielInfo;

    public ObservableCollection<string>                   ProtokollZeilen    { get; } = [];
    public ObservableCollection<EinzelspielEreignisZeile> ProtokollEreignisse { get; } = [];

    public bool HatProtokoll          => ProtokollZeilen.Count > 0;
    public bool HatProtokollEreignisse => ProtokollEreignisse.Count > 0;

    // ── Status ────────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KannTurnierSpielSimulieren))]
    [NotifyPropertyChangedFor(nameof(EinzelspielKannSimuliert))]
    private bool    _istLadend;

    [ObservableProperty] private string? _fehlermeldung;
    [ObservableProperty] private string? _statusMeldung;

    // ── Vorauswahl (gesetzt vom SpielplanViewModel vor AktivierenAsync) ───────

    public VorausgewaehlteSimulation? Vorauswahl { get; set; }

    // ── Befehle ───────────────────────────────────────────────────────────────

    public ICommand TurnierSpielSimulierenBefehl { get; }
    public ICommand EinzelspielFreiBefehl        { get; }

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

        TurnierSpielSimulierenBefehl = new AsyncRelayCommand(TurnierSpielSimulierenAsync, () => KannTurnierSpielSimulieren);
        EinzelspielFreiBefehl        = new AsyncRelayCommand(EinzelspielFreiSimulierenAsync, () => EinzelspielKannSimuliert);
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

        if (Vorauswahl is { } v)
        {
            Vorauswahl = null;
            await VorauswahlLadenUndSimulierenAsync(v);
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

    // ── Turnierspiel simulieren ───────────────────────────────────────────────

    private async Task TurnierSpielSimulierenAsync()
    {
        if (AusgewaehlteSpiel is null) return;
        int spielId  = AusgewaehlteSpiel.Id;
        int? rundeId = AusgewaehlteRunde?.Id;
        Fehlermeldung = null;
        StatusMeldung = null;
        IstLadend     = true;
        LeerProtokolll();
        try
        {
            await _simulationsService.SpielSimulierenAsync(spielId, AusgewaehlterModus.Wert);

            if (rundeId.HasValue)
            {
                var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId.Value);
                Spiele.Clear();
                foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i))) Spiele.Add(MapSpielZeile(s, i));
                AusgewaehlteSpiel = Spiele.FirstOrDefault(s => s.Id == spielId);
            }

            await LadeProtokollAsync(spielId);
            StatusMeldung = "Spiel simuliert.";
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
                    foreach (var e in erg1.Ereignisse)
                        ProtokollEreignisse.Add(MapEreignis(e, heim.Name, auswaerts.Name, "Hin"));
                    foreach (var e in erg2.Ereignisse)
                        ProtokollEreignisse.Add(MapEreignis(e, auswaerts.Name, heim.Name, "Rück"));
                }
            }
            else
            {
                var erg = _simulationsService.EinzelspielSimulieren(heim, auswaerts, verfahren, modus);
                ProtokollSpielInfo = $"{heim.Name} vs. {auswaerts.Name}";
                ProtokollZeilen.Add(FormatErgebnisZeile(erg, heim.Name, auswaerts.Name));

                if (modus == SimulationsModus.AusfuehrlicheSimulation)
                {
                    foreach (var e in erg.Ereignisse)
                        ProtokollEreignisse.Add(MapEreignis(e, heim.Name, auswaerts.Name, null));
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
            AktualisiereBefehle();
            OnPropertyChanged(nameof(HatProtokoll));
            OnPropertyChanged(nameof(HatProtokollEreignisse));
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

        AusgewaehlteSpiel  = spiel;
        AusgewaehlterModus = ModusOptionen.First(m => m.Wert == SimulationsModus.AusfuehrlicheSimulation);

        // In Turnierspiel-Modus wechseln
        _ladeUnterdrückt++;
        ModusIndex = 0;
        _ladeUnterdrückt--;

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
            string ergebnis = $"{heim}  {e.HeimToreGesamt}:{e.AuswaertsToreGesamt}  {auswaerts}";
            string detail   = e.EntscheidungArt switch
            {
                EntscheidungArt.Verlaengerung     => " (n.V.)",
                EntscheidungArt.GoldenGoal        => " (G.G.)",
                EntscheidungArt.SilverGoal        => " (S.G.)",
                EntscheidungArt.Elfmeterschiessen => $" (i.E. {e.HeimElfmetertore}:{e.AuswaertsElfmetertore})",
                EntscheidungArt.Auswaertstorregel => " (A.T.)",
                _                                 => ""
            };
            ProtokollZeilen.Add(ergebnis + detail);
        }

        foreach (var ereignis in spiel.Ereignisse.OrderBy(ev => ev.Minute))
        {
            string seite = ereignis.MannschaftId == spiel.HeimMannschaftId ? heim : auswaerts;
            string art   = ereignis.EreignisArt switch
            {
                EreignisArt.Tor        => "Tor",
                EreignisArt.GelbeKarte => "Gelbe Karte",
                EreignisArt.RoteKarte  => "Rote Karte",
                EreignisArt.GoldenGoal => "Golden Goal",
                EreignisArt.SilverGoal => "Silver Goal",
                _                      => ereignis.EreignisArt.ToString()
            };
            ProtokollEreignisse.Add(new EinzelspielEreignisZeile($"{ereignis.Minute}'", seite, art));
        }

        OnPropertyChanged(nameof(HatProtokoll));
        OnPropertyChanged(nameof(HatProtokollEreignisse));
    }

    private void LeerProtokolll()
    {
        ProtokollSpielInfo = null;
        ProtokollZeilen.Clear();
        ProtokollEreignisse.Clear();
        OnPropertyChanged(nameof(HatProtokoll));
        OnPropertyChanged(nameof(HatProtokollEreignisse));
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    private void AktualisiereBefehle()
    {
        ((AsyncRelayCommand)TurnierSpielSimulierenBefehl).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)EinzelspielFreiBefehl).NotifyCanExecuteChanged();
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
            IstRueckspiel  = s.IstRueckspiel,
            IstPlatzhalterSpiel = !s.HeimMannschaftId.HasValue || !s.AuswaertsMannschaftId.HasValue
        };
    }
}
