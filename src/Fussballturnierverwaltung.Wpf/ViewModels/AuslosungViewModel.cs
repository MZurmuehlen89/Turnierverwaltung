using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

// ── Darstellungstypen ────────────────────────────────────────────────────────

public record TeilnehmerAnzeige(int? MannschaftId, string Name, string? Kuerzel, int EintragId = 0, int? LandId = null)
{
    public bool IstPlatzhalter => !MannschaftId.HasValue;

    public static TeilnehmerAnzeige AusMannschaft(Mannschaft m) =>
        new(m.Id, m.Name, m.Land?.Kurzname, LandId: m.LandId);

    public static TeilnehmerAnzeige AusPlatzhalter(string name) =>
        new(null, name, null);

    public static TeilnehmerAnzeige AusEintrag(LostopfEintrag e) =>
        e.MannschaftId.HasValue
            ? new(e.MannschaftId, e.Mannschaft?.Name ?? "?", e.Mannschaft?.Land?.Kurzname, e.Id, e.Mannschaft?.LandId)
            : new(null, e.PlatzhalterName ?? "?", null, e.Id);
}

public record ErgebnisAnzeige(int PaarungsNummer, string HeimName, string AuswaertsName, bool IstFreilos);
public record GruppenErgebnisAnzeige(int GruppenNummer, string GruppenName, IReadOnlyList<string> Teams);

public class ManuellerKoSlot : ObservableObject
{
    private TeilnehmerAnzeige? _team;
    public int PaarungsNr { get; init; }
    public bool IstHeim { get; init; }
    public int? QuellTopfId { get; private set; }

    public TeilnehmerAnzeige? Team
    {
        get => _team;
        private set
        {
            if (SetProperty(ref _team, value))
            {
                OnPropertyChanged(nameof(IstLeer));
                OnPropertyChanged(nameof(AnzeigeName));
            }
        }
    }
    public bool IstLeer => Team is null;
    public string AnzeigeName => Team?.Name ?? "…";

    public void Besetzen(TeilnehmerAnzeige team, int topfId) { Team = team; QuellTopfId = topfId; }
    public (TeilnehmerAnzeige Team, int TopfId)? Leeren()
    {
        if (Team is null) return null;
        var ergebnis = (Team, QuellTopfId!.Value);
        Team = null; QuellTopfId = null;
        return ergebnis;
    }
}

public record ManuelleKoPaarungAnzeige(int PaarungsNr, ManuellerKoSlot HeimSlot, ManuellerKoSlot AuswaertsSlot);

public class ManuellerGruppenSlot : ObservableObject
{
    private TeilnehmerAnzeige? _team;
    public int GruppenNr { get; init; }
    public int SlotNr { get; init; }
    public int? QuellTopfId { get; private set; }

    public TeilnehmerAnzeige? Team
    {
        get => _team;
        private set
        {
            if (SetProperty(ref _team, value))
            {
                OnPropertyChanged(nameof(IstLeer));
                OnPropertyChanged(nameof(AnzeigeName));
            }
        }
    }
    public bool IstLeer => Team is null;
    public string AnzeigeName => Team?.Name ?? "…";

    public void Besetzen(TeilnehmerAnzeige team, int topfId) { Team = team; QuellTopfId = topfId; }
    public (TeilnehmerAnzeige Team, int TopfId)? Leeren()
    {
        if (Team is null) return null;
        var ergebnis = (Team, QuellTopfId!.Value);
        Team = null; QuellTopfId = null;
        return ergebnis;
    }
}

public class ManuelleGruppe : ObservableObject
{
    public int GruppenNr { get; init; }
    public string GruppenName { get; init; } = "";
    public ObservableCollection<ManuellerGruppenSlot> Slots { get; init; } = [];
}

public class LostopfViewModel : ObservableObject
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public int Reihenfolge { get; init; }
    public ObservableCollection<TeilnehmerAnzeige> Teilnehmer { get; init; } = [];
}

public class RegelViewModel : ObservableObject
{
    private readonly Func<bool, Task> _callback;
    private bool _istAktiv;
    private bool _istRedundant;

    public AuslosungsRegelArt Regelart { get; init; }
    public string Anzeige { get; init; } = "";
    public string Beschreibung { get; init; } = "";
    public string RedundanzHinweis { get; set; } = "";

    public bool IstAktiv
    {
        get => _istAktiv;
        set { if (SetProperty(ref _istAktiv, value)) _ = _callback(value); }
    }

    public bool IstRedundant
    {
        get => _istRedundant;
        set => SetProperty(ref _istRedundant, value);
    }

    public RegelViewModel(AuslosungsRegelArt art, string anzeige, string beschreibung, bool initial, Func<bool, Task> callback)
    {
        Regelart = art; Anzeige = anzeige; Beschreibung = beschreibung; _istAktiv = initial; _callback = callback;
    }
}

public record AuslosungsartOption(string Anzeige, Auslosungsart Wert);

// ── Haupt-ViewModel ─────────────────────────────────────────────────────────

public partial class AuslosungViewModel : ViewModelBase, IRibbonAware
{
    private readonly AuslosungService _auslosungService;
    private readonly TurnierInstanzService _instanzService;
    private readonly MannschaftService _mannschaftService;

    private Auslosung? _auslosungDetail;

    // ── Linke Spalte ──────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<TurnierInstanz> _instanzen = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NeuCommand))]
    [NotifyPropertyChangedFor(nameof(KeinTurnierGewaehlt))]
    private TurnierInstanz? _ausgewaehlteInstanz;

    [ObservableProperty] private ObservableCollection<Auslosung> _auslosungen = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private Auslosung? _ausgewaehlteAuslosung;

    [ObservableProperty] private ObservableCollection<TurnierRunde> _instanzRunden = [];

