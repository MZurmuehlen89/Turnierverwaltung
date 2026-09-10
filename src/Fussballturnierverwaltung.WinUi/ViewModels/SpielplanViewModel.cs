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
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

// ── Hilfsklasse — 1:1 aus Wpf/ViewModels/SpielplanViewModel.cs portiert ──────────────────────────

public class SpielZeile
{
    public int Id { get; init; }
    public int Spielnummer { get; init; }
    public int? Spieltag { get; init; }
    public string? KoLabel { get; init; }
    public string SpieltagAnzeige => KoLabel ?? (Spieltag.HasValue ? $"ST {Spieltag}" : "-");
    public string HeimName { get; init; } = "";
    public string AuswaertsName { get; init; } = "";
    public byte[]? HeimWappen { get; init; }
    public byte[]? AuswaertsWappen { get; init; }
    public SpielStatus Status { get; init; }
    public string StatusAnzeige => Status switch
    {
        SpielStatus.Geplant => "Geplant",
        SpielStatus.Laufend => "Läuft",
        SpielStatus.Abgeschlossen => "Fertig",
        SpielStatus.Verschoben => "Verschoben",
        SpielStatus.Abgebrochen => "Abgebrochen",
        _ => Status.ToString()
    };
    public bool HatErgebnis { get; init; }
    public string ErgebnisText { get; init; } = "-:-";
    public string ErgebnisDetail { get; init; } = "";
    public bool IstRueckspiel { get; init; }
    public bool IstPlatzhalterSpiel { get; init; }
    public bool HeimIstVorschau { get; init; }
    public bool AuswaertsIstVorschau { get; init; }
    public int? GruppeNummer { get; init; }
    public string GruppeAnzeige => GruppeNummer.HasValue ? $"Gr.{GruppeNummer}" : "";
}

