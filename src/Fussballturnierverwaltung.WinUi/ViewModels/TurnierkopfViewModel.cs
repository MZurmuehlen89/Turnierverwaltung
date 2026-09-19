using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Converters;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using Fussballturnierverwaltung.WinUi.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

public record VerbandOption(string Anzeige, Verband? Wert);
public record LandOption(string Anzeige, Land? Wert);
public record TeamfilterOption(string Anzeige, MannschaftsartFilter Wert);
public record TurnierStatusOption(string Anzeige, TurnierStatus Wert);

/// <summary>Eine Verband-Quoten-Zeile im Turnierkopf (Issue #161/#154, hier #297) — lokaler UI-Zustand
/// zwischen Laden und Speichern, analog WPF-Original <c>VerbandsQuoteZeile</c>.</summary>
public partial class VerbandsQuoteZeile : ObservableObject
{
    [ObservableProperty] private Verband? _verband;
    [ObservableProperty] private int _anzahl = 1;
}

/// <summary>Eine Landes-Quoten-Zeile im Turnierkopf (Issue #189) — lokaler UI-Zustand zwischen Laden und
/// Speichern, 1:1 analog <see cref="VerbandsQuoteZeile"/>, aber auf Land statt Kontinentalverband.</summary>
public partial class LandesQuoteZeile : ObservableObject
{
    [ObservableProperty] private Land? _land;
    [ObservableProperty] private int _anzahl = 1;
}

