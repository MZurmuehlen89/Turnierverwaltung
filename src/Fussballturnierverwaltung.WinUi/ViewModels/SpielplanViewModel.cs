using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Diagnose;
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
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>
/// "Spielplan"-Bereich des #301-Composite (<see cref="SpielplanUndBaumViewModel"/>) — 1:1 aus
/// Wpf/ViewModels/SpielplanViewModel.cs portiert. Wichtigste WinUi-Anpassungen: <c>MessageBox.Show</c>
/// (3-Wege Speichern/Verwerfen/Abbrechen) → <see cref="IDialogService.UngespeicherteAenderungenAbfragenAsync"/>,
/// einfache Ja/Nein-Dialoge → <see cref="IDialogService.BestaetigenAsync"/>; Ribbon-Simulieren-Aktionen als
/// flache Einzel-Buttons statt SplitButtons/Unteraktionen (mit dem Nutzer abgestimmte Vereinfachung für #301,
/// siehe Plan) — "Runde/Gruppe/Spieltag zurücksetzen" stehen als eigene Buttons neben den Simulieren-Buttons.
/// </summary>
public partial class SpielplanViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen,
    ISpielmengenKontext
{
    private readonly SpielErgebnisService _spielService;
    private readonly SpielplanService _spielplanService;
    private readonly PlatzhalterAufloesung _platzhalterAufloesung;
    private readonly PlatzhalterKaskadeService _kaskade;
    private readonly FinaleAutomatik _finaleAutomatik;
    private readonly AuslosungService _auslosungService;
    private readonly TurnierBaumService _baumService;
    private readonly RegelwerkService _regelwerkService;
    private readonly TabellenService _tabellenService;
    private readonly TurnierInstanzService _instanzService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<SpielplanViewModel> _logger;

    private Dictionary<int, int> _teamGruppeMap = [];
    private IReadOnlyDictionary<int, (string? Heim, string? Auswaerts)> _platzhalterVorschau =
        new Dictionary<int, (string?, string?)>();

    private readonly WechselUnterdrueckung _spielWechsel = new();
    private readonly WechselUnterdrueckung _rundeWechsel = new();

    /// <summary>Reiht alle Datenbankzugriffe dieses Reiters hintereinander: Runde-/Spiel-Wechsel (#338), die
    /// Aktionen (Simulieren, Zurücksetzen, Speichern …) und — über den vom
    /// <see cref="TurnierdurchfuehrungViewModel"/> gesetzten gemeinsamen Schutz — auch Turnier-/Reiterwechsel
    /// (#415). Ein eigener Schutz je Reiter schloss das nicht aus: eine laufende Simulation kollidierte mit dem
    /// Laden nach einem Runden-, Reiter- oder Turnierwechsel und brach dabei selbst mittendrin ab. Nicht
    /// wiedereintrittsfähig — <see cref="InstanzSetzenAsync"/> läuft bereits unter dem Schutz und darf nichts
    /// abwarten, das ihn betritt (die selbstverursachte Reentrancy decken
    /// _rundeWechsel/_spielWechsel ab).</summary>
    public DbContextReentrancySchutz DbContextSchutz { get; set; } = new();

    IReadOnlyList<SpielZeile> ISpielmengenKontext.Spiele => Spiele;
    GruppenTabelle? ISpielmengenKontext.EffektiveGruppe => EffektiveGruppe;
    int? ISpielmengenKontext.EffektiverSpieltag => EffektiverSpieltag;
    bool ISpielmengenKontext.HatHinUndRueckspiel => HatHinUndRueckspiel;
    Task<string> ISpielmengenKontext.KaskadenHinweisAsync(IReadOnlyList<int> spielIds) => KaskadenHinweisAsync(spielIds);

    // ── linke Spalte ──────────────────────────────────────────────────────────

    [ObservableProperty] private TurnierInstanz? _ausgewaehlteInstanz;

    [ObservableProperty] private ObservableCollection<TurnierRunde> _runden = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatRunde))]
    [NotifyPropertyChangedFor(nameof(IstGruppenphase))]
    [NotifyPropertyChangedFor(nameof(IstKoRunde))]
    [NotifyPropertyChangedFor(nameof(IstLigaRunde))]
    [NotifyPropertyChangedFor(nameof(IstLigaMitRueckspiel))]
    [NotifyPropertyChangedFor(nameof(IstSpieltagRunde))]
    [NotifyPropertyChangedFor(nameof(ZeigeFokusListe))]
    [NotifyPropertyChangedFor(nameof(ZeigtSchweizerRundenSteuerung))]
    [NotifyPropertyChangedFor(nameof(IstKoMitRueckspiel))]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    [NotifyPropertyChangedFor(nameof(RundenHinweis))]
    private TurnierRunde? _ausgewaehlteRunde;

    /// <summary>Issue #352/#360: Zusatz in der Spiellisten-Kopfzeile und im Ergebnisformular.</summary>
    public string RundenHinweis =>
        (AusgewaehlteRunde?.NeutralerSpielort == true ? " · neutraler Platz" : "")
        + (AusgewaehlteRunde?.Ueberraschungsfaktor is int f and > 0 ? $" · Überraschungsfaktor {f} %" : "");

    public bool HatRunde => AusgewaehlteRunde is not null;
    public bool IstGruppenphase => AusgewaehlteRunde?.Rundenart == Rundenart.Gruppenphase;
    public bool IstLigaRunde => AusgewaehlteRunde?.Rundenart == Rundenart.Liga;

    private bool IstTabellenRunde => AusgewaehlteRunde?.Rundenart is
        Rundenart.Gruppenphase or Rundenart.Liga or Rundenart.SchweizerSystem or Rundenart.TopfLiga;

    /// <summary>Rundenarten mit Spieltagen, deren Spielplan je Spieltag durchgeblättert wird (Issue #363) —
    /// K.-o.-Runden behalten die Paarungsansicht aus #340.</summary>
    public bool IstSpieltagRunde => IstTabellenRunde;

    public bool IstLigaMitRueckspiel => IstLigaRunde && (AusgewaehlteRunde?.MitRueckspiel ?? false);

    /// <summary>Issue #387: Die Menüpunkte Hin-/Rückrunde gelten in K.-o.-Runden für Hin- und Rückspiele.</summary>
    public bool IstKoMitRueckspiel => IstKoRunde && (AusgewaehlteRunde?.MitRueckspiel ?? false);
    private bool HatHinUndRueckspiel => IstLigaMitRueckspiel || IstKoMitRueckspiel;
    private string HinrundeMenuText => IstKoMitRueckspiel ? "Hinspiele" : "Hinrunde";
    private string RueckrundeMenuText => IstKoMitRueckspiel ? "Rückspiele" : "Rückrunde";

    public ObservableCollection<GruppenTabelle> GruppenTabellen { get; } = [];

    // ── Gruppen-/Spieltag-Fokus ───────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstGefiltertAufGruppe))]
    [NotifyPropertyChangedFor(nameof(ZeigeFokusListe))]
    [NotifyPropertyChangedFor(nameof(SpieltagSimulierenMenuText))]
    [NotifyPropertyChangedFor(nameof(SpieltagZuruecksetzenMenuText))]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private GruppenTabelle? _ausgewaehlteGruppe;

    public bool IstGefiltertAufGruppe => AusgewaehlteGruppe is not null;

    public bool ZeigeFokusListe => AusgewaehlteGruppe is not null || IstSpieltagRunde;

    // ── Schweizer-Rundensteuerung (Issue #329 Teil E) ────────────────────────────
    // "Nächste Runde generieren" ist bewusst ein eigener Inline-Button in der Spielliste-Kopfzeile statt im
    // Ribbon (laut #301-Fund oben bereits überfüllt) und bewusst getrennt vom Ribbon-"Neu"-Befehl
    // (SpielplanNeuGenerierenAsync), der den kompletten Spielplan löscht+aus der Auslosung neu generiert —
    // für eine laufende Schweizer-Rundenfolge wäre das destruktiv. Rundenabschluss nach dem letzten
    // Spieltag braucht keinen Sonderfall: sobald alle Ergebnisse eingetragen sind, lösen die bestehenden
    // Mechanismen (PlatzhalterAutomatischAufloesenAsync) die Übergangsregeln der Folgerunde ganz normal auf,
    // exakt wie bei Liga-Runden heute schon.

    public bool ZeigtSchweizerRundenSteuerung => AusgewaehlteRunde?.Rundenart == Rundenart.SchweizerSystem;

    private int SchweizerAktuellerSpieltag =>
        Spiele.Where(s => s.Spieltag.HasValue).Select(s => s.Spieltag!.Value).DefaultIfEmpty(0).Max();

    private int SchweizerZielSpieltage => AusgewaehlteRunde is { } r
        ? RundenKonfiguration.AusJson(r.KonfigurationJson).AnzahlSpieltage ?? RundenKonfiguration.VorschlagAnzahlSpieltage(r.EingangsMannschaftsanzahl)
        : 0;

    private bool SchweizerAktuellerSpieltagKomplett => SchweizerAktuellerSpieltag > 0 &&
        Spiele.Where(s => s.Spieltag == SchweizerAktuellerSpieltag).All(s => s.Status == SpielStatus.Abgeschlossen);

    public string SchweizerFortschrittsText => $"Spieltag {SchweizerAktuellerSpieltag} von {SchweizerZielSpieltage}";
    public string NaechsteSchweizerRundeLabel => $"Spieltag {SchweizerAktuellerSpieltag + 1} generieren";

    private void SchweizerAnzeigeAktualisieren()
    {
        OnPropertyChanged(nameof(SchweizerFortschrittsText));
        OnPropertyChanged(nameof(NaechsteSchweizerRundeLabel));
        _naechsteSchweizerRundeBefehl.NotifyCanExecuteChanged();
    }

    public SpieltagFokusViewModel Fokus { get; } = new();

    public string SpieltagSimulierenMenuText => EffektiverSpieltag is { } st ? $"Spieltag {st} simulieren" : "Spieltag simulieren";
    public string SpieltagZuruecksetzenMenuText => EffektiverSpieltag is { } st ? $"Spieltag {st} zurücksetzen" : "Spieltag zurücksetzen";

    private readonly RelayCommand _zurueckZurUebersichtBefehl;

    public ICommand ZurueckZurUebersichtBefehl => _zurueckZurUebersichtBefehl;

    // ── Spielliste ────────────────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<SpielZeile> _spiele = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpieltagSimulierenMenuText))]
    [NotifyPropertyChangedFor(nameof(SpieltagZuruecksetzenMenuText))]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private SpielZeile? _ausgewaehlteSpiel;

    // ── Ergebnis-Formular ─────────────────────────────────────────────────────

    [ObservableProperty] private bool _formAktiv;
    [ObservableProperty] private string? _fehlermeldung;

    public bool HatUngespeicherteAenderungen => FormAktiv && Formular.IsDirty;
    public ErgebnisFormularViewModel Formular { get; } = new();

    public bool IstKoRunde => AusgewaehlteRunde?.Rundenart is
        Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;

    // ── Platzhalter-Vorschau (Issue #208, sichtbarer Toggle-Zustand #325) ────────────────────────────────
    // RibbonAktion.IstAktiv steuert das AppBarToggleButton-Rendering in MainWindow.xaml.cs
    // (siehe RibbonZusatzGruppen unten).

    public bool HatPlatzhalterSpiele => Spiele.Any(s => s.IstPlatzhalterSpiel);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private bool _vorschauAktiv;

    private readonly RelayCommand _vorschauUmschaltenBefehl;

    private async Task AktualisierePlatzhalterVorschauAsync()
    {
        _platzhalterVorschau = VorschauAktiv && AusgewaehlteRunde is not null
            ? await _platzhalterAufloesung.VorschauAsync(AusgewaehlteRunde.Id)
            : new Dictionary<int, (string?, string?)>();

        if (AusgewaehlteRunde is null) return;
        var spiele = await _spielService.AlleNachRundeAsync(AusgewaehlteRunde.Id);
        Spiele.Clear();
        SpieleHinzufuegen(spiele, AusgewaehlteRunde);
        Fokus.SpieleMelden();
    }

    // ── Simulation ────────────────────────────────────────────────────────────

    [ObservableProperty] private bool _istSimulierend;

    /// <summary>Simulieren und Zurücksetzen auf Ebene Runde, Hin-/Rückrunde, Gruppe, Spieltag und Auswahl
    /// (Issue #14, #123).</summary>
    public SpielmengenAktionen Mengen { get; }

    // ── Manuelle Multi-Selektion (Issue #124) ────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnzahlMehrfachSimulierbar))]
    [NotifyPropertyChangedFor(nameof(AuswahlSimulierenMenuText))]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private IReadOnlyList<SpielZeile> _mehrfachAusgewaehlteSpiele = [];

    public int AnzahlMehrfachSimulierbar =>
        MehrfachAusgewaehlteSpiele.Count(SpielmengenAktionen.IstSimulierbar);
    public string AuswahlSimulierenMenuText => $"Auswahl ({AnzahlMehrfachSimulierbar}) simulieren";

    public void AktualisiereMehrfachAuswahl(IEnumerable<SpielZeile> ausgewaehlt)
    {
        MehrfachAusgewaehlteSpiele = ausgewaehlt.ToList();
        _loeschenBefehl.NotifyCanExecuteChanged();
        Mengen.BefehleMelden();
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
                        new RibbonUnteraktion("Runde", Mengen.RundeSimulierenBefehl),
                        new RibbonUnteraktion(HinrundeMenuText, Mengen.HinrundeSimulierenBefehl),
                        new RibbonUnteraktion(RueckrundeMenuText, Mengen.RueckrundeSimulierenBefehl),
                        new RibbonUnteraktion("Gruppe", Mengen.GruppeSimulierenBefehl),
                        new RibbonUnteraktion(SpieltagSimulierenMenuText, Mengen.SpieltagSimulierenBefehl),
                        new RibbonUnteraktion(AuswahlSimulierenMenuText, Mengen.AuswahlSimulierenBefehl),
                    ],
                },
                new RibbonAktion
                {
                    Header = "Zurücksetzen",
                    IconGlyph = "",
                    Unteraktionen =
                    [
                        new RibbonUnteraktion("Runde", Mengen.RundeZuruecksetzenBefehl),
                        new RibbonUnteraktion(HinrundeMenuText, Mengen.HinrundeZuruecksetzenBefehl),
                        new RibbonUnteraktion(RueckrundeMenuText, Mengen.RueckrundeZuruecksetzenBefehl),
                        new RibbonUnteraktion("Gruppe", Mengen.GruppeZuruecksetzenBefehl),
                        new RibbonUnteraktion(SpieltagZuruecksetzenMenuText, Mengen.SpieltagZuruecksetzenBefehl),
                    ],
                },
                new RibbonAktion
                {
                    Header = "Ausführlich simulieren",
                    Befehl = _ausfuehrlichSimulierenBefehl,
                    IconGlyph = "",
                },
                new RibbonAktion
                {
                    Header = "Spielverlauf ansehen",
                    Befehl = _spielverlaufAnsehenBefehl,
                    IconGlyph = "",
                },
                new RibbonAktion
                {
                    Header = "Platzhalter-Vorschau",
                    Befehl = _vorschauUmschaltenBefehl,
                    IstAktiv = VorschauAktiv,
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
    private readonly AsyncRelayCommand _naechsteSchweizerRundeBefehl;

    public ICommand NaechsteSchweizerRundeBefehl => _naechsteSchweizerRundeBefehl;

    // ── Konstruktor ───────────────────────────────────────────────────────────

    public SpielplanViewModel(
        SpielErgebnisService spielService,
        SpielplanService spielplanService,
        PlatzhalterAufloesung platzhalterAufloesung,
        PlatzhalterKaskadeService kaskade,
        FinaleAutomatik finaleAutomatik,
        AuslosungService auslosungService,
        TurnierBaumService baumService,
        RegelwerkService regelwerkService,
        TabellenService tabellenService,
        SimulationsService simulationsService,
        TurnierInstanzService instanzService,
        IDialogService dialogService,
        ILogger<SpielplanViewModel> logger)
    {
        _spielService = spielService;
        _spielplanService = spielplanService;
        _platzhalterAufloesung = platzhalterAufloesung;
        _kaskade = kaskade;
        _finaleAutomatik = finaleAutomatik;
        _auslosungService = auslosungService;
        _baumService = baumService;
        _regelwerkService = regelwerkService;
        _tabellenService = tabellenService;
        _instanzService = instanzService;
        _dialogService = dialogService;
        _logger = logger;

        _neuBefehl = new AsyncRelayCommand(SpielplanNeuGenerierenAsync, () => HatRunde);
        _speichernBefehl = new AsyncRelayCommand(SpeichernAsync, () => FormAktiv && Formular.IsDirty);
        _abbrechenBefehl = new RelayCommand(FormZuruecksetzen, () => FormAktiv && Formular.IsDirty);
        _loeschenBefehl = new AsyncRelayCommand(ErgebnisLoeschenAsync, CanErgebnisLoeschen);

        _zurueckZurUebersichtBefehl = new RelayCommand(() => AusgewaehlteGruppe = null, () => AusgewaehlteGruppe is not null);

        Mengen = new SpielmengenAktionen(this, simulationsService, spielService, dialogService);

        // #315: Vorschau nur bei offenen Platzhalter-Spielen anbietbar — eine aktive Vorschau bleibt aber
        // abschaltbar, auch wenn die Runde inzwischen keine Platzhalter mehr hat.
        _vorschauUmschaltenBefehl = new RelayCommand(() =>
        {
            VorschauAktiv = !VorschauAktiv;
            _vorschauUmschaltenBefehl!.NotifyCanExecuteChanged();
            _ = DbContextSchutz.AusfuehrenAsync(AktualisierePlatzhalterVorschauAsync, ex => Fehlermeldung = ex.Message);
        }, () => HatPlatzhalterSpiele || VorschauAktiv);

        _ausfuehrlichSimulierenBefehl = new RelayCommand(AusfuehrlichSimulieren, CanAusfuehrlichSimulieren);
        _spielverlaufAnsehenBefehl = new RelayCommand(SpielverlaufAnsehen, CanSpielverlaufAnsehen);

        _naechsteSchweizerRundeBefehl = new AsyncRelayCommand(NaechsteSchweizerRundeGenerierenAsync, KannNaechsteSchweizerRundeGenerieren);
        Fokus.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(SpieltagFokusViewModel.GewaehlterSpieltag)) return;
            OnPropertyChanged(nameof(SpieltagSimulierenMenuText));
            OnPropertyChanged(nameof(SpieltagZuruecksetzenMenuText));
            OnPropertyChanged(nameof(RibbonZusatzGruppen));
            Mengen.BefehleMelden();
            SpieltagNavigiert?.Invoke();
        };
        Formular.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(ErgebnisFormularViewModel.IsDirty)) return;
            _speichernBefehl.NotifyCanExecuteChanged();
            _abbrechenBefehl.NotifyCanExecuteChanged();
        };
        Spiele.CollectionChanged += (_, __) =>
        {
            SchweizerAnzeigeAktualisieren();
            _vorschauUmschaltenBefehl.NotifyCanExecuteChanged();
        };
    }

    // ── Initialisierung ───────────────────────────────────────────────────────

    public override async Task AktivierenAsync()
    {
        if (AusgewaehlteRunde is null || (FormAktiv && Formular.IsDirty)) return;
        await SpiellisteAktualisierenAsync();
    }

    private async Task SpiellisteAktualisierenAsync()
    {
        if (AusgewaehlteRunde is null) return;
        int rundeId = AusgewaehlteRunde.Id;
        int? spielId = AusgewaehlteSpiel?.Id;
        var runde = AusgewaehlteRunde;

        // #317: Guard umschließt Spiele.Clear()/Add() bis zum Ende der davon abhängigen Folge-Abfragen,
        // nicht nur die AusgewaehlteSpiel-Zuweisung — eine WinUI-ComboBox kann SelectionChanged bei
        // ItemsSource.Clear() über die DispatcherQueue VERZÖGERT statt synchron auslösen. Feuert der
        // Callback erst während der Turnierbaum-Aktualisierung/BeiSpielAenderungAsync, entsteht sonst ein
        // ungeschützter Parallelzugriff auf denselben DbContext ("second operation"-Exception, #317).
        using (_spielWechsel.Betreten())
        {
            await SpieleNeuLadenAsync(rundeId);
            OnPropertyChanged(nameof(HatPlatzhalterSpiele));

            AusgewaehlteSpiel = spielId.HasValue ? Spiele.FirstOrDefault(s => s.Id == spielId) : null;

            await _baumService.NachSpielplanAenderungAktualisierenAsync(runde.TurnierInstanzId);
        }

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
                Fokus.SpieleMelden();
                break;
        }
    }

    // ── Selektions-Handler ────────────────────────────────────────────────────

    private async Task BeiInstanzAenderungAsync()
    {
        using var vorgang = _logger.Vorgang($"Spielplan: Turnier setzen (Instanz {AusgewaehlteInstanz?.Id})");

        FormAktiv = false;

        int? vorherigeRundeId = AusgewaehlteRunde?.Id;

        var instanz = AusgewaehlteInstanz is null
            ? null
            : await _instanzService.FindenNachIdMitRundenAsync(AusgewaehlteInstanz.Id);

        // #317: Guard umschließt Runden.Clear()/Add() bis zum Ende des kompletten Reloads (inkl. des
        // verschachtelten SpiellisteAktualisierenAsync-Aufrufs), nicht nur die AusgewaehlteRunde-
        // Zuweisung — eine WinUI-ComboBox kann SelectionChanged bei ItemsSource.Clear() über die
        // DispatcherQueue VERZÖGERT statt synchron auslösen. Feuert der Callback erst während einer der
        // nachfolgenden Abfragen, entsteht sonst ein ungeschützter Parallelzugriff auf denselben
        // DbContext ("second operation"-Exception, bislang nur per Retry im Composite abgefangen, #317).
        using var unterdrueckung = _rundeWechsel.Betreten();

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

        AusgewaehlteRunde = wiederhergestellteRunde;

        // Spiele/Tabellen frisch laden (externe Änderungen aus anderen Tabs sichtbar machen), aber
        // Spiel-Auswahl anhand ihrer Id wiederherstellen statt hart zurückzusetzen (Precedent:
        // SpiellisteAktualisierenAsync, bereits genau dieses Muster für AktivierenAsync).
        await SpiellisteAktualisierenAsync();
    }

    private async Task RundeUndSpielZuruecksetzenAsync()
    {
        Spiele.Clear();
        AusgewaehlteSpiel = null;
        RundeStillSetzen(null);

        await BeiRundeAenderungAsync();
    }

    /// <summary>Setzt die Rundenauswahl, ohne den Rundenwechsel (Abfrage, Laden) auszulösen.</summary>
    private void RundeStillSetzen(TurnierRunde? runde)
    {
        using (_rundeWechsel.Betreten())
            AusgewaehlteRunde = runde;
    }

    /// <summary>Setzt die Spielauswahl, ohne den Spielwechsel (Abfrage, Laden) auszulösen.</summary>
    private void SpielStillSetzen(SpielZeile? spiel)
    {
        using (_spielWechsel.Betreten())
            AusgewaehlteSpiel = spiel;
    }

    private async Task RundeWechselAsync()
    {
        if (_rundeWechsel.IstAktiv) return;

        var neueRundeId = AusgewaehlteRunde?.Id;

        if (Formular.IsDirty && Formular.RundeId is { } vorherigeRundeId && vorherigeRundeId != neueRundeId)
        {
            var vorherigeSpielId = Formular.SpielId;
            TurnierRunde? VorherigeRunde() => Runden.FirstOrDefault(r => r.Id == vorherigeRundeId);

            var antwort = await _dialogService.UngespeicherteAenderungenAbfragenAsync();
            bool wechseln = await AntwortUmsetzenAsync(antwort, async () =>
                vorherigeSpielId is not { } spielId ||
                VorherigeRunde() is { } vorherigeRunde &&
                await ErgebnisSpeichernAsync(spielId, vorherigeRunde, danachNeuLaden: false));

            if (!wechseln)
            {
                RundeStillSetzen(VorherigeRunde());
                return;
            }

            Formular.AenderungenVergessen();
        }

        // #338: zwei echte, schnell aufeinanderfolgende Runde-Auswahländerungen (z.B. Pfeiltasten in der
        // ComboBox) können sonst überlappende Abfragen auf demselben DbContext auslösen ("second
        // operation"-Exception) — derselbe Root Cause wie #317, hier innerhalb des Tabs statt beim
        // Instanzwechsel. istNochAktuell verwirft veraltete Zwischenauswahlen aus der Lock-Warteschlange
        // (Ruckel-Fund aus #338-Test) — nur die zuletzt tatsächlich ausgewählte Runde führt die Abfrage aus.
        await DbContextSchutz.AusfuehrenAsync(
            BeiRundeAenderungAsync, ex => Fehlermeldung = ex.Message,
            istNochAktuell: () => AusgewaehlteRunde?.Id == neueRundeId);
    }

    /// <summary>Setzt die Antwort auf die Ungespeichert-Abfrage um und liefert, ob der Wechsel weitergeht:
    /// „Abbrechen“ nein, „Verwerfen“ ja, „Speichern“ je nach <paramref name="speichern"/> — das läuft im
    /// <see cref="DbContextSchutz"/>. Was bei „nein“ zurückgenommen und danach geladen wird, entscheidet der
    /// Aufrufer.</summary>
    private async Task<bool> AntwortUmsetzenAsync(UngespeichertAntwort antwort, Func<Task<bool>> speichern)
    {
        if (antwort == UngespeichertAntwort.Abbrechen) return false;
        if (antwort != UngespeichertAntwort.Speichern) return true;

        using (await DbContextSchutz.BetretenAsync())
            return await speichern();
    }

    private async Task BeiRundeAenderungAsync()
    {
        using var vorgang = _logger.Vorgang($"Spielplan: Runde laden (Runde {AusgewaehlteRunde?.Id})");

        Spiele.Clear();
        AusgewaehlteSpiel = null;
        FormAktiv = false;
        Fehlermeldung = null;
        Formular.ErlaubteVerfahrenSetzen([]);
        AusgewaehlteGruppe = null;
        Fokus.AuswahlFreigeben();
        FokusSpieltageAbgleichen();
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
        SpieleHinzufuegen(liste, AusgewaehlteRunde);

        OnPropertyChanged(nameof(HatPlatzhalterSpiele));
        if (VorschauAktiv) await AktualisierePlatzhalterVorschauAsync();

        var regelwerkId = AusgewaehlteRunde.RegelwerkId ?? AusgewaehlteInstanz?.StandardRegelwerkId;
        var regelwerk = regelwerkId is { } rid ? await _regelwerkService.FindenNachIdAsync(rid) : null;

        Formular.ErlaubteVerfahrenSetzen(regelwerk?.Entscheidungsverfahren
            .OrderBy(v => v.Reihenfolge)
            .Select(v => v.VerfahrenArt) ?? []);

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
        if (_spielWechsel.IstAktiv) return;

        var neuesSpielId = AusgewaehlteSpiel?.Id;

        if (Formular.IsDirty && Formular.SpielId is { } vorherigeSpielId &&
            Formular.RundeId == AusgewaehlteRunde?.Id && vorherigeSpielId != neuesSpielId)
        {
            var vorherigeRundeId = Formular.RundeId!.Value;

            var antwort = await _dialogService.UngespeicherteAenderungenAbfragenAsync();

            // Die Unterdrückung reicht vom Speichern bis zum Setzen der Auswahl: das Neuladen nach dem Speichern
            // baut die Liste neu auf und nullt dabei die Auswahl.
            using (_spielWechsel.Betreten())
            {
                // Ein gescheitertes Speichern hält den Spielwechsel auf wie beim Rundenwechsel: die Auswahl geht
                // zurück auf das vorherige Spiel, Eingabe und Fehlermeldung bleiben stehen (#461).
                bool wechseln = await AntwortUmsetzenAsync(antwort, async () =>
                    AusgewaehlteRunde is not { } runde || runde.Id != vorherigeRundeId ||
                    await ErgebnisSpeichernAsync(vorherigeSpielId, runde, danachNeuLaden: true));

                int? zielSpielId = wechseln ? neuesSpielId : vorherigeSpielId;
                AusgewaehlteSpiel = Spiele.FirstOrDefault(s => s.Id == zielSpielId);
                if (!wechseln) return;
            }
        }

        // #338: siehe Kommentar in RundeWechselAsync — schützt gegen zwei echte, schnell
        // aufeinanderfolgende Spiel-Auswahländerungen; istNochAktuell verwirft veraltete Zwischenauswahlen.
        await DbContextSchutz.AusfuehrenAsync(
            BeiSpielAenderungAsync, ex => Fehlermeldung = ex.Message,
            istNochAktuell: () => AusgewaehlteSpiel?.Id == neuesSpielId);
    }

    private async Task BeiSpielAenderungAsync()
    {
        FormAktiv = false;
        Fehlermeldung = null;

        AktualisiereBefehle();

        if (AusgewaehlteSpiel is null) return;

        var spiel = await _spielService.FindenNachIdAsync(AusgewaehlteSpiel.Id);
        if (spiel is null) return;

        Formular.AusFuellenMit(spiel, AusgewaehlteRunde, RundenHinweis, await HinspielToreAsync(spiel));
        FormAktiv = true;
        AktualisiereBefehle();
    }

    /// <summary>Tore des Hinspiels (aus dessen Sicht) zu einem K.-o.-Rückspiel, sofern das Hinspiel schon ein
    /// Ergebnis hat (Issue #346).</summary>
    private async Task<(int Heim, int Auswaerts)?> HinspielToreAsync(Spiel spiel)
    {
        if (!IstKoMitRueckspiel || !spiel.IstRueckspiel || spiel.HinspielId is not int hinspielId) return null;
        if ((await _spielService.FindenNachIdAsync(hinspielId))?.Ergebnis is not { } hinspiel) return null;

        return (HinRueckspielWertung.HinspielTore(hinspiel, heim: true),
                HinRueckspielWertung.HinspielTore(hinspiel, heim: false));
    }

    private async Task SpielAuswahlWiederherstellenAsync(int? spielId)
    {
        SpielStillSetzen(spielId.HasValue ? Spiele.FirstOrDefault(s => s.Id == spielId) : null);
        await BeiSpielAenderungAsync();
    }

    // ── Ribbon-Aktionen ───────────────────────────────────────────────────────

    private async Task SpielplanNeuGenerierenAsync()
    {
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (AusgewaehlteRunde is null || AusgewaehlteInstanz is null) return;

        var rundeId = AusgewaehlteRunde.Id;
        var instanzId = AusgewaehlteInstanz.Id;
        Fehlermeldung = null;

        try
        {
            // Issue #433: vor Rückfrage und Löschen prüfen — ein ohne Auslosung automatisch generiertes Finale
            // (#113) hat Spiele, aber keine abgeschlossene Auslosung; das Löschen davor nahm ihm das Spiel
            // samt Ergebnis, ohne dass etwas neu generiert werden konnte.
            var auslosung = await _auslosungService.FindenAbgeschlosseneNachRundeAsync(instanzId, rundeId);
            if (auslosung is null)
            {
                Fehlermeldung = MeldungFehlendeAuslosung(AusgewaehlteRunde, Spiele.Count > 0);
                return;
            }

            if (Spiele.Count > 0)
            {
                var bestaetigt = await _dialogService.BestaetigenAsync(
                    "Spielplan neu generieren",
                    $"Die Runde '{AusgewaehlteRunde.Name}' hat bereits {Spiele.Count} Spiel(e). " +
                    "Soll der Spielplan gelöscht und neu generiert werden?");
                if (!bestaetigt) return;
            }

            using var vorgang = _logger.Vorgang($"Spielplan neu generieren (Runde {rundeId})");
            // Löschen, Generieren und die eine Kaskade danach (#320, #306) stehen im Dienst — derselbe Ablauf
            // wie beim Abschließen einer Auslosung.
            await _kaskade.RundenSpielplanAusAuslosungErsetzenAsync(rundeId, [.. auslosung.Ergebnisse]);

            await SpieleNeuLadenAsync(rundeId);
            OnPropertyChanged(nameof(HatPlatzhalterSpiele));
            AusgewaehlteSpiel = null;
            await _baumService.NachSpielplanAenderungAktualisierenAsync(AusgewaehlteRunde.TurnierInstanzId);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    /// <summary>Issue #433: fest vorgegebene Paarungen entstehen unter „Turniere“ im Reiter „Vorbelegung“ und
    /// liegen erst nach dem Abschließen dort als abgeschlossene Auslosung vor — für solche Runden gibt es in
    /// der Turnierdurchführung keine Auslosung, auf die die Meldung verweisen könnte. Eine Runde mit zwei
    /// Teilnehmern bekommt ihr Spiel ohne Auslosung automatisch (#113,
    /// <c>FinaleAutomatik.AbgleichenAsync</c>) — auch dort führt der Hinweis auf die Auslosung in
    /// die Irre.</summary>
    private static string MeldungFehlendeAuslosung(TurnierRunde runde, bool hatSpiele)
    {
        if (runde.PaarungFestVorgegeben)
            return "Für diese Runde sind die fest vorgegebenen Paarungen noch nicht festgelegt. " +
                   "Bitte zuerst unter „Turniere“ im Reiter „Vorbelegung“ die Paarungen festlegen und abschließen.";

        if (runde.EingangsMannschaftsanzahl != 2)
            return "Für diese Runde wurde noch keine Auslosung abgeschlossen. " +
                   "Bitte zuerst die Auslosung durchführen.";

        return hatSpiele
            ? "Das Spiel dieser Runde wurde ohne Auslosung automatisch aus der vorherigen Runde angelegt. " +
              "Ein Neugenerieren ist hier nicht nötig."
            : "Diese Runde hat nur zwei Teilnehmer und braucht keine Auslosung: Das Spiel wird automatisch " +
              "angelegt, sobald beide Teilnehmer aus der vorherigen Runde feststehen.";
    }

    private bool KannNaechsteSchweizerRundeGenerieren() =>
        ZeigtSchweizerRundenSteuerung && Spiele.Count > 0 &&
        SchweizerAktuellerSpieltagKomplett && SchweizerAktuellerSpieltag < SchweizerZielSpieltage;

    private async Task NaechsteSchweizerRundeGenerierenAsync()
    {
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (AusgewaehlteRunde is null) return;
        var rundeId = AusgewaehlteRunde.Id;
        Fehlermeldung = null;
        try
        {
            using var vorgang = _logger.Vorgang($"Spielplan: nächste Schweizer Runde generieren (Runde {rundeId})");
            await _spielplanService.SchweizerNaechsteRundeGenerierenAsync(rundeId);

            await SpieleNeuLadenAsync(rundeId);

            // Issue #363: direkt den gerade generierten Spieltag zeigen.
            if (AusgewaehlteGruppe is null) Fokus.LetztenSpieltagZeigen();
            OnPropertyChanged(nameof(HatPlatzhalterSpiele));
            await _baumService.NachSpielplanAenderungAktualisierenAsync(AusgewaehlteRunde.TurnierInstanzId);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    private async Task SpeichernAsync()
    {
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (AusgewaehlteSpiel is null || AusgewaehlteRunde is null) return;
        var spielId = AusgewaehlteSpiel.Id;

        if (await ErgebnisSpeichernAsync(spielId, AusgewaehlteRunde, danachNeuLaden: true))
            AusgewaehlteSpiel = Spiele.FirstOrDefault(s => s.Id == spielId);
    }

    /// <summary>Speichert das Formular als Ergebnis des Spiels; läuft im bereits betretenen
    /// <see cref="DbContextSchutz"/>.</summary>
    /// <param name="danachNeuLaden">Tabellen und Spiele der Runde danach frisch laden — nur, wenn
    /// <paramref name="runde"/> die ausgewählte bleibt. Beim Rundenwechsel lädt die neue Runde ohnehin.</param>
    private async Task<bool> ErgebnisSpeichernAsync(int spielId, TurnierRunde runde, bool danachNeuLaden)
    {
        Fehlermeldung = null;
        try
        {
            using var vorgang = _logger.Vorgang($"Spielplan: Ergebnis speichern (Spiel {spielId})");
            await _spielService.ErgebnisErfassenAsync(spielId, Formular.AlsEingabe());
            await _finaleAutomatik.AbgleichenAsync(runde.TurnierInstanzId);

            if (!danachNeuLaden) return true;

            // Das gesamte Neuladen läuft unterdrückt: sowohl das Neusetzen von AusgewaehlteGruppe in
            // GruppenTabellenLadenAsync als auch FokusSpiele geben der Fokus-Liste (Gruppenfilter/Spieltag) eine
            // neue ItemsSource, die deren SelectedItem nullt — das löste trotz gerade laufendem Speichern die
            // Ungespeichert-Abfrage aus (Issue #345).
            using (_spielWechsel.Betreten())
                await SpieleNeuLadenAsync(runde.Id);

            return true;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
            return false;
        }
    }

    private void FormZuruecksetzen() =>
        _ = DbContextSchutz.AusfuehrenAsync(BeiSpielAenderungAsync, ex => Fehlermeldung = ex.Message);

    private Task ErgebnisLoeschenAsync() =>
        MehrfachAusgewaehlteSpiele.Count > 1 ? Mengen.AuswahlZuruecksetzenAsync() : ErgebnisEinzelLoeschenAsync();

    private async Task ErgebnisEinzelLoeschenAsync()
    {
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (AusgewaehlteSpiel is null || !Formular.HatErgebnis || AusgewaehlteRunde is null) return;

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
            using var vorgang = _logger.Vorgang($"Spielplan: Ergebnis löschen (Spiel {spielId})");
            await _spielService.ErgebnisLoeschenAsync(spielId);

            await SpieleNeuLadenAsync(rundeId);

            AusgewaehlteSpiel = Spiele.FirstOrDefault(s => s.Id == spielId);
            await _baumService.NachSpielplanAenderungAktualisierenAsync(AusgewaehlteRunde.TurnierInstanzId);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    private async Task<string> KaskadenHinweisAsync(IReadOnlyList<int> spielIds)
    {
        if (AusgewaehlteInstanz is null || spielIds.Count == 0) return "";
        using var vorgang = _logger.Vorgang($"Spielplan: Kaskaden-Vorschau ({spielIds.Count} Spiele)");
        var vorschau = await _kaskade.PlanenAsync(AusgewaehlteInstanz.Id, spielIds.ToHashSet());
        return vorschau.HatAuswirkungen
            ? $"\n\nZusätzlich werden dadurch {vorschau.GesamtBetroffeneSpiele} Ergebnis(se) in Folgerunden " +
              "zurückgesetzt, deren Paarungen davon abhängen."
            : "";
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    private void AktualisiereBefehle()
    {
        _neuBefehl.NotifyCanExecuteChanged();
        _speichernBefehl.NotifyCanExecuteChanged();
        _abbrechenBefehl.NotifyCanExecuteChanged();
        _loeschenBefehl.NotifyCanExecuteChanged();
        Mengen.BefehleMelden();
        _ausfuehrlichSimulierenBefehl.NotifyCanExecuteChanged();
        _spielverlaufAnsehenBefehl.NotifyCanExecuteChanged();
        SchweizerAnzeigeAktualisieren();
    }

    private void SpieleHinzufuegen(IReadOnlyList<Spiel> spiele, TurnierRunde? runde)
    {
        foreach (var zeile in SpielZeilenMapper.SpielplanZeilen(spiele, runde, _teamGruppeMap, _platzhalterVorschau))
            Spiele.Add(zeile);

        FokusSpieltageAbgleichen();
    }

    private int? _vorherigeFokusGruppenNummer;

    private void BeiGruppeFokusAenderung()
    {
        bool istEchterGruppenwechsel = AusgewaehlteGruppe?.GruppenNummer != _vorherigeFokusGruppenNummer;
        _vorherigeFokusGruppenNummer = AusgewaehlteGruppe?.GruppenNummer;

        Mengen.BefehleMelden();
        _zurueckZurUebersichtBefehl.NotifyCanExecuteChanged();

        // Beim Gruppenwechsel bleibt der angezeigte Spieltag stehen, sofern die Gruppe ihn hat (Issue #363).
        if (istEchterGruppenwechsel)
            FokusSpieltageAbgleichen();
    }

    private IReadOnlyList<SpielZeile>? SpieltagRelevanteSpiele() =>
        AusgewaehlteGruppe is { } gruppe ? Spiele.Where(s => s.GruppeNummer == gruppe.GruppenNummer).ToList()
        : IstSpieltagRunde ? Spiele.ToList()
        : null;

    /// <summary>Gibt dem Spieltag-Fokus die gerade betrachteten Spiele: die der gewählten Gruppe, sonst alle
    /// einer Runde mit Spieltagen (Issue #363).</summary>
    private void FokusSpieltageAbgleichen()
    {
        Fokus.Abgleichen(
            SpieltagRelevanteSpiele() ?? [], nachGruppeGegliedert: AusgewaehlteGruppe is null && IstGruppenphase);
        Mengen.BefehleMelden();
    }

    private GruppenTabelle? EffektiveGruppe =>
        AusgewaehlteGruppe ?? (AusgewaehlteSpiel?.GruppeNummer is { } gn
            ? GruppenTabellen.FirstOrDefault(g => g.GruppenNummer == gn)
            : null);

    private int? EffektiverSpieltag =>
        ZeigeFokusListe && Fokus.GewaehlterSpieltag is { } spieltag
            ? spieltag
            : AusgewaehlteSpiel?.Spieltag;

    private bool CanErgebnisLoeschen() =>
        !IstSimulierend
        && (MehrfachAusgewaehlteSpiele.Count > 1 && MehrfachAusgewaehlteSpiele.Any(s => s.HatErgebnis)
            || FormAktiv && Formular.HatErgebnis);

    // ── Gemeinsamer Ablauf ────────────────────────────────────────────────────

    /// <summary>Führt eine Aktion auf mehreren Spielen der Runde aus und zeigt danach den neuen Stand. Läuft im
    /// bereits betretenen <see cref="DbContextSchutz"/>.</summary>
    async Task ISpielmengenKontext.SpielmengeBearbeitenAsync(
        string vorgangsName, Func<Task> aktion, AuswahlDanach auswahlDanach, bool finaleAbgleichen, bool baumNachziehen)
    {
        if (AusgewaehlteRunde is not { } runde) return;
        int? spielId = AusgewaehlteSpiel?.Id;
        Fehlermeldung = null;
        IstSimulierend = true;
        try
        {
            using var vorgang = _logger.Vorgang(vorgangsName);
            await aktion();
            if (finaleAbgleichen) await _finaleAutomatik.AbgleichenAsync(runde.TurnierInstanzId);

            await SpieleNeuLadenAsync(runde.Id);
            await SpielAuswahlSetzenAsync(auswahlDanach, spielId);
            if (baumNachziehen) await _baumService.NachSpielplanAenderungAktualisierenAsync(runde.TurnierInstanzId);
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

    private async Task SpielAuswahlSetzenAsync(AuswahlDanach auswahlDanach, int? bisherigesSpielId)
    {
        if (auswahlDanach == AuswahlDanach.Wiederherstellen)
        {
            await SpielAuswahlWiederherstellenAsync(bisherigesSpielId);
            return;
        }

        AusgewaehlteSpiel = null;
        if (auswahlDanach == AuswahlDanach.MitMehrfachauswahlAufheben)
            MehrfachAusgewaehlteSpiele = [];
    }

    /// <summary>Tabellen und Spiele der Runde frisch aus der Datenbank; die Spiel-Auswahl setzt der Aufrufer.</summary>
    private async Task SpieleNeuLadenAsync(int rundeId)
    {
        await TabellenNeuLadenAsync(rundeId);

        var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
        Spiele.Clear();
        SpieleHinzufuegen(neueSpiele, AusgewaehlteRunde);
        Fokus.SpieleMelden();
    }
}
