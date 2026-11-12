using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Zufall;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>
/// "Auslosung"-Tab der Turnierdurchführung (#300) — Master-Detail: Auslosungsliste/Neu-Formular + Workspace
/// (Lostöpfe, Teilnehmerfilter, Verbandsquoten-Spalte, Regeln, Ranglisten-Automatik), Durchführen und
/// Zurücksetzen. Die TV-Ziehung liegt in <see cref="ZiehungViewModel"/>, die Gegner-Matrix der Ligaphase in
/// <see cref="GegnerMatrixViewModel"/> (#401). Runden mit „Paarung fest vorgegeben“ legt der
/// <see cref="VorbelegungViewModel"/> fest (#297); hier lost der Anwender bei <see cref="Auslosungsart.Manuell"/>
/// selbst (#482).
///
/// Anders als das WPF-Original hält dieses ViewModel KEINE eigene Instanzliste/-auswahl — die geteilte
/// Instanzauswahl kommt von <see cref="TurnierdurchfuehrungViewModel"/> über <see cref="InstanzSetzenAsync"/>
/// (Precedent: <see cref="VorbelegungViewModel.InstanzSetzenAsync"/>), analog zum <see cref="TurnierTabKoordinator"/>-
/// Muster aus #297. Bewusst auf Turniere mit Status ≠ <see cref="TurnierStatus.Vorlage"/> beschränkt (siehe
/// <see cref="KannNeu"/>) — anders als Vorbelegung zieht die echte Auslosung primär gegen reale Mannschaften.
/// </summary>
public partial class AuslosungViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen,
    ILostopfArbeitsbereichKontext, ITeilnehmerpoolKontext
{
    private readonly AuslosungService _auslosungService;
    private readonly TurnierInstanzService _instanzService;
    private readonly SpielplanService _spielplanService;
    private readonly SpielplanVerlustVorschau _verlustVorschau;
    private readonly TurnierBaumService _baumService;
    private readonly AuslosungsTeilnehmerpool _teilnehmerpool;
    private readonly IDialogService _dialogService;
    private readonly Zufallsquelle _zufallsquelle;
    private readonly ILogger<AuslosungViewModel> _logger;

    private TurnierInstanz? _instanz;
    private Auslosung? _auslosungDetail;

    /// <summary>Die Mannschaften hinter den Platzhaltern der gezeigten Auslosung (#483) — für Anzeige, Ziehung und die
    /// Land-/Verbandsregeln beim Auslosen.</summary>
    private Qualifikanten _qualifikanten = Qualifikanten.Keine;

    /// <summary>Alle für die Runde der gezeigten Auslosung zugelassenen Teams — der Pool wählt daraus, was die Lostöpfe
    /// anbieten (#576).</summary>
    private AuslosungsTeilnehmer? _teilnehmer;
    private bool _ladenAktiv;
    private bool _seitenlisteAktualisierungAktiv;
    private bool _befuellungLaeuft;

    /// <summary>Schutz des geteilten DbContext — <see cref="TurnierdurchfuehrungViewModel"/> setzt hier den des
    /// <see cref="TurnierTabKoordinator"/> ein, damit sich die Aktionen dieses Reiters und das Laden nach einem
    /// Turnier-/Reiterwechsel gegenseitig abwarten statt zu kollidieren (#413). Betreten wird er nur an den
    /// Einstiegspunkten (Befehle, Auswahlwechsel, Regel-Schalter), nie in den Hilfsmethoden darunter — er ist
    /// nicht wiedereintrittsfähig. <see cref="InstanzSetzenAsync"/> läuft bereits unter dem Schutz des Aufrufers.</summary>
    public DbContextReentrancySchutz DbContextSchutz { get; set; } = new();

    // ── Rückkanal des Lostopf-Arbeitsbereichs ───────────────────────────────────
    Auslosung? ILostopfArbeitsbereichKontext.AktuellesDetail => _auslosungDetail;
    Task ILostopfArbeitsbereichKontext.DetailNeuLadenAsync(int auslosungId) => LadeDetailAsync(auslosungId);
    void ILostopfArbeitsbereichKontext.FehlerMelden(string? meldung) => Fehlermeldung = meldung;

    void ILostopfArbeitsbereichKontext.BefuellungLaeuft(bool laeuft)
    {
        _befuellungLaeuft = laeuft;
        BearbeitbarWeitergeben();
    }

    // ── Geteilter Instanz-Kontext (siehe Klassendoku) ────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NeuCommand))]
    private bool _keinTurnierGewaehlt = true;

    [ObservableProperty] private ObservableCollection<Auslosung> _auslosungen = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private Auslosung? _ausgewaehlteAuslosung;

    [ObservableProperty] private ObservableCollection<TurnierRunde> _instanzRunden = [];

    /// <summary>Runden mit PaarungFestVorgegeben=true werden im Vorbelegung-Tab verwaltet, nicht hier. Runden
    /// mit bereits vorhandener Auslosung (auch einer im Vorbelegung-Tab abgeschlossenen, hier unsichtbaren
    /// Vorbelegung — siehe <see cref="_belegteRundenIds"/>) werden ebenfalls ausgeblendet: sonst könnte
    /// eine zweite Auslosung für dieselbe Runde angelegt werden, was den Vorbelegung-Tab beim
    /// ToDictionary(a => a.TurnierRundeId)-Aufbau mit einer ArgumentException abstürzen lässt (Issue #320).
    /// Eine Folgerunde, deren Qualifikanten noch nicht alle feststehen, fehlt ebenfalls (#483) — siehe
    /// <see cref="AuslosbareRundenErmittelnAsync"/>.</summary>
    private IEnumerable<TurnierRunde> AuslosungsKandidaten() =>
        InstanzRunden.Where(r => !r.PaarungFestVorgegeben && !_belegteRundenIds.Contains(r.Id));

    /// <summary>TurnierRundeId aller Auslosungen der Instanz — inkl. der hier absichtlich ausgeblendeten
    /// Vorbelegungen (siehe <see cref="LadeAuslosungenGefiltertAsync"/>), damit
    /// <see cref="AuslosungsKandidaten"/> auch solche Runden zuverlässig ausschließt (Issue #320).</summary>
    private HashSet<int> _belegteRundenIds = [];

    // ── Neue-Auslosung-Formular ────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigtHinweis))]
    [NotifyCanExecuteChangedFor(nameof(NeuCommand))]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private bool _neuFormAktiv;

    /// <summary>Felder des Neu-Formulars (Kind-ViewModel, #496).</summary>
    public NeueAuslosungViewModel NeuFormular { get; } = new();

    // ── Auslosung-Detail ──────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigtHinweis))]
    [NotifyCanExecuteChangedFor(nameof(AuslosungAusfuehrenCommand))]
    [NotifyCanExecuteChangedFor(nameof(AuslosungZuruecksetzenCommand))]
    private bool _detailAktiv;

    /// <summary>Platzhalter-Hinweistext ("Turnier und Runde wählen …") — nur solange weder Neu-Formular
    /// noch Detail-Workspace aktiv sind, damit sich nichts überlappt.</summary>
    public bool ZeigtHinweis => !NeuFormAktiv && !DetailAktiv;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AuslosungAusfuehrenCommand))]
    [NotifyPropertyChangedFor(nameof(ZeigeBasisdaten))]
    private bool _istBearbeitbar;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailAuslosungsartHinweis))]
    [NotifyPropertyChangedFor(nameof(DetailAuslosungsartWarnung))]
    [NotifyPropertyChangedFor(nameof(DetailAnzahlLostoepfeBearbeitbar))]
    [NotifyPropertyChangedFor(nameof(DetailAuslosungsartAusgewaehlt))]
    [NotifyCanExecuteChangedFor(nameof(AuslosungAusfuehrenCommand))]
    private Auslosungsart _detailAuslosungsart;

    /// <summary>Gecacht, siehe <see cref="NeueAuslosungViewModel.AuslosungsartOptionen"/> — aktualisiert in <see cref="LadeDetailAsync"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailAuslosungsartAusgewaehlt))]
    private IReadOnlyList<AuslosungsartOption> _detailAuslosungsartOptionen = [];

    /// <summary>Wrapper für die ComboBox-Bindung, siehe <see cref="NeueAuslosungViewModel.AuslosungsartAusgewaehlt"/>.</summary>
    public AuslosungsartOption? DetailAuslosungsartAusgewaehlt
    {
        get => DetailAuslosungsartOptionen.FirstOrDefault(o => o.Wert == DetailAuslosungsart);
        set { if (value is not null) DetailAuslosungsart = value.Wert; }
    }

    /// <summary>Position in der Gruppe (#475), nur in der Gruppenphase wählbar.</summary>
    public GruppenpositionAuswahl DetailGruppenposition { get; } = new();

    [ObservableProperty] private string _detailName = "";
    [ObservableProperty] private int _detailAnzahlLostoepfe = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    private bool _istDetailDirty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigeErgebnis))]
    [NotifyPropertyChangedFor(nameof(ZeigeVorbereitung))]
    [NotifyCanExecuteChangedFor(nameof(AuslosungZuruecksetzenCommand))]
    private bool _istAbgeschlossen;

    /// <summary>Ergebnis der abgeschlossenen Auslosung (Kind-ViewModel, #496).</summary>
    public AuslosungsErgebnisViewModel Ergebnis { get; } = new();

    [ObservableProperty] private string _auslosungKopfzeile = "";

    /// <summary>Verfügbar-Liste, Lostöpfe und deren Befüllung (Kind-ViewModel).</summary>
    public LostopfArbeitsbereichViewModel Arbeitsbereich { get; }

    /// <summary>Regel-Schalter des Ribbon-Flyouts "Auslosungs-Regeln" (Kind-ViewModel, nicht an XAML gebunden).</summary>
    private readonly AuslosungsRegelnViewModel _regeln;

    /// <summary>Teilnehmerpool der ersten Runde eines Turniers mit Quoten, eigener Reiter vor den Lostöpfen (Kind-ViewModel, #576).</summary>
    public TeilnehmerpoolViewModel Teilnehmerpool { get; }

    // ── Reiter Teilnehmerpool | Lostöpfe (#576) ───────────────────────────────
    public bool ZeigeReiterleiste => ZeigeVorbereitung && Teilnehmerpool.Aktiv;
    public bool ZeigeTeilnehmerpool => ZeigeReiterleiste && Teilnehmerpool.PoolReiterAktiv;
    public bool ZeigeLostopfArbeitsbereich => ZeigeVorbereitung && !ZeigeTeilnehmerpool;

    /// <summary>In beiden Reitern: der Pool erfüllt seine Quoten nicht, die Automatik ist gesperrt (#593, vorher je Reiter
    /// eine eigene Meldung).</summary>
    public bool ZeigePoolHinweis => ZeigeReiterleiste && Teilnehmerpool.HatMaengel;

    public bool ZeigeUngespeichertenPool => ZeigeReiterleiste && Teilnehmerpool.HatUngespeicherteAenderungen;

    public string VerfuegbarUeberschrift => Teilnehmerpool.Aktiv ? "TEILNEHMERPOOL – OHNE TOPF" : "VERFÜGBAR";

    private void ReiterAnzeigeMelden()
    {
        OnPropertyChanged(nameof(ZeigeReiterleiste));
        OnPropertyChanged(nameof(ZeigeTeilnehmerpool));
        OnPropertyChanged(nameof(ZeigeLostopfArbeitsbereich));
        OnPropertyChanged(nameof(ZeigePoolHinweis));
        OnPropertyChanged(nameof(ZeigeUngespeichertenPool));
        OnPropertyChanged(nameof(VerfuegbarUeberschrift));
    }

    private void BeiTeilnehmerpoolAenderung(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TeilnehmerpoolViewModel.Aktiv) or nameof(TeilnehmerpoolViewModel.PoolReiterAktiv)
            or nameof(TeilnehmerpoolViewModel.HatMaengel) or nameof(TeilnehmerpoolViewModel.HatUngespeicherteAenderungen))
            ReiterAnzeigeMelden();
        if (e.PropertyName == nameof(TeilnehmerpoolViewModel.Aktiv))
            OnPropertyChanged(nameof(RibbonZusatzGruppen));
    }

    // ── TV-Modus: Schrittweise Ziehungsanimation (#303, Issue #5) ────────────────
    /// <summary>Wie das Ergebnis nach dem Auslosen erscheint: sofort oder als TV-Ziehung.</summary>
    private enum ZiehungsArt { Keine, Automatisch, Manuell }

    /// <summary>Was nach dem Auslosen als TV-Ziehung gezeigt wird: die Gegner-Matrizen der Ligaphase oder die
    /// Schritte einer Gruppen-/K.-o.-Ziehung — ohne TV-Modus keines von beiden.</summary>
    private sealed record ZiehungsVorlage(ObservableCollection<GegnerMatrixTopf>? Matrizen, List<ZiehungsSchritt>? Schritte)
    {
        public static readonly ZiehungsVorlage Keine = new(null, null);
    }

    /// <summary>Live-Anzeige der TV-Ziehung (Kind-ViewModel, ohne Datenbankzugriff).</summary>
    public ZiehungViewModel Ziehung { get; } = new();

    /// <summary>Ziehung von Hand bei der Auslosungsart „Manuell“ (#482, Kind-ViewModel, ohne Datenbankzugriff).</summary>
    public HandZiehungViewModel HandZiehung { get; } = new();

    /// <summary>Lostöpfe, Quoten und Basisdaten nur vor dem Auslosen — und nicht, während von Hand gezogen wird.</summary>
    public bool ZeigeVorbereitung => !IstAbgeschlossen && !HandZiehung.Laeuft;
    public bool ZeigeBasisdaten   => IstBearbeitbar && !HandZiehung.Laeuft;

    /// <summary>Zwischen dem Auslosen und dem Start der TV-Ziehung (Spielplan-Generierung, #406) — hält das
    /// schon geladene Ergebnis verborgen, damit es nicht vor der Ziehung aufblitzt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigeErgebnis))]
    private bool _ziehungInVorbereitung;

    /// <summary>Ergebnis-Panel bleibt während der Ziehungsanimation verborgen — das bereits von
    /// LadeDetailAsync geladene Endergebnis wird erst nach Animationsende (bzw. bei "Überspringen" sofort)
    /// sichtbar.</summary>
    public bool ZeigeErgebnis => IstAbgeschlossen && Ziehung.AnimationLaeuftNicht && !ZiehungInVorbereitung;

    /// <summary>Die Gegner-Matrix baut sich erst mit dem Einblenden des Ergebnisses auf (#428) — hier zentral,
    /// weil <see cref="ZeigeErgebnis"/> von drei Stellen aus wechselt.</summary>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(ZeigeErgebnis)) Ergebnis.SichtbarSetzen(ZeigeErgebnis);
    }

    // ── Status ────────────────────────────────────────────────────────────────
    [ObservableProperty] private string? _fehlermeldung;

    // ── Berechnete Eigenschaften ─────────────────────────────────────────────
    public string DetailAuslosungsartHinweis   => AuslosungAnzeigeTexte.AuslosungsartHinweisText(DetailAuslosungsart);
    public string? DetailAuslosungsartWarnung  => AuslosungAnzeigeTexte.AuslosungsartWarnungText(DetailAuslosungsart, _auslosungDetail?.TurnierRunde?.Rundenart);

    public bool DetailAnzahlLostoepfeBearbeitbar => AuslosungsartVorgabe.ErlaubtMehrereToepfe(DetailAuslosungsart);

    // ── IRibbonAware ──────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Auslosung";
    public ICommand NeuBefehl => NeuCommand;
    public ICommand SpeichernBefehl => SpeichernCommand;
    public ICommand AbbrechenBefehl => AbbrechenCommand;
    public ICommand LoeschenBefehl => LoeschenCommand;

    // Zwei Plätze (#400): vorn je nach Phase die Vorbereitung oder — während der laufenden TV-Ziehung — die
    // Live-Steuerung, dahinter immer die Ziehung. Der Tausch statt einer zusätzlichen dritten Gruppe hält die
    // Leiste ruhig: die Ziehungs-Buttons bleiben stehen, Vorbereitung ist während der Ziehung ohnehin nutzlos.
    // Die Ziehung von Hand ist selbst die Ziehung — bei „Manuell“ entfällt der TV-Modus (#482).
    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen =>
    [
        Ziehung.AnimationLaeuft ? LiveSteuerungGruppe() : HandZiehung.Laeuft ? HandZiehungGruppe() : VorbereitungGruppe(),
        new RibbonGruppe
        {
            Header = "Ziehung",
            Aktionen =
            [
                new RibbonAktion { Header = "Auslosung durchführen", AutomationId = "Befehlsleiste.AuslosungDurchfuehren", Befehl = AuslosungAusfuehrenCommand, IconGlyph = "" },
                .. (IstHandZiehungsart ? Array.Empty<RibbonAktion>() : [TvModusAktion()]),
                // Bewusst am Gruppenende statt direkt neben "Auslosung durchführen" — beide sind zwar nie zugleich
                // aktiv (bearbeitbar vs. abgeschlossen), sollen aber auch optisch nicht nebeneinander liegen.
                new RibbonAktion { Header = "Zurücksetzen", AutomationId = "Befehlsleiste.AuslosungZuruecksetzen", Befehl = AuslosungZuruecksetzenCommand, IconGlyph = "" },
            ],
        },
    ];

    // "Töpfe" und "TV-Modus" als je EIN Flyout-Button statt Einzelbuttons (Nutzerfund #303-Test: Ribbon-Gruppe
    // voll, #319) — analog zum "Simulieren"-Flyout-Muster aus SpielplanViewModel (#301).
    private RibbonGruppe VorbereitungGruppe() => new()
    {
        Header = "Vorbereitung",
        Aktionen =
        [
            new RibbonAktion
            {
                Header = "Töpfe",
                AutomationId = "Befehlsleiste.Toepfe",
                Unteraktionen =
                [
                    new RibbonUnteraktion("Automatisch befüllen", Arbeitsbereich.LostoepfeAutomatischBefuellenCommand,
                        AutomationId: "Befehlsleiste.ToepfeAutomatischBefuellen"),
                    new RibbonUnteraktion("Alle leeren", Arbeitsbereich.AlleToepfeZuruecksetzenCommand),
                ],
                IconGlyph = "",
            },
            .. (Teilnehmerpool.Aktiv ? [PoolNeuVorbelegenAktion()] : Array.Empty<RibbonAktion>()),
            new RibbonAktion
            {
                Header = "Auslosungs-Regeln",
                AutomationId = "Befehlsleiste.AuslosungsRegeln",
                ToggleUnterpunkte = _regeln.RibbonSchalter(),
                IconGlyph = "",
            },
        ],
    };

    private RibbonAktion PoolNeuVorbelegenAktion() => new()
    {
        Header = "Pool neu vorbelegen",
        AutomationId = "Befehlsleiste.PoolNeuVorbelegen",
        Befehl = Teilnehmerpool.NeuVorbelegenCommand,
        IconGlyph = "",
    };

    private RibbonAktion TvModusAktion() => new()
    {
        Header = "TV-Modus",
        AutomationId = "Befehlsleiste.TvModus",
        Unteraktionen =
        [
            new RibbonUnteraktion("Mit TV-Modus", AuslosungMitTvModusAusfuehrenCommand),
            new RibbonUnteraktion("Mit TV-Modus manuell", AuslosungMitTvModusManuellAusfuehrenCommand,
                AutomationId: "Befehlsleiste.TvModusManuell"),
        ],
        IconGlyph = "",
    };

    /// <summary>Steuerung der Ziehung von Hand (#482) — tritt wie die Live-Steuerung an die Stelle der Vorbereitung.</summary>
    private RibbonGruppe HandZiehungGruppe() => new()
    {
        Header = "Ziehung von Hand",
        Aktionen =
        [
            new RibbonAktion { Header = "Letzten Zug zurücknehmen", AutomationId = "Befehlsleiste.HandZiehungZuruecknehmen", Befehl = HandZiehung.ZuruecknehmenCommand, IconGlyph = "" },
            new RibbonAktion { Header = "Ziehung abbrechen", AutomationId = "Befehlsleiste.HandZiehungAbbrechen", Befehl = HandZiehungAbbrechenCommand, IconGlyph = "" },
            new RibbonAktion { Header = "Auslosung abschließen", AutomationId = "Befehlsleiste.AuslosungAbschliessen", Befehl = HandZiehungAbschliessenCommand, IconGlyph = "" },
        ],
    };

    private bool IstHandZiehungsart => _auslosungDetail?.Auslosungsart == Auslosungsart.Manuell;

    /// <summary>Direkte Buttons statt Flyout (#364): während der Live-Ziehung ständig gebraucht. Beide stehen in
    /// beiden TV-Modi da, damit die Leiste gleich bleibt — "Weiter" ist in der automatischen Ziehung lediglich
    /// deaktiviert (wartet nie auf einen Klick).</summary>
    private RibbonGruppe LiveSteuerungGruppe() => new()
    {
        Header = "Live-Steuerung",
        Aktionen =
        [
            new RibbonAktion { Header = "Weiter", AutomationId = "Befehlsleiste.ZiehungWeiter", Befehl = Ziehung.AnimationWeiterCommand, IconGlyph = "" },
            new RibbonAktion { Header = "Rest sofort anzeigen", AutomationId = "Befehlsleiste.ZiehungRestAnzeigen", Befehl = Ziehung.AnimationUeberspringenCommand, IconGlyph = "" },
        ],
    };

    public AuslosungViewModel(
        AuslosungService auslosungService,
        TurnierInstanzService instanzService,
        SpielplanService spielplanService,
        SpielplanVerlustVorschau verlustVorschau,
        TurnierBaumService baumService,
        RanglisteService ranglisteService,
        AuslosungsTeilnehmerpool teilnehmerpool,
        LostopfBefuellung befuellung,
        TeilnehmerpoolVorbelegung poolVorbelegung,
        IDialogService dialogService,
        Zufallsquelle zufallsquelle,
        ILogger<AuslosungViewModel> logger)
    {
        _auslosungService = auslosungService;
        _instanzService = instanzService;
        _spielplanService = spielplanService;
        _verlustVorschau = verlustVorschau;
        _baumService = baumService;
        _teilnehmerpool = teilnehmerpool;
        _dialogService = dialogService;
        _zufallsquelle = zufallsquelle;
        _logger = logger;
        Arbeitsbereich = new LostopfArbeitsbereichViewModel(this, auslosungService, ranglisteService, befuellung, dialogService);
        _regeln = new AuslosungsRegelnViewModel(auslosungService, () => DbContextSchutz, meldung => Fehlermeldung = meldung);
        _regeln.Geaendert += () => OnPropertyChanged(nameof(RibbonZusatzGruppen));
        Teilnehmerpool = new TeilnehmerpoolViewModel(this, poolVorbelegung);
        Teilnehmerpool.PropertyChanged += BeiTeilnehmerpoolAenderung;
        Arbeitsbereich.RanglisteGewaehlt += id => _ = PoolRanglisteSetzenAsync(id);
        PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ZeigeVorbereitung)) ReiterAnzeigeMelden(); };
        Ziehung.PropertyChanged += BeiZiehungAenderung;
        HandZiehung.PropertyChanged += BeiHandZiehungAenderung;
        NeuFormular.PropertyChanged += BeiNeuFormularAenderung;
        DetailGruppenposition.Geaendert += () => { if (!_ladenAktiv) IstDetailDirty = true; };
    }

    /// <summary>Beginn und Ende der TV-Ziehung wirken auf Ergebnisanzeige, Ribbon-Gruppe und "Zurücksetzen".</summary>
    private void BeiZiehungAenderung(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ZiehungViewModel.AnimationLaeuft)) return;
        OnPropertyChanged(nameof(ZeigeErgebnis));
        OnPropertyChanged(nameof(RibbonZusatzGruppen));
        AuslosungZuruecksetzenCommand.NotifyCanExecuteChanged();
    }

    private void BeiNeuFormularAenderung(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NeueAuslosungViewModel.Runde) or nameof(NeueAuslosungViewModel.Name))
            SpeichernCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Von <see cref="TurnierdurchfuehrungViewModel"/> einmalig beim Aktivieren geladen — analog
    /// <see cref="TurnierkopfViewModel.StammdatenLadenAsync"/>.</summary>
    public async Task RanglistenLadenAsync()
    {
        // Eigener Vorgang, damit ein hängendes Laden im Log einen Beginn ohne Ende hinterlässt (#631, Fall 1).
        using var vorgang = _logger.Vorgang("Auslosung: Ranglisten laden");
        await Arbeitsbereich.RanglistenLadenAsync();
    }

    /// <summary>Vom Composite gesetzt, wenn sich die geteilte Instanzauswahl ändert (Precedent:
    /// <see cref="VorbelegungViewModel.InstanzSetzenAsync"/>).</summary>
    public async Task InstanzSetzenAsync(TurnierInstanz? instanz)
    {
        using var vorgang = _logger.Vorgang($"Auslosung: Turnier setzen (Instanz {instanz?.Id})");

        Ziehung.Beenden();
        HandZiehung.Beenden();
        _instanz = instanz;
        _teilnehmerpool.Zuruecksetzen();
        Teilnehmerpool.Leeren();
        _teilnehmer = null;
        _qualifikanten = Qualifikanten.Keine;
        // Eine hinter diesem Wechsel wartende Aktion darf nicht mehr auf dem bisherigen Detail arbeiten (#413).
        _auslosungDetail = null;
        KeinTurnierGewaehlt = instanz is null;
        Auslosungen.Clear();
        InstanzRunden.Clear();
        Arbeitsbereich.Leeren();
        DetailAktiv = false;
        NeuFormAktiv = false;
        AusgewaehlteAuslosung = null;
        if (instanz is null) return;

        Auslosungen = await LadeAuslosungenGefiltertAsync(instanz.Id);
        var detail = await _instanzService.FindenNachIdMitRundenAsync(instanz.Id);
        if (detail is not null)
            InstanzRunden = new ObservableCollection<TurnierRunde>(detail.Runden.OrderBy(r => r.Reihenfolge));
    }

    // ── Ribbon-Befehle ────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannNeu))]
    private async Task Neu()
    {
        if (!await VerlassenBestaetigtAsync()) return;
        Fehlermeldung = null;
        (IReadOnlyList<TurnierRunde> Runden, string? Hinweis) auslosbar;
        using (await DbContextSchutz.BetretenAsync())
        {
            try { auslosbar = await AuslosbareRundenErmittelnAsync(); }
            catch (Exception ex)
            {
                Fehlermeldung = ex.Message;
                return;
            }
        }

        NeuFormAktiv        = true;
        DetailAktiv         = false;
        NeuFormular.Oeffnen(auslosbar.Runden, auslosbar.Hinweis);
    }

    /// <summary>Eine Folgerunde wird erst ausgelost, wenn alle ihre Qualifikanten feststehen (#483) — wie im realen
    /// Ablauf, in dem die Achtelfinal-Auslosung auf die Gruppenphase folgt. Läuft unter dem Schutz des Aufrufers.</summary>
    private async Task<(IReadOnlyList<TurnierRunde> Runden, string? Hinweis)> AuslosbareRundenErmittelnAsync()
    {
        using var vorgang = _logger.Vorgang($"Auslosung: auslosbare Runden ermitteln (Instanz {_instanz?.Id})");

        var nichtAuslosbar = new List<(TurnierRunde Runde, QualifikantenStand Stand)>();
        foreach (var runde in AuslosungsKandidaten().ToList())
        {
            var stand = await _teilnehmerpool.QualifikantenStandAsync(runde, InstanzRunden);
            if (!stand.AlleFest) nichtAuslosbar.Add((runde, stand));
        }

        var nichtAuslosbareIds = nichtAuslosbar.Select(n => n.Runde.Id).ToHashSet();
        var hinweis = nichtAuslosbar.Count == 0
            ? null
            : "Noch nicht auslosbar, weil nicht alle Qualifikanten feststehen:" +
              string.Concat(nichtAuslosbar.Select(n => $"\n•  {n.Runde.Name} – {n.Stand.Offen} von {n.Stand.Gesamt} offen"));
        return (AuslosungsKandidaten().Where(r => !nichtAuslosbareIds.Contains(r.Id)).ToList(), hinweis);
    }

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        Fehlermeldung = null;
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (NeuFormAktiv)
        {
            if (_instanz is null || NeuFormular.Runde is not { } runde) return;
            // Klammert Anlegen, Befüllen nach Standard-Rangliste (bei 36 Teams ~5 s) und das Neuladen danach: Ohne ihn
            // meldete die Seite schon nach dem ersten Laden „Bereit“, und eine Pool-Änderung in dieser Zeit verwarf das
            // abschließende Neuladen (#631, Wackler TeilnehmerpoolTests)
            using var vorgang = _logger.Vorgang($"Auslosung anlegen (Runde {runde.Id})", "Auslosung wird angelegt …");
            try
            {
                var neu = await _auslosungService.ErstellenAsync(NeuFormular.Eingabe(_instanz.Id, runde));

                await _auslosungService.LostoepfeSynchronisierenAsync(neu.Id, NeuFormular.Lostoepfe);

                var standardRanglisteId = runde.StandardRanglisteId;
                NeuFormAktiv = false;

                using (_logger.Vorgang($"Auslosung anlegen: Seitenliste laden (Instanz {_instanz.Id})"))
                    Auslosungen = await LadeAuslosungenGefiltertAsync(_instanz.Id);

                // Standard-Rangliste der Runde (#349): Detail hier sequenziell laden und befüllen, BEVOR die
                // Auswahl gesetzt wird — deren Change-Hook lädt fire-and-forget und liefe sonst parallel auf
                // demselben DbContext (Reentrancy, siehe #317/#338). Danach greift dort der Gleiche-Id-Guard.
                if (standardRanglisteId is not null)
                {
                    await RanglistenLadenAsync();
                    await LadeDetailAsync(neu.Id);
                    if (!await Arbeitsbereich.NachStandardRanglisteBefuellenAsync())
                        Fehlermeldung = "Die Standard-Rangliste dieser Runde wurde nicht gefunden. " +
                                        "Lostöpfe bitte manuell befüllen.";
                }

                AusgewaehlteAuslosung = Auslosungen.FirstOrDefault(a => a.Id == neu.Id);
                _logger.LogInformation(
                    "Auslosung {Id} angelegt: Auswahl {AuswahlId}, Detail {DetailId}, Detail offen {DetailAktiv}.",
                    neu.Id, AusgewaehlteAuslosung?.Id, _auslosungDetail?.Id, DetailAktiv);
            }
            catch (Exception ex) { Fehlermeldung = ex.Message; }
        }
        else if (IstDetailDirty && _auslosungDetail is not null)
        {
            try
            {
                await _auslosungService.BearbeitenAsync(
                    _auslosungDetail.Id, DetailName, DetailAuslosungsart, DetailGruppenposition.Wert);
                await _auslosungService.LostoepfeSynchronisierenAsync(_auslosungDetail.Id, DetailAnzahlLostoepfe);
                await LadeDetailAsync(_auslosungDetail.Id);
                await AktualisiereSeitenlisteAsync();
            }
            catch (Exception ex) { Fehlermeldung = ex.Message; }
        }
    }

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private async Task Abbrechen()
    {
        if (NeuFormAktiv)
        {
            NeuFormAktiv = false;
            if (AusgewaehlteAuslosung is not null)
                DetailAktiv = true;
            return;
        }

        using var schutz = await DbContextSchutz.BetretenAsync();
        if (!IstDetailDirty || _auslosungDetail is null) return;
        try { await LadeDetailAsync(_auslosungDetail.Id); }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task Loeschen()
    {
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (AusgewaehlteAuslosung is null || _instanz is null) return;
        var auslosung = AusgewaehlteAuslosung;
        Fehlermeldung = null;
        try
        {
            // Löschen nimmt wie das Zurücksetzen den Spielplan der Runde mit (#320) — die Rückfrage nennt ihn (#405).
            var verlust = await _verlustVorschau.ErmittelnAsync(auslosung.TurnierInstanzId, auslosung.TurnierRundeId);
            var bestaetigt = await _dialogService.BestaetigenAsync(
                "Auslosung löschen", $"Soll '{auslosung.Name}' wirklich gelöscht werden?{verlust.Hinweis}");
            if (!bestaetigt) return;
            Ziehung.Beenden();
            HandZiehung.Beenden();

            await _auslosungService.LoeschenAsync(auslosung.Id);
            Auslosungen = await LadeAuslosungenGefiltertAsync(_instanz.Id);
            AusgewaehlteAuslosung = null;
            DetailAktiv = false;
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    // ── Durchführen und Zurücksetzen ──────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannAuslosungAusfuehren))]
    private async Task AuslosungAusfuehren()
    {
        await AuslosungDurchfuehrenAsync(ZiehungsArt.Keine);
    }

    /// <summary>Ribbon-Unteraktion (Flyout "TV-Modus"): führt wie der Hauptbutton sofort aus, aber mit
    /// TV-Modus-Ziehungsanimation (automatischer Schrittablauf).</summary>
    [RelayCommand(CanExecute = nameof(KannAuslosungAusfuehren))]
    private async Task AuslosungMitTvModusAusfuehren()
    {
        await AuslosungDurchfuehrenAsync(ZiehungsArt.Automatisch);
    }

    /// <summary>Ribbon-Unteraktion (Flyout "TV-Modus"): führt wie der Hauptbutton sofort aus, aber mit
    /// TV-Modus-Ziehungsanimation im manuellen Schrittmodus (Klick je Schritt).</summary>
    [RelayCommand(CanExecute = nameof(KannAuslosungAusfuehren))]
    private async Task AuslosungMitTvModusManuellAusfuehren()
    {
        await AuslosungDurchfuehrenAsync(ZiehungsArt.Manuell);
    }

    private async Task AuslosungDurchfuehrenAsync(ZiehungsArt ziehungsArt)
    {
        if (IstHandZiehungsart)
        {
            await HandZiehungStartenAsync();
            return;
        }
        Fehlermeldung = null;
        try
        {
            int auslosungId;
            ZiehungsVorlage? vorlage;
            // Nur der Datenbankteil läuft unter dem Schutz (#413) — die Ziehung danach ist reine Anzeige und
            // wartet im manuellen TV-Modus beliebig lange auf Klicks.
            using (await DbContextSchutz.BetretenAsync())
            {
                if (_auslosungDetail is null) return;
                auslosungId = _auslosungDetail.Id;
                vorlage = await AuslosenUndNeuLadenAsync(_auslosungDetail, tvModus: ziehungsArt != ZiehungsArt.Keine);
            }
            if (vorlage is null) return;

            // Zwischenzeitlich verlassen (Turnier-/Reiter-/Auslosungswechsel) — dann gibt es nichts mehr zu zeigen.
            if (!DetailAktiv || _auslosungDetail?.Id != auslosungId) return;

            await ZiehungZeigenAsync(vorlage, manuell: ziehungsArt == ZiehungsArt.Manuell);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
        finally { ZiehungInVorbereitung = false; }
    }

    /// <summary>Datenbankteil des Auslosens — läuft unter dem Schutz des Aufrufers. Liefert, was danach als
    /// TV-Ziehung zu zeigen ist, und <c>null</c>, wenn nicht ausgelost wurde (Rückfrage verneint, Fehler) oder
    /// das Detail inzwischen ein anderes ist.</summary>
    private async Task<ZiehungsVorlage?> AuslosenUndNeuLadenAsync(Auslosung auszulosen, bool tvModus)
    {
        int auslosungId = auszulosen.Id;
        if (!await ErgebnisVerlustBestaetigtAsync(auszulosen.TurnierRundeId)) return null;

        // Erst ab hier messen — die Wartezeit auf die Rückfrage gehört nicht zur Dauer der Aktion.
        using var vorgang = _logger.Vorgang($"Auslosung durchführen (Auslosung {auslosungId})", "Auslosung wird durchgeführt …");

        // Das Ergebnis steht ab AusfuehrenAsync fest und wird von LadeDetailAsync geladen — im TV-Modus
        // bleibt es bis zum Start der Ziehung verborgen, siehe ZeigeErgebnis. Der Service ersetzt dabei
        // auch den Spielplan der Runde und zieht die Folgerunden nach (#414) — VOR der Ziehung (#406): die
        // enthüllt nur noch und darf jederzeit enden (siehe ZiehungBeenden).
        ZiehungInVorbereitung = tvModus;
        if (await AbschliessenUndNeuLadenAsync(auslosungId,
                () => _auslosungService.AusfuehrenAsync(auslosungId, _qualifikanten)) is not { } detail)
            return null;

        // Ziehungsdaten bewusst direkt NACH dem Reload aus dessen Stand bauen (nicht aus einem vor
        // AusfuehrenAsync genommenen Lostöpfe-Snapshot + dem separaten Rückgabewert von AusfuehrenAsync):
        // ViewModel und AuslosungService teilen denselben DbContext-Scope, ein Snapshot kann durch
        // EF-Change-Tracking/Identity-Resolution zwischen zwei Aufrufen verändert werden. Direkt nach dem
        // Reload stammen Lostöpfe UND Ergebnisse garantiert aus demselben, aktuellen Snapshot — exakt die
        // Daten, die danach auch als "echtes" Ergebnis angezeigt werden (Fund aus #303-Test: TV-Modus-
        // Endanzeige wich bei Gruppenphase nach vorherigem Zurücksetzen vom echten Ergebnis ab). Deshalb
        // auch VOR der Turnierbaum-Aktualisierung, die auf demselben DbContext weiterarbeitet.
        var vorlage = tvModus ? ZiehungsVorlageAus(detail) : ZiehungsVorlage.Keine;
        await TurnierbaumUndSeitenlisteAktualisierenAsync(detail.TurnierInstanzId);
        return vorlage;
    }

    private ZiehungsVorlage ZiehungsVorlageAus(Auslosung detail) =>
        Ergebnis.IstGegnerMatrixergebnis
            ? new(GegnerMatrixBuilder.Aufbauen(detail, _qualifikanten), null)
            : new(null, ZiehungsReihenfolge.Aufbauen(detail.Lostoepfe, detail.Ergebnisse, _zufallsquelle.Erzeugen()));

    private async Task TurnierbaumUndSeitenlisteAktualisierenAsync(int turnierInstanzId)
    {
        using (_logger.Vorgang($"Auslosung: Turnierbaum aktualisieren (Instanz {turnierInstanzId})"))
            await _baumService.NachSpielplanAenderungAktualisierenAsync(turnierInstanzId);
        await AktualisiereSeitenlisteAsync();
    }

    private async Task ZiehungZeigenAsync(ZiehungsVorlage vorlage, bool manuell)
    {
        if (vorlage.Matrizen is not null)
            await new LigaphasenZiehungsAblauf(Ziehung, vorlage.Matrizen, _zufallsquelle.Erzeugen()).AbspielenAsync(manuell);
        else if (vorlage.Schritte is not null)
            await new GruppenKoZiehungsAblauf(Ziehung, vorlage.Schritte, Arbeitsbereich.Lostoepfe, _qualifikanten)
                .AbspielenAsync(manuell);
    }

    // ── Ziehung von Hand (#482) ───────────────────────────────────────────────
    // Wie beim Losen: Rückfrage zum Spielplan, dann die Vorbereitung des Service (Gruppenköpfe, Prüfung, Regeln) —
    // gespeichert wird erst beim Abschließen, Abbrechen verwirft nur.

    private async Task HandZiehungStartenAsync()
    {
        Fehlermeldung = null;
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (_auslosungDetail is not { } detail) return;
        try
        {
            if (!await ErgebnisVerlustBestaetigtAsync(detail.TurnierRundeId)) return;
            using var vorgang = _logger.Vorgang($"Ziehung von Hand starten (Auslosung {detail.Id})", "Ziehung wird vorbereitet …");
            var ziehung = await _auslosungService.ZiehungStartenAsync(detail.Id, _qualifikanten);
            HandZiehung.Starten(detail.Id, ziehung, _qualifikanten);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    [RelayCommand(CanExecute = nameof(KannHandZiehungAbschliessen))]
    private async Task HandZiehungAbschliessen()
    {
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (HandZiehung.AuslosungId is not { } auslosungId || !HandZiehung.IstFertig) return;
        Fehlermeldung = null;
        using var vorgang = _logger.Vorgang($"Ziehung von Hand abschließen (Auslosung {auslosungId})", "Auslosung wird gespeichert …");

        try
        {
            var detail = await AbschliessenUndNeuLadenAsync(auslosungId,
                () => _auslosungService.ManuelleErgebnisseAbschliessenAsync(auslosungId, HandZiehung.Ergebnis()));
            // Auch nach einem Fehler: ist die Auslosung schon abgeschlossen, ist auch die Ziehung vorbei.
            if (IstAbgeschlossen) HandZiehung.Beenden();
            if (detail is not null) await TurnierbaumUndSeitenlisteAktualisierenAsync(detail.TurnierInstanzId);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    /// <summary>Führt die abschließende Aktion aus (Auslosen, Ziehung von Hand speichern) und lädt das Detail auch
    /// nach einem Fehler neu: scheitert erst die Spielplan-Generierung, ist die Auslosung bereits abgeschlossen —
    /// die Anzeige zeigte sonst weiter den Stand davor. Liefert das neu geladene Detail, oder <c>null</c> nach
    /// einem Fehler (steht dann in <see cref="Fehlermeldung"/>) bzw. wenn das Detail nicht mehr diese Auslosung ist.</summary>
    private async Task<Auslosung?> AbschliessenUndNeuLadenAsync(int auslosungId, Func<Task> aktion)
    {
        Exception? fehler = null;
        try { await aktion(); }
        catch (Exception ex) { fehler = ex; }

        await LadeDetailAsync(auslosungId);
        if (fehler is not null)
        {
            Fehlermeldung = fehler.Message;
            return null;
        }
        return _auslosungDetail is { } detail && detail.Id == auslosungId ? detail : null;
    }

    [RelayCommand(CanExecute = nameof(KannHandZiehungAbbrechen))]
    private async Task HandZiehungAbbrechen()
    {
        if (!await HandZiehungVerwerfenBestaetigtAsync()) return;

        // Beim Start kann ein Gastgeber in Topf 1 gekommen sein (#487) — die Lostöpfe zeigen dann den neuen Stand.
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (_auslosungDetail is not { } detail) return;
        try { await LadeDetailAsync(detail.Id); }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    /// <summary>Rückfrage, bevor eine begonnene Ziehung von Hand verloren geht; bei „Ja“ endet sie. Vor dem ersten
    /// Zug endet sie ohne Rückfrage — es geht nichts verloren.</summary>
    private async Task<bool> HandZiehungVerwerfenBestaetigtAsync()
    {
        if (HandZiehung.IstBegonnen && !await _dialogService.BestaetigenAsync(
                "Ziehung von Hand verwerfen",
                "Die Ziehung ist noch nicht abgeschlossen – die bisher gezogenen Teams gehen verloren. Wirklich fortfahren?"))
            return false;
        HandZiehung.Beenden();
        return true;
    }

    private bool KannHandZiehungAbschliessen() => HandZiehung.IstFertig;
    private bool KannHandZiehungAbbrechen()    => HandZiehung.Laeuft;

    private void BeiHandZiehungAenderung(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HandZiehungViewModel.IstFertig))
            HandZiehungAbschliessenCommand.NotifyCanExecuteChanged();
        if (e.PropertyName != nameof(HandZiehungViewModel.Laeuft)) return;

        OnPropertyChanged(nameof(ZeigeVorbereitung));
        OnPropertyChanged(nameof(ZeigeBasisdaten));
        OnPropertyChanged(nameof(RibbonZusatzGruppen));
        HandZiehungAbbrechenCommand.NotifyCanExecuteChanged();
        AuslosungAusfuehrenCommand.NotifyCanExecuteChanged();
    }

    private Task<bool> ErgebnisVerlustBestaetigtAsync(int rundeId) =>
        SpielplanErsetzenRueckfrage.ErgebnisVerlustBestaetigtAsync(
            _spielplanService, _dialogService, rundeId, "Auslosung durchführen", "Die Auslosung", "auslosen");

    /// <summary>Alle Rückfragen, bevor die gezeigte Auslosung verlassen wird (Menüpunkt, Reiter, Turnierwahl, andere
    /// Auslosung, "Neu"): eine laufende Ziehung, dann ein geänderter Teilnehmerpool.</summary>
    public async Task<bool> VerlassenBestaetigtAsync() =>
        await ZiehungBeendenNachRueckfrageAsync() && await PoolVerwerfenBestaetigtAsync();

    /// <summary>Rückfrage, bevor ein geänderter Teilnehmerpool verloren geht (#576) — er lebt nicht in der Datenbank,
    /// gespeichert ist nur, was in den Töpfen liegt. Darum keine Speichern-Abfrage, nur fortfahren oder bleiben (wie die
    /// Vorbelegung). Ohne Änderung synchron <c>true</c>.</summary>
    private async Task<bool> PoolVerwerfenBestaetigtAsync()
    {
        if (!ZeigeVorbereitung || !Teilnehmerpool.HatUngespeicherteAenderungen) return true;

        return await _dialogService.BestaetigenAsync(
            "Teilnehmerpool verwerfen",
            "Der Teilnehmerpool ist geändert, aber noch nicht alle seine Teams liegen in den Töpfen. Gespeichert ist nur, " +
            "was in den Töpfen liegt – die übrigen Änderungen gehen verloren. Wirklich fortfahren?");
    }

    /// <summary>Rückfrage, bevor eine laufende TV-Ziehung verlassen wird (Menüpunkt, Reiter, Turnierwahl, andere
    /// Auslosung, "Neu") — bei "Ja" endet die Ziehung sofort. Ohne laufende Ziehung synchron <c>true</c>.</summary>
    private async Task<bool> ZiehungBeendenNachRueckfrageAsync()
    {
        if (HandZiehung.Laeuft) return await HandZiehungVerwerfenBestaetigtAsync();
        if (!Ziehung.AnimationLaeuft) return true;

        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Ziehung läuft",
            "Das beendet die laufende Ziehung. Auslosung und Spielplan stehen bereits fest – das Ergebnis wird " +
            "dann ohne weitere Ziehung angezeigt. Wirklich fortfahren?");
        if (bestaetigt) Ziehung.Beenden();
        return bestaetigt;
    }

    [RelayCommand(CanExecute = nameof(KannAuslosungZuruecksetzen))]
    private async Task AuslosungZuruecksetzen()
    {
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (_auslosungDetail is not { } detail) return;
        Fehlermeldung = null;
        try
        {
            // Rückfrage nur, wenn tatsächlich Daten betroffen sind (#405) — frisch ausgelost und sofort
            // zurückgesetzt bleibt ohne Dialog.
            SpielplanVerlust verlust;
            using (_logger.Vorgang($"Auslosung zurücksetzen: Vorschau (Auslosung {detail.Id})"))
                verlust = await _verlustVorschau.ErmittelnAsync(detail.TurnierInstanzId, detail.TurnierRundeId);
            if (verlust.DatenBetroffen && !await _dialogService.BestaetigenAsync(
                    "Auslosung zurücksetzen",
                    $"Soll '{detail.Name}' wirklich zurückgesetzt werden?{verlust.Hinweis}"))
                return;

            // Erst ab hier messen — die Wartezeit auf die Rückfrage gehört nicht zur Dauer der Aktion.
            using var vorgang = _logger.Vorgang($"Auslosung zurücksetzen (Auslosung {detail.Id})", "Auslosung wird zurückgesetzt …");
            await _auslosungService.ZuruecksetzenAsync(detail.Id);
            await _baumService.NachSpielplanAenderungAktualisierenAsync(detail.TurnierInstanzId);
            await LadeDetailAsync(detail.Id);
            await AktualisiereSeitenlisteAsync();
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    // ── Interne Hilfsmethoden ─────────────────────────────────────────────────

    partial void OnAusgewaehlteAuslosungChanged(Auslosung? value)
    {
        // #631, Fall 1: zeigt im Log, ob die Bindung die Auswahl beim Tausch der Seitenliste kurz auf null setzt.
        // Befristet: diese und die anderen Auswahl-Logzeilen fallen mit dem Abschluss von #631 wieder weg (die
        // Vorgänge „Seitenliste laden“ und „Ranglisten laden“ bleiben).
        _logger.LogInformation(
            "Auswahl Auslosung: {Id} (Seitenliste wird getauscht: {Guard}, Detail {DetailId}, Detail offen {DetailAktiv})",
            value?.Id, _seitenlisteAktualisierungAktiv, _auslosungDetail?.Id, DetailAktiv);
        _ = BeiAuslosungAenderungAsync();
    }

    private async Task BeiAuslosungAenderungAsync()
    {
        // AktualisiereSeitenlisteAsync() tauscht die ListView-ItemsSource (neue Auslosungen-Collection-
        // Instanz) aus, BEVOR sie AusgewaehlteAuslosung passend zur neuen Collection neu zuweist — die
        // TwoWay-SelectedItem-Bindung nullt AusgewaehlteAuslosung dabei transient (WinUI3-Fallstrick, siehe
        // WinUi/CLAUDE.md "Collection.Clear() vor async Abfrage nullt gebundene Selektion"), bevor die
        // korrekte Zuweisung nachkommt. Ohne diesen Guard setzt genau dieses transiente Null DetailAktiv auf
        // false, und da die anschließende korrekte Zuweisung dieselbe Id hat, greift unten der Id-Vergleich
        // und lädt NICHT neu — DetailAktiv bliebe fälschlich false (Fokusverlust, Fund aus #318-Nachtest).
        if (_seitenlisteAktualisierungAktiv) return;

        if (!await AuswahlwechselErlaubtAsync())
        {
            _logger.LogInformation("Auswahlwechsel nicht bestätigt, Auswahl zurück auf Auslosung {Id}.", _auslosungDetail?.Id);
            AuswahlZurueckstellen();
            return;
        }

        NeuFormAktiv = false;
        if (AusgewaehlteAuslosung is null)
        {
            _logger.LogInformation("Keine Auslosung ausgewählt, Detail geschlossen.");
            Ziehung.Beenden(); HandZiehung.Beenden(); DetailAktiv = false;
            return;
        }

        // AktualisiereSeitenlisteAsync() weist AusgewaehlteAuslosung nach einem Reload der Seitenliste
        // erneut zu (neue Objektinstanz, gleiche Id) — das würde hier ohne diesen Guard einen bereits
        // frisch geladenen Detail-Stand ein zweites Mal nachladen (sichtbares Flackern, Issue #318).
        // Nur bei offenem Detail: nach einem Reset (z.B. InstanzSetzenAsync beim Reiter-Wechsel) steht
        // _auslosungDetail noch auf der alten Id, und erneutes Auswählen derselben Auslosung zeigte sonst nichts (#347).
        if (DetailAktiv && _auslosungDetail?.Id == AusgewaehlteAuslosung.Id)
        {
            _logger.LogInformation("Auslosung {Id} ist schon geöffnet, kein Neuladen.", AusgewaehlteAuslosung.Id);
            return;
        }

        Ziehung.Beenden();
        HandZiehung.Beenden();
        await AusgewaehltesDetailLadenAsync(AusgewaehlteAuslosung.Id);
    }

    /// <summary>Andere Auslosung während der TV-Ziehung (#406) oder mit geändertem Teilnehmerpool (#576) angeklickt:
    /// erst nach Rückfrage wechseln.</summary>
    private async Task<bool> AuswahlwechselErlaubtAsync() =>
        AusgewaehlteAuslosung?.Id == _auslosungDetail?.Id || await VerlassenBestaetigtAsync();

    /// <summary>Stellt die Auswahl auf die gezeigte Auslosung zurück — unter dem Guard der Seitenliste, damit
    /// diese Zuweisung nicht erneut als Auswahlwechsel ankommt.</summary>
    private void AuswahlZurueckstellen()
    {
        _seitenlisteAktualisierungAktiv = true;
        try { AusgewaehlteAuslosung = Auslosungen.FirstOrDefault(a => a.Id == _auslosungDetail?.Id); }
        finally { _seitenlisteAktualisierungAktiv = false; }
    }

    /// <summary>Einstiegspunkt des Auswahlwechsels (Fire-and-Forget): reiht sich hinter einer laufenden Aktion
    /// oder einem Turnierwechsel ein und zeigt Fehler an, statt sie zu verschlucken (#413). Nach dem Warten
    /// zählt der dann aktuelle Stand — die Auswahl kann weitergewandert oder das Detail inzwischen von der
    /// Aktion selbst geladen sein.</summary>
    private async Task AusgewaehltesDetailLadenAsync(int auslosungId)
    {
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (AusgewaehlteAuslosung?.Id != auslosungId || (DetailAktiv && _auslosungDetail?.Id == auslosungId))
        {
            _logger.LogInformation(
                "Auslosung {Id} nach dem Warten nicht geladen: Auswahl {AuswahlId}, Detail {DetailId}, Detail offen {DetailAktiv}.",
                auslosungId, AusgewaehlteAuslosung?.Id, _auslosungDetail?.Id, DetailAktiv);
            return;
        }
        try { await LadeDetailAsync(auslosungId); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auslosung {AuslosungId} konnte nicht geladen werden.", auslosungId);
            Fehlermeldung = ex.Message;
        }
    }

    /// <summary>Lädt die Auslosung und zeigt sie im Detailbereich. Die Reihenfolge der Schritte ist fachlich
    /// gebunden: die <see cref="_ladenAktiv"/>-Klammer umschließt alles, was Detail-Felder setzt (deren
    /// Change-Hooks werteten das sonst als Bearbeitung), die Regeln stehen vor den Kopfdaten (das Setzen der
    /// Auslosungsart aktualisiert die Regel-Sperren), und "geändert" wird zuletzt zurückgenommen.</summary>
    private async Task LadeDetailAsync(int auslosungId)
    {
        using var vorgang = _logger.Vorgang($"Auslosung laden (Auslosung {auslosungId})", "Auslosung wird geladen …");

        _ladenAktiv = true;
        try
        {
            var detail = await _auslosungService.FindenNachIdAsync(auslosungId);
            if (detail is null) return;
            _auslosungDetail = detail;

            AuslosungsTeilnehmer teilnehmer;
            using (_logger.Vorgang($"Auslosung: Teilnehmer ermitteln, Platzhalter auflösen (Auslosung {auslosungId})"))
                teilnehmer = await _teilnehmerpool.ErmittelnAsync(detail, _instanz, InstanzRunden);
            _qualifikanten = teilnehmer.Qualifikanten;
            _teilnehmer = teilnehmer;
            await Teilnehmerpool.ZeigenAsync(detail, teilnehmer.Teilnehmer, Arbeitsbereich.RanglisteIdFuer(detail), Mannschaftsart);
            ToepfeAnPoolAnpassen(detail);
            _regeln.Zeigen(detail, InstanzRunden);
            Ergebnis.Zeigen(detail, _qualifikanten);
            KopfdatenZeigen(detail);

            DetailAktiv    = true;
            IstDetailDirty = false;
            Fehlermeldung  = null;
        }
        finally
        {
            _ladenAktiv = false;
        }
    }

    private void KopfdatenZeigen(Auslosung detail)
    {
        IstBearbeitbar        = detail.Status is not (AuslosungsStatus.Abgeschlossen or AuslosungsStatus.Abgebrochen);
        IstAbgeschlossen      = detail.Status == AuslosungsStatus.Abgeschlossen;
        DetailName            = detail.Name;
        DetailAuslosungsart   = detail.Auslosungsart;
        DetailAuslosungsartOptionen = AuslosungAnzeigeTexte.AuslosungsartOptionen(detail.TurnierRunde?.Rundenart);
        // Siehe NeueAuslosungViewModel.OnRundeChanged — erzwungene Nachbenachrichtigung gegen den WinUI3-ComboBox-Reset-
        // Fallstrick (Fund aus #300-Test: Auslosungsart erschien nach Anlage einer Auslosung leer).
        OnPropertyChanged(nameof(DetailAuslosungsart));
        DetailGruppenposition.Zeigen(detail.Gruppenposition, detail.TurnierRunde?.Rundenart, detail.Auslosungsart);
        DetailAnzahlLostoepfe = Math.Max(1, detail.Lostoepfe.Count);

        // Bei „Manuell“ ohne TV-Modus (#482).
        OnPropertyChanged(nameof(RibbonZusatzGruppen));

        AuslosungKopfzeile = $"{detail.Name}  ·  {detail.TurnierRunde.Name}  ·  " +
                             $"{AuslosungAnzeigeTexte.AuslosungsartAnzeigeName(detail.Auslosungsart)}  ·  " +
                             $"Status: {AuslosungAnzeigeTexte.StatusAnzeigeName(detail.Status)}";
    }

    private async Task AktualisiereSeitenlisteAsync()
    {
        if (_instanz is null) return;
        var neueListe = await LadeAuslosungenGefiltertAsync(_instanz.Id);

        // Guard gegen den transienten Null-Durchlauf, den die TwoWay-SelectedItem-Bindung beim
        // ItemsSource-Tausch auslöst (siehe BeiAuslosungAenderungAsync) — ohne ihn würde dieser Reload den
        // bereits offenen Detail-Workspace kurz schließen (DetailAktiv=false) und nicht wieder öffnen.
        _seitenlisteAktualisierungAktiv = true;
        try
        {
            Auslosungen = neueListe;
            if (_auslosungDetail is not null)
                AusgewaehlteAuslosung = Auslosungen.FirstOrDefault(a => a.Id == _auslosungDetail.Id);
        }
        finally { _seitenlisteAktualisierungAktiv = false; }
    }

    /// <summary>Die Vorbelegungen (Runden mit „Paarung fest vorgegeben“) werden hier nicht angezeigt — sie leben
    /// ausschließlich im Vorbelegung-Tab.</summary>
    private async Task<ObservableCollection<Auslosung>> LadeAuslosungenGefiltertAsync(int turnierInstanzId)
    {
        var auslosungen = await _auslosungService.AlleNachInstanzAbrufenAsync(turnierInstanzId);

        // Issue #320: _belegteRundenIds bewusst aus der UNGEFILTERTEN Liste (inkl. Vorbelegungen) befüllen —
        // das Neu-Formular muss auch Runden mit einer hier unsichtbaren Vorbelegung ausblenden.
        _belegteRundenIds = auslosungen.Select(a => a.TurnierRundeId).ToHashSet();

        return new ObservableCollection<Auslosung>(
            auslosungen.Where(a => !VorbelegungsZustaendigkeit.GehoertZurVorbelegung(a)));
    }

    private bool KannNeu()       => !NeuFormAktiv && _instanz is not null && _instanz.Status != TurnierStatus.Vorlage;
    private bool KannSpeichern() => (NeuFormAktiv && NeuFormular.Runde is not null)
                                 || (IstDetailDirty && IstBearbeitbar);
    private bool KannAbbrechen() => NeuFormAktiv || IstDetailDirty;

    public bool HatUngespeicherteAenderungen => NeuFormAktiv || IstDetailDirty;
    private bool KannLoeschen()  => AusgewaehlteAuslosung is not null && !NeuFormAktiv;

    private bool KannAuslosungAusfuehren()             => DetailAktiv && IstBearbeitbar && !HandZiehung.Laeuft;
    private bool KannAuslosungZuruecksetzen()           => DetailAktiv && IstAbgeschlossen && !Ziehung.AnimationLaeuft;

    partial void OnDetailAktivChanged(bool value)    => BearbeitbarWeitergeben();
    partial void OnIstBearbeitbarChanged(bool value) => BearbeitbarWeitergeben();

    private void BearbeitbarWeitergeben()
    {
        Arbeitsbereich.BearbeitbarSetzen(DetailAktiv && IstBearbeitbar);
        Teilnehmerpool.BearbeitbarSetzen(DetailAktiv && IstBearbeitbar && !_befuellungLaeuft);
    }

    // ── Teilnehmerpool (#576) ─────────────────────────────────────────────────

    private MannschaftsartFilter Mannschaftsart => _instanz?.MannschaftsartFilter ?? MannschaftsartFilter.Alle;

    /// <summary>Die Lostöpfe bieten bei aktivem Pool nur seine Teams an, und die Automatik wartet, bis er seine Quoten
    /// erfüllt.</summary>
    private void ToepfeAnPoolAnpassen(Auslosung detail)
    {
        if (_teilnehmer is null) return;
        Arbeitsbereich.BefuellenSperren(!Teilnehmerpool.Erfuellt);
        Arbeitsbereich.Zeigen(detail, _teilnehmer with { Teilnehmer = Teilnehmerpool.FuerToepfe(_teilnehmer.Teilnehmer) }, Mannschaftsart);
    }

    /// <summary>Wer den Pool verlassen hat und noch in einem Topf liegt, verlässt auch den Topf — das lädt die Auslosung
    /// neu. Sonst genügt es, die Verfügbar-Liste der Lostöpfe nachzuziehen.</summary>
    async Task ITeilnehmerpoolKontext.PoolGeaendertAsync(IReadOnlyCollection<int> herausgenommen)
    {
        if (herausgenommen.Count > 0)
            await Arbeitsbereich.MannschaftenAusToepfenNehmenAsync(herausgenommen);
        else if (_auslosungDetail is { } detail)
            ToepfeAnPoolAnpassen(detail);
    }

    /// <summary>Fire-and-Forget aus dem Wechsel der Rangliste: reiht sich hinter laufende Aktionen ein (#413).</summary>
    private async Task PoolRanglisteSetzenAsync(int? ranglisteId)
    {
        using var schutz = await DbContextSchutz.BetretenAsync();
        try { await Teilnehmerpool.RanglisteSetzenAsync(ranglisteId); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rangliste {RanglisteId} für den Teilnehmerpool konnte nicht geladen werden.", ranglisteId);
            Fehlermeldung = ex.Message;
        }
    }

    partial void OnDetailNameChanged(string value)           { if (!_ladenAktiv) IstDetailDirty = true; }
    partial void OnDetailAuslosungsartChanged(Auslosungsart value)
    {
        if (!_ladenAktiv) IstDetailDirty = true;
        _regeln.SperrenAktualisieren(value);
        DetailGruppenposition.SperreAktualisieren(_auslosungDetail?.TurnierRunde?.Rundenart, value);

        if (!DetailAnzahlLostoepfeBearbeitbar)
            DetailAnzahlLostoepfe = 1;
    }

    partial void OnDetailAnzahlLostoepfeChanged(int value)   { if (!_ladenAktiv) IstDetailDirty = true; }
}