    // ── Neue-Auslosung-Formular ────────────────────────────────────────────────
    [ObservableProperty]
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
    private Auslosungsart _neuAuslosungsart = Auslosungsart.Zufall;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyPropertyChangedFor(nameof(AuslosungsartHinweis))]
    [NotifyPropertyChangedFor(nameof(AuslosungsartWarnung))]
    private TurnierRunde? _neuRunde;

    private string _autoNeuName = "";

    [ObservableProperty] private int _neuAnzahlLostoepfe = 1;

    // ── Auslosung-Detail ──────────────────────────────────────────────────────
    [ObservableProperty] private bool _detailAktiv;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstManuellerModus))]
    [NotifyPropertyChangedFor(nameof(IstBearbeitbarNichtManuell))]
    [NotifyPropertyChangedFor(nameof(IstManuellKoModus))]
    [NotifyPropertyChangedFor(nameof(IstManuellGruppenModus))]
    private bool _istBearbeitbar;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailAuslosungsartHinweis))]
    [NotifyPropertyChangedFor(nameof(DetailAuslosungsartWarnung))]
    [NotifyPropertyChangedFor(nameof(IstManuellerModus))]
    [NotifyPropertyChangedFor(nameof(IstBearbeitbarNichtManuell))]
    [NotifyPropertyChangedFor(nameof(IstManuellKoModus))]
    [NotifyPropertyChangedFor(nameof(IstManuellGruppenModus))]
    private Auslosungsart _detailAuslosungsart;

    [ObservableProperty] private string _detailName = "";
    [ObservableProperty] private int _detailAnzahlLostoepfe = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    private bool _istDetailDirty;

    private bool _ladenAktiv;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstPaarungsergebnis))]
    private bool _istAbgeschlossen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstPaarungsergebnis))]
    private bool _istGruppenergebnis;

    public bool IstPaarungsergebnis => IstAbgeschlossen && !IstGruppenergebnis;

    [ObservableProperty] private string _auslosungKopfzeile = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerfuegbareTeilnehmerGefiltert))]
    private ObservableCollection<TeilnehmerAnzeige> _verfuegbareTeilnehmer = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerfuegbareTeilnehmerGefiltert))]
    private string _teilnehmerFilter = "";

    [ObservableProperty] private TeilnehmerAnzeige? _ausgewaehlterVerfuegbarTeilnehmer;

    [ObservableProperty] private ObservableCollection<LostopfViewModel> _lostoepfe = [];
    [ObservableProperty] private ObservableCollection<RegelViewModel> _regeln = [];
    [ObservableProperty] private ObservableCollection<ErgebnisAnzeige> _ergebnisseAnzeige = [];
    [ObservableProperty] private ObservableCollection<GruppenErgebnisAnzeige> _gruppenErgebnisse = [];

    // ── Manuelle Auslosung ────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<ManuelleKoPaarungAnzeige> _manuelleKoPaarungen = [];
    [ObservableProperty] private ObservableCollection<ManuelleGruppe> _manuelleGruppen = [];
    [ObservableProperty] private string? _manuellesFehler;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TeamsStatusText))]
    [NotifyPropertyChangedFor(nameof(TeamsVollstaendig))]
    private int _teamsZugewiesen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TeamsStatusText))]
    [NotifyPropertyChangedFor(nameof(TeamsVollstaendig))]
    private int _teamsBenoetigt;

    public bool TeamsVollstaendig => TeamsBenoetigt > 0 && TeamsZugewiesen == TeamsBenoetigt;

    public string TeamsStatusText => TeamsBenoetigt == 0
        ? $"{TeamsZugewiesen} Teams zugewiesen"
        : TeamsZugewiesen == TeamsBenoetigt
            ? $"✓  Alle {TeamsBenoetigt} Teams zugewiesen"
            : TeamsZugewiesen > TeamsBenoetigt
                ? $"⚠  {TeamsZugewiesen} Teams zugewiesen — {TeamsZugewiesen - TeamsBenoetigt} zu viel (Runde erwartet {TeamsBenoetigt})"
                : $"{TeamsZugewiesen} / {TeamsBenoetigt} Teams zugewiesen — noch {TeamsBenoetigt - TeamsZugewiesen} fehlen";

    // ── Manueller Modus ───────────────────────────────────────────────────────
    public bool IstManuellerModus          => IstBearbeitbar && DetailAuslosungsart == Auslosungsart.Manuell;
    public bool IstBearbeitbarNichtManuell => IstBearbeitbar && !IstManuellerModus;
    public bool IstManuellKoModus          => IstManuellerModus && _auslosungDetail?.TurnierRunde?.Rundenart != Rundenart.Gruppenphase;
    public bool IstManuellGruppenModus     => IstManuellerModus && _auslosungDetail?.TurnierRunde?.Rundenart == Rundenart.Gruppenphase;

    // ── Status ────────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    // ── Berechnete Eigenschaften ─────────────────────────────────────────────
    public bool KeinTurnierGewaehlt => AusgewaehlteInstanz is null;

    public IEnumerable<TeilnehmerAnzeige> VerfuegbareTeilnehmerGefiltert =>
        string.IsNullOrWhiteSpace(TeilnehmerFilter)
            ? VerfuegbareTeilnehmer
            : VerfuegbareTeilnehmer.Where(t =>
                t.Name.Contains(TeilnehmerFilter, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<AuslosungsartOption> AuslosungsartOptionen =>
        Enum.GetValues<Auslosungsart>()
            .Select(a => new AuslosungsartOption(AuslosungsartAnzeigeName(a), a))
            .ToList();

    public string AuslosungsartHinweis         => AuslosungsartHinweisText(NeuAuslosungsart);
    public string? AuslosungsartWarnung        => AuslosungsartWarnungText(NeuAuslosungsart, NeuRunde?.Rundenart);
    public string DetailAuslosungsartHinweis   => AuslosungsartHinweisText(DetailAuslosungsart);
    public string? DetailAuslosungsartWarnung  => AuslosungsartWarnungText(DetailAuslosungsart, _auslosungDetail?.TurnierRunde?.Rundenart);

    private static string AuslosungsartHinweisText(Auslosungsart art) => art switch
    {
        Auslosungsart.Zufall =>
            "Alle Teams werden zufällig gemischt und paarweise zugelost. Passt zu jeder Rundenart.",
        Auslosungsart.NachSetzliste =>
            "Gesetzte Teams (Topf 1) werden gleichmäßig auf Gruppen oder Paare verteilt, " +
            "damit stärkere Teams erst in späteren Runden aufeinandertreffen. " +
            "Typisch für Nationenturniere und nationale Pokal-Auslosungen.",
        Auslosungsart.NachToepfen =>
            "Teams werden manuell auf Töpfe verteilt und dann topfübergreifend zugelost — " +
            "mindestens ein Lostopf ist erforderlich. " +
            "Typisch für UEFA Europa League und internationale Pokal-Wettbewerbe.",
        Auslosungsart.TopfZuordnung =>
            "Jeder Topf belegt genau einen Setzplatz: Topf 1 → Gruppenplatz A, Topf 2 → Gruppenplatz B usw. " +
            "Alle Töpfe müssen gleich groß sein. " +
            "Gängig in der UEFA Champions League (Gruppen- und K.O.-Phase).",
        Auslosungsart.GruppensiegerGegenGruppenzweite =>
            "Gruppensieger werden den Zweitplatzierten einer anderen Gruppe zugelost, " +
            "wobei Teams aus derselben Gruppe nicht aufeinandertreffen dürfen. " +
            "Typisch für K.O.-Runden nach einer Gruppenphase (z. B. WM-Achtelfinale, UEFA-Playoff).",
        Auslosungsart.Benutzerdefiniert =>
            "Teams werden manuell den Losplätzen zugewiesen — keine automatische Verlosung. " +
            "Für besondere Anforderungen oder wenn keine andere Methode passt.",
        Auslosungsart.Manuell =>
            "Vollständig manuelle Auslosung ohne Automation: Sie bestimmen jede Paarung (KO-Runde) " +
            "oder Gruppenzuordnung (Gruppenphase) selbst. " +
            "Die konfigurierten Auslosungsregeln (z. B. 'Kein gleiches Land') werden bei jeder Eingabe geprüft, " +
            "damit keine ungültige Paarung entstehen kann.",
        _ => ""
    };

    private static string? AuslosungsartWarnungText(Auslosungsart art, Rundenart? rundenart) =>
        (art, rundenart) switch
        {
            (Auslosungsart.GruppensiegerGegenGruppenzweite, Rundenart.Liga) or
            (Auslosungsart.GruppensiegerGegenGruppenzweite, Rundenart.Gruppenphase) or
            (Auslosungsart.GruppensiegerGegenGruppenzweite, Rundenart.TopfLiga) or
            (Auslosungsart.GruppensiegerGegenGruppenzweite, Rundenart.SchweizerSystem) =>
                "⚠  Diese Auslosungsart setzt voraus, dass bereits eine Gruppenphase gespielt wurde. " +
                "Sie ist für K.O.-Runden gedacht, die auf eine Gruppenphase folgen.",
            (Auslosungsart.TopfZuordnung, Rundenart.Gruppenphase) =>
                "⚠  Für eine klassische Gruppenphase verwenden Sie 'Nach Töpfen' — " +
                "dabei wird ein Team pro Topf in jede Gruppe gelost. " +
                "'Topf-Zuordnung' ist für die UCL-Ligaphase (direkte Gegnerbestimmung) gedacht.",
            (Auslosungsart.TopfZuordnung, Rundenart.KoRunde) or
            (Auslosungsart.TopfZuordnung, Rundenart.Finale) or
            (Auslosungsart.TopfZuordnung, Rundenart.SpielUmPlatzDrei) =>
                "⚠  Topf-Zuordnung weist Teams direkt Gruppenplätzen zu. " +
                "Für K.O.-Runden ist 'Nach Töpfen' oder 'Zufällig' besser geeignet.",
            (Auslosungsart.NachSetzliste, Rundenart.SchweizerSystem) =>
                "⚠  Im Schweizer System wird die Paarung automatisch anhand der Tabelle berechnet. " +
                "Eine manuelle Setzlisten-Auslosung ist hier in der Regel nicht sinnvoll.",
            _ => null
        };

    // ── IRibbonAware ──────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Auslosung";
    public ICommand NeuBefehl => NeuCommand;
    public ICommand SpeichernBefehl => SpeichernCommand;
    public ICommand AbbrechenBefehl => AbbrechenCommand;
    public ICommand LoeschenBefehl => LoeschenCommand;

    public AuslosungViewModel(
        AuslosungService auslosungService,
        TurnierInstanzService instanzService,
        MannschaftService mannschaftService)
    {
        _auslosungService  = auslosungService;
        _instanzService    = instanzService;
        _mannschaftService = mannschaftService;
    }

    public override async Task AktivierenAsync()
    {
        IstLadend = true;
        try
        {
            var instanzen = await _instanzService.AlleAbrufenAsync();
            Instanzen = new ObservableCollection<TurnierInstanz>(instanzen);
        }
        finally { IstLadend = false; }
    }

    // ── Ribbon-Befehle ────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannNeu))]
    private void Neu()
    {
        NeuFormAktiv        = true;
        DetailAktiv         = false;
        _autoNeuName        = "";
        NeuName             = "";
        NeuAuslosungsart    = Auslosungsart.Zufall;
        NeuRunde            = InstanzRunden.FirstOrDefault();
        NeuAnzahlLostoepfe  = 1;
        Fehlermeldung       = null;
    }

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        Fehlermeldung = null;
        if (NeuFormAktiv)
        {
            if (AusgewaehlteInstanz is null || NeuRunde is null) return;
            try
            {
                var name    = string.IsNullOrWhiteSpace(NeuName) ? NeuRunde.Name : NeuName.Trim();
                var eingabe = new AuslosungErstellenEingabe(
                    AusgewaehlteInstanz.Id, NeuRunde.Id, name,
                    NeuAuslosungsart, false);
                var neu = await _auslosungService.ErstellenAsync(eingabe);

                await _auslosungService.LostoepfeSynchronisierenAsync(neu.Id, Math.Max(1, NeuAnzahlLostoepfe));

                NeuFormAktiv = false;

                var auslosungen = await _auslosungService.AlleNachInstanzAbrufenAsync(AusgewaehlteInstanz.Id);
                Auslosungen = new ObservableCollection<Auslosung>(auslosungen);
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
        }
        else if (IstDetailDirty && _auslosungDetail is not null)
        {
            await LadeDetailAsync(_auslosungDetail.Id);
        }
    }

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task Loeschen()
    {
        if (AusgewaehlteAuslosung is null) return;
        var r = MessageBox.Show($"Soll '{AusgewaehlteAuslosung.Name}' wirklich gelöscht werden?",
            "Auslosung löschen", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;
        Fehlermeldung = null;
        try
        {
            await _auslosungService.LoeschenAsync(AusgewaehlteAuslosung.Id);
            var auslosungen = await _auslosungService.AlleNachInstanzAbrufenAsync(AusgewaehlteInstanz!.Id);
            Auslosungen = new ObservableCollection<Auslosung>(auslosungen);
            AusgewaehlteAuslosung = null;
            DetailAktiv = false;
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    // ── Workspace-Befehle ─────────────────────────────────────────────────────

    [RelayCommand]
    private async Task TeilnehmerZuTopfHinzufuegen(int topfId)
    {
        if (_auslosungDetail is null || AusgewaehlterVerfuegbarTeilnehmer is null) return;
        Fehlermeldung = null;
        var t = AusgewaehlterVerfuegbarTeilnehmer;
        try
        {
            if (t.MannschaftId.HasValue)
                await _auslosungService.MannschaftZuLostopfHinzufuegenAsync(_auslosungDetail.Id, topfId, t.MannschaftId.Value);
            else
                await _auslosungService.PlatzhalterZuLostopfHinzufuegenAsync(_auslosungDetail.Id, topfId, t.Name);
            await LadeDetailAsync(_auslosungDetail.Id);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    [RelayCommand]
    private async Task AuslosungAusfuehren()
    {
        if (_auslosungDetail is null) return;
        Fehlermeldung = null;
        try
        {
            await _auslosungService.AusfuehrenAsync(_auslosungDetail.Id);
            await LadeDetailAsync(_auslosungDetail.Id);
            await AktualisiereSeitenlisteAsync();
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    // ── Öffentliche Methoden: Manuelle Slot-Zuweisung ────────────────────────

    public string? TeamInKoSlotPlatzieren(TeilnehmerAnzeige team, int quellTopfId, ManuellerKoSlot slot)
    {
        ManuellesFehler = null;
        if (HatKeinGleichesLandVerletzung(team, slot.PaarungsNr, istHeim: slot.IstHeim, out var fehler))
        {
            ManuellesFehler = fehler;
            return fehler;
        }
        EntferneAusTopf(team, quellTopfId);
        slot.Besetzen(team, quellTopfId);
        return null;
    }

    public void TeamAusKoSlotEntfernen(ManuellerKoSlot slot)
    {
        var ergebnis = slot.Leeren();
        if (ergebnis is not null) GibeAnTopfZurueck(ergebnis.Value.Team, ergebnis.Value.TopfId);
        ManuellesFehler = null;
    }

    public string? TeamInGruppenSlotPlatzieren(TeilnehmerAnzeige team, int quellTopfId, ManuellerGruppenSlot slot)
    {
        ManuellesFehler = null;
        if (HatKeinGleichesLandVerletzungGruppe(team, slot.GruppenNr, out var fehler))
        {
            ManuellesFehler = fehler;
            return fehler;
        }
        EntferneAusTopf(team, quellTopfId);
        slot.Besetzen(team, quellTopfId);
        return null;
    }

    public void TeamAusGruppenSlotEntfernen(ManuellerGruppenSlot slot)
    {
        var ergebnis = slot.Leeren();
        if (ergebnis is not null) GibeAnTopfZurueck(ergebnis.Value.Team, ergebnis.Value.TopfId);
        ManuellesFehler = null;
    }

    private void EntferneAusTopf(TeilnehmerAnzeige team, int topfId)
    {
        var topf    = Lostoepfe.FirstOrDefault(t => t.Id == topfId);
        var eintrag = topf?.Teilnehmer.FirstOrDefault(t =>
            (t.EintragId > 0 && t.EintragId == team.EintragId) || t.Name == team.Name);
        if (eintrag is not null) topf!.Teilnehmer.Remove(eintrag);
    }

    private void GibeAnTopfZurueck(TeilnehmerAnzeige team, int topfId)
    {
        var topf = Lostoepfe.FirstOrDefault(t => t.Id == topfId);
        if (topf is null) return;
        bool bereitsVorhanden = topf.Teilnehmer.Any(t =>
            (t.EintragId > 0 && t.EintragId == team.EintragId) || t.Name == team.Name);
        if (!bereitsVorhanden) topf.Teilnehmer.Add(team);
    }

    public ManuellerKoSlot? NaechsterFreierKoSlot() =>
        ManuelleKoPaarungen.SelectMany(p => new[] { p.HeimSlot, p.AuswaertsSlot }).FirstOrDefault(s => s.IstLeer);

    public ManuellerGruppenSlot? NaechsterFreierGruppenSlot() =>
        ManuelleGruppen.SelectMany(g => g.Slots).FirstOrDefault(s => s.IstLeer);

    [RelayCommand]
    private async Task ManuelleAuslosungAbschliessen()
    {
        if (_auslosungDetail is null) return;
        ManuellesFehler = null;
        Fehlermeldung = null;
        try
        {
            List<ManuellePaarungEingabe> paarungen;
            if (IstManuellGruppenModus)
            {
                var alleSlots = ManuelleGruppen
                    .SelectMany(g => g.Slots.Select(s => (g.GruppenNr, s)))
                    .Where(x => !x.s.IstLeer)
                    .ToList();
                if (!alleSlots.Any())
                {
                    ManuellesFehler = "Bitte mindestens eine Zuweisung vornehmen.";
                    return;
                }
                paarungen = alleSlots.Select(x => new ManuellePaarungEingabe(
                    HeimMannschaftId:         x.s.Team!.MannschaftId,
                    HeimPlatzhalterName:      x.s.Team!.MannschaftId is null ? x.s.Team!.Name : null,
                    AuswaertsMannschaftId:    null,
                    AuswaertsPlatzhalterName: null,
                    PaarungsNummer:           x.GruppenNr
                )).ToList();
            }
            else
            {
                var allePaarungen = ManuelleKoPaarungen
                    .Where(p => !p.HeimSlot.IstLeer)
                    .ToList();
                if (!allePaarungen.Any())
                {
                    ManuellesFehler = "Bitte mindestens eine Paarung befüllen.";
                    return;
                }
                paarungen = allePaarungen.Select(p => new ManuellePaarungEingabe(
                    HeimMannschaftId:         p.HeimSlot.Team!.MannschaftId,
                    HeimPlatzhalterName:      p.HeimSlot.Team!.MannschaftId is null ? p.HeimSlot.Team!.Name : null,
                    AuswaertsMannschaftId:    p.AuswaertsSlot.Team?.MannschaftId,
                    AuswaertsPlatzhalterName: p.AuswaertsSlot.Team?.MannschaftId is null ? p.AuswaertsSlot.Team?.Name : null,
                    PaarungsNummer:           p.PaarungsNr
                )).ToList();
            }
            await _auslosungService.ManuelleErgebnisseAbschliessenAsync(_auslosungDetail.Id, paarungen);
            await LadeDetailAsync(_auslosungDetail.Id);
            await AktualisiereSeitenlisteAsync();
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    private bool HatKeinGleichesLandVerletzung(TeilnehmerAnzeige neuTeam, int paarungsNr, bool istHeim, out string? fehler)
    {
        fehler = null;
        if (_auslosungDetail?.Regeln.Any(r => r.IstAktiv && r.Regelart == AuslosungsRegelArt.KeinGleichesLand) != true)
            return false;
        if (!neuTeam.LandId.HasValue) return false;
        var paarung = ManuelleKoPaarungen.FirstOrDefault(p => p.PaarungsNr == paarungsNr);
        if (paarung is null) return false;
        var gegnerSlot = istHeim ? paarung.AuswaertsSlot : paarung.HeimSlot;
        if (gegnerSlot.IstLeer || !gegnerSlot.Team!.LandId.HasValue) return false;
        if (gegnerSlot.Team!.LandId == neuTeam.LandId)
        {
            fehler = $"Regel 'Kein gleiches Land': '{neuTeam.Name}' und '{gegnerSlot.Team!.Name}' " +
                     "sind aus demselben Land und dürfen nicht gepaart werden.";
            return true;
        }
        return false;
    }

    private bool HatKeinGleichesLandVerletzungGruppe(TeilnehmerAnzeige neuTeam, int gruppenNr, out string? fehler)
    {
        fehler = null;
        if (_auslosungDetail?.Regeln.Any(r => r.IstAktiv && r.Regelart == AuslosungsRegelArt.KeinGleichesLand) != true)
            return false;
        if (!neuTeam.LandId.HasValue) return false;
        var gruppe = ManuelleGruppen.FirstOrDefault(g => g.GruppenNr == gruppenNr);
        if (gruppe is null) return false;
        var konflikt = gruppe.Slots.FirstOrDefault(s => !s.IstLeer && s.Team!.LandId == neuTeam.LandId);
        if (konflikt is not null)
        {
            fehler = $"Regel 'Kein gleiches Land': '{neuTeam.Name}' und '{konflikt.Team!.Name}' " +
                     "sind aus demselben Land und dürfen nicht in dieselbe Gruppe.";
            return true;
        }
        return false;
    }

    [RelayCommand]
    private async Task AuslosungZuruecksetzen()
    {
        if (_auslosungDetail is null) return;
        Fehlermeldung = null;
        try
        {
            await _auslosungService.ZuruecksetzenAsync(_auslosungDetail.Id);
            await LadeDetailAsync(_auslosungDetail.Id);
            await AktualisiereSeitenlisteAsync();
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    // ── Öffentliche Methoden für D&D und Kontextmenü ─────────────────────────

    public async Task TeilnehmerZuTopfVerschieben(TeilnehmerAnzeige teilnehmer, int? vonTopfId, int nachTopfId)
    {
        if (_auslosungDetail is null) return;
        Fehlermeldung = null;
        try
        {
            if (vonTopfId.HasValue)
                await _auslosungService.EintragAusLostopfEntfernenAsync(_auslosungDetail.Id, vonTopfId.Value, teilnehmer.EintragId);

            if (teilnehmer.MannschaftId.HasValue)
                await _auslosungService.MannschaftZuLostopfHinzufuegenAsync(_auslosungDetail.Id, nachTopfId, teilnehmer.MannschaftId.Value);
            else
                await _auslosungService.PlatzhalterZuLostopfHinzufuegenAsync(_auslosungDetail.Id, nachTopfId, teilnehmer.Name);

            await LadeDetailAsync(_auslosungDetail.Id);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    public async Task TeilnehmerZuVerfuegbarZurueck(TeilnehmerAnzeige teilnehmer, int vonTopfId)
    {
        if (_auslosungDetail is null) return;
        Fehlermeldung = null;
        try
        {
            await _auslosungService.EintragAusLostopfEntfernenAsync(_auslosungDetail.Id, vonTopfId, teilnehmer.EintragId);
            await LadeDetailAsync(_auslosungDetail.Id);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    // ── Interne Hilfsmethoden ─────────────────────────────────────────────────

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(AusgewaehlteInstanz))
            _ = BeiInstanzAenderungAsync();
        if (e.PropertyName == nameof(AusgewaehlteAuslosung))
            _ = BeiAuslosungAenderungAsync();
    }

    private async Task BeiInstanzAenderungAsync()
    {
        Auslosungen.Clear();
        InstanzRunden.Clear();
        VerfuegbareTeilnehmer.Clear();
        TeilnehmerFilter = "";
        DetailAktiv  = false;
        NeuFormAktiv = false;
        if (AusgewaehlteInstanz is null) return;

        IstLadend = true;
        try
        {
            var auslosungen = await _auslosungService.AlleNachInstanzAbrufenAsync(AusgewaehlteInstanz.Id);
            Auslosungen = new ObservableCollection<Auslosung>(auslosungen);

            var detail = await _instanzService.FindenNachIdMitRundenAsync(AusgewaehlteInstanz.Id);
            if (detail is not null)
                InstanzRunden = new ObservableCollection<TurnierRunde>(detail.Runden.OrderBy(r => r.Reihenfolge));
        }
        finally { IstLadend = false; }
    }

    private async Task BeiAuslosungAenderungAsync()
    {
        NeuFormAktiv = false;
        if (AusgewaehlteAuslosung is null) { DetailAktiv = false; return; }
        await LadeDetailAsync(AusgewaehlteAuslosung.Id);
    }

    private async Task LadeDetailAsync(int auslosungId)
    {
        IstLadend    = true;
        _ladenAktiv  = true;
        try
        {
            var detail = await _auslosungService.FindenNachIdAsync(auslosungId);
            if (detail is null) return;
            _auslosungDetail = detail;

            ManuelleKoPaarungen = [];
            ManuelleGruppen     = [];
            ManuellesFehler     = null;

            // Determine round order to decide real-teams vs placeholders
            var aktuelleRunde = InstanzRunden.FirstOrDefault(r => r.Id == detail.TurnierRundeId);
            var vorherigeRunde = aktuelleRunde is not null
                ? InstanzRunden.Where(r => r.Reihenfolge < aktuelleRunde.Reihenfolge)
                               .OrderByDescending(r => r.Reihenfolge)
                               .FirstOrDefault()
                : null;

            IReadOnlyList<TeilnehmerAnzeige> alleTeilnehmer;
            if (vorherigeRunde is null)
            {
                var teams = await _mannschaftService.AlleNachTurnierfilterAsync(
                    AusgewaehlteInstanz?.VerbandId,
                    AusgewaehlteInstanz?.MannschaftsartFilter ?? MannschaftsartFilter.Alle);
                alleTeilnehmer = teams.Select(TeilnehmerAnzeige.AusMannschaft).ToList();
            }
            else
            {
                alleTeilnehmer = PlatzhalterGenerieren(aktuelleRunde!, vorherigeRunde);
            }

            var assignedMannschaftIds = detail.Lostoepfe
                .SelectMany(l => l.Eintraege)
                .Where(e => e.MannschaftId.HasValue)
                .Select(e => e.MannschaftId!.Value)
                .ToHashSet();
            var assignedPlatzhalter = detail.Lostoepfe
                .SelectMany(l => l.Eintraege)
                .Where(e => e.PlatzhalterName != null)
                .Select(e => e.PlatzhalterName!)
                .ToHashSet();

            VerfuegbareTeilnehmer = new ObservableCollection<TeilnehmerAnzeige>(
                alleTeilnehmer
                    .Where(t => t.MannschaftId.HasValue
                        ? !assignedMannschaftIds.Contains(t.MannschaftId.Value)
                        : !assignedPlatzhalter.Contains(t.Name))
                    .OrderBy(t => t.Name));

            Lostoepfe = new ObservableCollection<LostopfViewModel>(
                detail.Lostoepfe.Select(l => new LostopfViewModel
                {
                    Id         = l.Id,
                    Name       = l.Name,
                    Reihenfolge = l.Reihenfolge,
                    Teilnehmer = new ObservableCollection<TeilnehmerAnzeige>(
                        l.Eintraege
                            .Select(TeilnehmerAnzeige.AusEintrag)
                            .OrderBy(t => t.Name))
                }));

            Regeln = new ObservableCollection<RegelViewModel>(
                Enum.GetValues<AuslosungsRegelArt>().Select(art =>
                {
                    var vorh = detail.Regeln.FirstOrDefault(r => r.Regelart == art);
                    return new RegelViewModel(art, RegelAnzeigeName(art), RegelBeschreibung(art), vorh?.IstAktiv ?? false,
                        async aktiv =>
                        {
                            try
                            {
                                if (aktiv) await _auslosungService.RegelHinzufuegenOderAktivierenAsync(auslosungId, art, RegelAnzeigeName(art));
                                else       await _auslosungService.RegelDeaktivierenAsync(auslosungId, art);
                            }
                            catch (Exception ex) { Fehlermeldung = ex.Message; }
                        });
                }));
            AktualisiereRedundanteRegeln(detail.Auslosungsart);

            // Detect whether results are group assignments (Gruppenphase) or match pairings
            bool istGruppe = detail.Ergebnisse.Any() &&
                detail.Ergebnisse.All(e => !e.IstFreilos &&
                    e.AuswaertsMannschaftId == null &&
                    e.AuswaertsPlatzhalterName == null);

            IstGruppenergebnis = istGruppe;

            if (istGruppe)
            {
                GruppenErgebnisse = new ObservableCollection<GruppenErgebnisAnzeige>(
                    detail.Ergebnisse
                        .GroupBy(e => e.PaarungsNummer)
                        .OrderBy(g => g.Key)
                        .Select(g => new GruppenErgebnisAnzeige(
                            g.Key,
                            $"Gruppe {GruppenBuchstabe(g.Key)}",
                            g.Select(e => e.HeimMannschaft?.Name ?? e.HeimPlatzhalterName ?? "?")
                             .OrderBy(n => n)
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
                        e.IstFreilos)));
            }

            TeamsZugewiesen       = detail.Lostoepfe.SelectMany(l => l.Eintraege).Count();
            TeamsBenoetigt        = detail.TurnierRunde?.EingangsMannschaftsanzahl ?? 0;

            // Slot-Initialisierung für manuelle Auslosung (nur wenn noch nicht abgeschlossen)
            if (detail.Auslosungsart == Auslosungsart.Manuell &&
                detail.Status is not (AuslosungsStatus.Abgeschlossen or AuslosungsStatus.Abgebrochen))
            {
                int teamAnzahl = detail.TurnierRunde?.EingangsMannschaftsanzahl ?? 0;
                if (detail.TurnierRunde?.Rundenart == Rundenart.Gruppenphase)
                {
                    int gruppenAnzahl = detail.TurnierRunde.GruppenAnzahl ?? 1;
                    int slotsProGruppe = teamAnzahl > 0 && gruppenAnzahl > 0 ? teamAnzahl / gruppenAnzahl : 4;
                    ManuelleGruppen = new ObservableCollection<ManuelleGruppe>(
                        Enumerable.Range(1, gruppenAnzahl).Select(gNr => new ManuelleGruppe
                        {
                            GruppenNr   = gNr,
                            GruppenName = $"Gruppe {GruppenBuchstabe(gNr)}",
                            Slots       = new ObservableCollection<ManuellerGruppenSlot>(
                                Enumerable.Range(1, slotsProGruppe).Select(sNr => new ManuellerGruppenSlot
                                {
                                    GruppenNr = gNr,
                                    SlotNr    = sNr
                                }))
                        }));
                    ManuelleKoPaarungen = [];
                }
                else
                {
                    int paarAnzahl = teamAnzahl > 0 ? (int)Math.Ceiling(teamAnzahl / 2.0) : 1;
                    ManuelleKoPaarungen = new ObservableCollection<ManuelleKoPaarungAnzeige>(
                        Enumerable.Range(1, paarAnzahl).Select(nr => new ManuelleKoPaarungAnzeige(
                            nr,
                            new ManuellerKoSlot { PaarungsNr = nr, IstHeim = true },
                            new ManuellerKoSlot { PaarungsNr = nr, IstHeim = false })));
                    ManuelleGruppen = [];
                }
                OnPropertyChanged(nameof(IstManuellKoModus));
                OnPropertyChanged(nameof(IstManuellGruppenModus));
            }

            IstBearbeitbar        = detail.Status is not (AuslosungsStatus.Abgeschlossen or AuslosungsStatus.Abgebrochen);
            IstAbgeschlossen      = detail.Status == AuslosungsStatus.Abgeschlossen;
            DetailName            = detail.Name;
            DetailAuslosungsart   = detail.Auslosungsart;
            DetailAnzahlLostoepfe = Math.Max(1, detail.Lostoepfe.Count);

            AuslosungKopfzeile = $"{detail.Name}  ·  {detail.TurnierRunde.Name}  ·  " +
                                 $"{AuslosungsartAnzeigeName(detail.Auslosungsart)}  ·  " +
                                 $"Status: {StatusAnzeigeName(detail.Status)}";
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

    private static IReadOnlyList<TeilnehmerAnzeige> PlatzhalterGenerieren(
        TurnierRunde aktuelleRunde, TurnierRunde vorherigeRunde)
    {
        int anzahl = aktuelleRunde.EingangsMannschaftsanzahl;

        if (vorherigeRunde.Rundenart == Rundenart.Gruppenphase && vorherigeRunde.GruppenAnzahl.HasValue)
        {
            var gruppen  = vorherigeRunde.GruppenAnzahl.Value;
            var proGruppe = vorherigeRunde.AusgangsMannschaftsanzahl / gruppen;
            var liste = new List<TeilnehmerAnzeige>();
            for (int platz = 1; platz <= proGruppe; platz++)
            {
                for (int g = 0; g < gruppen; g++)
                {
                    var buchstabe = (char)('A' + g);
                    liste.Add(TeilnehmerAnzeige.AusPlatzhalter($"{platz}. Gruppe {buchstabe}"));
                }
            }
            return liste.Take(anzahl).ToList();
        }

        if (vorherigeRunde.Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei)
        {
            var spiele = vorherigeRunde.EingangsMannschaftsanzahl / 2;
            return Enumerable.Range(1, Math.Min(spiele, anzahl))
                .Select(i => TeilnehmerAnzeige.AusPlatzhalter($"Sieger Spiel {i} ({vorherigeRunde.Name})"))
                .ToList();
        }

        return Enumerable.Range(1, anzahl)
            .Select(i => TeilnehmerAnzeige.AusPlatzhalter($"Platz {i} ({vorherigeRunde.Name})"))
            .ToList();
    }

    private async Task AlleTeamsZuErstemTopfHinzufuegenAsync(int auslosungId, TurnierRunde runde)
    {
        var detail = await _auslosungService.FindenNachIdAsync(auslosungId);
        var ersterTopf = detail?.Lostoepfe.FirstOrDefault();
        if (ersterTopf is null) return;

        var aktuelleRunde  = InstanzRunden.FirstOrDefault(r => r.Id == runde.Id);
        var vorherigeRunde = aktuelleRunde is not null
            ? InstanzRunden.Where(r => r.Reihenfolge < aktuelleRunde.Reihenfolge)
                           .OrderByDescending(r => r.Reihenfolge)
                           .FirstOrDefault()
            : null;

        if (vorherigeRunde is null)
        {
            var teams = await _mannschaftService.AlleNachTurnierfilterAsync(
                AusgewaehlteInstanz?.VerbandId,
                AusgewaehlteInstanz?.MannschaftsartFilter ?? MannschaftsartFilter.Alle);
            var zuFuellende = runde.EingangsMannschaftsanzahl > 0
                ? teams.Take(runde.EingangsMannschaftsanzahl)
                : (IEnumerable<Mannschaft>)teams;
            foreach (var team in zuFuellende)
                await _auslosungService.MannschaftZuLostopfHinzufuegenAsync(auslosungId, ersterTopf.Id, team.Id);
        }
        else if (aktuelleRunde is not null)
        {
            var platzhalter = PlatzhalterGenerieren(aktuelleRunde, vorherigeRunde);
            foreach (var p in platzhalter)
                await _auslosungService.PlatzhalterZuLostopfHinzufuegenAsync(auslosungId, ersterTopf.Id, p.Name);
        }
    }

    private async Task AktualisiereSeitenlisteAsync()
    {
        if (AusgewaehlteInstanz is null) return;
        var auslosungen = await _auslosungService.AlleNachInstanzAbrufenAsync(AusgewaehlteInstanz.Id);
        Auslosungen = new ObservableCollection<Auslosung>(auslosungen);
        if (_auslosungDetail is not null)
            AusgewaehlteAuslosung = Auslosungen.FirstOrDefault(a => a.Id == _auslosungDetail.Id);
    }

    partial void OnNeuRundeChanged(TurnierRunde? value)
    {
        if (value is null) return;
        if (string.IsNullOrWhiteSpace(NeuName) || NeuName == _autoNeuName)
        {
            _autoNeuName = value.Name;
            NeuName      = value.Name;
        }
    }

    private bool KannNeu()       => !NeuFormAktiv && AusgewaehlteInstanz is not null;
    private bool KannSpeichern() => (NeuFormAktiv && NeuRunde is not null)
                                 || (IstDetailDirty && IstBearbeitbar);
    private bool KannAbbrechen() => NeuFormAktiv || IstDetailDirty;
    private bool KannLoeschen()  => AusgewaehlteAuslosung is not null && !NeuFormAktiv;

    partial void OnDetailNameChanged(string value)           { if (!_ladenAktiv) IstDetailDirty = true; }
    partial void OnDetailAuslosungsartChanged(Auslosungsart value)
    {
        if (!_ladenAktiv) IstDetailDirty = true;
        AktualisiereRedundanteRegeln(value);
    }

    private void AktualisiereRedundanteRegeln(Auslosungsart art)
    {
        foreach (var regel in Regeln)
        {
            bool redundant = regel.Regelart switch
            {
                AuslosungsRegelArt.GruppensiegerGegenGruppenzweite =>
                    art == Auslosungsart.GruppensiegerGegenGruppenzweite,
                _ => false
            };
            regel.IstRedundant    = redundant;
            regel.RedundanzHinweis = redundant
                ? "Diese Regel ist bei der gewählten Auslosungsart bereits im Algorithmus enthalten und hat keine zusätzliche Wirkung."
                : "";
        }
    }
    partial void OnDetailAnzahlLostoepfeChanged(int value)   { if (!_ladenAktiv) IstDetailDirty = true; }

    private static string GruppenBuchstabe(int nr) =>
        nr <= 26 ? ((char)('A' + nr - 1)).ToString() : $"G{nr}";

    // ── Anzeigetexte ──────────────────────────────────────────────────────────

    public static string AuslosungsartAnzeigeName(Auslosungsart a) => a switch
    {
        Auslosungsart.Zufall                          => "Zufällig",
        Auslosungsart.NachSetzliste                   => "Nach Setzliste",
        Auslosungsart.NachToepfen                     => "Nach Töpfen",
        Auslosungsart.TopfZuordnung                   => "Topf-Zuordnung (UCL)",
        Auslosungsart.GruppensiegerGegenGruppenzweite => "Gruppensieger vs. Zweite",
        Auslosungsart.Benutzerdefiniert               => "Benutzerdefiniert",
        Auslosungsart.Manuell                         => "Manuell",
        _                                             => a.ToString()
    };

    private static string StatusAnzeigeName(AuslosungsStatus s) => s switch
    {
        AuslosungsStatus.Vorbereitung  => "Vorbereitung",
        AuslosungsStatus.Laufend       => "Laufend",
        AuslosungsStatus.Abgeschlossen => "Abgeschlossen",
        AuslosungsStatus.Abgebrochen   => "Abgebrochen",
        _                              => s.ToString()
    };

    private static string RegelAnzeigeName(AuslosungsRegelArt art) => art switch
    {
        AuslosungsRegelArt.KeinGleichesLand                => "Kein gleiches Land",
        AuslosungsRegelArt.KeineGleicheGruppe              => "Keine gleiche Gruppe",
        AuslosungsRegelArt.GruppensiegerGegenGruppenzweite => "Gruppensieger vs. Gruppenzweite",
        AuslosungsRegelArt.GesetzteGegenUngesetzte         => "Gesetzte vs. Ungesetzte",
        AuslosungsRegelArt.HeimrechtNachLos                => "Heimrecht nach Los",
        AuslosungsRegelArt.HeimrechtNachSetzliste          => "Heimrecht nach Setzliste",
        AuslosungsRegelArt.FreilosMoeglich                 => "Freilos möglich",
        AuslosungsRegelArt.ManuelleEinschraenkung          => "Manuelle Einschränkung",
        _                                                  => art.ToString()
    };

    private static string RegelBeschreibung(AuslosungsRegelArt art) => art switch
    {
        AuslosungsRegelArt.KeinGleichesLand =>
            "Teams aus demselben Land dürfen nicht in dieselbe Gruppe oder gegeneinander gelost werden. " +
            "Verhindert z. B. nationale Duelle in der Gruppenphase (typisch bei Nationenturnieren).",
        AuslosungsRegelArt.KeineGleicheGruppe =>
            "Teams, die in einer vorangegangenen Gruppenphase bereits in derselben Gruppe waren, " +
            "dürfen nicht erneut aufeinandertreffen. Typisch für UCL/UEL K.O.-Runden.",
        AuslosungsRegelArt.GruppensiegerGegenGruppenzweite =>
            "Gruppensieger werden nur mit Zweitplatzierten einer anderen Gruppe gepaart — " +
            "nie gegeneinander und nie gegen den Zweiten der eigenen Gruppe. " +
            "Strukturregel für K.O.-Runden nach einer Gruppenphase (z. B. WM-Achtelfinale).",
        AuslosungsRegelArt.GesetzteGegenUngesetzte =>
            "Gesetzte Teams (Topf 1) werden ausschließlich mit ungesetzten Teams gepaart. " +
            "Zwei gesetzte Teams treffen nie direkt aufeinander. " +
            "Klassische Setzlisten-Regel für nationale und internationale Pokal-Auslosungen. " +
            "Ergänzt 'NachSetzliste' als Paarungsbeschränkung — die Auslosungsart befüllt die Töpfe, diese Regel steuert die Paarungsbildung.",
        AuslosungsRegelArt.HeimrechtNachLos =>
            "Das Heimrecht wird separat ausgelost: Ein zufällig gezogenes Team erhält das Heimrecht " +
            "für das Hinspiel, das andere für das Rückspiel. " +
            "Nur relevant bei Hin- und Rückspielen.",
        AuslosungsRegelArt.HeimrechtNachSetzliste =>
            "Das höher gesetzte (besser platzierte) Team erhält automatisch das Heimrecht im Rückspiel. " +
            "Bevorzugt Titelverteidiger und Top-Gesetzte — typisch für UEFA-Wettbewerbe.",
        AuslosungsRegelArt.FreilosMoeglich =>
            "Bei ungerader Teamanzahl erhält ein zufällig gezogenes Team ein Freilos und zieht " +
            "kampflos in die nächste Runde ein. Ohne diese Regel schlägt die Auslosung fehl, " +
            "wenn die Anzahl ungerade ist.",
        AuslosungsRegelArt.ManuelleEinschraenkung =>
            "Bestimmte Paarungen sind aus besonderen Gründen ausgeschlossen (z. B. politische " +
            "oder logistische Einschränkungen). Die konkreten Verbote werden separat konfiguriert.",
        _ => ""
    };
}
