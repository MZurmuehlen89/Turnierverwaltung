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

// ── Hilfsklasse — 1:1 aus Wpf/ViewModels/SpielplanViewModel.cs portiert ──────────────────────────

public partial class SpielZeile : ObservableObject
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
    /// <summary>Gesamtergebnis-Zeile unter einem K.-o.-Rückspiel (Issue #340), z.B. "Gesamt 3:3 · i.E. 2:4".</summary>
    public string GesamtText { get; init; } = "";
    public bool HatGesamtText => !string.IsNullOrEmpty(GesamtText);
    public bool IstRueckspiel { get; init; }
    public int? HinspielId { get; init; }
    public bool IstPlatzhalterSpiel { get; init; }
    public bool HeimIstVorschau { get; init; }
    public bool AuswaertsIstVorschau { get; init; }
    public int? GruppeNummer { get; init; }
    /// <summary>Erste Zeile einer Hin-/Rückspiel-Paarung nach der ersten (Issue #340) — bekommt Abstand nach oben,
    /// damit Paarungen wie in der kicker-Paarungsansicht als Block erkennbar sind.</summary>
    public bool IstPaarungsStart { get; init; }
    public Microsoft.UI.Xaml.Thickness PaarungsAbstand => IstPaarungsStart ? new(0, 10, 0, 0) : new(0);
    public string GruppeAnzeige => GruppeNummer.HasValue ? $"Gr.{GruppeNummer}" : "";

    /// <summary>Zwischenüberschrift über der ersten Zeile einer Gruppe in der Spieltag-Ansicht der Gruppenphase
    /// (Issue #363) — hängt von der gerade angezeigten Auswahl ab, deshalb veränderlich statt init.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatGruppenKopf))]
    private string? _gruppenKopf;

    public bool HatGruppenKopf => GruppenKopf is not null;
}

/// <summary>Eintrag der Spieltag-Auswahl (Issue #363); <see cref="Nummer"/> <c>null</c> = "Alle Spieltage".</summary>
public sealed class SpieltagOption(int? nummer, string anzeige)
{
    public int? Nummer { get; } = nummer;
    public string Anzeige { get; } = anzeige;
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
    private readonly ILogger<SpielplanViewModel> _logger;

    private int _populiertGerade;
    private Dictionary<int, int> _teamGruppeMap = [];
    private readonly List<VerfahrenArt> _erlaubteVerfahren = [];
    private IReadOnlyDictionary<int, (string? Heim, string? Auswaerts)> _platzhalterVorschau =
        new Dictionary<int, (string?, string?)>();

    private int? _formularSpielId;

    // Issue #346: K.-o.-Paarung mit Hin- und Rückspiel — im Hinspiel gibt es kein Entscheidungsverfahren,
    // im Rückspiel nur bei ausgeglichenem Gesamtergebnis (Hinspiel-Tore aus Sicht des Hinspiels).
    private bool _formularIstKoHinspiel;
    private (int Heim, int Auswaerts)? _formularHinspielTore;
    private int? _formularRundeId;
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
    [NotifyPropertyChangedFor(nameof(FokusSpiele))]
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

    public ObservableCollection<int> FokusSpieltage { get; } = [];

    // ── Spieltag-Auswahl (Issue #363, Vorbild kicker.de: ‹ [n. Spieltag ▾] ›) ─────────────────────────

    public ObservableCollection<SpieltagOption> SpieltagOptionen { get; } = [];
    private int _spieltagOptionenImAufbau;

    // false nach Runden-/Gruppenwechsel: der nächste Abgleich wählt den Start-Spieltag neu, statt die
    // bisherige Auswahl zu übernehmen (Spieltagnummern verschiedener Runden überschneiden sich).
    private bool _spieltagAuswahlFestgelegt;

