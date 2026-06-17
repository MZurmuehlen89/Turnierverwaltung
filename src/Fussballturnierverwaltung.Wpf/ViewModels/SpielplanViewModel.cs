using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Spiele.Eingaben;
using Fussballturnierverwaltung.Application.Spielplan;
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
    public string SpieltagAnzeige => Spieltag.HasValue ? $"ST {Spieltag}" : "-";
    public string HeimName { get; init; } = "";
    public string AuswaertsName { get; init; } = "";
    public SpielStatus Status { get; init; }
    public bool HatErgebnis { get; init; }
    public string ErgebnisText { get; init; } = "-:-";
    public string ErgebnisDetail { get; init; } = "";
    public bool IstRueckspiel { get; init; }
    public bool IstPlatzhalterSpiel { get; init; }
}

public record EntscheidungArtOption(string Anzeige, EntscheidungArt Wert);

// ── ViewModel ─────────────────────────────────────────────────────────────────

public partial class SpielplanViewModel : ViewModelBase, IRibbonAware
{
    private readonly SpielErgebnisService _spielService;
    private readonly TurnierInstanzService _instanzService;
    private readonly SpielplanService _spielplanService;
    private readonly AuslosungService _auslosungService;

    private int _populiertGerade;
    private static readonly HashSet<string> _formFelder =
    [
        nameof(HeimToreRegulaer),       nameof(AuswaertsToreRegulaer),
        nameof(AusgewaehlteEntscheidungArt),
        nameof(HeimToreVerlaengerung),  nameof(AuswaertsToreVerlaengerung),
        nameof(HeimElfmetertore),       nameof(AuswaertsElfmetertore)
    ];

    // ── linke Spalte ──────────────────────────────────────────────────────────

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
    [NotifyPropertyChangedFor(nameof(VerlaengerungAktiv))]
    [NotifyPropertyChangedFor(nameof(ElfmeterAktiv))]
    private EntscheidungArt _ausgewaehlteEntscheidungArt = EntscheidungArt.RegulaereSpielzeit;

    [ObservableProperty] private string _heimToreVerlaengerung = "";
    [ObservableProperty] private string _auswaertsToreVerlaengerung = "";
    [ObservableProperty] private string _heimElfmetertore = "";
    [ObservableProperty] private string _auswaertsElfmetertore = "";

    public bool VerlaengerungAktiv => AusgewaehlteEntscheidungArt is
        EntscheidungArt.Verlaengerung or EntscheidungArt.GoldenGoal or
        EntscheidungArt.SilverGoal    or EntscheidungArt.Elfmeterschiessen;

    public bool ElfmeterAktiv =>
        AusgewaehlteEntscheidungArt == EntscheidungArt.Elfmeterschiessen;

    public List<EntscheidungArtOption> EntscheidungArtOptionen { get; } =
    [
        new("Reguläre Spielzeit",   EntscheidungArt.RegulaereSpielzeit),
        new("Verlängerung",          EntscheidungArt.Verlaengerung),
        new("Golden Goal",           EntscheidungArt.GoldenGoal),
        new("Silver Goal",           EntscheidungArt.SilverGoal),
        new("Elfmeterschießen",      EntscheidungArt.Elfmeterschiessen),
        new("Auswärtstorregel",      EntscheidungArt.Auswaertstorregel),
        new("Losentscheid",          EntscheidungArt.Losentscheid),
        new("Münzwurf",              EntscheidungArt.Muenzwurf),
    ];

    // ── Platzhalter ───────────────────────────────────────────────────────────

