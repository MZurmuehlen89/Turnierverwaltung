using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Application.Turniere;
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
/// <see cref="GegnerMatrixViewModel"/> (#401). Bewusst OHNE <see cref="Auslosungsart.Manuell"/> (bleibt beim
/// <see cref="VorbelegungViewModel"/>, #297 — keine Dopplung).
///
/// Anders als das WPF-Original hält dieses ViewModel KEINE eigene Instanzliste/-auswahl — die geteilte
/// Instanzauswahl kommt von <see cref="TurnierdurchfuehrungViewModel"/> über <see cref="InstanzSetzenAsync"/>
/// (Precedent: <see cref="VorbelegungViewModel.InstanzSetzenAsync"/>), analog zum <see cref="TurnierTabKoordinator"/>-
/// Muster aus #297. Bewusst auf Turniere mit Status ≠ <see cref="TurnierStatus.Vorlage"/> beschränkt (siehe
/// <see cref="KannNeu"/>) — anders als Vorbelegung zieht die echte Auslosung primär gegen reale Mannschaften.
/// </summary>
public partial class AuslosungViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen,
    ILostopfArbeitsbereichKontext
{
    private readonly AuslosungService _auslosungService;
    private readonly TurnierInstanzService _instanzService;
    private readonly MannschaftService _mannschaftService;
    private readonly SpielplanService _spielplanService;
    private readonly TurnierBaumService _baumService;
    private readonly VerbandsQuoteService _verbandsQuoteService;
    private readonly LandesQuoteService _landesQuoteService;
    private readonly UebergangsregelService _uebergangsregelService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<AuslosungViewModel> _logger;

    private TurnierInstanz? _instanz;
    private Auslosung? _auslosungDetail;
    private bool _ladenAktiv;
    private bool _seitenlisteAktualisierungAktiv;

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

    // ── Geteilter Instanz-Kontext (siehe Klassendoku) ────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NeuCommand))]
    private bool _keinTurnierGewaehlt = true;

    [ObservableProperty] private ObservableCollection<Auslosung> _auslosungen = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private Auslosung? _ausgewaehlteAuslosung;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeuRundenOptionen))]
    private ObservableCollection<TurnierRunde> _instanzRunden = [];

    /// <summary>Runden mit PaarungFestVorgegeben=true werden im Vorbelegung-Tab verwaltet, nicht hier. Runden
    /// mit bereits vorhandener Auslosung (auch einer im Vorbelegung-Tab abgeschlossenen, hier unsichtbaren
    /// Manuell-Auslosung — siehe <see cref="_belegteRundenIds"/>) werden ebenfalls ausgeblendet: sonst könnte
    /// eine zweite Auslosung für dieselbe Runde angelegt werden, was den Vorbelegung-Tab beim
    /// ToDictionary(a => a.TurnierRundeId)-Aufbau mit einer ArgumentException abstürzen lässt (Issue #320).</summary>
    public IEnumerable<TurnierRunde> NeuRundenOptionen =>
        InstanzRunden.Where(r => !r.PaarungFestVorgegeben && !_belegteRundenIds.Contains(r.Id));

    /// <summary>TurnierRundeId aller Auslosungen der Instanz — inkl. der hier absichtlich ausgeblendeten
    /// Auslosungsart.Manuell (siehe <see cref="LadeAuslosungenGefiltertAsync"/>), damit
    /// <see cref="NeuRundenOptionen"/> auch solche Runden zuverlässig ausschließt (Issue #320).</summary>
    private HashSet<int> _belegteRundenIds = [];

    // ── Neue-Auslosung-Formular ────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigtHinweis))]
    [NotifyCanExecuteChangedFor(nameof(NeuCommand))]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private bool _neuFormAktiv;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private string _neuName = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AuslosungsartHinweis))]
    [NotifyPropertyChangedFor(nameof(AuslosungsartWarnung))]
    [NotifyPropertyChangedFor(nameof(NeuAnzahlLostoepfeBearbeitbar))]
    [NotifyPropertyChangedFor(nameof(NeuAuslosungsartAusgewaehlt))]
    private Auslosungsart _neuAuslosungsart = Auslosungsart.Zufall;

    /// <summary>Wrapper für die ComboBox-Bindung: <c>SelectedItem</c> statt <c>SelectedValue</c>/
    /// <c>SelectedValuePath</c> — Letzteres zeigte in #300-Tests wiederholt einen WinUI3-Fallstrick (Auswahl
    /// erschien nach Reload/Sichtbarkeitswechsel leer, obwohl der zugrunde liegende Wert korrekt war), auch
    /// nach gezielter Nachbenachrichtigung. <c>SelectedItem</c> gegen den Options-Record (Werte-Gleichheit
    /// dank <c>record</c>) ist der im Rest der App bereits bewährte, robustere Mechanismus.</summary>
    public AuslosungsartOption? NeuAuslosungsartAusgewaehlt
    {
        get => NeuAuslosungsartOptionen.FirstOrDefault(o => o.Wert == NeuAuslosungsart);
        set { if (value is not null) NeuAuslosungsart = value.Wert; }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyPropertyChangedFor(nameof(AuslosungsartHinweis))]
    [NotifyPropertyChangedFor(nameof(AuslosungsartWarnung))]
    private TurnierRunde? _neuRunde;

    /// <summary>Gecachte Optionsliste statt einer bei jedem Binding-Zugriff neu berechneten Liste — eine
    /// dauerhaft neue Listeninstanz pro Lesezugriff ist ein bekannter WinUI3-ComboBox-Fallstrick
    /// (siehe WinUi/CLAUDE.md, "ItemsSource pro Fokus neu berechnet"): kombiniert mit
    /// <c>SelectedValuePath</c> kann das zu einem transienten Reset führen, der die eigentlich korrekte
    /// Auswahl visuell verwaist zurücklässt (Fund aus #300-Test — "Auslosungsart erscheint leer trotz
    /// vorheriger Auswahl"). Wird explizit in <see cref="OnNeuRundeChanged"/> aktualisiert.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeuAuslosungsartAusgewaehlt))]
    private IReadOnlyList<AuslosungsartOption> _neuAuslosungsartOptionen = [];

    private string _autoNeuName = "";

    [ObservableProperty] private int _neuAnzahlLostoepfe = 1;

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
    private bool _istBearbeitbar;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailAuslosungsartHinweis))]
    [NotifyPropertyChangedFor(nameof(DetailAuslosungsartWarnung))]
    [NotifyPropertyChangedFor(nameof(DetailAnzahlLostoepfeBearbeitbar))]
    [NotifyPropertyChangedFor(nameof(DetailAuslosungsartAusgewaehlt))]
    [NotifyCanExecuteChangedFor(nameof(AuslosungAusfuehrenCommand))]
    private Auslosungsart _detailAuslosungsart;

    /// <summary>Gecacht, siehe <see cref="NeuAuslosungsartOptionen"/> — aktualisiert in <see cref="LadeDetailAsync"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailAuslosungsartAusgewaehlt))]
    private IReadOnlyList<AuslosungsartOption> _detailAuslosungsartOptionen = [];

    /// <summary>Wrapper für die ComboBox-Bindung, siehe <see cref="NeuAuslosungsartAusgewaehlt"/>.</summary>
    public AuslosungsartOption? DetailAuslosungsartAusgewaehlt
    {
        get => DetailAuslosungsartOptionen.FirstOrDefault(o => o.Wert == DetailAuslosungsart);
        set { if (value is not null) DetailAuslosungsart = value.Wert; }
    }

    [ObservableProperty] private string _detailName = "";
    [ObservableProperty] private int _detailAnzahlLostoepfe = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    private bool _istDetailDirty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstPaarungsergebnis))]
    [NotifyPropertyChangedFor(nameof(ZeigeErgebnis))]
    [NotifyCanExecuteChangedFor(nameof(AuslosungZuruecksetzenCommand))]
    private bool _istAbgeschlossen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstPaarungsergebnis))]
    private bool _istGruppenergebnis;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstPaarungsergebnis))]
    private bool _istGegnerMatrixergebnis;

    public bool IstPaarungsergebnis => IstAbgeschlossen && !IstGruppenergebnis && !IstGegnerMatrixergebnis;

    /// <summary>Gegner-Matrix der Ligaphase samt Team-Fokus (#362).</summary>
    public GegnerMatrixViewModel GegnerMatrix { get; } = new();

    [ObservableProperty] private string _auslosungKopfzeile = "";

    /// <summary>Verfügbar-Liste, Lostöpfe und deren Befüllung (Kind-ViewModel).</summary>
    public LostopfArbeitsbereichViewModel Arbeitsbereich { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private ObservableCollection<RegelViewModel> _regeln = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigeVerbandsQuoten))]
    private ObservableCollection<VerbandsQuoteAnzeigeViewModel> _verbandsQuotenAnzeige = [];

    public bool ZeigeVerbandsQuoten => VerbandsQuotenAnzeige.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigeLandesQuoten))]
    private ObservableCollection<LandesQuoteAnzeigeViewModel> _landesQuotenAnzeige = [];

    public bool ZeigeLandesQuoten => LandesQuotenAnzeige.Count > 0;

    [ObservableProperty] private ObservableCollection<ErgebnisAnzeige> _ergebnisseAnzeige = [];
    [ObservableProperty] private ObservableCollection<GruppenErgebnisAnzeige> _gruppenErgebnisse = [];

    // ── TV-Modus: Schrittweise Ziehungsanimation (#303, Issue #5) ────────────────
    /// <summary>Wie das Ergebnis nach dem Auslosen erscheint: sofort oder als TV-Ziehung.</summary>
    private enum ZiehungsArt { Keine, Automatisch, Manuell }

    /// <summary>Live-Anzeige der TV-Ziehung (Kind-ViewModel, ohne Datenbankzugriff).</summary>
    public ZiehungViewModel Ziehung { get; } = new();

    /// <summary>Zwischen dem Auslosen und dem Start der TV-Ziehung (Spielplan-Generierung, #406) — hält das
    /// schon geladene Ergebnis verborgen, damit es nicht vor der Ziehung aufblitzt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigeErgebnis))]
    private bool _ziehungInVorbereitung;

    /// <summary>Ergebnis-Panel bleibt während der Ziehungsanimation verborgen — das bereits von
    /// LadeDetailAsync geladene Endergebnis wird erst nach Animationsende (bzw. bei "Überspringen" sofort)
    /// sichtbar.</summary>
    public bool ZeigeErgebnis => IstAbgeschlossen && Ziehung.AnimationLaeuftNicht && !ZiehungInVorbereitung;

    // ── Status ────────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    // ── Berechnete Eigenschaften ─────────────────────────────────────────────
    // Auslosungsart.Manuell wird hier nicht angeboten — die Paarungsfestlegung für Runden mit von vornherein
    // feststehenden Paarungen lebt im Vorbelegung-Tab (VorbelegungViewModel/VorbelegungWorkspace, #297).
    private static IReadOnlyList<AuslosungsartOption> AuslosungsartOptionenFuer(Rundenart? rundenart) =>
        Enum.GetValues<Auslosungsart>()
            .Where(a => a != Auslosungsart.Manuell)
            .Where(a => rundenart is null || AuslosungsKompatibilitaet.IstKompatibel(rundenart.Value, a))
            .Select(a => new AuslosungsartOption(AuslosungAnzeigeTexte.AuslosungsartAnzeigeName(a), a))
            .ToList();

    public string AuslosungsartHinweis         => AuslosungAnzeigeTexte.AuslosungsartHinweisText(NeuAuslosungsart);
    public string? AuslosungsartWarnung        => AuslosungAnzeigeTexte.AuslosungsartWarnungText(NeuAuslosungsart, NeuRunde?.Rundenart);
    public string DetailAuslosungsartHinweis   => AuslosungAnzeigeTexte.AuslosungsartHinweisText(DetailAuslosungsart);
    public string? DetailAuslosungsartWarnung  => AuslosungAnzeigeTexte.AuslosungsartWarnungText(DetailAuslosungsart, _auslosungDetail?.TurnierRunde?.Rundenart);

    /// <summary>Zufall/Benutzerdefiniert kennen keine Topf-Struktur und sind auf genau 1 Lostopf begrenzt (Issue #134).</summary>
    private static bool ErlaubtMehrereToepfe(Auslosungsart art) =>
        art is not (Auslosungsart.Zufall or Auslosungsart.Benutzerdefiniert);

    public bool NeuAnzahlLostoepfeBearbeitbar    => ErlaubtMehrereToepfe(NeuAuslosungsart);
    public bool DetailAnzahlLostoepfeBearbeitbar => ErlaubtMehrereToepfe(DetailAuslosungsart);

    // ── IRibbonAware ──────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Auslosung";
    public ICommand NeuBefehl => NeuCommand;
    public ICommand SpeichernBefehl => SpeichernCommand;
    public ICommand AbbrechenBefehl => AbbrechenCommand;
    public ICommand LoeschenBefehl => LoeschenCommand;

    // Zwei Plätze (#400): vorn je nach Phase die Vorbereitung oder — während der laufenden TV-Ziehung — die
    // Live-Steuerung, dahinter immer die Ziehung. Der Tausch statt einer zusätzlichen dritten Gruppe hält die
    // Leiste ruhig: die Ziehungs-Buttons bleiben stehen, Vorbereitung ist während der Ziehung ohnehin nutzlos.
    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen =>
    [
        Ziehung.AnimationLaeuft ? LiveSteuerungGruppe() : VorbereitungGruppe(),
        new RibbonGruppe
        {
            Header = "Ziehung",
            Aktionen =
            [
                new RibbonAktion { Header = "Auslosung durchführen", Befehl = AuslosungAusfuehrenCommand, IconGlyph = "" },
                new RibbonAktion
                {
                    Header = "TV-Modus",
                    Unteraktionen =
                    [
                        new RibbonUnteraktion("Mit TV-Modus", AuslosungMitTvModusAusfuehrenCommand),
                        new RibbonUnteraktion("Mit TV-Modus manuell", AuslosungMitTvModusManuellAusfuehrenCommand),
                    ],
                    IconGlyph = "",
                },
                // Bewusst am Gruppenende statt direkt neben "Auslosung durchführen" — beide sind zwar nie zugleich
                // aktiv (bearbeitbar vs. abgeschlossen), sollen aber auch optisch nicht nebeneinander liegen.
                new RibbonAktion { Header = "Zurücksetzen", Befehl = AuslosungZuruecksetzenCommand, IconGlyph = "" },
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
                Unteraktionen =
                [
                    new RibbonUnteraktion("Automatisch befüllen", Arbeitsbereich.LostoepfeAutomatischBefuellenCommand),
                    new RibbonUnteraktion("Alle leeren", Arbeitsbereich.AlleToepfeZuruecksetzenCommand),
                ],
                IconGlyph = "",
            },
            new RibbonAktion
            {
                Header = "Auslosungs-Regeln",
                ToggleUnterpunkte = Regeln
                    .Select(r => new RibbonToggleAktion(
                        r.Anzeige, r.ImmerAktivHinweis is not null || r.IstAktiv, aktiv => r.IstAktiv = aktiv,
                        r.Beschreibung,
                        SperrHinweis: r.ImmerAktivHinweis ?? r.SperrHinweis,
                        // Die Heimrecht-Regeln schließen sich aus: Anhaken der einen hakt die andere ab (#331).
                        ExklusivGruppe: IstHeimrechtRegel(r.Regelart) ? "Heimrecht" : null))
                    .ToList(),
                IconGlyph = "",
            },
        ],
    };

    /// <summary>Direkte Buttons statt Flyout (#364): während der Live-Ziehung ständig gebraucht. Beide stehen in
    /// beiden TV-Modi da, damit die Leiste gleich bleibt — "Weiter" ist in der automatischen Ziehung lediglich
    /// deaktiviert (wartet nie auf einen Klick).</summary>
    private RibbonGruppe LiveSteuerungGruppe() => new()
    {
        Header = "Live-Steuerung",
        Aktionen =
        [
            new RibbonAktion { Header = "Weiter", Befehl = Ziehung.AnimationWeiterCommand, IconGlyph = "" },
            new RibbonAktion { Header = "Rest sofort anzeigen", Befehl = Ziehung.AnimationUeberspringenCommand, IconGlyph = "" },
        ],
    };

    public AuslosungViewModel(
        AuslosungService auslosungService,
        TurnierInstanzService instanzService,
        MannschaftService mannschaftService,
        SpielplanService spielplanService,
        TurnierBaumService baumService,
        RanglisteService ranglisteService,
        VerbandsQuoteService verbandsQuoteService,
        LandesQuoteService landesQuoteService,
        UebergangsregelService uebergangsregelService,
        IDialogService dialogService,
        ILogger<AuslosungViewModel> logger)
    {
        _auslosungService = auslosungService;
        _instanzService = instanzService;
        _mannschaftService = mannschaftService;
        _spielplanService = spielplanService;
        _baumService = baumService;
        _verbandsQuoteService = verbandsQuoteService;
        _landesQuoteService = landesQuoteService;
        _uebergangsregelService = uebergangsregelService;
        _dialogService = dialogService;
        _logger = logger;
        Arbeitsbereich = new LostopfArbeitsbereichViewModel(this, auslosungService, ranglisteService, instanzService, dialogService);
        Ziehung.PropertyChanged += BeiZiehungAenderung;
    }

    /// <summary>Beginn und Ende der TV-Ziehung wirken auf Ergebnisanzeige, Ribbon-Gruppe und "Zurücksetzen".</summary>
    private void BeiZiehungAenderung(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ZiehungViewModel.AnimationLaeuft)) return;
        OnPropertyChanged(nameof(ZeigeErgebnis));
        OnPropertyChanged(nameof(RibbonZusatzGruppen));
        AuslosungZuruecksetzenCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Aktualisiert den Turnierbaum nach jeder Spielplan-Änderung automatisch (Issue #320-
    /// Folgefund: der Nutzer soll den Baum nie manuell neu generieren müssen). Für Turniere ganz ohne
    /// K.o.-Runden wirft <see cref="TurnierBaumService.GenerierenAsync"/>, was hier bewusst geschluckt wird
    /// (analog SpielplanViewModel.TurnierBaumAktualisierenAsync).</summary>
    private async Task TurnierBaumAktualisierenAsync(int turnierInstanzId)
    {
        try { await _baumService.GenerierenAsync(turnierInstanzId); }
        catch { /* TurnierBaum-Rebuild ist optional — nie Fehler an den Benutzer weitergeben */ }
    }

    /// <summary>Von <see cref="TurnierdurchfuehrungViewModel"/> einmalig beim Aktivieren geladen — analog
    /// <see cref="TurnierkopfViewModel.StammdatenLadenAsync"/>.</summary>
    public Task RanglistenLadenAsync() => Arbeitsbereich.RanglistenLadenAsync();

    /// <summary>Vom Composite gesetzt, wenn sich die geteilte Instanzauswahl ändert (Precedent:
    /// <see cref="VorbelegungViewModel.InstanzSetzenAsync"/>).</summary>
    public async Task InstanzSetzenAsync(TurnierInstanz? instanz)
    {
        using var vorgang = _logger.Vorgang($"Auslosung: Turnier setzen (Instanz {instanz?.Id})");

        Ziehung.Beenden();
        _instanz = instanz;
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

        IstLadend = true;
        try
        {
            Auslosungen = await LadeAuslosungenGefiltertAsync(instanz.Id);
            var detail = await _instanzService.FindenNachIdMitRundenAsync(instanz.Id);
            if (detail is not null)
                InstanzRunden = new ObservableCollection<TurnierRunde>(detail.Runden.OrderBy(r => r.Reihenfolge));
        }
        finally { IstLadend = false; }
    }

    // ── Ribbon-Befehle ────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannNeu))]
    private async Task Neu()
    {
        if (!await ZiehungBeendenNachRueckfrageAsync()) return;
        NeuFormAktiv        = true;
        DetailAktiv         = false;
        _autoNeuName        = "";
        NeuName             = "";
        NeuAnzahlLostoepfe  = 1;
        NeuAuslosungsart    = Auslosungsart.Zufall;
        NeuRunde            = NeuRundenOptionen.FirstOrDefault();
        // Explizit statt nur über OnNeuRundeChanged: bleibt die vorausgewählte Runde dieselbe wie beim letzten
        // Mal, feuert der Hook nicht, und die Vorgabe der Runde (Issue #347) ginge durch den Reset oben verloren.
        NeuAuslosungsart    = StartAuslosungsartFuer(NeuRunde, NeuAuslosungsart);
        Fehlermeldung       = null;
    }

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        Fehlermeldung = null;
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (NeuFormAktiv)
        {
            if (_instanz is null || NeuRunde is null) return;
            try
            {
                var name    = string.IsNullOrWhiteSpace(NeuName) ? NeuRunde.Name : NeuName.Trim();
                var eingabe = new AuslosungErstellenEingabe(
                    _instanz.Id, NeuRunde.Id, name,
                    NeuAuslosungsart, false);
                var neu = await _auslosungService.ErstellenAsync(eingabe);

                await _auslosungService.LostoepfeSynchronisierenAsync(neu.Id, Math.Max(1, NeuAnzahlLostoepfe));

                var standardQuelleId = NeuRunde.StandardRanglistenQuelleId;
                NeuFormAktiv = false;

                Auslosungen = await LadeAuslosungenGefiltertAsync(_instanz.Id);

                // Standard-Rangliste der Runde (#349): Detail hier sequenziell laden und befüllen, BEVOR die
                // Auswahl gesetzt wird — deren Change-Hook lädt fire-and-forget und liefe sonst parallel auf
                // demselben DbContext (Reentrancy, siehe #317/#338). Danach greift dort der Gleiche-Id-Guard.
                if (standardQuelleId is not null)
                {
                    await RanglistenLadenAsync();
                    await LadeDetailAsync(neu.Id);
                    if (!await Arbeitsbereich.NachStandardRanglisteBefuellenAsync())
                        Fehlermeldung = "Für die Standard-Rangliste dieser Runde wurde noch keine Rangliste importiert " +
                                        "(Ranglisten → Import). Lostöpfe bitte manuell befüllen.";
                }

                AusgewaehlteAuslosung = Auslosungen.FirstOrDefault(a => a.Id == neu.Id);
            }
            catch (Exception ex) { Fehlermeldung = ex.Message; }
        }
        else if (IstDetailDirty && _auslosungDetail is not null)
        {
            try
            {
                await _auslosungService.BearbeitenAsync(_auslosungDetail.Id, DetailName, DetailAuslosungsart);
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
            var verlust = await SpielplanVerlustAsync(auslosung.TurnierInstanzId, auslosung.TurnierRundeId);
            var bestaetigt = await _dialogService.BestaetigenAsync(
                "Auslosung löschen", $"Soll '{auslosung.Name}' wirklich gelöscht werden?{verlust.Hinweis}");
            if (!bestaetigt) return;
            Ziehung.Beenden();

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
        bool tvModus = ziehungsArt != ZiehungsArt.Keine;
        bool manuell = ziehungsArt == ZiehungsArt.Manuell;
        Fehlermeldung = null;
        int auslosungId;
        ObservableCollection<GegnerMatrixTopf>? matrizen;
        List<ZiehungsSchritt>? schritte;
        try
        {
            // Nur der Datenbankteil läuft unter dem Schutz (#413) — die Ziehung danach ist reine Anzeige und
            // wartet im manuellen TV-Modus beliebig lange auf Klicks.
            using (await DbContextSchutz.BetretenAsync())
            {
                if (_auslosungDetail is null) return;
                auslosungId = _auslosungDetail.Id;
                if (!await ErgebnisVerlustBestaetigtAsync(_auslosungDetail.TurnierRundeId)) return;

                // Erst ab hier messen — die Wartezeit auf die Rückfrage gehört nicht zur Dauer der Aktion.
                using var vorgang = _logger.Vorgang($"Auslosung durchführen (Auslosung {auslosungId})");

                // Das Ergebnis steht ab AusfuehrenAsync fest und wird von LadeDetailAsync geladen — im TV-Modus
                // bleibt es bis zum Start der Ziehung verborgen, siehe ZeigeErgebnis. Der Service ersetzt dabei
                // auch den Spielplan der Runde und zieht die Folgerunden nach (#414) — VOR der Ziehung (#406): die
                // enthüllt nur noch und darf jederzeit enden (siehe ZiehungBeenden).
                ZiehungInVorbereitung = tvModus;
                Exception? fehler = null;
                try { await _auslosungService.AusfuehrenAsync(auslosungId); }
                catch (Exception ex) { fehler = ex; }

                // Auch nach einem Fehler neu laden: scheitert erst die Spielplan-Generierung, ist die Auslosung
                // bereits abgeschlossen — die Anzeige zeigte sonst weiter den Stand davor.
                await LadeDetailAsync(auslosungId);
                if (fehler is not null)
                {
                    Fehlermeldung = fehler.Message;
                    return;
                }
                if (_auslosungDetail is not { } detail || detail.Id != auslosungId) return;

                // Ziehungsdaten bewusst direkt NACH dem Reload aus dessen Stand bauen (nicht aus einem vor
                // AusfuehrenAsync genommenen Lostöpfe-Snapshot + dem separaten Rückgabewert von AusfuehrenAsync):
                // ViewModel und AuslosungService teilen denselben DbContext-Scope, ein Snapshot kann durch
                // EF-Change-Tracking/Identity-Resolution zwischen zwei Aufrufen verändert werden. Direkt nach dem
                // Reload stammen Lostöpfe UND Ergebnisse garantiert aus demselben, aktuellen Snapshot — exakt die
                // Daten, die danach auch als "echtes" Ergebnis angezeigt werden (Fund aus #303-Test: TV-Modus-
                // Endanzeige wich bei Gruppenphase nach vorherigem Zurücksetzen vom echten Ergebnis ab). Deshalb
                // auch VOR der Turnierbaum-Aktualisierung, die auf demselben DbContext weiterarbeitet.
                matrizen = tvModus && IstGegnerMatrixergebnis ? GegnerMatrixBuilder.Aufbauen(detail) : null;
                schritte = tvModus && matrizen is null
                    ? ZiehungsReihenfolge.Aufbauen(detail.Lostoepfe, detail.Ergebnisse)
                    : null;

                IstLadend = true;
                try
                {
                    // TEMPORÄR (#390-Messung): Wann arbeitet der UI-Thread nach dem Neuladen der Auslosung wieder
                    // Nachrichten ab? Solange er beschäftigt ist, wartet auch die Fortsetzung der ersten
                    // Datenbankabfrage der Turnierbaum-Aktualisierung.
                    var uiUhr = System.Diagnostics.Stopwatch.StartNew();
                    Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() =>
                        _logger.LogInformation("Messung #390: UI-Thread wieder frei nach {Dauer} ms", uiUhr.ElapsedMilliseconds));

                    using (_logger.Vorgang($"Auslosung: Turnierbaum aktualisieren (Instanz {detail.TurnierInstanzId})"))
                        await TurnierBaumAktualisierenAsync(detail.TurnierInstanzId);
                    await AktualisiereSeitenlisteAsync();
                }
                finally { IstLadend = false; }
            }

            // Zwischenzeitlich verlassen (Turnier-/Reiter-/Auslosungswechsel) — dann gibt es nichts mehr zu zeigen.
            if (!DetailAktiv || _auslosungDetail?.Id != auslosungId) return;

            if (matrizen is not null)
                await Ziehung.LigaphasenZiehungAnimierenAsync(matrizen, manuell);
            else if (schritte is not null)
                await Ziehung.ZiehungAnimierenAsync(schritte, Arbeitsbereich.Lostoepfe, manuell);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
        finally { ZiehungInVorbereitung = false; }
    }

    /// <summary>Die Auslosung ersetzt den Spielplan ihrer Runde ohne Rückfrage — wer auslost, will den Spielplan
    /// dazu. Nachgefragt wird nur, wenn dabei bereits eingetragene Ergebnisse verloren gingen (z.B. ein
    /// automatisch generiertes und schon gespieltes Finale), und dann VOR dem Auslosen: ein "Nein" danach
    /// hinterließe eine abgeschlossene Auslosung mit einem Spielplan, der nicht zu ihr passt.</summary>
    private async Task<bool> ErgebnisVerlustBestaetigtAsync(int rundeId)
    {
        var vorhandeneSpiele = await _spielplanService.SpielplanAbrufenAsync(rundeId);
        int mitErgebnis = vorhandeneSpiele.Count(s => s.Ergebnis is not null);
        if (mitErgebnis == 0) return true;

        return await _dialogService.BestaetigenAsync(
            "Auslosung durchführen",
            $"Die Runde hat bereits {mitErgebnis} Spiel(e) mit eingetragenem Ergebnis. Die Auslosung ersetzt den " +
            "Spielplan der Runde, diese Ergebnisse gehen dabei verloren. Trotzdem auslosen?");
    }

    /// <summary>Rückfrage, bevor eine laufende TV-Ziehung verlassen wird (Menüpunkt, Reiter, Turnierwahl, andere
    /// Auslosung, "Neu") — bei "Ja" endet die Ziehung sofort. Ohne laufende Ziehung synchron <c>true</c>.</summary>
    public async Task<bool> ZiehungBeendenNachRueckfrageAsync()
    {
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
            (string Hinweis, bool DatenBetroffen) verlust;
            using (_logger.Vorgang($"Auslosung zurücksetzen: Vorschau (Auslosung {detail.Id})"))
                verlust = await SpielplanVerlustAsync(detail.TurnierInstanzId, detail.TurnierRundeId);
            if (verlust.DatenBetroffen && !await _dialogService.BestaetigenAsync(
                    "Auslosung zurücksetzen",
                    $"Soll '{detail.Name}' wirklich zurückgesetzt werden?{verlust.Hinweis}"))
                return;

            // Erst ab hier messen — die Wartezeit auf die Rückfrage gehört nicht zur Dauer der Aktion.
            using var vorgang = _logger.Vorgang($"Auslosung zurücksetzen (Auslosung {detail.Id})");
            await _auslosungService.ZuruecksetzenAsync(detail.Id);
            await TurnierBaumAktualisierenAsync(detail.TurnierInstanzId);
            await LadeDetailAsync(detail.Id);
            await AktualisiereSeitenlisteAsync();
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    /// <summary>Was beim Zurücksetzen/Löschen einer Auslosung außer dem Auslosungsergebnis verloren geht (#405):
    /// der Spielplan der Runde samt eingetragener Ergebnisse (#320) und über die Kaskade bereits aufgelöste
    /// Paarungen in Folgerunden (#306). <c>Hinweis</c> ist der an die Rückfrage anzuhängende Text (leer ohne
    /// Spielplan), <c>DatenBetroffen</c> gilt bei eingetragenen Ergebnissen oder betroffenen Folgerunden.</summary>
    private async Task<(string Hinweis, bool DatenBetroffen)> SpielplanVerlustAsync(int turnierInstanzId, int rundeId)
    {
        var spiele = await _spielplanService.SpielplanAbrufenAsync(rundeId);
        if (spiele.Count == 0) return ("", false);

        int mitErgebnis = spiele.Count(s => s.Ergebnis is not null);
        var hinweis = mitErgebnis > 0
            ? $"\n\nDer Spielplan der Runde wird mitgelöscht: {spiele.Count} Spiel(e), davon {mitErgebnis} mit " +
              "eingetragenem Ergebnis."
            : $"\n\nDer Spielplan der Runde wird mitgelöscht: {spiele.Count} Spiel(e), noch ohne Ergebnis.";

        // Vorschau wie vor "Ergebnisse zurücksetzen" im Spielplan (#389): alle Spiele der Runde gelten als
        // entfernt. Neu zu generierende leere Runden zählen nicht — dort geht nichts verloren.
        var plan = await _spielplanService.PlatzhalterKaskadePlanenAsync(
            turnierInstanzId, spiele.Select(s => s.Id).ToHashSet());
        var folgeSchritte = plan.Schritte.OfType<KaskadenSpielSchritt>().Where(s => s.RundeId != rundeId).ToList();
        if (folgeSchritte.Count > 0)
        {
            var runden = string.Join(", ", folgeSchritte.Select(s => s.RundeName).Distinct());
            int folgeErgebnisse = folgeSchritte.Count(s => s.HatteErgebnis);
            hinweis += folgeErgebnisse > 0
                ? $"\n\nZusätzlich werden {folgeErgebnisse} Ergebnis(se) in Folgerunden zurückgesetzt, deren " +
                  $"Paarungen davon abhängen ({runden})."
                : $"\n\nZusätzlich werden bereits feststehende Paarungen in Folgerunden zurückgesetzt ({runden}).";
        }

        return (hinweis, mitErgebnis > 0 || folgeSchritte.Count > 0);
    }

    // ── Interne Hilfsmethoden ─────────────────────────────────────────────────

    partial void OnAusgewaehlteAuslosungChanged(Auslosung? value) => _ = BeiAuslosungAenderungAsync();

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

        // Andere Auslosung während der TV-Ziehung angeklickt (#406): bei "Nein" die Auswahl zurückstellen —
        // unter demselben Guard, damit diese Zuweisung hier nicht erneut ankommt.
        if (Ziehung.AnimationLaeuft && AusgewaehlteAuslosung?.Id != _auslosungDetail?.Id
            && !await ZiehungBeendenNachRueckfrageAsync())
        {
            _seitenlisteAktualisierungAktiv = true;
            try { AusgewaehlteAuslosung = Auslosungen.FirstOrDefault(a => a.Id == _auslosungDetail?.Id); }
            finally { _seitenlisteAktualisierungAktiv = false; }
            return;
        }

        NeuFormAktiv = false;
        if (AusgewaehlteAuslosung is null) { Ziehung.Beenden(); DetailAktiv = false; return; }

        // AktualisiereSeitenlisteAsync() weist AusgewaehlteAuslosung nach einem Reload der Seitenliste
        // erneut zu (neue Objektinstanz, gleiche Id) — das würde hier ohne diesen Guard einen bereits
        // frisch geladenen Detail-Stand ein zweites Mal nachladen (sichtbares Flackern, Issue #318).
        // Nur bei offenem Detail: nach einem Reset (z.B. InstanzSetzenAsync beim Reiter-Wechsel) steht
        // _auslosungDetail noch auf der alten Id, und erneutes Auswählen derselben Auslosung zeigte sonst nichts (#347).
        if (DetailAktiv && _auslosungDetail?.Id == AusgewaehlteAuslosung.Id) return;

        Ziehung.Beenden();
        int auslosungId = AusgewaehlteAuslosung.Id;

        // Läuft als Fire-and-Forget: hinter einer laufenden Aktion oder einem Turnierwechsel einreihen und
        // Fehler anzeigen statt verschlucken (#413). Nach dem Warten zählt der dann aktuelle Stand — die
        // Auswahl kann weitergewandert oder das Detail inzwischen von der Aktion selbst geladen sein.
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (AusgewaehlteAuslosung?.Id != auslosungId) return;
        if (DetailAktiv && _auslosungDetail?.Id == auslosungId) return;
        try { await LadeDetailAsync(auslosungId); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auslosung {AuslosungId} konnte nicht geladen werden.", auslosungId);
            Fehlermeldung = ex.Message;
        }
    }

    private async Task LadeDetailAsync(int auslosungId)
    {
        using var vorgang = _logger.Vorgang($"Auslosung laden (Auslosung {auslosungId})");

        IstLadend    = true;
        _ladenAktiv  = true;
        try
        {
            var detail = await _auslosungService.FindenNachIdAsync(auslosungId);
            if (detail is null) return;
            _auslosungDetail = detail;

            Arbeitsbereich.Zeigen(detail, await TeilnehmerPoolErmittelnAsync(detail));

            await AktualisiereVerbandsQuotenAnzeigeAsync(detail);
            await AktualisiereLandesQuotenAnzeigeAsync(detail);

            var regelRunde = InstanzRunden.FirstOrDefault(r => r.Id == detail.TurnierRundeId) ?? detail.TurnierRunde;
            _regelRundenart = regelRunde?.Rundenart;
            _regelFolgtAufGruppenphase = regelRunde is not null && InstanzRunden.Any(r =>
                r.Rundenart == Rundenart.Gruppenphase && r.Reihenfolge < regelRunde.Reihenfolge);

            Regeln = new ObservableCollection<RegelViewModel>(
                AuslosungsRegelKompatibilitaet.AngeboteneRegeln
                .Select(art =>
                {
                    var vorh = detail.Regeln.FirstOrDefault(r => r.Regelart == art);
                    return new RegelViewModel(art, AuslosungsRegelKompatibilitaet.AnzeigeName(art), AuslosungAnzeigeTexte.RegelBeschreibung(art), vorh?.IstAktiv ?? false,
                        async aktiv =>
                        {
                            using var schutz = await DbContextSchutz.BetretenAsync();
                            try
                            {
                                if (aktiv)
                                {
                                    // Nacheinander statt parallel speichern — ein DbContext verträgt keine zwei
                                    // gleichzeitigen Operationen.
                                    foreach (var andere in Regeln.Where(r =>
                                                 r.IstAktiv && AuslosungsRegelKompatibilitaet.SchliessenSichAus(art, r.Regelart)).ToList())
                                    {
                                        andere.OhneSpeichernSetzen(false);
                                        await _auslosungService.RegelDeaktivierenAsync(auslosungId, andere.Regelart);
                                    }
                                    await _auslosungService.RegelHinzufuegenOderAktivierenAsync(auslosungId, art, AuslosungsRegelKompatibilitaet.AnzeigeName(art));
                                }
                                else await _auslosungService.RegelDeaktivierenAsync(auslosungId, art);
                            }
                            catch (Exception ex) { Fehlermeldung = ex.Message; }
                        });
                }));
            AktualisiereRegelSperren(detail.Auslosungsart);

            bool istGruppe = detail.Ergebnisse.Any() &&
                detail.Ergebnisse.All(e => !e.IstFreilos &&
                    e.AuswaertsMannschaftId == null &&
                    e.AuswaertsPlatzhalterName == null);

            IstGruppenergebnis = istGruppe;

            // Ligaphase: Ergebnis als Gegner-Matrix je Topf statt flacher Paarungsliste (#362).
            bool istMatrix = !istGruppe && detail.Ergebnisse.Any() &&
                detail.Auslosungsart == Auslosungsart.TopfZuordnung && detail.Lostoepfe.Count >= 2;
            IstGegnerMatrixergebnis = istMatrix;
            GegnerMatrix.Zeigen(istMatrix ? GegnerMatrixBuilder.Aufbauen(detail) : []);

            if (istGruppe)
            {
                // Team-Reihenfolge innerhalb der Gruppe bewusst NICHT alphabetisch sortiert: sie muss mit
                // der tatsächlich gespeicherten Reihenfolge übereinstimmen, aus der
                // SpielplanService.GenerierenAusAuslosungAsync den Spielplan generiert (dort ohne eigene
                // Sortierung, ebenfalls direkt aus der PaarungsNummer-Gruppierung) — siehe #303-Diskussion.
                GruppenErgebnisse = new ObservableCollection<GruppenErgebnisAnzeige>(
                    detail.Ergebnisse
                        .GroupBy(e => e.PaarungsNummer)
                        .OrderBy(g => g.Key)
                        .Select(g => new GruppenErgebnisAnzeige(
                            g.Key,
                            $"Gruppe {GruppenHerkunft.Buchstabe(g.Key)}",
                            g.Select(e => new GruppenTeamAnzeige(
                                    e.HeimMannschaft?.Name ?? e.HeimPlatzhalterName ?? "?",
                                    TeilnehmerAnzeige.MannschaftsIcon(e.HeimMannschaft)))
                             .ToList())));
                ErgebnisseAnzeige = [];
            }
            else
            {
                GruppenErgebnisse = [];
                ErgebnisseAnzeige = new ObservableCollection<ErgebnisAnzeige>(
                    detail.Ergebnisse.Select(e => new ErgebnisAnzeige(
                        e.PaarungsNummer,
                        e.HeimMannschaft?.Name ?? e.HeimPlatzhalterName ?? "?",
                        e.AuswaertsMannschaft?.Name ?? e.AuswaertsPlatzhalterName ?? "?",
                        e.IstFreilos,
                        TeilnehmerAnzeige.MannschaftsIcon(e.HeimMannschaft),
                        TeilnehmerAnzeige.MannschaftsIcon(e.AuswaertsMannschaft))));
            }

            IstBearbeitbar        = detail.Status is not (AuslosungsStatus.Abgeschlossen or AuslosungsStatus.Abgebrochen);
            IstAbgeschlossen      = detail.Status == AuslosungsStatus.Abgeschlossen;
            DetailName            = detail.Name;
            DetailAuslosungsart   = detail.Auslosungsart;
            DetailAuslosungsartOptionen = AuslosungsartOptionenFuer(detail.TurnierRunde?.Rundenart);
            // Siehe OnNeuRundeChanged — erzwungene Nachbenachrichtigung gegen den WinUI3-ComboBox-Reset-
            // Fallstrick (Fund aus #300-Test: Auslosungsart erschien nach Anlage einer Auslosung leer).
            OnPropertyChanged(nameof(DetailAuslosungsart));
            DetailAnzahlLostoepfe = Math.Max(1, detail.Lostoepfe.Count);

            AuslosungKopfzeile = $"{detail.Name}  ·  {detail.TurnierRunde.Name}  ·  " +
                                 $"{AuslosungAnzeigeTexte.AuslosungsartAnzeigeName(detail.Auslosungsart)}  ·  " +
                                 $"Status: {AuslosungAnzeigeTexte.StatusAnzeigeName(detail.Status)}";
            DetailAktiv    = true;
            IstDetailDirty = false;
            Fehlermeldung  = null;
        }
        finally
        {
            _ladenAktiv = false;
            IstLadend   = false;
        }
    }

    /// <summary>Alle Teilnehmer, die für die Runde der Auslosung in Frage kommen — Platzhalter aus den
    /// eingehenden Übergangsregeln, sonst aus der Vorgängerrunde, in der ersten Runde die Mannschaften nach
    /// dem Turnierfilter. Bereits einem Lostopf zugeordnete sind noch enthalten.</summary>
    private async Task<IReadOnlyList<TeilnehmerAnzeige>> TeilnehmerPoolErmittelnAsync(Auslosung detail)
    {
        var aktuelleRunde = InstanzRunden.FirstOrDefault(r => r.Id == detail.TurnierRundeId);
        var eingehendeRegeln = aktuelleRunde is not null
            ? await _uebergangsregelService.AlleNachZielRundeIdAsync(aktuelleRunde.Id)
            : [];

        if (eingehendeRegeln.Count > 0)
        {
            return TeilnehmerPoolHilfsmittel.PlatzhalterAusRegelnGenerieren(eingehendeRegeln);
        }
        else
        {
            var vorherigeRunde = aktuelleRunde is not null
                ? TeilnehmerPoolHilfsmittel.VorgaengerrundeErmitteln(InstanzRunden, aktuelleRunde)
                : null;
            if (vorherigeRunde is null)
            {
                var teams = await _mannschaftService.AlleNachTurnierfilterAsync(
                    _instanz?.VerbandId,
                    _instanz?.LandId,
                    _instanz?.MannschaftsartFilter ?? MannschaftsartFilter.Alle);
                return teams.Select(TeilnehmerAnzeige.AusMannschaft).ToList();
            }
            else
            {
                return TeilnehmerPoolHilfsmittel.PlatzhalterOhneRegelnGenerieren(aktuelleRunde!, vorherigeRunde);
            }
        }
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

    /// <summary>Auslosungsart.Manuell wird hier nicht angezeigt — deren Paarungsfestlegung
    /// lebt ausschließlich im Vorbelegung-Tab.</summary>
    private async Task<ObservableCollection<Auslosung>> LadeAuslosungenGefiltertAsync(int turnierInstanzId)
    {
        var auslosungen = await _auslosungService.AlleNachInstanzAbrufenAsync(turnierInstanzId);

        // Issue #320: _belegteRundenIds bewusst aus der UNGEFILTERTEN Liste (inkl. Manuell) befüllen —
        // NeuRundenOptionen muss auch Runden mit einer hier unsichtbaren Manuell-Auslosung ausblenden.
        _belegteRundenIds = auslosungen.Select(a => a.TurnierRundeId).ToHashSet();
        OnPropertyChanged(nameof(NeuRundenOptionen));

        return new ObservableCollection<Auslosung>(auslosungen.Where(a => a.Auslosungsart != Auslosungsart.Manuell));
    }

    partial void OnNeuRundeChanged(TurnierRunde? value)
    {
        // Reihenfolge bewusst: erst den Zielwert festlegen (falls die aktuelle Art in der neuen Runde nicht
        // mehr kompatibel ist), DANACH die Optionsliste (ItemsSource) neu setzen — minimiert das Zeitfenster,
        // in dem die ComboBox eine ItemsSource ohne passenden Eintrag zur aktuellen Auswahl hätte (siehe
        // Klassendoku bei NeuAuslosungsartOptionen).
        NeuAuslosungsart = StartAuslosungsartFuer(value, NeuAuslosungsart);

        NeuAuslosungsartOptionen = AuslosungsartOptionenFuer(value?.Rundenart);
        // Erzwungene Nachbenachrichtigung: falls WinUI die ComboBox-Auswahl beim ItemsSource-Wechsel
        // transient zurücksetzt und der anschließende Vergleich zufällig denselben Enum-Wert ergibt,
        // unterdrückt CommunityToolkits Gleichheitsprüfung die Benachrichtigung — die ComboBox bliebe dann
        // visuell leer, obwohl NeuAuslosungsart korrekt ist (Fund aus #300-Test).
        OnPropertyChanged(nameof(NeuAuslosungsart));

        if (value is null) return;
        if (string.IsNullOrWhiteSpace(NeuName) || NeuName == _autoNeuName)
        {
            _autoNeuName = value.Name;
            NeuName      = value.Name;
        }
    }

    partial void OnNeuAuslosungsartChanged(Auslosungsart value)
    {
        if (!NeuAnzahlLostoepfeBearbeitbar)
            NeuAnzahlLostoepfe = 1;
        // Topf-Zuordnung braucht mindestens 2 gleich große Töpfe — 4 entspricht der UCL-Ligaphase (Issue #347).
        else if (value == Auslosungsart.TopfZuordnung && NeuAnzahlLostoepfe < 2)
            NeuAnzahlLostoepfe = 4;
    }

    /// <summary>Auslosungsart, mit der das Neu-Formular für <paramref name="runde"/> startet (Issue #347):
    /// zuerst die von der Vorlage vorgegebene Art der Runde, sonst die bisherige Auswahl, solange sie zur
    /// Rundenart passt, sonst die erste kompatible Art (z.B. Topf-Zuordnung bei einer TopfLiga).</summary>
    private static Auslosungsart StartAuslosungsartFuer(TurnierRunde? runde, Auslosungsart bisher)
    {
        if (runde is null) return bisher;
        if (runde.VorgegebeneAuslosungsart is { } vorgabe && AuslosungsKompatibilitaet.IstKompatibel(runde.Rundenart, vorgabe))
            return vorgabe;
        if (AuslosungsKompatibilitaet.IstKompatibel(runde.Rundenart, bisher))
            return bisher;
        return AuslosungsartOptionenFuer(runde.Rundenart).Select(o => o.Wert).DefaultIfEmpty(Auslosungsart.Zufall).First();
    }

    private bool KannNeu()       => !NeuFormAktiv && _instanz is not null && _instanz.Status != TurnierStatus.Vorlage;
    private bool KannSpeichern() => (NeuFormAktiv && NeuRunde is not null)
                                 || (IstDetailDirty && IstBearbeitbar);
    private bool KannAbbrechen() => NeuFormAktiv || IstDetailDirty;

    public bool HatUngespeicherteAenderungen => NeuFormAktiv || IstDetailDirty;
    private bool KannLoeschen()  => AusgewaehlteAuslosung is not null && !NeuFormAktiv;

    private bool KannAuslosungAusfuehren()             => DetailAktiv && IstBearbeitbar;
    private bool KannAuslosungZuruecksetzen()           => DetailAktiv && IstAbgeschlossen && !Ziehung.AnimationLaeuft;

    partial void OnDetailAktivChanged(bool value)    => Arbeitsbereich.BearbeitbarSetzen(DetailAktiv && IstBearbeitbar);
    partial void OnIstBearbeitbarChanged(bool value) => Arbeitsbereich.BearbeitbarSetzen(DetailAktiv && IstBearbeitbar);

    partial void OnDetailNameChanged(string value)           { if (!_ladenAktiv) IstDetailDirty = true; }
    partial void OnDetailAuslosungsartChanged(Auslosungsart value)
    {
        if (!_ladenAktiv) IstDetailDirty = true;
        AktualisiereRegelSperren(value);

        if (!DetailAnzahlLostoepfeBearbeitbar)
            DetailAnzahlLostoepfe = 1;
    }

    private Rundenart? _regelRundenart;
    private bool _regelFolgtAufGruppenphase;

    // Unpassende Regeln bleiben sichtbar, werden aber ausgegraut und begründet (#331) — Quelle ist dieselbe Matrix,
    // nach der auch der AuslosungService entscheidet, welche Regeln er anwendet.
    private void AktualisiereRegelSperren(Auslosungsart art)
    {
        foreach (var regel in Regeln)
        {
            regel.ImmerAktivHinweis = AuslosungsRegelKompatibilitaet.ImmerAktivGrund(regel.Regelart, art);
            regel.SperrHinweis = AuslosungsRegelKompatibilitaet.SperrGrund(
                regel.Regelart, art, _regelRundenart, _regelFolgtAufGruppenphase);
        }
        OnPropertyChanged(nameof(RibbonZusatzGruppen));
    }

    private static bool IstHeimrechtRegel(AuslosungsRegelArt art) =>
        art is AuslosungsRegelArt.HeimrechtNachLos or AuslosungsRegelArt.HeimrechtNachSetzliste;

    private async Task AktualisiereVerbandsQuotenAnzeigeAsync(Auslosung detail)
    {
        if (_instanz is null) { VerbandsQuotenAnzeige = []; return; }

        // Verbandsquoten gelten laut Domänen-Annahme nur für die initiale Turnierrunde (Zulassung),
        // nicht kumulativ für Folgerunden mit eigenem Losvorgang — siehe [[project_verbandsquote_scope]].
        bool istErsteRunde = InstanzRunden.Count > 0 && InstanzRunden[0].Id == detail.TurnierRundeId;
        if (!istErsteRunde) { VerbandsQuotenAnzeige = []; return; }

        var quoten = await _verbandsQuoteService.AlleNachTurnierInstanzIdAsync(_instanz.Id);
        if (quoten.Count == 0) { VerbandsQuotenAnzeige = []; return; }

        var anzahlProVerband = detail.Lostoepfe
            .SelectMany(l => l.Eintraege)
            .Where(e => e.Mannschaft?.Land is not null)
            .GroupBy(e => e.Mannschaft!.Land!.VerbandId)
            .ToDictionary(g => g.Key, g => g.Count());

        VerbandsQuotenAnzeige = new ObservableCollection<VerbandsQuoteAnzeigeViewModel>(
            quoten.Select(q => new VerbandsQuoteAnzeigeViewModel
            {
                VerbandName = q.Verband?.Name ?? $"Verband {q.VerbandId}",
                Aktuell     = anzahlProVerband.GetValueOrDefault(q.VerbandId),
                Anzahl      = q.Anzahl
            }));
    }

    /// <summary>Landes-Quoten-Pendant zu <see cref="AktualisiereVerbandsQuotenAnzeigeAsync"/> (Issue #189)
    /// — dieselbe "nur erste Runde"-Domänenregel, zusätzlich nur für Vereinswettbewerbe sichtbar
    /// (Landesquoten sind bei Nationalmannschaften trivial, siehe TurnierkopfViewModel.LandesQuotenSichtbar).
    /// Gruppierung über Mannschaft.LandId direkt statt der Verband-Indirektion über Land.VerbandId.</summary>
    private async Task AktualisiereLandesQuotenAnzeigeAsync(Auslosung detail)
    {
        if (_instanz is null || _instanz.MannschaftsartFilter != MannschaftsartFilter.Verein) { LandesQuotenAnzeige = []; return; }

        bool istErsteRunde = InstanzRunden.Count > 0 && InstanzRunden[0].Id == detail.TurnierRundeId;
        if (!istErsteRunde) { LandesQuotenAnzeige = []; return; }

        var quoten = await _landesQuoteService.AlleNachTurnierInstanzIdAsync(_instanz.Id);
        if (quoten.Count == 0) { LandesQuotenAnzeige = []; return; }

        var anzahlProLand = detail.Lostoepfe
            .SelectMany(l => l.Eintraege)
            .Where(e => e.Mannschaft is not null)
            .GroupBy(e => e.Mannschaft!.LandId)
            .ToDictionary(g => g.Key, g => g.Count());

        LandesQuotenAnzeige = new ObservableCollection<LandesQuoteAnzeigeViewModel>(
            quoten.Select(q => new LandesQuoteAnzeigeViewModel
            {
                LandName = q.Land?.Name ?? $"Land {q.LandId}",
                Aktuell  = anzahlProLand.GetValueOrDefault(q.LandId),
                Anzahl   = q.Anzahl
            }));
    }

    partial void OnDetailAnzahlLostoepfeChanged(int value)   { if (!_ladenAktiv) IstDetailDirty = true; }
}
