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
public partial class SpielplanViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
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
    private readonly SimulationsService _simulationsService;
    private readonly TurnierInstanzService _instanzService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<SpielplanViewModel> _logger;

    private Dictionary<int, int> _teamGruppeMap = [];
    private IReadOnlyDictionary<int, (string? Heim, string? Auswaerts)> _platzhalterVorschau =
        new Dictionary<int, (string?, string?)>();

    private int _spielWechselUnterdrueckt;
    private int _rundeWechselUnterdrueckt;

    /// <summary>Reiht alle Datenbankzugriffe dieses Reiters hintereinander: Runde-/Spiel-Wechsel (#338), die
    /// Aktionen (Simulieren, Zurücksetzen, Speichern …) und — über den vom
    /// <see cref="TurnierdurchfuehrungViewModel"/> gesetzten gemeinsamen Schutz — auch Turnier-/Reiterwechsel
    /// (#415). Ein eigener Schutz je Reiter schloss das nicht aus: eine laufende Simulation kollidierte mit dem
    /// Laden nach einem Runden-, Reiter- oder Turnierwechsel und brach dabei selbst mittendrin ab. Nicht
    /// wiedereintrittsfähig — <see cref="InstanzSetzenAsync"/> läuft bereits unter dem Schutz und darf nichts
    /// abwarten, das ihn betritt (die selbstverursachte Reentrancy decken
    /// _rundeWechselUnterdrueckt/_spielWechselUnterdrueckt ab).</summary>
    public DbContextReentrancySchutz DbContextSchutz { get; set; } = new();

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
        Fokus.FokusSpieleMelden();
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
                        new RibbonUnteraktion(HinrundeMenuText, HinrundeSimulierenBefehl),
                        new RibbonUnteraktion(RueckrundeMenuText, RueckrundeSimulierenBefehl),
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
                        new RibbonUnteraktion(HinrundeMenuText, HinrundeZuruecksetzenBefehl),
                        new RibbonUnteraktion(RueckrundeMenuText, RueckrundeZuruecksetzenBefehl),
                        new RibbonUnteraktion("Gruppe", GruppeZuruecksetzenBefehl),
                        new RibbonUnteraktion(SpieltagZuruecksetzenMenuText, SpieltagZuruecksetzenBefehl),
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
        _simulationsService = simulationsService;
        _instanzService = instanzService;
        _dialogService = dialogService;
        _logger = logger;

        _neuBefehl = new AsyncRelayCommand(SpielplanNeuGenerierenAsync, () => HatRunde);
        _speichernBefehl = new AsyncRelayCommand(SpeichernAsync, () => FormAktiv && Formular.IsDirty);
        _abbrechenBefehl = new RelayCommand(FormZuruecksetzen, () => FormAktiv && Formular.IsDirty);
        _loeschenBefehl = new AsyncRelayCommand(ErgebnisLoeschenAsync, CanErgebnisLoeschen);

        _zurueckZurUebersichtBefehl = new RelayCommand(() => AusgewaehlteGruppe = null, () => AusgewaehlteGruppe is not null);

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
            if (e.PropertyName is not (nameof(SpieltagFokusViewModel.FokusAlleAktiv)
                or nameof(SpieltagFokusViewModel.FokusAktiverSpieltag))) return;
            OnPropertyChanged(nameof(SpieltagSimulierenMenuText));
            OnPropertyChanged(nameof(SpieltagZuruecksetzenMenuText));
            OnPropertyChanged(nameof(RibbonZusatzGruppen));
            _spieltagSimulierenBefehl.NotifyCanExecuteChanged();
            _spieltagZuruecksetzenBefehl.NotifyCanExecuteChanged();
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
        _spielWechselUnterdrueckt++;
        try
        {
            await TabellenNeuLadenAsync(rundeId);
            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            SpieleHinzufuegen(neueSpiele, runde);
            Fokus.FokusSpieleMelden();
            OnPropertyChanged(nameof(HatPlatzhalterSpiele));

            AusgewaehlteSpiel = spielId.HasValue ? Spiele.FirstOrDefault(s => s.Id == spielId) : null;

            await _baumService.NachSpielplanAenderungAktualisierenAsync(runde.TurnierInstanzId);
        }
        finally
        {
            _spielWechselUnterdrueckt--;
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
                Fokus.FokusSpieleMelden();
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
        _rundeWechselUnterdrueckt++;
        try
        {
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
        finally
        {
            _rundeWechselUnterdrueckt--;
        }
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

        if (Formular.IsDirty && Formular.RundeId is { } vorherigeRundeId && vorherigeRundeId != neueRundeId)
        {
            var vorherigeSpielId = Formular.SpielId;

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
                bool gespeichert;
                using (await DbContextSchutz.BetretenAsync())
                    gespeichert = vorherigeRunde is not null &&
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

    private async Task<bool> ErgebnisSpeichernBeiRundenwechselAsync(int spielId, TurnierRunde runde)
    {
        Fehlermeldung = null;
        try
        {
            using var vorgang = _logger.Vorgang($"Spielplan: Ergebnis speichern bei Rundenwechsel (Spiel {spielId})");
            await _spielService.ErgebnisErfassenAsync(spielId, Formular.AlsEingabe());
            await _finaleAutomatik.AbgleichenAsync(runde.TurnierInstanzId);
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
        using var vorgang = _logger.Vorgang($"Spielplan: Runde laden (Runde {AusgewaehlteRunde?.Id})");

        Spiele.Clear();
        AusgewaehlteSpiel = null;
        FormAktiv = false;
        Fehlermeldung = null;
        Formular.ErlaubteVerfahrenSetzen([]);
        AusgewaehlteGruppe = null;
        Fokus.SpieltagAuswahlFreigeben();
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
        if (_spielWechselUnterdrueckt > 0) return;

        var neuesSpielId = AusgewaehlteSpiel?.Id;

        if (Formular.IsDirty && Formular.SpielId is { } vorherigeSpielId &&
            Formular.RundeId == AusgewaehlteRunde?.Id && vorherigeSpielId != neuesSpielId)
        {
            var vorherigeRundeId = Formular.RundeId!.Value;

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
                {
                    using (await DbContextSchutz.BetretenAsync())
                        await ErgebnisSpeichernAsync(vorherigeSpielId, vorherigeRundeId);
                }

                AusgewaehlteSpiel = neuesSpielId.HasValue ? Spiele.FirstOrDefault(s => s.Id == neuesSpielId) : null;
            }
            finally
            {
                _spielWechselUnterdrueckt--;
            }

            // #338: siehe Kommentar in RundeWechselAsync — schützt gegen zwei echte, schnell
            // aufeinanderfolgende Spiel-Auswahländerungen; istNochAktuell verwirft veraltete Zwischenauswahlen.
            await DbContextSchutz.AusfuehrenAsync(
                BeiSpielAenderungAsync, ex => Fehlermeldung = ex.Message,
                istNochAktuell: () => AusgewaehlteSpiel?.Id == neuesSpielId);
            return;
        }

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
            if (Spiele.Count > 0)
                // Issue #320-Folgefund: kaskadenlos löschen — LoeschenAsync würde selbst sofort wieder die
                // Kaskade auslösen, die bei einer Platzhalter-K.o.-Runde mit weiterhin abgeschlossener
                // Auslosung erneut komplett neu generiert, bevor der folgende GenerierenAusAuslosungAsync-
                // Aufruf noch einmal generiert — macht aus den erwarteten Spielen doppelt so viele.
                await _spielplanService.RundenSpielplanOhneKaskadeLoeschenAsync(rundeId);

            await _spielplanService.GenerierenAusAuslosungAsync(rundeId, [.. auslosung.Ergebnisse]);

            // Folgerunden können bereits aufgelöste Platzhalter enthalten, die auf den alten (jetzt
            // ersetzten) Spielplan verwiesen haben (#306) — ohne die Kaskade blieben sie fälschlich stehen.
            await _kaskade.KaskadeAusloesenAsync(instanzId);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            SpieleHinzufuegen(neueSpiele, AusgewaehlteRunde);
            OnPropertyChanged(nameof(HatPlatzhalterSpiele));
            Fokus.FokusSpieleMelden();
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

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            SpieleHinzufuegen(neueSpiele, AusgewaehlteRunde);

            // Issue #363: direkt den gerade generierten Spieltag zeigen.
            if (AusgewaehlteGruppe is null) Fokus.LetztenSpieltagZeigen();
            OnPropertyChanged(nameof(HatPlatzhalterSpiele));
            Fokus.FokusSpieleMelden();
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

        if (await ErgebnisSpeichernAsync(spielId, AusgewaehlteRunde.Id))
            AusgewaehlteSpiel = Spiele.FirstOrDefault(s => s.Id == spielId);
    }

    private async Task<bool> ErgebnisSpeichernAsync(int spielId, int rundeId)
    {
        if (AusgewaehlteRunde is null || AusgewaehlteRunde.Id != rundeId) return false;
        Fehlermeldung = null;
        try
        {
            using var vorgang = _logger.Vorgang($"Spielplan: Ergebnis speichern (Spiel {spielId})");
            await _spielService.ErgebnisErfassenAsync(spielId, Formular.AlsEingabe());
            await _finaleAutomatik.AbgleichenAsync(AusgewaehlteRunde.TurnierInstanzId);

            // Das gesamte Neuladen läuft im Schutz: sowohl das Neusetzen von AusgewaehlteGruppe in
            // GruppenTabellenLadenAsync als auch FokusSpiele geben der Fokus-Liste (Gruppenfilter/Spieltag) eine
            // neue ItemsSource, die deren SelectedItem nullt — das löste trotz gerade laufendem Speichern die
            // Ungespeichert-Abfrage aus (Issue #345).
            _spielWechselUnterdrueckt++;
            try
            {
                await TabellenNeuLadenAsync(rundeId);

                var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
                Spiele.Clear();
                SpieleHinzufuegen(neueSpiele, AusgewaehlteRunde);
                Fokus.FokusSpieleMelden();
            }
            finally
            {
                _spielWechselUnterdrueckt--;
            }

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
        MehrfachAusgewaehlteSpiele.Count > 1 ? ErgebnisMehrfachLoeschenAsync() : ErgebnisEinzelLoeschenAsync();

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

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            SpieleHinzufuegen(neueSpiele, AusgewaehlteRunde);
            Fokus.FokusSpieleMelden();

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

        _gruppeSimulierenBefehl.NotifyCanExecuteChanged();
        _spieltagSimulierenBefehl.NotifyCanExecuteChanged();
        _spieltagZuruecksetzenBefehl.NotifyCanExecuteChanged();
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
        Fokus.FokusSpieltageAbgleichen(
            SpieltagRelevanteSpiele() ?? [], nachGruppe: AusgewaehlteGruppe is null && IstGruppenphase);
        _spieltagSimulierenBefehl.NotifyCanExecuteChanged();
        _spieltagZuruecksetzenBefehl.NotifyCanExecuteChanged();
    }

    private GruppenTabelle? EffektiveGruppe =>
        AusgewaehlteGruppe ?? (AusgewaehlteSpiel?.GruppeNummer is { } gn
            ? GruppenTabellen.FirstOrDefault(g => g.GruppenNummer == gn)
            : null);

    private int? EffektiverSpieltag =>
        ZeigeFokusListe && !Fokus.FokusAlleAktiv
            ? Fokus.FokusAktiverSpieltag
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
        HatHinUndRueckspiel && !IstSimulierend
        && Spiele.Any(s => !s.IstRueckspiel && s.Status != SpielStatus.Abgeschlossen && !s.IstPlatzhalterSpiel);

    // K.-o.-Rückspiele nur mit abgeschlossenem Hinspiel (SimulationsService überspringt die übrigen).
    private bool CanRueckrundeSimulieren() =>
        HatHinUndRueckspiel && !IstSimulierend
        && Spiele.Any(s => s.IstRueckspiel && s.Status != SpielStatus.Abgeschlossen && !s.IstPlatzhalterSpiel
            && (!IstKoMitRueckspiel || HinspielAbgeschlossen(s)));

    private bool HinspielAbgeschlossen(SpielZeile rueckspiel) =>
        rueckspiel.HinspielId is int hinId && Spiele.Any(h => h.Id == hinId && h.HatErgebnis);

    private bool CanRundeZuruecksetzen() => HatRunde && !IstSimulierend && Spiele.Any(s => s.HatErgebnis);

    private bool CanGruppeZuruecksetzen() =>
        EffektiveGruppe is { } gruppe && !IstSimulierend
        && Spiele.Any(s => s.GruppeNummer == gruppe.GruppenNummer && s.HatErgebnis);

    private bool CanSpieltagZuruecksetzen() =>
        !IstSimulierend && SpieleAmEffektivenSpieltag().Any(s => s.HatErgebnis);

    private bool CanHinrundeZuruecksetzen() =>
        HatHinUndRueckspiel && !IstSimulierend && Spiele.Any(s => !s.IstRueckspiel && s.HatErgebnis);

    private bool CanRueckrundeZuruecksetzen() =>
        HatHinUndRueckspiel && !IstSimulierend && Spiele.Any(s => s.IstRueckspiel && s.HatErgebnis);

    private bool CanErgebnisLoeschen() =>
        !IstSimulierend
        && (MehrfachAusgewaehlteSpiele.Count > 1 && MehrfachAusgewaehlteSpiele.Any(s => s.HatErgebnis)
            || FormAktiv && Formular.HatErgebnis);

    // ── Simulation: Aktionen ───────────────────────────────────────────────────

    private async Task RundeSimulierenAsync()
    {
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (AusgewaehlteRunde is null) return;
        int rundeId = AusgewaehlteRunde.Id;
        Fehlermeldung = null;
        IstSimulierend = true;
        try
        {
            using var vorgang = _logger.Vorgang($"Spielplan: Runde simulieren (Runde {rundeId})");
            await _simulationsService.RundeSimulierenAsync(rundeId, SimulationsModus.AusfuehrlicheSimulation);
            await _finaleAutomatik.AbgleichenAsync(AusgewaehlteRunde.TurnierInstanzId);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            SpieleHinzufuegen(neueSpiele, AusgewaehlteRunde);
            Fokus.FokusSpieleMelden();
            AusgewaehlteSpiel = null;
            await _baumService.NachSpielplanAenderungAktualisierenAsync(AusgewaehlteRunde!.TurnierInstanzId);
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
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (EffektiveGruppe is not { } gruppe || AusgewaehlteRunde is null) return;
        int rundeId = AusgewaehlteRunde.Id;
        int? spielId = AusgewaehlteSpiel?.Id;
        var teamIds = gruppe.Tabelle.Select(t => t.Mannschaft.Id).ToList();
        Fehlermeldung = null;
        IstSimulierend = true;
        try
        {
            using var vorgang = _logger.Vorgang($"Spielplan: Gruppe simulieren (Runde {rundeId})");
            await _simulationsService.GruppeSimulierenAsync(rundeId, teamIds, SimulationsModus.AusfuehrlicheSimulation);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            SpieleHinzufuegen(neueSpiele, AusgewaehlteRunde);
            Fokus.FokusSpieleMelden();
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
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (AusgewaehlteRunde is null || EffektiverSpieltag is not { } spieltag) return;

        int rundeId = AusgewaehlteRunde.Id;
        int? spielId = AusgewaehlteSpiel?.Id;
        Fehlermeldung = null;
        IstSimulierend = true;
        try
        {
            using var vorgang = _logger.Vorgang($"Spielplan: Spieltag simulieren (Runde {rundeId}, Spieltag {spieltag})");
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
            SpieleHinzufuegen(neueSpiele, AusgewaehlteRunde);
            Fokus.FokusSpieleMelden();
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
        using var schutz = await DbContextSchutz.BetretenAsync();
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
            using var vorgang = _logger.Vorgang($"Spielplan: Auswahl simulieren ({spielIds.Count} Spiele)");
            await _simulationsService.SpieleSimulierenAsync(spielIds, SimulationsModus.AusfuehrlicheSimulation);
            await _finaleAutomatik.AbgleichenAsync(AusgewaehlteRunde.TurnierInstanzId);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            SpieleHinzufuegen(neueSpiele, AusgewaehlteRunde);
            Fokus.FokusSpieleMelden();
            AusgewaehlteSpiel = null;
            MehrfachAusgewaehlteSpiele = [];
            await _baumService.NachSpielplanAenderungAktualisierenAsync(AusgewaehlteRunde!.TurnierInstanzId);
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
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (AusgewaehlteRunde is null) return;
        int rundeId = AusgewaehlteRunde.Id;
        Fehlermeldung = null;
        IstSimulierend = true;
        try
        {
            using var vorgang = _logger.Vorgang($"Spielplan: Rundenhälfte simulieren (Runde {rundeId})");
            await _simulationsService.RundenHaelfteSimulierenAsync(rundeId, istRueckrunde, SimulationsModus.AusfuehrlicheSimulation);
            await _finaleAutomatik.AbgleichenAsync(AusgewaehlteRunde.TurnierInstanzId);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            SpieleHinzufuegen(neueSpiele, AusgewaehlteRunde);
            Fokus.FokusSpieleMelden();
            AusgewaehlteSpiel = null;
            await _baumService.NachSpielplanAenderungAktualisierenAsync(AusgewaehlteRunde!.TurnierInstanzId);
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
        using var schutz = await DbContextSchutz.BetretenAsync();
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
            using var vorgang = _logger.Vorgang($"Spielplan: Runde zurücksetzen ({spielIds.Count} Ergebnisse)");
            await _spielService.MehrereErgebnisseLoeschenAsync(spielIds);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            SpieleHinzufuegen(neueSpiele, AusgewaehlteRunde);
            Fokus.FokusSpieleMelden();
            AusgewaehlteSpiel = null;
            await _baumService.NachSpielplanAenderungAktualisierenAsync(AusgewaehlteRunde!.TurnierInstanzId);
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
        using var schutz = await DbContextSchutz.BetretenAsync();
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
            using var vorgang = _logger.Vorgang($"Spielplan: Gruppe zurücksetzen ({spielIds.Count} Ergebnisse)");
            await _spielService.MehrereErgebnisseLoeschenAsync(spielIds);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            SpieleHinzufuegen(neueSpiele, AusgewaehlteRunde);
            Fokus.FokusSpieleMelden();
            await SpielAuswahlWiederherstellenAsync(spielId);
            await _baumService.NachSpielplanAenderungAktualisierenAsync(AusgewaehlteRunde!.TurnierInstanzId);
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
        using var schutz = await DbContextSchutz.BetretenAsync();
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
            using var vorgang = _logger.Vorgang($"Spielplan: Spieltag zurücksetzen ({spielIds.Count} Ergebnisse)");
            await _spielService.MehrereErgebnisseLoeschenAsync(spielIds);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            SpieleHinzufuegen(neueSpiele, AusgewaehlteRunde);
            Fokus.FokusSpieleMelden();
            await SpielAuswahlWiederherstellenAsync(spielId);
            await _baumService.NachSpielplanAenderungAktualisierenAsync(AusgewaehlteRunde!.TurnierInstanzId);
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
        using var schutz = await DbContextSchutz.BetretenAsync();
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
            using var vorgang = _logger.Vorgang($"Spielplan: Auswahl zurücksetzen ({spielIds.Count} Ergebnisse)");
            await _spielService.MehrereErgebnisseLoeschenAsync(spielIds);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            SpieleHinzufuegen(neueSpiele, AusgewaehlteRunde);
            Fokus.FokusSpieleMelden();
            AusgewaehlteSpiel = null;
            MehrfachAusgewaehlteSpiele = [];
            await _baumService.NachSpielplanAenderungAktualisierenAsync(AusgewaehlteRunde!.TurnierInstanzId);
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
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (AusgewaehlteRunde is null) return;
        var spielIds = Spiele.Where(s => s.IstRueckspiel == istRueckrunde && s.HatErgebnis)
            .Select(s => s.Id).ToList();
        if (spielIds.Count == 0) return;
        var kaskadenHinweis = await KaskadenHinweisAsync(spielIds);

        var bezeichnung = IstKoMitRueckspiel
            ? (istRueckrunde ? "Rückspiele" : "Hinspiele")
            : (istRueckrunde ? "Rückrunde" : "Hinrunde");
        // Issue #387: Ohne Hinspiel-Ergebnis ist die Gesamtwertung eines K.-o.-Rückspiels hinfällig — der
        // Ergebnis-Service setzt die zugehörigen Rückspiele mit zurück (Issue #346).
        var rueckspielHinweis = IstKoMitRueckspiel && !istRueckrunde
            && Spiele.Any(s => s.IstRueckspiel && s.HatErgebnis && s.HinspielId is int h && spielIds.Contains(h))
            ? "\n\nDie Ergebnisse der zugehörigen Rückspiele werden ebenfalls zurückgesetzt."
            : "";
        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Ergebnisse zurücksetzen",
            $"Alle {spielIds.Count} Ergebnis(se) der {bezeichnung} zurücksetzen?{rueckspielHinweis}{kaskadenHinweis}");
        if (!bestaetigt) return;

        int rundeId = AusgewaehlteRunde.Id;
        Fehlermeldung = null;
        IstSimulierend = true;
        try
        {
            using var vorgang = _logger.Vorgang($"Spielplan: Rundenhälfte zurücksetzen ({spielIds.Count} Ergebnisse)");
            await _spielService.MehrereErgebnisseLoeschenAsync(spielIds);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            SpieleHinzufuegen(neueSpiele, AusgewaehlteRunde);
            Fokus.FokusSpieleMelden();
            AusgewaehlteSpiel = null;
            await _baumService.NachSpielplanAenderungAktualisierenAsync(AusgewaehlteRunde!.TurnierInstanzId);
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
}