    public bool HatPlatzhalterSpiele => Spiele.Any(s => s.IstPlatzhalterSpiel);
    public ICommand PlatzhalterBefehl => _platzhalterBefehl;
    private readonly AsyncRelayCommand _platzhalterBefehl;

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
        AuslosungService auslosungService)
    {
        _spielService     = spielService;
        _instanzService   = instanzService;
        _spielplanService = spielplanService;
        _auslosungService = auslosungService;

        _neuBefehl          = new AsyncRelayCommand(SpielplanNeuGenerierenAsync,  () => HatRunde);
        _speichernBefehl    = new AsyncRelayCommand(SpeichernAsync,               () => FormAktiv && IsDirty);
        _abbrechenBefehl    = new RelayCommand(FormZuruecksetzen,                 () => FormAktiv);
        _loeschenBefehl     = new AsyncRelayCommand(ErgebnisLoeschenAsync,        () => FormAktiv && FormHatErgebnis);
        _platzhalterBefehl  = new AsyncRelayCommand(PlatzhalterAktualisierenAsync, () => HatRunde && HatPlatzhalterSpiele);
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
                _ = BeiSpielAenderungAsync();
                break;
        }

        if (_populiertGerade == 0 && e.PropertyName is not null && _formFelder.Contains(e.PropertyName))
        {
            IsDirty = true;
            _speichernBefehl.NotifyCanExecuteChanged();
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
        AusgewaehlteSpiel = null;
        FormAktiv         = false;
        Fehlermeldung     = null;
        _neuBefehl.NotifyCanExecuteChanged();

        if (AusgewaehlteRunde is null) return;

        var liste = await _spielService.AlleNachRundeAsync(AusgewaehlteRunde.Id);
        foreach (var s in liste)
            Spiele.Add(MapSpielZeile(s));

        OnPropertyChanged(nameof(HatPlatzhalterSpiele));
        _platzhalterBefehl.NotifyCanExecuteChanged();
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

            await _spielplanService.LoeschenAsync(rundeId);
        }

        try
        {
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
                    await _spielplanService.GenerierenAusGruppenAsync(rundeId, gruppen);
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

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var s in neueSpiele) Spiele.Add(MapSpielZeile(s));
            OnPropertyChanged(nameof(HatPlatzhalterSpiele));
            _platzhalterBefehl.NotifyCanExecuteChanged();
            AusgewaehlteSpiel = null;
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

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var s in neueSpiele) Spiele.Add(MapSpielZeile(s));
            OnPropertyChanged(nameof(HatPlatzhalterSpiele));
            _platzhalterBefehl.NotifyCanExecuteChanged();
            AusgewaehlteSpiel = null;

            if (aufgeloest == 0)
                Fehlermeldung = "Keine Platzhalter konnten aufgelöst werden. " +
                                "Bitte sicherstellen, dass die Vorrunden-Ergebnisse vollständig eingetragen sind.";
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

            // Spielliste neu laden (ohne AusgewaehlteSpiel zu nullen via BeiRundeAenderung)
            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var s in neueSpiele) Spiele.Add(MapSpielZeile(s));

            // Gleiche Partie erneut selektieren
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

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var s in neueSpiele) Spiele.Add(MapSpielZeile(s));

            AusgewaehlteSpiel = Spiele.FirstOrDefault(s => s.Id == spielId);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
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
                EntscheidungArt.Elfmeterschiessen =>
                    $"i.E. ({e.HeimElfmetertore}:{e.AuswaertsElfmetertore})",
                EntscheidungArt.Auswaertstorregel => "A.T.",
                _                                 => ""
            };
        }

        return new SpielZeile
        {
            Id                 = s.Id,
            Spielnummer        = s.Spielnummer,
            Spieltag           = s.Spieltag,
            HeimName           = s.HeimMannschaft?.Name ?? s.HeimPlatzhalterName ?? "?",
            AuswaertsName      = s.AuswaertsMannschaft?.Name ?? s.AuswaertsPlatzhalterName ?? "?",
            Status             = s.Status,
            HatErgebnis        = s.Ergebnis is not null,
            ErgebnisText       = text,
            ErgebnisDetail     = detail,
            IstRueckspiel      = s.IstRueckspiel,
            IstPlatzhalterSpiel = !s.HeimMannschaftId.HasValue || !s.AuswaertsMannschaftId.HasValue
        };
    }

    private static int  ParseInt(string s) =>
        int.TryParse(s, out var v) ? v : 0;

    private static int? ParseIntNullable(string s) =>
        string.IsNullOrWhiteSpace(s) ? null : int.TryParse(s, out var v) ? v : null;
}