/// <summary>
/// "Spielplan"-Bereich des #301-Composite (<see cref="SpielplanUndBaumViewModel"/>) — 1:1 aus
/// Wpf/ViewModels/SpielplanViewModel.cs portiert. Wichtigste WinUi-Anpassungen: <c>MessageBox.Show</c>
/// (3-Wege Speichern/Verwerfen/Abbrechen) → <see cref="IDialogService.UngespeicherteAenderungenAbfragenAsync"/>,
/// einfache Ja/Nein-Dialoge → <see cref="IDialogService.BestaetigenAsync"/>; Ribbon-Simulieren-Aktionen als
/// flache Einzel-Buttons statt SplitButtons/Unteraktionen (mit dem Nutzer abgestimmte Vereinfachung für #301,
/// siehe Plan) — "Runde/Gruppe/Spieltag zurücksetzen" stehen als eigene Buttons neben den Simulieren-Buttons.
/// </summary>
public partial class SpielplanViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly SpielErgebnisService _spielService;
    private readonly SpielplanService _spielplanService;
    private readonly AuslosungService _auslosungService;
    private readonly TurnierBaumService _baumService;
    private readonly RegelwerkService _regelwerkService;
    private readonly TabellenService _tabellenService;
    private readonly SimulationsService _simulationsService;
    private readonly TurnierInstanzService _instanzService;
    private readonly IDialogService _dialogService;

    private int _populiertGerade;
    private Dictionary<int, int> _teamGruppeMap = [];
    private readonly List<VerfahrenArt> _erlaubteVerfahren = [];
    private IReadOnlyDictionary<int, (string? Heim, string? Auswaerts)> _platzhalterVorschau =
        new Dictionary<int, (string?, string?)>();

    private int? _formularSpielId;
    private int? _formularRundeId;
    private int _spielWechselUnterdrueckt;
    private int _rundeWechselUnterdrueckt;

    private static readonly HashSet<string> _formFelder =
    [
        nameof(HeimToreRegulaer), nameof(AuswaertsToreRegulaer),
        nameof(HeimToreVerlaengerung), nameof(AuswaertsToreVerlaengerung),
        nameof(HeimElfmetertore), nameof(AuswaertsElfmetertore),
        nameof(HeimHalbzeitTore), nameof(AuswaertsHalbzeitTore)
    ];

    private static readonly HashSet<string> _scoreFelder =
    [
        nameof(HeimToreRegulaer), nameof(AuswaertsToreRegulaer),
        nameof(HeimToreVerlaengerung), nameof(AuswaertsToreVerlaengerung),
    ];

    // ── linke Spalte ──────────────────────────────────────────────────────────

    [ObservableProperty] private TurnierInstanz? _ausgewaehlteInstanz;

    [ObservableProperty] private ObservableCollection<TurnierRunde> _runden = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatRunde))]
    [NotifyPropertyChangedFor(nameof(IstGruppenphase))]
    [NotifyPropertyChangedFor(nameof(IstKoRunde))]
    [NotifyPropertyChangedFor(nameof(IstLigaRunde))]
    [NotifyPropertyChangedFor(nameof(IstLigaMitRueckspiel))]
    [NotifyPropertyChangedFor(nameof(ZeigeLigaSpieltagUmschalter))]
    private TurnierRunde? _ausgewaehlteRunde;

    public bool HatRunde => AusgewaehlteRunde is not null;
    public bool IstGruppenphase => AusgewaehlteRunde?.Rundenart == Rundenart.Gruppenphase;
    public bool IstLigaRunde => AusgewaehlteRunde?.Rundenart == Rundenart.Liga;

    private bool IstTabellenRunde => AusgewaehlteRunde?.Rundenart is
        Rundenart.Gruppenphase or Rundenart.Liga or Rundenart.SchweizerSystem or Rundenart.TopfLiga;

    public bool IstLigaMitRueckspiel => IstLigaRunde && (AusgewaehlteRunde?.MitRueckspiel ?? false);

    public ObservableCollection<GruppenTabelle> GruppenTabellen { get; } = [];

    // ── Gruppen-/Spieltag-Fokus ───────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstGefiltertAufGruppe))]
    [NotifyPropertyChangedFor(nameof(ZeigeFokusListe))]
    [NotifyPropertyChangedFor(nameof(FokusSpiele))]
    [NotifyPropertyChangedFor(nameof(SpieltagSimulierenMenuText))]
    [NotifyPropertyChangedFor(nameof(SpieltagZuruecksetzenMenuText))]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private GruppenTabelle? _ausgewaehlteGruppe;

    public bool IstGefiltertAufGruppe => AusgewaehlteGruppe is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigeFokusListe))]
    [NotifyPropertyChangedFor(nameof(FokusSpiele))]
    [NotifyPropertyChangedFor(nameof(SpieltagSimulierenMenuText))]
    [NotifyPropertyChangedFor(nameof(SpieltagZuruecksetzenMenuText))]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private bool _ligaProSpieltagAktiv;

    public bool ZeigeLigaSpieltagUmschalter => IstLigaRunde;
    public bool ZeigeFokusListe => AusgewaehlteGruppe is not null || LigaProSpieltagAktiv;

    public ObservableCollection<int> FokusSpieltage { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FokusSpiele))]
    [NotifyPropertyChangedFor(nameof(SpieltagSimulierenMenuText))]
    [NotifyPropertyChangedFor(nameof(SpieltagZuruecksetzenMenuText))]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private bool _fokusAlleAktiv = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FokusSpiele))]
    [NotifyPropertyChangedFor(nameof(FokusSpieltagnummer))]
    [NotifyPropertyChangedFor(nameof(SpieltagSimulierenMenuText))]
    [NotifyPropertyChangedFor(nameof(SpieltagZuruecksetzenMenuText))]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private int _fokusAktiverSpieltag;

    public string FokusSpieltagnummer => $"Spieltag {FokusAktiverSpieltag}";
    public bool HatMehrereSpieltage => FokusSpieltage.Count > 1;

    public string SpieltagSimulierenMenuText => EffektiverSpieltag is { } st ? $"Spieltag {st} simulieren" : "Spieltag simulieren";
    public string SpieltagZuruecksetzenMenuText => EffektiverSpieltag is { } st ? $"Spieltag {st} zurücksetzen" : "Spieltag zurücksetzen";
    public bool KannFokusZurueck => !FokusAlleAktiv && FokusSpieltage.Count > 0 && FokusAktiverSpieltag > FokusSpieltage[0];
    public bool KannFokusVor => FokusSpieltage.Count > 1 && (FokusAlleAktiv || FokusAktiverSpieltag < FokusSpieltage[^1]);

    public IReadOnlyList<SpielZeile> FokusSpiele => BerechneFokusSpiele();

    private readonly RelayCommand _zurueckZurUebersichtBefehl;
    private readonly RelayCommand _fokusVorigerSpieltag;
    private readonly RelayCommand _fokusNaechsterSpieltag;
    private readonly RelayCommand _fokusAlleSpieltageBefehl;
    private readonly RelayCommand _ligaGesamtAnsichtBefehl;
    private readonly RelayCommand _ligaProSpieltagAnsichtBefehl;

    public ICommand ZurueckZurUebersichtBefehl => _zurueckZurUebersichtBefehl;
    public ICommand FokusVorigerSpieltag => _fokusVorigerSpieltag;
    public ICommand FokusNaechsterSpieltag => _fokusNaechsterSpieltag;
    public ICommand FokusAlleSpieltageBefehl => _fokusAlleSpieltageBefehl;
    public ICommand LigaGesamtAnsichtBefehl => _ligaGesamtAnsichtBefehl;
    public ICommand LigaProSpieltagAnsichtBefehl => _ligaProSpieltagAnsichtBefehl;

    // ── Spielliste ────────────────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<SpielZeile> _spiele = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpieltagSimulierenMenuText))]
    [NotifyPropertyChangedFor(nameof(SpieltagZuruecksetzenMenuText))]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private SpielZeile? _ausgewaehlteSpiel;

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

    [ObservableProperty] private EntscheidungArt _ausgewaehlteEntscheidungArt = EntscheidungArt.RegulaereSpielzeit;

    [ObservableProperty] private string _heimToreVerlaengerung = "";
    [ObservableProperty] private string _auswaertsToreVerlaengerung = "";
    [ObservableProperty] private string _heimElfmetertore = "";
    [ObservableProperty] private string _auswaertsElfmetertore = "";
    [ObservableProperty] private string _heimHalbzeitTore = "";
    [ObservableProperty] private string _auswaertsHalbzeitTore = "";

    public bool IstKoRunde => AusgewaehlteRunde?.Rundenart is
        Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;

    private bool HatVerlaengerungsVerfahren =>
        _erlaubteVerfahren.Any(v => v is VerfahrenArt.VerlaengerungKlassisch or VerfahrenArt.GoldenGoal or VerfahrenArt.SilverGoal);

    private bool HatElfmeterVerfahren => _erlaubteVerfahren.Any(v => v == VerfahrenArt.Elfmeterschiessen);

    public bool VerlaengerungAktiv =>
        IstKoRunde && HatVerlaengerungsVerfahren &&
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
            int? ausV = ParseIntNullable(AuswaertsToreVerlaengerung);
            return heimV.HasValue && ausV.HasValue && heimV == ausV;
        }
    }

    // ── Platzhalter-Vorschau (Issue #208) ────────────────────────────────────
    // WinUi-Ribbon kennt kein IstAktivBinding-Toggle wie WPF — stattdessen ein einfacher
    // RelayCommand, dessen Header den aktuellen Zustand zeigt (siehe RibbonZusatzGruppen unten).

    public bool HatPlatzhalterSpiele => Spiele.Any(s => s.IstPlatzhalterSpiel);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private bool _vorschauAktiv;

    private readonly RelayCommand _vorschauUmschaltenBefehl;

    private async Task AktualisierePlatzhalterVorschauAsync()
    {
        _platzhalterVorschau = VorschauAktiv && AusgewaehlteRunde is not null
            ? await _spielplanService.PlatzhalterVorschauAsync(AusgewaehlteRunde.Id)
            : new Dictionary<int, (string?, string?)>();

        if (AusgewaehlteRunde is null) return;
        var spiele = await _spielService.AlleNachRundeAsync(AusgewaehlteRunde.Id);
        Spiele.Clear();
        foreach (var (s, i) in spiele.Select((x, i) => (x, i)))
            Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap, _platzhalterVorschau));
        OnPropertyChanged(nameof(FokusSpiele));
    }

    // ── Simulation ────────────────────────────────────────────────────────────

    [ObservableProperty] private bool _istSimulierend;

    private readonly AsyncRelayCommand _rundeSimulierenBefehl;
    private readonly AsyncRelayCommand _gruppeSimulierenBefehl;
    private readonly AsyncRelayCommand _spieltagSimulierenBefehl;
    private readonly AsyncRelayCommand _auswahlSimulierenBefehl;
    private readonly AsyncRelayCommand _hinrundeSimulierenBefehl;
    private readonly AsyncRelayCommand _rueckrundeSimulierenBefehl;

    public ICommand RundeSimulierenBefehl => _rundeSimulierenBefehl;
    public ICommand GruppeSimulierenBefehl => _gruppeSimulierenBefehl;
    public ICommand SpieltagSimulierenBefehl => _spieltagSimulierenBefehl;
    public ICommand AuswahlSimulierenBefehl => _auswahlSimulierenBefehl;
    public ICommand HinrundeSimulierenBefehl => _hinrundeSimulierenBefehl;
    public ICommand RueckrundeSimulierenBefehl => _rueckrundeSimulierenBefehl;

    // ── Ergebnis-Reset auf Ebene Runde/Gruppe/Spieltag (Issue #123) ─────────────

    private readonly AsyncRelayCommand _rundeZuruecksetzenBefehl;
    private readonly AsyncRelayCommand _gruppeZuruecksetzenBefehl;
    private readonly AsyncRelayCommand _spieltagZuruecksetzenBefehl;
    private readonly AsyncRelayCommand _hinrundeZuruecksetzenBefehl;
    private readonly AsyncRelayCommand _rueckrundeZuruecksetzenBefehl;

    public ICommand RundeZuruecksetzenBefehl => _rundeZuruecksetzenBefehl;
    public ICommand GruppeZuruecksetzenBefehl => _gruppeZuruecksetzenBefehl;
    public ICommand SpieltagZuruecksetzenBefehl => _spieltagZuruecksetzenBefehl;
    public ICommand HinrundeZuruecksetzenBefehl => _hinrundeZuruecksetzenBefehl;
    public ICommand RueckrundeZuruecksetzenBefehl => _rueckrundeZuruecksetzenBefehl;

    // ── Manuelle Multi-Selektion (Issue #124) ────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnzahlMehrfachSimulierbar))]
    [NotifyPropertyChangedFor(nameof(AuswahlSimulierenMenuText))]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private IReadOnlyList<SpielZeile> _mehrfachAusgewaehlteSpiele = [];

    public int AnzahlMehrfachSimulierbar =>
        MehrfachAusgewaehlteSpiele.Count(s => s.Status != SpielStatus.Abgeschlossen && !s.IstPlatzhalterSpiel);
    public string AuswahlSimulierenMenuText => $"Auswahl ({AnzahlMehrfachSimulierbar}) simulieren";

    public void AktualisiereMehrfachAuswahl(IEnumerable<SpielZeile> ausgewaehlt)
    {
        MehrfachAusgewaehlteSpiele = ausgewaehlt.ToList();
        _loeschenBefehl.NotifyCanExecuteChanged();
        _auswahlSimulierenBefehl.NotifyCanExecuteChanged();
    }

    // ── IRibbonAware ──────────────────────────────────────────────────────────

    public string RibbonTabTitel => "Spielplan";
    public ICommand NeuBefehl => _neuBefehl;
    public ICommand SpeichernBefehl => _speichernBefehl;
    public ICommand AbbrechenBefehl => _abbrechenBefehl;
    public ICommand LoeschenBefehl => _loeschenBefehl;

    /// <summary>#302: Sprung in den Simulation-Tab derselben, bereits offenen Turnierdurchführung-Seite
    /// (abgefangen von <see cref="TurnierdurchfuehrungViewModel"/>, Precedent WPF-Original
    /// <c>TurnierBereichViewModel</c>/<c>TurnierDurchfuehrungViewModel</c>). "Ausführlich simulieren"
    /// startet dort eine neue Simulation (ggf. mit Überschreiben-Rückfrage), "Spielverlauf ansehen" zeigt
    /// nur das bereits vorhandene Protokoll (Issue #240, <see cref="VorausgewaehlteSimulation.NurAnzeigen"/>).</summary>
    public event EventHandler<VorausgewaehlteSimulation>? AusfuehrlicheSimulationAngefordert;

    private void AusfuehrlichSimulieren()
    {
        if (AusgewaehlteSpiel is null || AusgewaehlteRunde is null || AusgewaehlteInstanz is null) return;
        AusfuehrlicheSimulationAngefordert?.Invoke(this, new VorausgewaehlteSimulation(
            AusgewaehlteInstanz.Id, AusgewaehlteRunde.Id, AusgewaehlteSpiel.Id));
    }

    private void SpielverlaufAnsehen()
    {
        if (AusgewaehlteSpiel is null || AusgewaehlteRunde is null || AusgewaehlteInstanz is null) return;
        AusfuehrlicheSimulationAngefordert?.Invoke(this, new VorausgewaehlteSimulation(
            AusgewaehlteInstanz.Id, AusgewaehlteRunde.Id, AusgewaehlteSpiel.Id, NurAnzeigen: true));
    }

    private bool CanAusfuehrlichSimulieren() => AusgewaehlteSpiel is { IstPlatzhalterSpiel: false };
    private bool CanSpielverlaufAnsehen() => AusgewaehlteSpiel is { IstPlatzhalterSpiel: false, HatErgebnis: true };

    // Simulieren/Zurücksetzen als je EIN Flyout-Button statt elf Einzelbuttons (Nutzerfund #301-Test:
    // Ribbon-Leiste selbst mit Overflow unübersichtlich überfüllt) — die einzelnen Ebenen (Runde/Hinrunde/
    // Rückrunde/Gruppe/Spieltag/Auswahl) bleiben als Flyout-Menüpunkte erreichbar, nur die Andockstelle im
    // Ribbon selbst schrumpft von 11 auf 2 Buttons. Platzhalter-Vorschau bleibt eigener Button (Toggle,
    // kein Aktions-Flyout).
    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen =>
    [
        new RibbonGruppe
        {
            Header = "Spielplan",
            Aktionen =
            [
                new RibbonAktion
                {
                    Header = "Simulieren",
                    IconGlyph = "",
                    Unteraktionen =
                    [
                        new RibbonUnteraktion("Runde", RundeSimulierenBefehl),
                        new RibbonUnteraktion("Hinrunde", HinrundeSimulierenBefehl),
                        new RibbonUnteraktion("Rückrunde", RueckrundeSimulierenBefehl),
                        new RibbonUnteraktion("Gruppe", GruppeSimulierenBefehl),
                        new RibbonUnteraktion(SpieltagSimulierenMenuText, SpieltagSimulierenBefehl),
                        new RibbonUnteraktion(AuswahlSimulierenMenuText, AuswahlSimulierenBefehl),
                    ],
                },
                new RibbonAktion
                {
                    Header = "Zurücksetzen",
                    IconGlyph = "",
                    Unteraktionen =
                    [
                        new RibbonUnteraktion("Runde", RundeZuruecksetzenBefehl),
                        new RibbonUnteraktion("Hinrunde", HinrundeZuruecksetzenBefehl),
                        new RibbonUnteraktion("Rückrunde", RueckrundeZuruecksetzenBefehl),
                        new RibbonUnteraktion("Gruppe", GruppeZuruecksetzenBefehl),
                        new RibbonUnteraktion(SpieltagZuruecksetzenMenuText, SpieltagZuruecksetzenBefehl),
                    ],
                },
                new RibbonAktion
                {
                    Header = "Ausführlich simulieren",
                    Befehl = _ausfuehrlichSimulierenBefehl,
                },
                new RibbonAktion
                {
                    Header = "Spielverlauf ansehen",
                    Befehl = _spielverlaufAnsehenBefehl,
                },
                new RibbonAktion
                {
                    Header = VorschauAktiv ? "Platzhalter-Vorschau: Ein" : "Platzhalter-Vorschau: Aus",
                    Befehl = _vorschauUmschaltenBefehl,
                    IconGlyph = "",
                },
            ],
        },
    ];

    private readonly AsyncRelayCommand _neuBefehl;
    private readonly AsyncRelayCommand _speichernBefehl;
    private readonly RelayCommand _abbrechenBefehl;
    private readonly AsyncRelayCommand _loeschenBefehl;
    private readonly RelayCommand _ausfuehrlichSimulierenBefehl;
    private readonly RelayCommand _spielverlaufAnsehenBefehl;

    // ── Konstruktor ───────────────────────────────────────────────────────────

    public SpielplanViewModel(
        SpielErgebnisService spielService,
        SpielplanService spielplanService,
        AuslosungService auslosungService,
        TurnierBaumService baumService,
        RegelwerkService regelwerkService,
        TabellenService tabellenService,
        SimulationsService simulationsService,
        TurnierInstanzService instanzService,
        IDialogService dialogService)
    {
        _spielService = spielService;
        _spielplanService = spielplanService;
        _auslosungService = auslosungService;
        _baumService = baumService;
        _regelwerkService = regelwerkService;
        _tabellenService = tabellenService;
        _simulationsService = simulationsService;
        _instanzService = instanzService;
        _dialogService = dialogService;

        _neuBefehl = new AsyncRelayCommand(SpielplanNeuGenerierenAsync, () => HatRunde);
        _speichernBefehl = new AsyncRelayCommand(SpeichernAsync, () => FormAktiv && IsDirty);
        _abbrechenBefehl = new RelayCommand(FormZuruecksetzen, () => FormAktiv && IsDirty);
        _loeschenBefehl = new AsyncRelayCommand(ErgebnisLoeschenAsync, CanErgebnisLoeschen);

        _zurueckZurUebersichtBefehl = new RelayCommand(() => AusgewaehlteGruppe = null, () => AusgewaehlteGruppe is not null);
        _fokusVorigerSpieltag = new RelayCommand(FokusEinSpieltag_Vorig, () => KannFokusZurueck);
        _fokusNaechsterSpieltag = new RelayCommand(FokusEinSpieltag_Vor, () => KannFokusVor);
        _fokusAlleSpieltageBefehl = new RelayCommand(() => FokusAlleAktiv = true, () => !FokusAlleAktiv);
        _ligaGesamtAnsichtBefehl = new RelayCommand(() => LigaProSpieltagAktiv = false);
        _ligaProSpieltagAnsichtBefehl = new RelayCommand(() => LigaProSpieltagAktiv = true);

        _rundeSimulierenBefehl = new AsyncRelayCommand(RundeSimulierenAsync, CanRundeSimulieren);
        _gruppeSimulierenBefehl = new AsyncRelayCommand(GruppeSimulierenAsync, CanGruppeSimulieren);
        _spieltagSimulierenBefehl = new AsyncRelayCommand(SpieltagSimulierenAsync, CanSpieltagSimulieren);
        _auswahlSimulierenBefehl = new AsyncRelayCommand(AuswahlSimulierenAsync, CanAuswahlSimulieren);
        _hinrundeSimulierenBefehl = new AsyncRelayCommand(HinrundeSimulierenAsync, CanHinrundeSimulieren);
        _rueckrundeSimulierenBefehl = new AsyncRelayCommand(RueckrundeSimulierenAsync, CanRueckrundeSimulieren);

        _rundeZuruecksetzenBefehl = new AsyncRelayCommand(RundeZuruecksetzenAsync, CanRundeZuruecksetzen);
        _gruppeZuruecksetzenBefehl = new AsyncRelayCommand(GruppeZuruecksetzenAsync, CanGruppeZuruecksetzen);
        _spieltagZuruecksetzenBefehl = new AsyncRelayCommand(SpieltagZuruecksetzenAsync, CanSpieltagZuruecksetzen);
        _hinrundeZuruecksetzenBefehl = new AsyncRelayCommand(HinrundeZuruecksetzenAsync, CanHinrundeZuruecksetzen);
        _rueckrundeZuruecksetzenBefehl = new AsyncRelayCommand(RueckrundeZuruecksetzenAsync, CanRueckrundeZuruecksetzen);

        _vorschauUmschaltenBefehl = new RelayCommand(() =>
        {
            VorschauAktiv = !VorschauAktiv;
            _ = AktualisierePlatzhalterVorschauAsync();
        });

        _ausfuehrlichSimulierenBefehl = new RelayCommand(AusfuehrlichSimulieren, CanAusfuehrlichSimulieren);
        _spielverlaufAnsehenBefehl = new RelayCommand(SpielverlaufAnsehen, CanSpielverlaufAnsehen);
    }

    // ── Initialisierung ───────────────────────────────────────────────────────

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
            Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap, _platzhalterVorschau));
        OnPropertyChanged(nameof(FokusSpiele));
        OnPropertyChanged(nameof(HatPlatzhalterSpiele));

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

    public async Task InstanzSetzenAsync(TurnierInstanz? instanz)
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
            case nameof(AusgewaehlteRunde):
                _ = RundeWechselAsync();
                break;
            case nameof(AusgewaehlteSpiel):
                _ = SpielWechselAsync();
                break;
            case nameof(AusgewaehlteGruppe):
                BeiGruppeFokusAenderung();
                break;
            case nameof(LigaProSpieltagAktiv):
                BeiLigaProSpieltagAenderung();
                break;
            case nameof(FokusAlleAktiv):
            case nameof(FokusAktiverSpieltag):
                _fokusVorigerSpieltag.NotifyCanExecuteChanged();
                _fokusNaechsterSpieltag.NotifyCanExecuteChanged();
                _fokusAlleSpieltageBefehl.NotifyCanExecuteChanged();
                _spieltagSimulierenBefehl.NotifyCanExecuteChanged();
                _spieltagZuruecksetzenBefehl.NotifyCanExecuteChanged();
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
        FormAktiv = false;

        int? vorherigeRundeId = AusgewaehlteRunde?.Id;

        // Runden.Clear() erst NACH der Abfrage (nicht davor): die ComboBox-Bindung auf AusgewaehlteRunde
        // nullt diese Property automatisch mit, sobald ihre ItemsSource geleert wird. Bricht die Abfrage
        // danach ab (z.B. #317, "second operation"-Exception + Retry im Composite), lief bislang genau
        // dieser destruktive Clear() bereits — der anschließende Retry startet BeiInstanzAenderungAsync
        // komplett neu und liest vorherigeRundeId dann als bereits leer (Fund aus #308-Nachtest).
        var instanz = AusgewaehlteInstanz is null
            ? null
            : await _instanzService.FindenNachIdMitRundenAsync(AusgewaehlteInstanz.Id);

        if (instanz is null)
        {
            Runden.Clear();
            await RundeUndSpielZuruecksetzenAsync();
            return;
        }

        Runden.Clear();
        foreach (var r in instanz.Runden.OrderBy(r => r.Reihenfolge))
            Runden.Add(r);

        // Reiner Tab-Wechsel ohne echten Instanzwechsel (#307): dieselbe Runde existiert nach dem
        // Neuladen noch — Auswahl anhand der Id wiederherstellen statt sie zu verlieren. Bei echtem
        // Instanzwechsel findet sich die alte Runden-Id nicht mehr (Ids sind global eindeutig), dann
        // greift der volle Reset wie zuvor.
        var wiederhergestellteRunde = vorherigeRundeId.HasValue
            ? Runden.FirstOrDefault(r => r.Id == vorherigeRundeId)
            : null;

        if (wiederhergestellteRunde is null)
        {
            await RundeUndSpielZuruecksetzenAsync();
            return;
        }

        _rundeWechselUnterdrueckt++;
        try
        {
            AusgewaehlteRunde = wiederhergestellteRunde;
        }
        finally
        {
            _rundeWechselUnterdrueckt--;
        }

        // Spiele/Tabellen frisch laden (externe Änderungen aus anderen Tabs sichtbar machen), aber
        // Spiel-Auswahl anhand ihrer Id wiederherstellen statt hart zurückzusetzen (Precedent:
        // SpiellisteAktualisierenAsync, bereits genau dieses Muster für AktivierenAsync).
        await SpiellisteAktualisierenAsync();
    }

    private async Task RundeUndSpielZuruecksetzenAsync()
    {
        Spiele.Clear();
        AusgewaehlteSpiel = null;

        _rundeWechselUnterdrueckt++;
        try
        {
            AusgewaehlteRunde = null;
        }
        finally
        {
            _rundeWechselUnterdrueckt--;
        }

        await BeiRundeAenderungAsync();
    }

    private async Task RundeWechselAsync()
    {
        if (_rundeWechselUnterdrueckt > 0) return;

        var neueRundeId = AusgewaehlteRunde?.Id;

        if (IsDirty && _formularRundeId is { } vorherigeRundeId && vorherigeRundeId != neueRundeId)
        {
            var vorherigeSpielId = _formularSpielId;

            var antwort = await _dialogService.UngespeicherteAenderungenAbfragenAsync();

            if (antwort == UngespeichertAntwort.Abbrechen)
            {
                _rundeWechselUnterdrueckt++;
                try
                {
                    AusgewaehlteRunde = Runden.FirstOrDefault(r => r.Id == vorherigeRundeId);
                }
                finally
                {
                    _rundeWechselUnterdrueckt--;
                }
                return;
            }

            if (antwort == UngespeichertAntwort.Speichern && vorherigeSpielId is { } spielId)
            {
                var vorherigeRunde = Runden.FirstOrDefault(r => r.Id == vorherigeRundeId);
                bool gespeichert = vorherigeRunde is not null &&
                    await ErgebnisSpeichernBeiRundenwechselAsync(spielId, vorherigeRunde);

                if (!gespeichert)
                {
                    _rundeWechselUnterdrueckt++;
                    try
                    {
                        AusgewaehlteRunde = vorherigeRunde;
                    }
                    finally
                    {
                        _rundeWechselUnterdrueckt--;
                    }
                    return;
                }
            }

            IsDirty = false;
        }

        await BeiRundeAenderungAsync();
    }

    private async Task<bool> ErgebnisSpeichernBeiRundenwechselAsync(int spielId, TurnierRunde runde)
    {
        Fehlermeldung = null;
        try
        {
            await _spielService.ErgebnisErfassenAsync(spielId, BaueEingabe());
            await _spielplanService.FinaleAutomatischGenerierenWennBereitAsync(runde.TurnierInstanzId);
            return true;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
            return false;
        }
    }

    private async Task BeiRundeAenderungAsync()
    {
        Spiele.Clear();
        AusgewaehlteSpiel = null;
        FormAktiv = false;
        Fehlermeldung = null;
        _erlaubteVerfahren.Clear();
        AusgewaehlteGruppe = null;
        LigaProSpieltagAktiv = false;
        GruppenTabellen.Clear();
        _teamGruppeMap = [];
        _platzhalterVorschau = new Dictionary<int, (string?, string?)>();
        AktualisiereBefehle();

        if (AusgewaehlteRunde is null)
        {
            if (RundeGeaendertAsync is not null) await RundeGeaendertAsync(null);
            return;
        }

        await TabellenNeuLadenAsync(AusgewaehlteRunde.Id);

        var liste = await _spielService.AlleNachRundeAsync(AusgewaehlteRunde.Id);
        foreach (var (s, i) in liste.Select((x, i) => (x, i)))
            Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap, _platzhalterVorschau));

        OnPropertyChanged(nameof(HatPlatzhalterSpiele));
        if (VorschauAktiv) await AktualisierePlatzhalterVorschauAsync();

        var regelwerkId = AusgewaehlteRunde.RegelwerkId ?? AusgewaehlteInstanz?.StandardRegelwerkId;
        var regelwerk = regelwerkId is { } rid ? await _regelwerkService.FindenNachIdAsync(rid) : null;

        foreach (var v in regelwerk?.Entscheidungsverfahren
                     .OrderBy(v => v.Reihenfolge)
                     .Select(v => v.VerfahrenArt) ?? [])
            _erlaubteVerfahren.Add(v);

        AktualisiereBefehle();
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

        if (AusgewaehlteGruppe is { } aktuelleGruppe)
            AusgewaehlteGruppe = GruppenTabellen.FirstOrDefault(g => g.GruppenNummer == aktuelleGruppe.GruppenNummer);
    }

    private async Task TabellenNeuLadenAsync(int rundeId)
    {
        if (AusgewaehlteRunde?.Rundenart == Rundenart.Gruppenphase)
            await GruppenTabellenLadenAsync(rundeId);

        if (SpielstandGeaendertAsync is not null)
            await SpielstandGeaendertAsync();

        if (RundeGeaendertAsync is not null)
            await RundeGeaendertAsync(AusgewaehlteRunde);
    }

    public Func<Task>? SpielstandGeaendertAsync { get; set; }
    public Func<TurnierRunde?, Task>? RundeGeaendertAsync { get; set; }
    public Action? SpieltagNavigiert { get; set; }

    private async Task SpielWechselAsync()
    {
        if (_spielWechselUnterdrueckt > 0) return;

        var neuesSpielId = AusgewaehlteSpiel?.Id;

        if (IsDirty && _formularSpielId is { } vorherigeSpielId &&
            _formularRundeId == AusgewaehlteRunde?.Id && vorherigeSpielId != neuesSpielId)
        {
            var vorherigeRundeId = _formularRundeId!.Value;

            var antwort = await _dialogService.UngespeicherteAenderungenAbfragenAsync();

            _spielWechselUnterdrueckt++;
            try
            {
                if (antwort == UngespeichertAntwort.Abbrechen)
                {
                    AusgewaehlteSpiel = Spiele.FirstOrDefault(s => s.Id == vorherigeSpielId);
                    return;
                }

                if (antwort == UngespeichertAntwort.Speichern)
                    await ErgebnisSpeichernAsync(vorherigeSpielId, vorherigeRundeId);

                AusgewaehlteSpiel = neuesSpielId.HasValue ? Spiele.FirstOrDefault(s => s.Id == neuesSpielId) : null;
            }
            finally
            {
                _spielWechselUnterdrueckt--;
            }

            await BeiSpielAenderungAsync();
            return;
        }

        await BeiSpielAenderungAsync();
    }

    private async Task BeiSpielAenderungAsync()
    {
        FormAktiv = false;
        Fehlermeldung = null;

        AktualisiereBefehle();

        if (AusgewaehlteSpiel is null) return;

        var spiel = await _spielService.FindenNachIdAsync(AusgewaehlteSpiel.Id);
        if (spiel is null) return;

        FormularAusFuellenMit(spiel);
        FormAktiv = true;
        AktualisiereBefehle();
    }

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
            FormHeimName = spiel.HeimMannschaft?.Name ?? spiel.HeimPlatzhalterName ?? "";
            FormAuswaertsName = spiel.AuswaertsMannschaft?.Name ?? spiel.AuswaertsPlatzhalterName ?? "";
            FormSpielInfo = spiel.Spieltag.HasValue
                ? $"Spieltag {spiel.Spieltag} · Spiel #{spiel.Spielnummer}"
                : $"Spiel #{spiel.Spielnummer}";
            FormHatErgebnis = spiel.Ergebnis is not null;

            if (spiel.Ergebnis is { } e)
            {
                HeimToreRegulaer = e.HeimToreRegulaer.ToString();
                AuswaertsToreRegulaer = e.AuswaertsToreRegulaer.ToString();
                AusgewaehlteEntscheidungArt = e.EntscheidungArt;
                HeimToreVerlaengerung = e.HeimToreVerlaengerung.HasValue
                    ? (e.HeimToreRegulaer + e.HeimToreVerlaengerung.Value).ToString() : "";
                AuswaertsToreVerlaengerung = e.AuswaertsToreVerlaengerung.HasValue
                    ? (e.AuswaertsToreRegulaer + e.AuswaertsToreVerlaengerung.Value).ToString() : "";
                HeimElfmetertore = e.HeimElfmetertore?.ToString() ?? "";
                AuswaertsElfmetertore = e.AuswaertsElfmetertore?.ToString() ?? "";
                HeimHalbzeitTore = e.HalbzeitHeimTore?.ToString() ?? "";
                AuswaertsHalbzeitTore = e.HalbzeitAuswaertsTore?.ToString() ?? "";
            }
            else
            {
                HeimToreRegulaer = "";
                AuswaertsToreRegulaer = "";
                AusgewaehlteEntscheidungArt = EntscheidungArt.RegulaereSpielzeit;
                HeimToreVerlaengerung = "";
                AuswaertsToreVerlaengerung = "";
                HeimElfmetertore = "";
                AuswaertsElfmetertore = "";
                HeimHalbzeitTore = "";
                AuswaertsHalbzeitTore = "";
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

        var rundeId = AusgewaehlteRunde.Id;
        var instanzId = AusgewaehlteInstanz.Id;
        Fehlermeldung = null;

        if (Spiele.Count > 0)
        {
            var bestaetigt = await _dialogService.BestaetigenAsync(
                "Spielplan neu generieren",
                $"Die Runde '{AusgewaehlteRunde.Name}' hat bereits {Spiele.Count} Spiel(e). " +
                "Soll der Spielplan gelöscht und neu generiert werden?");
            if (!bestaetigt) return;
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
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i))) Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap, _platzhalterVorschau));
            OnPropertyChanged(nameof(HatPlatzhalterSpiele));
            OnPropertyChanged(nameof(FokusSpiele));
            AusgewaehlteSpiel = null;
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
            await _spielplanService.FinaleAutomatischGenerierenWennBereitAsync(AusgewaehlteRunde.TurnierInstanzId);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            _spielWechselUnterdrueckt++;
            try
            {
                Spiele.Clear();
                foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i))) Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap, _platzhalterVorschau));
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

    private void FormZuruecksetzen() => _ = BeiSpielAenderungAsync();

    private Task ErgebnisLoeschenAsync() =>
        MehrfachAusgewaehlteSpiele.Count > 1 ? ErgebnisMehrfachLoeschenAsync() : ErgebnisEinzelLoeschenAsync();

    private async Task ErgebnisEinzelLoeschenAsync()
    {
        if (AusgewaehlteSpiel is null || !FormHatErgebnis || AusgewaehlteRunde is null) return;

        var spielId = AusgewaehlteSpiel.Id;
        var rundeId = AusgewaehlteRunde.Id;
        var spielnummer = AusgewaehlteSpiel.Spielnummer;
        var kaskadenHinweis = await KaskadenHinweisAsync([spielId]);

        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Ergebnis löschen", $"Ergebnis von Spiel #{spielnummer} wirklich löschen?{kaskadenHinweis}");
        if (!bestaetigt) return;

        Fehlermeldung = null;
        try
        {
            await _spielService.ErgebnisLoeschenAsync(spielId);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i))) Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap, _platzhalterVorschau));
            OnPropertyChanged(nameof(FokusSpiele));

            AusgewaehlteSpiel = Spiele.FirstOrDefault(s => s.Id == spielId);
            await TurnierBaumAktualisierenAsync(AusgewaehlteRunde.TurnierInstanzId);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    private async Task<string> KaskadenHinweisAsync(IReadOnlyList<int> spielIds)
    {
        if (AusgewaehlteInstanz is null || spielIds.Count == 0) return "";
        var vorschau = await _spielplanService.PlatzhalterKaskadePlanenAsync(AusgewaehlteInstanz.Id, spielIds.ToHashSet());
        return vorschau.HatAuswirkungen
            ? $"\n\nZusätzlich werden dadurch {vorschau.GesamtBetroffeneSpiele} Ergebnis(se) in Folgerunden " +
              "zurückgesetzt, deren Paarungen davon abhängen."
            : "";
    }

    /// <summary>Generiert/aktualisiert den Turnierbaum nach jeder Ergebnisänderung — anders als das WPF-
    /// Original (das hier nur einen bereits existierenden Baum aktualisierte, sodass der erste Baum
    /// weiterhin manuell über "Baum generieren" angestoßen werden musste) ruft dieser WinUi-Port
    /// <see cref="TurnierBaumService.GenerierenAsync"/> IMMER auf, auch für den allerersten Baum
    /// (Nutzerwunsch: manuelles Generieren soll nicht mehr nötig sein). Für Turniere ganz ohne K.o.-Runden
    /// wirft GenerierenAsync vermutlich, was der bestehende catch-Block wie bisher schluckt.</summary>
    private async Task TurnierBaumAktualisierenAsync(int turnierInstanzId)
    {
        try
        {
            await _baumService.GenerierenAsync(turnierInstanzId);
        }
        catch { /* TurnierBaum-Rebuild ist optional — nie Fehler an den Benutzer weitergeben */ }
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    private ErgebnisErfassenEingabe BaueEingabe()
    {
        int heimRegulaer = ParseInt(HeimToreRegulaer);
        int auswaertsRegulaer = ParseInt(AuswaertsToreRegulaer);

        int? heimVerlaengerungDelta = ParseIntNullable(HeimToreVerlaengerung) is { } hv ? hv - heimRegulaer : null;
        int? auswaertsVerlaengerungDelta = ParseIntNullable(AuswaertsToreVerlaengerung) is { } av ? av - auswaertsRegulaer : null;

        return new(
            heimRegulaer,
            auswaertsRegulaer,
            AusgewaehlteEntscheidungArt,
            heimVerlaengerungDelta,
            auswaertsVerlaengerungDelta,
            ParseIntNullable(HeimElfmetertore),
            ParseIntNullable(AuswaertsElfmetertore),
            ParseIntNullable(HeimHalbzeitTore),
            ParseIntNullable(AuswaertsHalbzeitTore));
    }

    private void AktualisiereBefehle()
    {
        _neuBefehl.NotifyCanExecuteChanged();
        _speichernBefehl.NotifyCanExecuteChanged();
        _abbrechenBefehl.NotifyCanExecuteChanged();
        _loeschenBefehl.NotifyCanExecuteChanged();
        _fokusVorigerSpieltag.NotifyCanExecuteChanged();
        _fokusNaechsterSpieltag.NotifyCanExecuteChanged();
        _fokusAlleSpieltageBefehl.NotifyCanExecuteChanged();
        _rundeSimulierenBefehl.NotifyCanExecuteChanged();
        _gruppeSimulierenBefehl.NotifyCanExecuteChanged();
        _spieltagSimulierenBefehl.NotifyCanExecuteChanged();
        _auswahlSimulierenBefehl.NotifyCanExecuteChanged();
        _rundeZuruecksetzenBefehl.NotifyCanExecuteChanged();
        _gruppeZuruecksetzenBefehl.NotifyCanExecuteChanged();
        _spieltagZuruecksetzenBefehl.NotifyCanExecuteChanged();
        _hinrundeSimulierenBefehl.NotifyCanExecuteChanged();
        _rueckrundeSimulierenBefehl.NotifyCanExecuteChanged();
        _hinrundeZuruecksetzenBefehl.NotifyCanExecuteChanged();
        _rueckrundeZuruecksetzenBefehl.NotifyCanExecuteChanged();
        _ausfuehrlichSimulierenBefehl.NotifyCanExecuteChanged();
        _spielverlaufAnsehenBefehl.NotifyCanExecuteChanged();
    }

    private static SpielZeile MapSpielZeile(
        Spiel s, TurnierRunde? runde, int positionInRunde,
        IReadOnlyDictionary<int, int>? teamGruppeMap = null,
        IReadOnlyDictionary<int, (string? Heim, string? Auswaerts)>? vorschau = null)
    {
        string text = "-:-";
        string detail = "";

        if (s.Ergebnis is { } e)
        {
            text = $"{e.HeimToreGesamt}:{e.AuswaertsToreGesamt}";
            detail = SpielErgebnisFormatierer.DetailText(
                e.EntscheidungArt,
                e.HeimToreRegulaer, e.AuswaertsToreRegulaer,
                e.HeimToreVerlaengerung, e.AuswaertsToreVerlaengerung,
                e.HeimElfmetertore, e.AuswaertsElfmetertore);

            if (detail.Length == 0)
                detail = SpielErgebnisFormatierer.HalbzeitKlammer(
                    e.EntscheidungArt, e.HeimToreRegulaer, e.AuswaertsToreRegulaer,
                    e.HalbzeitHeimTore, e.HalbzeitAuswaertsTore);
        }

        int? gruppeNummer = null;
        if (teamGruppeMap is not null && s.HeimMannschaftId.HasValue
            && teamGruppeMap.TryGetValue(s.HeimMannschaftId.Value, out var gr))
            gruppeNummer = gr;

        (string? Heim, string? Auswaerts) vor = default;
        vorschau?.TryGetValue(s.Id, out vor);
        string? heimVorschau = s.HeimMannschaftId is null ? vor.Heim : null;
        string? auswaertsVorschau = s.AuswaertsMannschaftId is null ? vor.Auswaerts : null;

        return new SpielZeile
        {
            Id = s.Id,
            Spielnummer = s.Spielnummer,
            Spieltag = s.Spieltag,
            KoLabel = KoPositionsLabel(s, runde, positionInRunde),
            HeimName = s.HeimMannschaft?.Name ?? (heimVorschau is not null ? $"({heimVorschau})" : null)
                       ?? s.HeimPlatzhalterName ?? "?",
            AuswaertsName = s.AuswaertsMannschaft?.Name ?? (auswaertsVorschau is not null ? $"({auswaertsVorschau})" : null)
                       ?? s.AuswaertsPlatzhalterName ?? "?",
            HeimWappen = TeilnehmerAnzeige.MannschaftsIcon(s.HeimMannschaft),
            AuswaertsWappen = TeilnehmerAnzeige.MannschaftsIcon(s.AuswaertsMannschaft),
            Status = s.Status,
            HatErgebnis = s.Ergebnis is not null,
            ErgebnisText = text,
            ErgebnisDetail = detail,
            IstRueckspiel = s.IstRueckspiel,
            IstPlatzhalterSpiel = !s.HeimMannschaftId.HasValue || !s.AuswaertsMannschaftId.HasValue,
            HeimIstVorschau = heimVorschau is not null,
            AuswaertsIstVorschau = auswaertsVorschau is not null,
            GruppeNummer = gruppeNummer
        };
    }

    internal static string? KoPositionsLabel(Spiel s, TurnierRunde? runde, int positionInRunde)
    {
        if (runde is null) return null;
        bool mitRueck = runde.MitRueckspiel;
        return runde.Rundenart switch
        {
            Rundenart.Finale => FormatKoLabel("Finale", positionInRunde, mitRueck, s.IstRueckspiel),
            Rundenart.SpielUmPlatzDrei => FormatKoLabel("3. Platz", positionInRunde, mitRueck, s.IstRueckspiel),
            Rundenart.KoRunde => KoKuerzel(runde.EingangsMannschaftsanzahl) is { } k
                                          ? FormatKoLabel(k, positionInRunde, mitRueck, s.IstRueckspiel) : null,
            _ => null
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
        4 => "HF",
        8 => "VF",
        16 => "AF",
        32 => "R16",
        64 => "R32",
        128 => "R64",
        _ => eingangTeams > 2 ? $"R{eingangTeams / 2}" : null
    };

    private static int ParseInt(string s) => int.TryParse(s, out var v) ? v : 0;
    private static int? ParseIntNullable(string s) => string.IsNullOrWhiteSpace(s) ? null : int.TryParse(s, out var v) ? v : null;

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

        if (!reg90Draw || !IstKoRunde) return EntscheidungArt.RegulaereSpielzeit;

        bool hatVerl = HatVerlaengerungsVerfahren;
        bool hatElf = HatElfmeterVerfahren;

        if (hatVerl)
        {
            int? heimV = ParseIntNullable(HeimToreVerlaengerung);
            int? ausV = ParseIntNullable(AuswaertsToreVerlaengerung);

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
                        _ => EntscheidungArt.Verlaengerung,
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
                _ => EntscheidungArt.Verlaengerung,
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
            FokusAlleAktiv = false;
            return;
        }
        int idx = FokusSpieltage.IndexOf(FokusAktiverSpieltag);
        if (idx >= 0 && idx + 1 < FokusSpieltage.Count)
            FokusAktiverSpieltag = FokusSpieltage[idx + 1];
    }

    private int? _vorherigeFokusGruppenNummer;

    private void BeiGruppeFokusAenderung()
    {
        bool istEchterGruppenwechsel = AusgewaehlteGruppe?.GruppenNummer != _vorherigeFokusGruppenNummer;
        _vorherigeFokusGruppenNummer = AusgewaehlteGruppe?.GruppenNummer;

        _gruppeSimulierenBefehl.NotifyCanExecuteChanged();
        _spieltagSimulierenBefehl.NotifyCanExecuteChanged();
        _spieltagZuruecksetzenBefehl.NotifyCanExecuteChanged();
        _zurueckZurUebersichtBefehl.NotifyCanExecuteChanged();

        if (istEchterGruppenwechsel)
        {
            AktualisiereFokusSpieltage(AusgewaehlteGruppe is not null
                ? Spiele.Where(s => s.GruppeNummer == AusgewaehlteGruppe.GruppenNummer && s.Spieltag.HasValue)
                        .Select(s => s.Spieltag!.Value)
                : null);
        }
    }

    private void BeiLigaProSpieltagAenderung()
    {
        _spieltagSimulierenBefehl.NotifyCanExecuteChanged();
        _spieltagZuruecksetzenBefehl.NotifyCanExecuteChanged();

        AktualisiereFokusSpieltage(LigaProSpieltagAktiv
            ? Spiele.Where(s => s.Spieltag.HasValue).Select(s => s.Spieltag!.Value)
            : null);
    }

    private void AktualisiereFokusSpieltage(IEnumerable<int>? spieltage)
    {
        FokusSpieltage.Clear();
        FokusAlleAktiv = true;
        FokusAktiverSpieltag = 0;

        if (spieltage is not null)
        {
            foreach (var st in spieltage.Distinct().OrderBy(t => t))
                FokusSpieltage.Add(st);
            if (FokusSpieltage.Count > 0)
                FokusAktiverSpieltag = FokusSpieltage[0];

            if (FokusSpieltage.Count == 1)
                FokusAlleAktiv = false;
        }

        OnPropertyChanged(nameof(HatMehrereSpieltage));
        _fokusVorigerSpieltag.NotifyCanExecuteChanged();
        _fokusNaechsterSpieltag.NotifyCanExecuteChanged();
        _fokusAlleSpieltageBefehl.NotifyCanExecuteChanged();
    }

    private GruppenTabelle? EffektiveGruppe =>
        AusgewaehlteGruppe ?? (AusgewaehlteSpiel?.GruppeNummer is { } gn
            ? GruppenTabellen.FirstOrDefault(g => g.GruppenNummer == gn)
            : null);

    private int? EffektiverSpieltag =>
        ZeigeFokusListe && !FokusAlleAktiv
            ? FokusAktiverSpieltag
            : AusgewaehlteSpiel?.Spieltag;

    // ── Simulation: CanExecute ─────────────────────────────────────────────────

    private bool CanRundeSimulieren() =>
        HatRunde && !IstSimulierend
        && Spiele.Any(s => s.Status != SpielStatus.Abgeschlossen && !s.IstPlatzhalterSpiel);

    private bool CanGruppeSimulieren() =>
        EffektiveGruppe is { } gruppe && !IstSimulierend
        && Spiele.Any(s => s.GruppeNummer == gruppe.GruppenNummer
                         && s.Status != SpielStatus.Abgeschlossen && !s.IstPlatzhalterSpiel);

    private IEnumerable<SpielZeile> SpieleAmEffektivenSpieltag()
    {
        if (EffektiverSpieltag is not { } spieltag) return [];
        return AusgewaehlteGruppe is { } gruppe
            ? Spiele.Where(s => s.GruppeNummer == gruppe.GruppenNummer && s.Spieltag == spieltag)
            : Spiele.Where(s => s.Spieltag == spieltag);
    }

    private bool CanSpieltagSimulieren() =>
        !IstSimulierend
        && SpieleAmEffektivenSpieltag().Any(s => s.Status != SpielStatus.Abgeschlossen && !s.IstPlatzhalterSpiel);

    private bool CanAuswahlSimulieren() =>
        !IstSimulierend && MehrfachAusgewaehlteSpiele.Count > 1 && AnzahlMehrfachSimulierbar > 0;

    private bool CanHinrundeSimulieren() =>
        IstLigaMitRueckspiel && !IstSimulierend
        && Spiele.Any(s => !s.IstRueckspiel && s.Status != SpielStatus.Abgeschlossen && !s.IstPlatzhalterSpiel);

    private bool CanRueckrundeSimulieren() =>
        IstLigaMitRueckspiel && !IstSimulierend
        && Spiele.Any(s => s.IstRueckspiel && s.Status != SpielStatus.Abgeschlossen && !s.IstPlatzhalterSpiel);

    private bool CanRundeZuruecksetzen() => HatRunde && !IstSimulierend && Spiele.Any(s => s.HatErgebnis);

    private bool CanGruppeZuruecksetzen() =>
        EffektiveGruppe is { } gruppe && !IstSimulierend
        && Spiele.Any(s => s.GruppeNummer == gruppe.GruppenNummer && s.HatErgebnis);

    private bool CanSpieltagZuruecksetzen() =>
        !IstSimulierend && SpieleAmEffektivenSpieltag().Any(s => s.HatErgebnis);

    private bool CanHinrundeZuruecksetzen() =>
        IstLigaMitRueckspiel && !IstSimulierend && Spiele.Any(s => !s.IstRueckspiel && s.HatErgebnis);

    private bool CanRueckrundeZuruecksetzen() =>
        IstLigaMitRueckspiel && !IstSimulierend && Spiele.Any(s => s.IstRueckspiel && s.HatErgebnis);

    private bool CanErgebnisLoeschen() =>
        !IstSimulierend
        && (MehrfachAusgewaehlteSpiele.Count > 1 && MehrfachAusgewaehlteSpiele.Any(s => s.HatErgebnis)
            || FormAktiv && FormHatErgebnis);

    // ── Simulation: Aktionen ───────────────────────────────────────────────────

    private async Task RundeSimulierenAsync()
    {
        if (AusgewaehlteRunde is null) return;
        int rundeId = AusgewaehlteRunde.Id;
        Fehlermeldung = null;
        IstSimulierend = true;
        try
        {
            await _simulationsService.RundeSimulierenAsync(rundeId, SimulationsModus.AusfuehrlicheSimulation);
            await _spielplanService.FinaleAutomatischGenerierenWennBereitAsync(AusgewaehlteRunde.TurnierInstanzId);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i)))
                Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap, _platzhalterVorschau));
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
        int rundeId = AusgewaehlteRunde.Id;
        int? spielId = AusgewaehlteSpiel?.Id;
        var teamIds = gruppe.Tabelle.Select(t => t.Mannschaft.Id).ToList();
        Fehlermeldung = null;
        IstSimulierend = true;
        try
        {
            await _simulationsService.GruppeSimulierenAsync(rundeId, teamIds, SimulationsModus.AusfuehrlicheSimulation);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i)))
                Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap, _platzhalterVorschau));
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
        if (AusgewaehlteRunde is null || EffektiverSpieltag is not { } spieltag) return;

        int rundeId = AusgewaehlteRunde.Id;
        int? spielId = AusgewaehlteSpiel?.Id;
        Fehlermeldung = null;
        IstSimulierend = true;
        try
        {
            if (AusgewaehlteGruppe is { } gruppe)
            {
                var teamIds = gruppe.Tabelle.Select(t => t.Mannschaft.Id).ToList();
                await _simulationsService.SpieltagInGruppeSimulierenAsync(
                    rundeId, spieltag, teamIds, SimulationsModus.AusfuehrlicheSimulation);
            }
            else
            {
                await _simulationsService.SpieltageSimulierenAsync(rundeId, spieltag, SimulationsModus.AusfuehrlicheSimulation);
            }

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i)))
                Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap, _platzhalterVorschau));
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

    private async Task AuswahlSimulierenAsync()
    {
        if (AusgewaehlteRunde is null) return;
        var spielIds = MehrfachAusgewaehlteSpiele
            .Where(s => s.Status != SpielStatus.Abgeschlossen && !s.IstPlatzhalterSpiel)
            .Select(s => s.Id).ToList();
        if (spielIds.Count == 0) return;

        int rundeId = AusgewaehlteRunde.Id;
        Fehlermeldung = null;
        IstSimulierend = true;
        try
        {
            await _simulationsService.SpieleSimulierenAsync(spielIds, SimulationsModus.AusfuehrlicheSimulation);
            await _spielplanService.FinaleAutomatischGenerierenWennBereitAsync(AusgewaehlteRunde.TurnierInstanzId);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i)))
                Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap, _platzhalterVorschau));
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

    private Task HinrundeSimulierenAsync() => RundenHaelfteSimulierenAsync(istRueckrunde: false);
    private Task RueckrundeSimulierenAsync() => RundenHaelfteSimulierenAsync(istRueckrunde: true);

    private async Task RundenHaelfteSimulierenAsync(bool istRueckrunde)
    {
        if (AusgewaehlteRunde is null) return;
        int rundeId = AusgewaehlteRunde.Id;
        Fehlermeldung = null;
        IstSimulierend = true;
        try
        {
            await _simulationsService.RundenHaelfteSimulierenAsync(rundeId, istRueckrunde, SimulationsModus.AusfuehrlicheSimulation);
            await _spielplanService.FinaleAutomatischGenerierenWennBereitAsync(AusgewaehlteRunde.TurnierInstanzId);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i)))
                Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap, _platzhalterVorschau));
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

    // ── Ergebnis-Reset: Aktionen (Issue #123) ────────────────────────────────

    private async Task RundeZuruecksetzenAsync()
    {
        if (AusgewaehlteRunde is null) return;
        var spielIds = Spiele.Where(s => s.HatErgebnis).Select(s => s.Id).ToList();
        if (spielIds.Count == 0) return;
        var kaskadenHinweis = await KaskadenHinweisAsync(spielIds);

        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Ergebnisse zurücksetzen",
            $"Alle {spielIds.Count} Ergebnis(se) der Runde '{AusgewaehlteRunde.Name}' zurücksetzen?{kaskadenHinweis}");
        if (!bestaetigt) return;

        int rundeId = AusgewaehlteRunde.Id;
        Fehlermeldung = null;
        IstSimulierend = true;
        try
        {
            await _spielService.MehrereErgebnisseLoeschenAsync(spielIds);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i)))
                Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap, _platzhalterVorschau));
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
        var kaskadenHinweis = await KaskadenHinweisAsync(spielIds);

        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Ergebnisse zurücksetzen",
            $"Alle {spielIds.Count} Ergebnis(se) der Gruppe {gruppe.GruppenNummer} zurücksetzen?{kaskadenHinweis}");
        if (!bestaetigt) return;

        int rundeId = AusgewaehlteRunde.Id;
        int? spielId = AusgewaehlteSpiel?.Id;
        Fehlermeldung = null;
        IstSimulierend = true;
        try
        {
            await _spielService.MehrereErgebnisseLoeschenAsync(spielIds);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i)))
                Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap, _platzhalterVorschau));
            OnPropertyChanged(nameof(FokusSpiele));
            await SpielAuswahlWiederherstellenAsync(spielId);
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

    private async Task SpieltagZuruecksetzenAsync()
    {
        if (AusgewaehlteRunde is null || EffektiverSpieltag is not { } spieltag) return;

        var spielIds = SpieleAmEffektivenSpieltag().Where(s => s.HatErgebnis).Select(s => s.Id).ToList();
        if (spielIds.Count == 0) return;
        var kaskadenHinweis = await KaskadenHinweisAsync(spielIds);

        var gruppenHinweis = AusgewaehlteGruppe is { } gruppe ? $" (Gruppe {gruppe.GruppenNummer})" : "";
        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Ergebnisse zurücksetzen",
            $"Alle {spielIds.Count} Ergebnis(se) von Spieltag {spieltag}{gruppenHinweis} zurücksetzen?{kaskadenHinweis}");
        if (!bestaetigt) return;

        int rundeId = AusgewaehlteRunde.Id;
        int? spielId = AusgewaehlteSpiel?.Id;
        Fehlermeldung = null;
        IstSimulierend = true;
        try
        {
            await _spielService.MehrereErgebnisseLoeschenAsync(spielIds);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i)))
                Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap, _platzhalterVorschau));
            OnPropertyChanged(nameof(FokusSpiele));
            await SpielAuswahlWiederherstellenAsync(spielId);
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

    private async Task ErgebnisMehrfachLoeschenAsync()
    {
        if (AusgewaehlteRunde is null) return;
        var spielIds = MehrfachAusgewaehlteSpiele.Where(s => s.HatErgebnis).Select(s => s.Id).ToList();
        if (spielIds.Count == 0) return;
        var kaskadenHinweis = await KaskadenHinweisAsync(spielIds);

        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Ergebnisse zurücksetzen", $"{spielIds.Count} Spielergebnis(se) zurücksetzen?{kaskadenHinweis}");
        if (!bestaetigt) return;

        int rundeId = AusgewaehlteRunde.Id;
        Fehlermeldung = null;
        IstSimulierend = true;
        try
        {
            await _spielService.MehrereErgebnisseLoeschenAsync(spielIds);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i)))
                Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap, _platzhalterVorschau));
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

    private Task HinrundeZuruecksetzenAsync() => RundenHaelfteZuruecksetzenAsync(istRueckrunde: false);
    private Task RueckrundeZuruecksetzenAsync() => RundenHaelfteZuruecksetzenAsync(istRueckrunde: true);

    private async Task RundenHaelfteZuruecksetzenAsync(bool istRueckrunde)
    {
        if (AusgewaehlteRunde is null) return;
        var spielIds = Spiele.Where(s => s.IstRueckspiel == istRueckrunde && s.HatErgebnis)
            .Select(s => s.Id).ToList();
        if (spielIds.Count == 0) return;
        var kaskadenHinweis = await KaskadenHinweisAsync(spielIds);

        var bezeichnung = istRueckrunde ? "Rückrunde" : "Hinrunde";
        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Ergebnisse zurücksetzen", $"Alle {spielIds.Count} Ergebnis(se) der {bezeichnung} zurücksetzen?{kaskadenHinweis}");
        if (!bestaetigt) return;

        int rundeId = AusgewaehlteRunde.Id;
        Fehlermeldung = null;
        IstSimulierend = true;
        try
        {
            await _spielService.MehrereErgebnisseLoeschenAsync(spielIds);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            foreach (var (s, i) in neueSpiele.Select((x, i) => (x, i)))
                Spiele.Add(MapSpielZeile(s, AusgewaehlteRunde, i, _teamGruppeMap, _platzhalterVorschau));
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

    private IReadOnlyList<SpielZeile> BerechneFokusSpiele()
    {
        IEnumerable<SpielZeile> spiele;
        if (AusgewaehlteGruppe is not null)
            spiele = Spiele.Where(s => s.GruppeNummer == AusgewaehlteGruppe.GruppenNummer);
        else if (LigaProSpieltagAktiv)
            spiele = Spiele;
        else
            return [];

        if (!FokusAlleAktiv)
            spiele = spiele.Where(s => s.Spieltag == FokusAktiverSpieltag);
        return spiele.ToList();
    }
}