/// <summary>"Turnierkopf"-Tab von <see cref="TurnierlisteViewModel"/> (Issue #297) — Basisdaten,
/// Auslosungsfilter (Verband/Land/Mannschaftsart), Standard-Regelwerk, Status-Anzeige/-Auswahl und
/// Verband-Quoten einer <see cref="TurnierInstanz"/> (Vorlage- oder Turnier-Status identisch behandelt).
/// Runden/Übergangsregeln/Vorbelegung liegen in den separaten Tabs. Struktur analog WPF-Original
/// <c>TurnierInstanzViewModel</c>, hier auf ein reines Formular-ViewModel zugeschnitten (die Instanzliste
/// samt Status-Filter lebt in <see cref="TurnierTabKoordinator"/>).</summary>
public partial class TurnierkopfViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly TurnierInstanzService _service;
    private readonly TurnierValidator _validator;
    private readonly IDialogService _dialogService;
    private readonly VerbandService _verbandService;
    private readonly LandService _landService;
    private readonly RegelwerkService _regelwerkService;
    private readonly VerbandsQuoteService _verbandsQuoteService;
    private readonly VerbandsQuoteValidator _verbandsQuoteValidator;
    private readonly LandesQuoteService _landesQuoteService;
    private readonly LandesQuoteValidator _landesQuoteValidator;
    private readonly TurnierKlonService _klonService;
    private int _populiertGerade;

    private static readonly VerbandOption _keinVerband = new("— Kein Filter —", null);
    private static readonly LandOption _keinLand = new("— Kein Filter —", null);
    private static readonly RegelwerkOption _keinStandardRegelwerk = new("— Kein Standard —", null);
    private static readonly TeamfilterOption _alleMannschaften = new("Alle Mannschaften", MannschaftsartFilter.Alle);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private TurnierInstanz? _aktuelleInstanz;

    // ── Formular-Zustand ──────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    [NotifyPropertyChangedFor(nameof(VerbandsQuotenSichtbar))]
    [NotifyPropertyChangedFor(nameof(LandesQuotenSichtbar))]
    private bool _formularAktiv;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    private bool _isDirty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    [NotifyPropertyChangedFor(nameof(VerbandsQuotenSichtbar))]
    [NotifyPropertyChangedFor(nameof(LandesQuotenSichtbar))]
    private bool _istNeuModus;

    [ObservableProperty] private bool _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatFormularFehler))]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private IReadOnlyList<string> _formularFehler = [];

    public bool HatFormularFehler => FormularFehler.Count > 0;

    // ── Basisdaten ────────────────────────────────────────────────────────────
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _beschreibung = "";
    [ObservableProperty] private string _saisonOderJahr = "";
    [ObservableProperty] private string _zielMannschaftsanzahl = "";
    [ObservableProperty] private TurnierKategorie _kategorie = TurnierKategorie.Benutzerdefiniert;
    [ObservableProperty] private string _startdatum = "";
    [ObservableProperty] private string _enddatum = "";
    [ObservableProperty] private byte[]? _logoDaten;

    public IEnumerable<TurnierKategorie> KategorieOptionen => Enum.GetValues<TurnierKategorie>();

    // ── Auslosungsfilter (Verband/Land/Mannschaftsart) ───────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerbandOptionenGefiltert))]
    private IReadOnlyList<VerbandOption> _verbandOptionen = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerbandsQuotenSichtbar))]
    [NotifyPropertyChangedFor(nameof(LandOptionenGefiltert))]
    private VerbandOption _verbandFilter = _keinVerband;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LandOptionenGefiltert))]
    private IReadOnlyList<LandOption> _landOptionen = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerbandsQuotenSichtbar))]
    [NotifyPropertyChangedFor(nameof(VerbandOptionenGefiltert))]
    private LandOption _landFilter = _keinLand;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LandesQuotenSichtbar))]
    private TeamfilterOption _teamFilter = _alleMannschaften;

    // Beide Filter schränken sich gegenseitig ein (Issue #211-Muster aus dem WPF-Original): FIFA ist die
    // Ausnahme, kein Land ist ihr direkt zugeordnet, als Filter bedeutet sie "weltweit". Null-conditional
    // auf VerbandFilter/LandFilter selbst (nicht nur .Wert) — derselbe transiente ComboBox-Reset-Fallstrick
    // wie in OnVerbandFilterChanged/OnLandFilterChanged kann diese Getter treffen, bevor die dortige
    // Korrektur greift (NRE hier gefunden bei #297-Test, Stapelüberwachung zeigte den Crash direkt im
    // Getter statt im Change-Handler).
    public IReadOnlyList<LandOption> LandOptionenGefiltert => VerbandFilter?.Wert is null || IstFifa(VerbandFilter.Wert)
        ? LandOptionen
        : [_keinLand, .. LandOptionen.Where(o => o.Wert?.VerbandId == VerbandFilter.Wert.Id)];

    public IReadOnlyList<VerbandOption> VerbandOptionenGefiltert => LandFilter?.Wert is null
        ? VerbandOptionen
        : [_keinVerband, .. VerbandOptionen.Where(o => o.Wert?.Id == LandFilter.Wert.VerbandId || IstFifa(o.Wert))];

    private static bool IstFifa(Verband? v) => v?.Kurzname == "FIFA";

    /// <summary>Setzt Verband- und Landfilter atomar statt sequenziell. Bei sequenzieller Zuweisung
    /// (erst VerbandFilter, dann LandFilter) feuert das Setzen von VerbandFilter sofort
    /// NotifyPropertyChangedFor(LandOptionenGefiltert) — die Land-ComboBox bekommt dadurch schon die neue,
    /// verbandsgefilterte ItemsSource-Liste, während LandFilter selbst noch den alten Wert des vorherigen
    /// Turniers trägt; ist dieser darin nicht mehr enthalten, nullt WinUI die Selektion sofort, und die
    /// eigentlich korrekte nachfolgende Zuweisung kommt zu spät (Fund: Verband-/Land-Feld blieb beim
    /// Wechsel zwischen Turniervorlagen sporadisch leer — ein reines Guard-Fenster half hier nicht, das
    /// Problem war die Reihenfolge, nicht das Timing).
    ///
    /// Benachrichtigt jede gefilterte Liste (ItemsSource) nur, wenn sich ihr bestimmender Filter
    /// tatsächlich geändert hat — ein unveränderter Filter soll keine neue Array-Instanz an die ComboBox
    /// ausliefern, jede ItemsSource-Neuzuweisung ist ein weiterer Anlass für den WinUI-Selection-Reset-
    /// Zyklus (Fund: bei Vorlagen mit gleichem Verband, aber unterschiedlichem Land — z.B. "DFB-Pokal"
    /// und "UEFA EM (16 Teams)", beide UEFA — trat der Bug trotz atomarer Zuweisung noch auf, weil
    /// LandOptionenGefiltert unnötig auch bei unverändertem VerbandFilter neu benachrichtigt wurde). Beide
    /// SelectedItem-Bindungen (VerbandFilter/LandFilter) werden dagegen IMMER neu angestoßen, sobald
    /// irgendetwas sich geändert hat — auch ohne eigene Wertänderung, da ihre eigene ComboBox ggf. gerade
    /// eine neue ItemsSource von der jeweils ANDEREN Änderung bekommen hat und resynchronisiert werden muss.</summary>
    private void AuslosungsfilterSetzen(VerbandOption verband, LandOption land)
    {
        var verbandGeaendert = VerbandFilter != verband;
        var landGeaendert = LandFilter != land;
        if (!verbandGeaendert && !landGeaendert) return;

        // Bewusster Direktzugriff auf die Backing-Fields statt der generierten Properties: Wir wollen die
        // Notify-Kaskade der generierten Setter (die pro Zuweisung sofort feuert) durch die kontrollierte
        // Reihenfolge unten ersetzen, siehe Klassenkommentar oben.
#pragma warning disable MVVMTK0034
        _verbandFilter = verband;
        _landFilter = land;
#pragma warning restore MVVMTK0034

        if (verbandGeaendert) OnPropertyChanged(nameof(LandOptionenGefiltert));
        if (landGeaendert) OnPropertyChanged(nameof(VerbandOptionenGefiltert));

        // Die beiden Notifications oben können die jeweils betroffene ComboBox veranlassen, ihre
        // SelectedItem-Bindung transient zurückzuschreiben (WinUI-ItemsSource-Reset-Quirk) — das läuft über
        // den GENERIERTEN Property-Setter (nicht über unsere Direktzuweisung oben), der dabei
        // OnVerbandFilterChanged/OnLandFilterChanged mit "null" aufruft; deren Selbstkorrektur ist aber
        // wegen des _populiertGerade-Guards hier absichtlich unterdrückt (siehe dort), sodass ein
        // zwischenzeitliches "null" im Backing-Field stehen bleiben würde. Fund (#189): führte dazu, dass
        // das Land-Feld nach einem Vorlagenwechsel komplett leer blieb (nicht nur fälschlich "Kein
        // Filter") — die anschließende Benachrichtigung unten hätte sonst dieses korrumpierte "null" statt
        // des beabsichtigten Werts gemeldet. Deshalb die Backing-Fields hier erneut auf die beabsichtigten
        // Werte zurücksetzen, unmittelbar vor der finalen Benachrichtigung.
#pragma warning disable MVVMTK0034
        _verbandFilter = verband;
        _landFilter = land;
#pragma warning restore MVVMTK0034

        OnPropertyChanged(nameof(VerbandFilter));
        OnPropertyChanged(nameof(LandFilter));
        OnPropertyChanged(nameof(VerbandsQuotenSichtbar));
    }

    partial void OnVerbandFilterChanged(VerbandOption value)
    {
        // WinUI setzt ComboBox.SelectedItem transient auf null, wenn ItemsSource neu zugewiesen wird
        // (LandOptionenGefiltert liefert bei jedem Zugriff ein neues Array) — TwoWay-Binding drückt das
        // kurzzeitig als null-value in diesen Handler, obwohl der Property-Typ nicht-nullable ist (Fund
        // aus #297-Test: NullReferenceException beim Setzen des Verbands). Explizit auf "Kein Filter"
        // zurücksetzen statt nur früh zurückzukehren, sonst bleibt das Backing-Field null und spätere
        // Lesezugriffe (VerbandsQuotenSichtbar, Speichern, …) crashen ebenfalls.
        if (_populiertGerade > 0) return;
        if (value is null) { VerbandFilter = _keinVerband; return; }
        if (value.Wert is not null && !IstFifa(value.Wert)
            && LandFilter?.Wert is not null && LandFilter.Wert.VerbandId != value.Wert.Id)
            LandFilter = _keinLand;
    }

    partial void OnLandFilterChanged(LandOption value)
    {
        // Siehe OnVerbandFilterChanged — derselbe ComboBox-ItemsSource-Reset-Fallstrick, hier für
        // VerbandOptionenGefiltert.
        if (_populiertGerade > 0) return;
        if (value is null) { LandFilter = _keinLand; return; }
        if (value.Wert is not null && VerbandFilter?.Wert is not null && !IstFifa(VerbandFilter.Wert)
            && VerbandFilter.Wert.Id != value.Wert.VerbandId)
            VerbandFilter = _keinVerband;
    }

    public IReadOnlyList<TeamfilterOption> TeamfilterOptionen =>
        [_alleMannschaften,
         new("Nur Vereine", MannschaftsartFilter.Verein),
         new("Nur Nationalmannschaften", MannschaftsartFilter.Nationalmannschaft)];

    // ── Standard-Regelwerk ────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StandardRegelwerkOptionen))]
    private ObservableCollection<Regelwerk> _regelwerke = [];

    [ObservableProperty] private RegelwerkOption _standardRegelwerk = _keinStandardRegelwerk;

    public IReadOnlyList<RegelwerkOption> StandardRegelwerkOptionen =>
        [_keinStandardRegelwerk, .. Regelwerke.Select(r => new RegelwerkOption(r.Name, r))];

    // ── Status-Anzeige/-Auswahl ───────────────────────────────────────────────
    [ObservableProperty] private TurnierStatusOption _status = new(TurnierStatusZuTextConverter.StatusAnzeigeName(TurnierStatus.Geplant), TurnierStatus.Geplant);

    public IReadOnlyList<TurnierStatusOption> StatusOptionen =>
        Enum.GetValues<TurnierStatus>().Select(s => new TurnierStatusOption(TurnierStatusZuTextConverter.StatusAnzeigeName(s), s)).ToList();

    /// <summary>Bei einer NEUEN, noch nicht gespeicherten Instanz ist die Statuswahl Teil der
    /// Erstellungs-Eingabe (kein Server-Aufruf möglich, es gibt noch keine Id) — bei einer bestehenden
    /// Instanz wirkt sie sofort über <see cref="TurnierInstanzService.StatusAendernAsync"/>, unabhängig
    /// vom Formular-Dirty-Status der Basisdaten (Issue #297: "Status-Anzeige/-Auswahl").</summary>
    partial void OnStatusChanged(TurnierStatusOption value)
    {
        if (_populiertGerade > 0) return;
        if (IstNeuModus) { IsDirty = true; return; }
        if (AktuelleInstanz is not null && value.Wert != AktuelleInstanz.Status)
            _ = StatusAendernAsync(value.Wert);
    }

    private async Task StatusAendernAsync(TurnierStatus neuerStatus)
    {
        if (AktuelleInstanz is null) return;
        Fehlermeldung = null;
        try
        {
            await _service.StatusAendernAsync(AktuelleInstanz.Id, neuerStatus);
            InstanzGeaendert?.Invoke(AktuelleInstanz);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
            _populiertGerade++;
            try { Status = StatusOptionen.First(o => o.Wert == AktuelleInstanz.Status); }
            finally { _populiertGerade--; }
        }
    }

    // ── Verband-Quoten (Issue #161/#154, hier #297) ──────────────────────────
    [ObservableProperty] private ObservableCollection<VerbandsQuoteZeile> _quotenZeilen = [];
    [ObservableProperty] private IReadOnlyList<Verband> _alleVerbaende = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KapazitaetsHinweisSichtbar))]
    private int? _ersteRundeEingang;

    public bool KapazitaetsHinweisSichtbar => ErsteRundeEingang.HasValue;

    // "Verband" ist ausschließlich die Kontinentalebene — ist Verbands- oder Länderfilter gesetzt, gehören
    // zwangsläufig alle zugelassenen Teams zu genau einem Kontinentalverband, eine Verband-Quote kann dann
    // nie etwas Sinnvolles ausdrücken (Issue #154). FIFA-Ausnahme (Issue #211): "weltweit" lässt wieder alle
    // Kontinentalverbände zu. Nur für bereits gespeicherte Instanzen sichtbar (Quoten brauchen eine Id).
    public bool VerbandsQuotenSichtbar => FormularAktiv && !IstNeuModus
        && (VerbandFilter?.Wert is null || IstFifa(VerbandFilter.Wert))
        && LandFilter?.Wert is null;

    [RelayCommand]
    private void VerbandsQuoteHinzufuegen() => QuotenZeilen.Add(new VerbandsQuoteZeile());

    [RelayCommand]
    private void VerbandsQuoteEntfernen(VerbandsQuoteZeile zeile) => QuotenZeilen.Remove(zeile);

    private void QuotenZeilenAbonnieren()
    {
        QuotenZeilen.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null)
                foreach (VerbandsQuoteZeile z in e.NewItems)
                    z.PropertyChanged += OnQuotenZeileGeaendert;
            if (_populiertGerade == 0) IsDirty = true;
        };
        foreach (var z in QuotenZeilen) z.PropertyChanged += OnQuotenZeileGeaendert;
    }

    private void OnQuotenZeileGeaendert(object? sender, PropertyChangedEventArgs e)
    {
        if (_populiertGerade == 0) IsDirty = true;
    }

    // ── Landes-Quoten (Issue #189) ────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<LandesQuoteZeile> _landesQuotenZeilen = [];
    [ObservableProperty] private IReadOnlyList<Land> _alleLaender = [];

    // Landesquote ist unabhängig vom Verbandsfilter sichtbar (orthogonal zur Verband-Quote, nicht
    // exklusiv) — reales Vorbild Klub-WM hat Konföderations-Slots (Verband-Quote) UND eine Landes-Quote
    // ("max. 2 Vereine pro Land") gleichzeitig aktiv, ohne dass ein Verbandsfilter das ausschließt.
    // Nur für Vereinswettbewerbe sinnvoll (bei Nationalmannschaften ist "max. 1 Team/Land" ohnehin
    // trivial) und nur für bereits gespeicherte Instanzen (Quoten brauchen eine Id).
    public bool LandesQuotenSichtbar => FormularAktiv && !IstNeuModus
        && TeamFilter.Wert == MannschaftsartFilter.Verein;

    [RelayCommand]
    private void LandesQuoteHinzufuegen() => LandesQuotenZeilen.Add(new LandesQuoteZeile());

    [RelayCommand]
    private void LandesQuoteEntfernen(LandesQuoteZeile zeile) => LandesQuotenZeilen.Remove(zeile);

    private void LandesQuotenZeilenAbonnieren()
    {
        LandesQuotenZeilen.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null)
                foreach (LandesQuoteZeile z in e.NewItems)
                    z.PropertyChanged += OnLandesQuoteZeileGeaendert;
            if (_populiertGerade == 0) IsDirty = true;
        };
        foreach (var z in LandesQuotenZeilen) z.PropertyChanged += OnLandesQuoteZeileGeaendert;
    }

    private void OnLandesQuoteZeileGeaendert(object? sender, PropertyChangedEventArgs e)
    {
        if (_populiertGerade == 0) IsDirty = true;
    }

    private static readonly HashSet<string> _formFelder =
    [
        nameof(Name), nameof(Beschreibung), nameof(SaisonOderJahr), nameof(ZielMannschaftsanzahl),
        nameof(Kategorie), nameof(Startdatum), nameof(Enddatum), nameof(LogoDaten),
        nameof(VerbandFilter), nameof(LandFilter), nameof(TeamFilter), nameof(StandardRegelwerk)
    ];

    /// <summary>Feuert nach erfolgreichem Speichern (mit der neuen/aktualisierten Instanz), Statuswechsel
    /// oder Löschen (mit null) — der Composite lädt daraufhin die geteilte Instanzliste neu.</summary>
    public event Action<TurnierInstanz?>? InstanzGeaendert;

    // ── IRibbonAware ──────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Turnierkopf";
    public ICommand? NeuBefehl => NeuCommand;
    public ICommand? SpeichernBefehl => SpeichernCommand;
    public ICommand? AbbrechenBefehl => AbbrechenCommand;
    public ICommand? LoeschenBefehl => LoeschenCommand;

    /// <summary>Issue #297-Nachzug: <see cref="TurnierKlonService"/> existiert seit #294, war aber in
    /// keiner UI verdrahtet (dokumentierte Scope-Lücke). "Aus Vorlage erstellen" legt eine neue Instanz an
    /// (echtes Turnier oder weitere Vorlage), löst nebenbei #46. Als Unterpunkt im "Neu"-Flyout statt
    /// eigener Ribbon-Gruppe (Nutzerwunsch aus #297-Test).</summary>
    public IReadOnlyList<RibbonAktion> NeuUnterpunkte =>
        [new RibbonAktion { Header = "Aus Vorlage erstellen", Befehl = AusVorlageErstellenCommand, IconGlyph = "" }];

    public TurnierkopfViewModel(
        TurnierInstanzService service, TurnierValidator validator, IDialogService dialogService,
        VerbandService verbandService, LandService landService, RegelwerkService regelwerkService,
        VerbandsQuoteService verbandsQuoteService, VerbandsQuoteValidator verbandsQuoteValidator,
        LandesQuoteService landesQuoteService, LandesQuoteValidator landesQuoteValidator,
        TurnierKlonService klonService)
    {
        _service = service;
        _validator = validator;
        _dialogService = dialogService;
        _verbandService = verbandService;
        _landService = landService;
        _regelwerkService = regelwerkService;
        _verbandsQuoteService = verbandsQuoteService;
        _verbandsQuoteValidator = verbandsQuoteValidator;
        _landesQuoteService = landesQuoteService;
        _landesQuoteValidator = landesQuoteValidator;
        _klonService = klonService;
        QuotenZeilenAbonnieren();
        LandesQuotenZeilenAbonnieren();
    }

    /// <summary>Lädt Verbände/Länder/Regelwerke einmalig — vom Composite (<see cref="TurnierlisteViewModel"/>)
    /// beim Navigieren aufgerufen, bevor die Instanzauswahl das Formular erstmals befüllt.</summary>
    public async Task StammdatenLadenAsync()
    {
        var verbaende = await _verbandService.AlleAbrufenAsync();
        VerbandOptionen = [_keinVerband, .. verbaende.Select(v => new VerbandOption(v.Name, v))];
        AlleVerbaende = verbaende;
        var laender = await _landService.AlleAbrufenAsync();
        LandOptionen = [_keinLand, .. laender.OrderBy(l => l.Name).Select(l => new LandOption(l.Name, l))];
        AlleLaender = laender;
        Regelwerke = new ObservableCollection<Regelwerk>(await _regelwerkService.AlleAbrufenAsync());
    }

    /// <summary>Vom Composite gesetzt, wenn sich die geteilte Instanzauswahl ändert. Der Guard
    /// (<see cref="_populiertGerade"/>) umspannt die gesamte Methode inkl. der nachfolgenden
    /// async-Ladeschritte, damit während des Ladens ausgelöste Zwischen-Änderungen nicht fälschlich
    /// IsDirty setzen. Verband-/Landfilter werden über <see cref="AuslosungsfilterSetzen"/> atomar
    /// gesetzt (siehe dort) statt sequenziell — sequenzielle Zuweisung führte zu sporadisch leeren
    /// Feldern beim Wechsel zwischen Turniervorlagen.</summary>
    public async Task InstanzSetzenAsync(TurnierInstanz? instanz)
    {
        AktuelleInstanz = instanz;
        _populiertGerade++;
        try
        {
            if (instanz is null)
            {
                FormularAktiv = false;
                IstNeuModus = false;
                Name = Beschreibung = SaisonOderJahr = ZielMannschaftsanzahl = Startdatum = Enddatum = "";
                Kategorie = TurnierKategorie.Benutzerdefiniert;
                AuslosungsfilterSetzen(_keinVerband, _keinLand);
                TeamFilter = _alleMannschaften;
                StandardRegelwerk = _keinStandardRegelwerk;
                Status = StatusOptionen.First(o => o.Wert == TurnierStatus.Geplant);
                LogoDaten = null;
                QuotenZeilen = [];
                LandesQuotenZeilen = [];
                ErsteRundeEingang = null;
            }
            else
            {
                FormularAktiv = true;
                IstNeuModus = false;
                Name = instanz.Name;
                Beschreibung = instanz.Beschreibung;
                SaisonOderJahr = instanz.SaisonOderJahr;
                ZielMannschaftsanzahl = instanz.ZielMannschaftsanzahl?.ToString() ?? "";
                Kategorie = instanz.Kategorie;
                Startdatum = instanz.Startdatum?.Year.ToString() ?? "";
                Enddatum = instanz.Enddatum?.Year.ToString() ?? "";
                AuslosungsfilterSetzen(
                    VerbandOptionen.FirstOrDefault(o => o.Wert?.Id == instanz.VerbandId) ?? _keinVerband,
                    LandOptionen.FirstOrDefault(o => o.Wert?.Id == instanz.LandId) ?? _keinLand);
                TeamFilter = TeamfilterOptionen.FirstOrDefault(o => o.Wert == instanz.MannschaftsartFilter) ?? _alleMannschaften;
                StandardRegelwerk = StandardRegelwerkOptionen.FirstOrDefault(o => o.Wert?.Id == instanz.StandardRegelwerkId) ?? _keinStandardRegelwerk;
                Status = StatusOptionen.First(o => o.Wert == instanz.Status);
                LogoDaten = instanz.Logo;
            }
            IsDirty = false;
            Fehlermeldung = null;

            AktualisiereFormularFehler();

            if (instanz is null) return;

            IstLadend = true;
            try
            {
                var detail = await _service.FindenNachIdMitRundenAsync(instanz.Id);
                ErsteRundeEingang = detail?.Runden.OrderBy(r => r.Reihenfolge).FirstOrDefault()?.EingangsMannschaftsanzahl;
                await QuotenLadenAsync(instanz.Id);
                await LandesQuotenLadenAsync(instanz.Id);
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
        finally
        {
            _populiertGerade--;
        }
    }

    private async Task QuotenLadenAsync(int turnierInstanzId)
    {
        var quoten = await _verbandsQuoteService.AlleNachTurnierInstanzIdAsync(turnierInstanzId);
        _populiertGerade++;
        try
        {
            QuotenZeilen = new ObservableCollection<VerbandsQuoteZeile>(
                quoten.Select(q => new VerbandsQuoteZeile { Verband = q.Verband, Anzahl = q.Anzahl }));
        }
        finally
        {
            _populiertGerade--;
        }
        QuotenZeilenAbonnieren();
    }

    private async Task LandesQuotenLadenAsync(int turnierInstanzId)
    {
        var quoten = await _landesQuoteService.AlleNachTurnierInstanzIdAsync(turnierInstanzId);
        _populiertGerade++;
        try
        {
            LandesQuotenZeilen = new ObservableCollection<LandesQuoteZeile>(
                quoten.Select(q => new LandesQuoteZeile { Land = q.Land, Anzahl = q.Anzahl }));
        }
        finally
        {
            _populiertGerade--;
        }
        LandesQuotenZeilenAbonnieren();
    }

    // ── Ribbon-Befehle ────────────────────────────────────────────────────────

    [RelayCommand]
    private void Neu()
    {
        _populiertGerade++;
        try
        {
            AktuelleInstanz = null;
            FormularAktiv = true;
            IstNeuModus = true;
            Name = Beschreibung = SaisonOderJahr = ZielMannschaftsanzahl = Startdatum = Enddatum = "";
            Kategorie = TurnierKategorie.Benutzerdefiniert;
            AuslosungsfilterSetzen(_keinVerband, _keinLand);
            TeamFilter = _alleMannschaften;
            StandardRegelwerk = _keinStandardRegelwerk;
            Status = StatusOptionen.First(o => o.Wert == TurnierStatus.Geplant);
            LogoDaten = null;
            QuotenZeilen = [];
            LandesQuotenZeilen = [];
            ErsteRundeEingang = null;
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }
        IsDirty = true;
        AktualisiereFormularFehler();
    }

    [RelayCommand]
    private async Task AusVorlageErstellenAsync()
    {
        var dialog = new AusVorlageErstellenDialog(_service, _klonService);
        await _dialogService.AnzeigenAsync(dialog);
        if (dialog.ViewModel.ErstellteInstanz is { } neu)
            InstanzGeaendert?.Invoke(neu);
    }

    [RelayCommand]
    private async Task LogoWaehlenAsync()
    {
        var daten = await _dialogService.BilddateiWaehlenAsync();
        if (daten is not null) LogoDaten = daten;
    }

    [RelayCommand]
    private void LogoEntfernen() => LogoDaten = null;

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        Fehlermeldung = null;
        try
        {
            var zielAnzahl = ParseZielAnzahl(ZielMannschaftsanzahl);
            var startdatum = ParseJahr(Startdatum);
            var enddatum = ParseJahr(Enddatum);
            var verbandId = VerbandFilter.Wert?.Id;
            var landId = LandFilter.Wert?.Id;
            var standardRwId = StandardRegelwerk.Wert?.Id;

            TurnierInstanz ergebnis;
            if (IstNeuModus)
            {
                var eingabe = new TurnierInstanzErstellenEingabe(
                    Name: Name, Beschreibung: Beschreibung, SaisonOderJahr: SaisonOderJahr, Kategorie: Kategorie,
                    ZielMannschaftsanzahl: zielAnzahl, Startdatum: startdatum, Enddatum: enddatum, Status: Status.Wert,
                    VerbandId: verbandId, LandId: landId, MannschaftsartFilter: TeamFilter.Wert,
                    StandardRegelwerkId: standardRwId, Logo: LogoDaten);
                ergebnis = await _service.ErstellenAsync(eingabe);
            }
            else if (AktuelleInstanz is not null)
            {
                var id = AktuelleInstanz.Id;

                var quotenZeilen = QuotenZeilen
                    .Where(z => z.Verband is not null)
                    .Select(z => new VerbandsQuoteZeileEingabe(z.Verband!.Id, z.Anzahl))
                    .ToList();
                ValidationException.WerfenWennUngueltig(_verbandsQuoteValidator.Validieren(quotenZeilen, ErsteRundeEingang));

                var landesQuotenZeilen = LandesQuotenZeilen
                    .Where(z => z.Land is not null)
                    .Select(z => new LandesQuoteZeileEingabe(z.Land!.Id, z.Anzahl))
                    .ToList();
                ValidationException.WerfenWennUngueltig(_landesQuoteValidator.Validieren(landesQuotenZeilen, ErsteRundeEingang));

                var eingabe = new TurnierInstanzVollAktualisierenEingabe(
                    Name: Name, Beschreibung: Beschreibung, SaisonOderJahr: SaisonOderJahr, Kategorie: Kategorie,
                    ZielMannschaftsanzahl: zielAnzahl, Startdatum: startdatum, Enddatum: enddatum, Status: AktuelleInstanz.Status,
                    VerbandId: verbandId, LandId: landId, MannschaftsartFilter: TeamFilter.Wert,
                    StandardRegelwerkId: standardRwId, Logo: LogoDaten);
                await _service.VollAktualisierenAsync(id, eingabe);
                await _verbandsQuoteService.QuotenErsetzenAsync(id, quotenZeilen, ErsteRundeEingang);
                await _landesQuoteService.QuotenErsetzenAsync(id, landesQuotenZeilen, ErsteRundeEingang);

                ergebnis = AktuelleInstanz;
            }
            else
            {
                return;
            }

            IsDirty = false;
            InstanzGeaendert?.Invoke(ergebnis);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private async Task Abbrechen() => await InstanzSetzenAsync(IstNeuModus ? null : AktuelleInstanz);

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task Loeschen()
    {
        if (AktuelleInstanz is null) return;
        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Turnier löschen",
            $"Soll '{AktuelleInstanz.Name}' wirklich gelöscht werden?\n\nDabei werden alle Spiele, Auslosungen und Simulationsläufe mitgelöscht.");
        if (!bestaetigt) return;

        Fehlermeldung = null;
        try
        {
            await _service.LoeschenAsync(AktuelleInstanz.Id);
            InstanzGeaendert?.Invoke(null);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    // ── Validierung ───────────────────────────────────────────────────────────

    private void AktualisiereFormularFehler()
    {
        if (!FormularAktiv) { FormularFehler = []; return; }

        var fehler = new List<string>();

        int? zielAnzahl = null;
        var zielText = ZielMannschaftsanzahl.Trim();
        if (!string.IsNullOrEmpty(zielText))
        {
            if (int.TryParse(zielText, out var anzahl) && anzahl >= 2)
                zielAnzahl = anzahl;
            else
                fehler.Add("Ziel-Mannschaftsanzahl muss eine Zahl ≥ 2 sein.");
        }

        if (!string.IsNullOrWhiteSpace(Startdatum) && ParseJahr(Startdatum) is null)
            fehler.Add("Startdatum muss eine Jahreszahl zwischen 1800 und 2100 sein.");
        if (!string.IsNullOrWhiteSpace(Enddatum) && ParseJahr(Enddatum) is null)
            fehler.Add("Enddatum muss eine Jahreszahl zwischen 1800 und 2100 sein.");

        fehler.AddRange(_validator.BasisdatenValidieren(
            Name, SaisonOderJahr, zielAnzahl, ParseJahr(Startdatum), ParseJahr(Enddatum), LogoDaten).Fehler);

        FormularFehler = fehler;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_populiertGerade == 0 && _formFelder.Contains(e.PropertyName!))
        {
            IsDirty = true;
            AktualisiereFormularFehler();
        }
    }

    private bool KannSpeichern() => FormularAktiv && IsDirty && !HatFormularFehler;
    private bool KannAbbrechen() => FormularAktiv && IsDirty;

    public bool HatUngespeicherteAenderungen => FormularAktiv && IsDirty;
    private bool KannLoeschen() => AktuelleInstanz is not null && !IstNeuModus;

    private static int? ParseZielAnzahl(string text)
    {
        var t = text.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        return int.TryParse(t, out var n) && n >= 2 ? n : null;
    }

    private static DateOnly? ParseJahr(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return int.TryParse(text.Trim(), out var jahr) && jahr >= 1800 && jahr <= 2100
            ? new DateOnly(jahr, 1, 1)
            : null;
    }
}