    /// <summary>Abgleich per Referenz aus <see cref="SpieltagOptionen"/> (WinUI-ComboBox, siehe #347-Fund);
    /// <c>null</c> beim Neuaufbau der Liste wird ignoriert.</summary>
    public SpieltagOption? AusgewaehlteSpieltagOption
    {
        get => FokusAlleAktiv
            ? SpieltagOptionen.FirstOrDefault(o => o.Nummer is null)
            : SpieltagOptionen.FirstOrDefault(o => o.Nummer == FokusAktiverSpieltag);
        set
        {
            if (value is null || _spieltagOptionenImAufbau > 0) return;
            if (value.Nummer is { } nummer)
            {
                FokusAktiverSpieltag = nummer;
                FokusAlleAktiv = false;
            }
            else
            {
                FokusAlleAktiv = true;
            }
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FokusSpiele))]
    [NotifyPropertyChangedFor(nameof(SpieltagSimulierenMenuText))]
    [NotifyPropertyChangedFor(nameof(SpieltagZuruecksetzenMenuText))]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private bool _fokusAlleAktiv = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FokusSpiele))]
    [NotifyPropertyChangedFor(nameof(SpieltagSimulierenMenuText))]
    [NotifyPropertyChangedFor(nameof(SpieltagZuruecksetzenMenuText))]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private int _fokusAktiverSpieltag;

    public bool HatMehrereSpieltage => FokusSpieltage.Count > 1;

    public string SpieltagSimulierenMenuText => EffektiverSpieltag is { } st ? $"Spieltag {st} simulieren" : "Spieltag simulieren";
    public string SpieltagZuruecksetzenMenuText => EffektiverSpieltag is { } st ? $"Spieltag {st} zurücksetzen" : "Spieltag zurücksetzen";
    public bool KannFokusZurueck => !FokusAlleAktiv && FokusSpieltage.Count > 0 && FokusAktiverSpieltag > FokusSpieltage[0];
    public bool KannFokusVor => FokusSpieltage.Count > 1 && (FokusAlleAktiv || FokusAktiverSpieltag < FokusSpieltage[^1]);

    public IReadOnlyList<SpielZeile> FokusSpiele => BerechneFokusSpiele();

    private readonly RelayCommand _zurueckZurUebersichtBefehl;
    private readonly RelayCommand _fokusVorigerSpieltag;
    private readonly RelayCommand _fokusNaechsterSpieltag;

    public ICommand ZurueckZurUebersichtBefehl => _zurueckZurUebersichtBefehl;
    public ICommand FokusVorigerSpieltag => _fokusVorigerSpieltag;
    public ICommand FokusNaechsterSpieltag => _fokusNaechsterSpieltag;

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
        IstKoRunde && HatVerlaengerungsVerfahren && EntscheidungsverfahrenNoetig();

    public bool ElfmeterAktiv
    {
        get
        {
            if (!IstKoRunde || !HatElfmeterVerfahren) return false;
            if (!EntscheidungsverfahrenNoetig()) return false;
            if (!HatVerlaengerungsVerfahren) return true;
            return VerlaengerungToreDifferenz() == 0;
        }
    }

    /// <summary>Tordifferenz (Heim minus Auswärts) allein der in der Verlängerung gefallenen Tore, oder
    /// <c>null</c>, solange der Stand nach Verlängerung nicht vollständig eingegeben ist. Die Felder enthalten
    /// den Spielstand inklusive regulärer Spielzeit — entscheidend ist aber nur, was IN der Verlängerung
    /// fällt: beim Einzelspiel identisch mit dem Vergleich der Spielstände, im K.-o.-Rückspiel mit
    /// ausgeglichenem Gesamtergebnis nicht (reguläre Spielzeit z.B. 3:2, Issue #346).</summary>
    private int? VerlaengerungToreDifferenz()
    {
        if (ParseIntNullable(HeimToreVerlaengerung) is not { } heimV ||
            ParseIntNullable(AuswaertsToreVerlaengerung) is not { } ausV ||
            ParseIntNullable(HeimToreRegulaer) is not { } heimReg ||
            ParseIntNullable(AuswaertsToreRegulaer) is not { } ausReg) return null;

        return (heimV - heimReg) - (ausV - ausReg);
    }

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
            ? await _spielplanService.PlatzhalterVorschauAsync(AusgewaehlteRunde.Id)
            : new Dictionary<int, (string?, string?)>();

        if (AusgewaehlteRunde is null) return;
        var spiele = await _spielService.AlleNachRundeAsync(AusgewaehlteRunde.Id);
        Spiele.Clear();
        SpieleHinzufuegen(spiele, AusgewaehlteRunde);
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
        _auslosungService = auslosungService;
        _baumService = baumService;
        _regelwerkService = regelwerkService;
        _tabellenService = tabellenService;
        _simulationsService = simulationsService;
        _instanzService = instanzService;
        _dialogService = dialogService;
        _logger = logger;

        _neuBefehl = new AsyncRelayCommand(SpielplanNeuGenerierenAsync, () => HatRunde);
        _speichernBefehl = new AsyncRelayCommand(SpeichernAsync, () => FormAktiv && IsDirty);
        _abbrechenBefehl = new RelayCommand(FormZuruecksetzen, () => FormAktiv && IsDirty);
        _loeschenBefehl = new AsyncRelayCommand(ErgebnisLoeschenAsync, CanErgebnisLoeschen);

        _zurueckZurUebersichtBefehl = new RelayCommand(() => AusgewaehlteGruppe = null, () => AusgewaehlteGruppe is not null);
        _fokusVorigerSpieltag = new RelayCommand(FokusEinSpieltag_Vorig, () => KannFokusZurueck);
        _fokusNaechsterSpieltag = new RelayCommand(FokusEinSpieltag_Vor, () => KannFokusVor);

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
        Spiele.CollectionChanged += (_, __) =>
        {
            SchweizerAnzeigeAktualisieren();
            _vorschauUmschaltenBefehl.NotifyCanExecuteChanged();
        };
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
        var runde = AusgewaehlteRunde;

        // #317: Guard umschließt Spiele.Clear()/Add() bis zum Ende der davon abhängigen Folge-Abfragen,
        // nicht nur die AusgewaehlteSpiel-Zuweisung — eine WinUI-ComboBox kann SelectionChanged bei
        // ItemsSource.Clear() über die DispatcherQueue VERZÖGERT statt synchron auslösen. Feuert der
        // Callback erst während TurnierBaumAktualisierenAsync/BeiSpielAenderungAsync, entsteht sonst ein
        // ungeschützter Parallelzugriff auf denselben DbContext ("second operation"-Exception, #317).
        _spielWechselUnterdrueckt++;
        try
        {
            await TabellenNeuLadenAsync(rundeId);
            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            SpieleHinzufuegen(neueSpiele, runde);
            OnPropertyChanged(nameof(FokusSpiele));
            OnPropertyChanged(nameof(HatPlatzhalterSpiele));

            AusgewaehlteSpiel = spielId.HasValue ? Spiele.FirstOrDefault(s => s.Id == spielId) : null;

            await TurnierBaumAktualisierenAsync(runde.TurnierInstanzId);
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
                break;
            case nameof(FokusAlleAktiv):
            case nameof(FokusAktiverSpieltag):
                OnPropertyChanged(nameof(AusgewaehlteSpieltagOption));
                _fokusVorigerSpieltag.NotifyCanExecuteChanged();
                _fokusNaechsterSpieltag.NotifyCanExecuteChanged();
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

            IsDirty = false;
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
        using var vorgang = _logger.Vorgang($"Spielplan: Runde laden (Runde {AusgewaehlteRunde?.Id})");

        Spiele.Clear();
        AusgewaehlteSpiel = null;
        FormAktiv = false;
        Fehlermeldung = null;
        _erlaubteVerfahren.Clear();
        AusgewaehlteGruppe = null;
        _spieltagAuswahlFestgelegt = false;
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

        bool koMitRueckspiel = IstKoRunde && AusgewaehlteRunde?.MitRueckspiel == true;
        _formularIstKoHinspiel = koMitRueckspiel && !spiel.IstRueckspiel;
        _formularHinspielTore  = null;
        if (koMitRueckspiel && spiel.IstRueckspiel && spiel.HinspielId is int hinspielId &&
            (await _spielService.FindenNachIdAsync(hinspielId))?.Ergebnis is { } hinErgebnis)
        {
            _formularHinspielTore = (
                HinRueckspielWertung.HinspielTore(hinErgebnis, heim: true),
                HinRueckspielWertung.HinspielTore(hinErgebnis, heim: false));
        }

        FormularAusFuellenMit(spiel);
        OnPropertyChanged(nameof(VerlaengerungAktiv));
        OnPropertyChanged(nameof(ElfmeterAktiv));
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
            FormSpielInfo = (spiel.Spieltag.HasValue
                ? $"Spieltag {spiel.Spieltag} · Spiel #{spiel.Spielnummer}"
                : $"Spiel #{spiel.Spielnummer}") + RundenHinweis;
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
        using var schutz = await DbContextSchutz.BetretenAsync();
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
            using var vorgang = _logger.Vorgang($"Spielplan neu generieren (Runde {rundeId})");
            if (Spiele.Count > 0)
                // Issue #320-Folgefund: kaskadenlos löschen — LoeschenAsync würde selbst sofort wieder die
                // Kaskade auslösen, die bei einer Platzhalter-K.o.-Runde mit weiterhin abgeschlossener
                // Auslosung erneut komplett neu generiert, bevor der folgende GenerierenAusAuslosungAsync-
                // Aufruf noch einmal generiert — macht aus den erwarteten Spielen doppelt so viele.
                await _spielplanService.RundenSpielplanOhneKaskadeLoeschenAsync(rundeId);
            var auslosung = await _auslosungService.FindenAbgeschlosseneNachRundeAsync(instanzId, rundeId);
            if (auslosung is null)
            {
                Fehlermeldung = "Für diese Runde wurde noch keine Auslosung abgeschlossen. " +
                                "Bitte zuerst die Auslosung durchführen.";
                return;
            }

            await _spielplanService.GenerierenAusAuslosungAsync(rundeId, [.. auslosung.Ergebnisse]);

            // Folgerunden können bereits aufgelöste Platzhalter enthalten, die auf den alten (jetzt
            // ersetzten) Spielplan verwiesen haben (#306) — ohne die Kaskade blieben sie fälschlich stehen.
            await _spielplanService.KaskadeAusloesenAsync(instanzId);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            SpieleHinzufuegen(neueSpiele, AusgewaehlteRunde);
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
            if (AusgewaehlteGruppe is null && FokusSpieltage.Count > 0)
            {
                FokusAktiverSpieltag = FokusSpieltage[^1];
                FokusAlleAktiv = false;
            }
            OnPropertyChanged(nameof(HatPlatzhalterSpiele));
            OnPropertyChanged(nameof(FokusSpiele));
            await TurnierBaumAktualisierenAsync(AusgewaehlteRunde.TurnierInstanzId);
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
            await _spielService.ErgebnisErfassenAsync(spielId, BaueEingabe());
            await _spielplanService.FinaleAutomatischGenerierenWennBereitAsync(AusgewaehlteRunde.TurnierInstanzId);

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
                OnPropertyChanged(nameof(FokusSpiele));
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
            using var vorgang = _logger.Vorgang($"Spielplan: Ergebnis löschen (Spiel {spielId})");
            await _spielService.ErgebnisLoeschenAsync(spielId);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            SpieleHinzufuegen(neueSpiele, AusgewaehlteRunde);
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
        using var vorgang = _logger.Vorgang($"Spielplan: Kaskaden-Vorschau ({spielIds.Count} Spiele)");
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
        var paarungsNummern = PaarungsNummern(spiele);

        // Issue #340: K.-o.-Runden mit Hin-/Rückspiel nach Paarung statt nach Spielnummer — Hin- und
        // Rückspiel stehen direkt untereinander (Vorbild kicker.de-Paarungsansicht).
        bool nachPaarung = runde is not null && runde.MitRueckspiel && HinRueckspielWertung.IstKoRunde(runde);
        var reihenfolge = nachPaarung
            ? spiele.OrderBy(s => paarungsNummern[s.Id]).ThenBy(s => s.IstRueckspiel).ToList()
            : spiele;
        var hinspielErgebnisse = spiele
            .Where(s => !s.IstRueckspiel && s.Ergebnis is not null)
            .ToDictionary(s => s.Id, s => s.Ergebnis!);

        int? letztePaarung = null;
        foreach (var s in reihenfolge)
        {
            int paarung = paarungsNummern[s.Id];
            bool paarungsStart = nachPaarung && letztePaarung is not null && paarung != letztePaarung;
            letztePaarung = paarung;

            var hinspielErgebnis = nachPaarung && s.IstRueckspiel && s.HinspielId is int hinId
                ? hinspielErgebnisse.GetValueOrDefault(hinId)
                : null;
            Spiele.Add(MapSpielZeile(s, runde, paarung, _teamGruppeMap, _platzhalterVorschau,
                hinspielErgebnis, paarungsStart));
        }

        FokusSpieltageAbgleichen();
    }

    /// <summary>Paarungsnummer (1-basiert) je Spiel-ID: Hinspiele bzw. Einzelspiele in Spielnummer-Reihenfolge,
    /// ein Rückspiel übernimmt die Nummer seines Hinspiels (Issue #346) — unabhängig von der Listenposition,
    /// die bei nach Spieltag sortierter Liste Hin- und Rückspiel nicht nebeneinander führt.</summary>
    internal static IReadOnlyDictionary<int, int> PaarungsNummern(IEnumerable<Spiel> spiele)
    {
        var liste = spiele.ToList();
        var nummern = liste.Where(s => !s.IstRueckspiel)
            .OrderBy(s => s.Spielnummer)
            .Select((s, i) => (s.Id, Nr: i + 1))
            .ToDictionary(x => x.Id, x => x.Nr);
        foreach (var rueck in liste.Where(s => s.IstRueckspiel).OrderBy(s => s.Spielnummer))
            nummern[rueck.Id] = rueck.HinspielId is int hinId && nummern.TryGetValue(hinId, out var nr)
                ? nr
                : nummern.Count + 1;
        return nummern;
    }

    private static SpielZeile MapSpielZeile(
        Spiel s, TurnierRunde? runde, int paarungsNummer,
        IReadOnlyDictionary<int, int>? teamGruppeMap = null,
        IReadOnlyDictionary<int, (string? Heim, string? Auswaerts)>? vorschau = null,
        SpielErgebnis? hinspielErgebnis = null, bool istPaarungsStart = false)
    {
        string text = "-:-";
        string detail = "";
        string gesamt = "";

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

            // Issue #340: unter dem Rückspiel eine eigene Zeile mit dem Gesamtergebnis der Paarung.
            if (hinspielErgebnis is not null)
                gesamt = SpielErgebnisFormatierer.RueckspielGesamtText(hinspielErgebnis, e);
        }

        // #397: gespeicherte Gruppe zuerst — die Team→Gruppe-Map ist nur noch Rückfall für Altdaten und kennt
        // Spiele mit Platzhalter-Heimteam nicht.
        int? gruppeNummer = s.GruppenNummer;
        if (gruppeNummer is null && teamGruppeMap is not null && s.HeimMannschaftId.HasValue
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
            KoLabel = KoPositionsLabel(s, runde, paarungsNummer),
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
            GesamtText = gesamt,
            IstRueckspiel = s.IstRueckspiel,
            HinspielId = s.HinspielId,
            IstPlatzhalterSpiel = !s.HeimMannschaftId.HasValue || !s.AuswaertsMannschaftId.HasValue,
            HeimIstVorschau = heimVorschau is not null,
            AuswaertsIstVorschau = auswaertsVorschau is not null,
            GruppeNummer = gruppeNummer,
            IstPaarungsStart = istPaarungsStart
        };
    }

    internal static string? KoPositionsLabel(Spiel s, TurnierRunde? runde, int paarungsNummer)
    {
        if (runde is null) return null;
        bool mitRueck = runde.MitRueckspiel;
        return runde.Rundenart switch
        {
            Rundenart.Finale => FormatKoLabel("Finale", paarungsNummer, mitRueck, s.IstRueckspiel),
            Rundenart.SpielUmPlatzDrei => FormatKoLabel("3. Platz", paarungsNummer, mitRueck, s.IstRueckspiel),
            Rundenart.KoRunde => KoKuerzel(runde.EingangsMannschaftsanzahl) is { } k
                                          ? FormatKoLabel(k, paarungsNummer, mitRueck, s.IstRueckspiel) : null,
            _ => null
        };
    }

    private static string FormatKoLabel(string kuerzel, int paarungsNummer, bool mitRueckspiel, bool istRueckspiel) =>
        mitRueckspiel
            ? $"{kuerzel} {paarungsNummer} {(istRueckspiel ? "R" : "H")}"
            : $"{kuerzel} {paarungsNummer}";

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

    /// <summary>Ob nach der regulären Spielzeit ein Entscheidungsverfahren fällig ist: im Einzelspiel bei
    /// Remis, im K.-o.-Hinspiel nie, im K.-o.-Rückspiel bei ausgeglichenem Gesamtergebnis (Issue #346).</summary>
    private bool EntscheidungsverfahrenNoetig()
    {
        if (ParseIntNullable(HeimToreRegulaer) is not { } heimReg ||
            ParseIntNullable(AuswaertsToreRegulaer) is not { } ausReg) return false;

        if (_formularIstKoHinspiel) return false;

        if (_formularHinspielTore is { } hin)
            return HinRueckspielWertung.EntscheidungsverfahrenNoetig(
                hin.Heim, hin.Auswaerts, heimReg, ausReg, AusgewaehlteRunde?.AuswaertstorRegelAktiv == true);

        return heimReg == ausReg;
    }

    private EntscheidungArt BerechneEntscheidungArt()
    {
        if (!IstKoRunde || !EntscheidungsverfahrenNoetig()) return EntscheidungArt.RegulaereSpielzeit;

        bool hatVerl = HatVerlaengerungsVerfahren;
        bool hatElf = HatElfmeterVerfahren;

        if (hatVerl)
        {
            if (VerlaengerungToreDifferenz() is { } differenz)
            {
                if (differenz != 0)
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

        // Beim Gruppenwechsel bleibt der angezeigte Spieltag stehen, sofern die Gruppe ihn hat (Issue #363).
        if (istEchterGruppenwechsel)
            FokusSpieltageAbgleichen();
    }

    private IReadOnlyList<SpielZeile>? SpieltagRelevanteSpiele() =>
        AusgewaehlteGruppe is { } gruppe ? Spiele.Where(s => s.GruppeNummer == gruppe.GruppenNummer).ToList()
        : IstSpieltagRunde ? Spiele.ToList()
        : null;

    /// <summary>Gleicht Spieltagliste und Auswahl mit den aktuell geladenen Spielen ab (Issue #363): die
    /// bisherige Auswahl bleibt erhalten, solange es sie noch gibt — sonst bzw. nach Runden-/Gruppenwechsel
    /// greift der Start-Spieltag (erster mit offenen Spielen, sonst der letzte).</summary>
    private void FokusSpieltageAbgleichen()
    {
        var relevante = SpieltagRelevanteSpiele() ?? [];
        var spieltage = relevante.Where(s => s.Spieltag.HasValue).Select(s => s.Spieltag!.Value)
            .Distinct().Order().ToList();

        if (!spieltage.SequenceEqual(FokusSpieltage))
        {
            _spieltagOptionenImAufbau++;
            try
            {
                FokusSpieltage.Clear();
                SpieltagOptionen.Clear();
                if (spieltage.Count > 1)
                    SpieltagOptionen.Add(new SpieltagOption(null, "Alle Spieltage"));
                foreach (var st in spieltage)
                {
                    FokusSpieltage.Add(st);
                    SpieltagOptionen.Add(new SpieltagOption(st, $"{st}. Spieltag"));
                }
            }
            finally
            {
                _spieltagOptionenImAufbau--;
            }
        }

        bool auswahlGueltig = _spieltagAuswahlFestgelegt &&
            (FokusAlleAktiv ? spieltage.Count > 1 : spieltage.Contains(FokusAktiverSpieltag));

        if (!auswahlGueltig)
        {
            if (spieltage.Count == 0)
            {
                FokusAktiverSpieltag = 0;
                FokusAlleAktiv = true;
            }
            else
            {
                FokusAktiverSpieltag = spieltage
                    .Where(st => relevante.Any(s => s.Spieltag == st && s.Status != SpielStatus.Abgeschlossen))
                    .DefaultIfEmpty(spieltage[^1])
                    .First();
                FokusAlleAktiv = false;
                _spieltagAuswahlFestgelegt = true;
            }
        }

        OnPropertyChanged(nameof(HatMehrereSpieltage));
        OnPropertyChanged(nameof(AusgewaehlteSpieltagOption));
        OnPropertyChanged(nameof(FokusSpiele));
        _fokusVorigerSpieltag.NotifyCanExecuteChanged();
        _fokusNaechsterSpieltag.NotifyCanExecuteChanged();
        _spieltagSimulierenBefehl.NotifyCanExecuteChanged();
        _spieltagZuruecksetzenBefehl.NotifyCanExecuteChanged();
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
            || FormAktiv && FormHatErgebnis);

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
            await _spielplanService.FinaleAutomatischGenerierenWennBereitAsync(AusgewaehlteRunde.TurnierInstanzId);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            SpieleHinzufuegen(neueSpiele, AusgewaehlteRunde);
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
            await _spielplanService.FinaleAutomatischGenerierenWennBereitAsync(AusgewaehlteRunde.TurnierInstanzId);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            SpieleHinzufuegen(neueSpiele, AusgewaehlteRunde);
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
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (AusgewaehlteRunde is null) return;
        int rundeId = AusgewaehlteRunde.Id;
        Fehlermeldung = null;
        IstSimulierend = true;
        try
        {
            using var vorgang = _logger.Vorgang($"Spielplan: Rundenhälfte simulieren (Runde {rundeId})");
            await _simulationsService.RundenHaelfteSimulierenAsync(rundeId, istRueckrunde, SimulationsModus.AusfuehrlicheSimulation);
            await _spielplanService.FinaleAutomatischGenerierenWennBereitAsync(AusgewaehlteRunde.TurnierInstanzId);

            await TabellenNeuLadenAsync(rundeId);

            var neueSpiele = await _spielService.AlleNachRundeAsync(rundeId);
            Spiele.Clear();
            SpieleHinzufuegen(neueSpiele, AusgewaehlteRunde);
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
        else if (IstSpieltagRunde)
            spiele = Spiele;
        else
            return [];

        if (!FokusAlleAktiv)
            spiele = spiele.Where(s => s.Spieltag == FokusAktiverSpieltag);

        // Issue #363: Gruppenphase ohne Gruppenfilter — ein Spieltag über alle Gruppen, nach Gruppe gegliedert.
        bool nachGruppe = AusgewaehlteGruppe is null && IstGruppenphase;
        var liste = nachGruppe
            ? spiele.OrderBy(s => s.GruppeNummer ?? int.MaxValue).ToList()
            : spiele.ToList();

        int? vorigeGruppe = null;
        for (int i = 0; i < liste.Count; i++)
        {
            var zeile = liste[i];
            zeile.GruppenKopf = nachGruppe && (i == 0 || zeile.GruppeNummer != vorigeGruppe)
                ? (zeile.GruppeNummer is { } nr ? $"Gruppe {nr}" : "Ohne Gruppe")
                : null;
            vorigeGruppe = zeile.GruppeNummer;
        }
        return liste;
    }
}
