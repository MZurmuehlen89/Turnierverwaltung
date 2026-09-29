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
using Microsoft.UI.Dispatching;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

public record VerbandOption(string Anzeige, Verband? Wert);
public record LandOption(string Anzeige, Land? Wert);
public record TeamfilterOption(string Anzeige, MannschaftsartFilter Wert);
public record TitelverteidigerOption(string Anzeige, int? Wert);
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
    private readonly MannschaftService _mannschaftService;
    private int _populiertGerade;

    private static readonly VerbandOption _keinVerband = new("— Kein Filter —", null);
    private static readonly LandOption _keinLand = new("— Kein Filter —", null);
    private static readonly RegelwerkOption _keinStandardRegelwerk = new("— Kein Standard —", null);
    private static readonly TeamfilterOption _alleMannschaften = new("Alle Mannschaften", MannschaftsartFilter.Alle);
    private static readonly TitelverteidigerOption _keinTitelverteidiger = new("— keiner —", null);

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
    [ObservableProperty] private string _variante = "";
    [ObservableProperty] private string _saisonOderJahr = "";
    [ObservableProperty] private string _zielMannschaftsanzahl = "";
    [ObservableProperty] private TurnierKategorie _kategorie = TurnierKategorie.Benutzerdefiniert;
    [ObservableProperty] private string _startdatum = "";
    [ObservableProperty] private string _enddatum = "";
    [ObservableProperty] private byte[]? _logoDaten;

    public IEnumerable<TurnierKategorie> KategorieOptionen => Enum.GetValues<TurnierKategorie>();

    // ── Auslosungsfilter (Verband/Land/Mannschaftsart) ───────────────────────
    [ObservableProperty] private IReadOnlyList<VerbandOption> _verbandOptionen = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerbandsQuotenSichtbar))]
    private VerbandOption _verbandFilter = _keinVerband;
    [ObservableProperty] private IReadOnlyList<LandOption> _landOptionen = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerbandsQuotenSichtbar))]
    private LandOption _landFilter = _keinLand;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LandesQuotenSichtbar))]
    private TeamfilterOption _teamFilter = _alleMannschaften;

    // Beide Filter schränken sich gegenseitig ein (Issue #211-Muster aus dem WPF-Original): FIFA ist die
    // Ausnahme, kein Land ist ihr direkt zugeordnet, als Filter bedeutet sie "weltweit".
    //
    // Stabile Collection-Instanzen statt berechneten Properties, die bei jedem Zugriff ein neues Array
    // liefern: Eine ComboBox, deren ItemsSource bei jeder Filteränderung eine komplett NEUE Listen-Instanz
    // zugewiesen bekommt, setzt ihr TwoWay-gebundenes SelectedItem in WinUI transient auf null zurück (der
    // reine null-Reassert-Guard in OnVerbandFilterChanged/OnLandFilterChanged reichte dagegen NICHT aus —
    // Fund #335, Land-Feld blieb beim direkten Setzen des Verband-Filters per ComboBox trotzdem leer;
    // siehe auch der projektweite Fallstrick "pro Fokus/Auswahl neu berechnete ItemsSource" im WinUi-
    // CLAUDE.md). <see cref="LandOptionenGefiltertSynchronisieren"/>/<see cref="VerbandOptionenGefiltertSynchronisieren"/>
    // aktualisieren den Inhalt dieser EINEN Instanz inkrementell (Index-Diff statt Clear+Add), die
    // ItemsSource-Referenz selbst wird nie ausgetauscht.
    public ObservableCollection<LandOption> LandOptionenGefiltert { get; } = [];
    public ObservableCollection<VerbandOption> VerbandOptionenGefiltert { get; } = [];

    partial void OnVerbandOptionenChanged(IReadOnlyList<VerbandOption> value) => VerbandOptionenGefiltertSynchronisieren();
    partial void OnLandOptionenChanged(IReadOnlyList<LandOption> value) => LandOptionenGefiltertSynchronisieren();

    private void LandOptionenGefiltertSynchronisieren()
    {
        IReadOnlyList<LandOption> soll = VerbandFilter?.Wert is null || IstFifa(VerbandFilter.Wert)
            ? LandOptionen
            : [_keinLand, .. LandOptionen.Where(o => o.Wert?.VerbandId == VerbandFilter.Wert.Id)];
        CollectionInkrementellSynchronisieren(LandOptionenGefiltert, soll);
    }

    private void VerbandOptionenGefiltertSynchronisieren()
    {
        IReadOnlyList<VerbandOption> soll = LandFilter?.Wert is null
            ? VerbandOptionen
            : [_keinVerband, .. VerbandOptionen.Where(o => o.Wert?.Id == LandFilter.Wert.VerbandId || IstFifa(o.Wert))];
        CollectionInkrementellSynchronisieren(VerbandOptionenGefiltert, soll);
    }

    /// <summary>Bringt <paramref name="ziel"/> per reinem Merge auf denselben Inhalt wie <paramref name="soll"/>
    /// — nur <c>RemoveAt</c> für weggefallene und <c>Insert</c> für neu hinzugekommene Einträge, NIE
    /// Indexer-Zuweisung/<c>Replace</c>, NIE <c>Clear()</c>+Neubefüllen und NIE ein bereits vorhandenes
    /// Element entfernen+an anderer Stelle neu einfügen ("Move" per Remove+Insert).
    ///
    /// Zwei WinUI3-Fallstricke wurden hier nacheinander gefunden (#335):
    /// 1. <c>Replace</c>-Notifications lösten eine <c>ArgumentException</c> ("cannot work with indices
    ///    larger than Int32.MaxValue - 1") in der WinRT-ABI-Marshalling-Schicht der gebundenen ComboBox aus
    ///    (<c>ABI.System.Collections.IList.ToAbiHelper.GetAt</c>) — vermutlich ein intern nicht gefundener
    ///    <c>IndexOf</c> (-1), der als riesiger <c>UInt32</c>-Index missinterpretiert wird.
    /// 2. Ein naiver Index-Diff (RemoveAt+Insert pro abweichendem Index) kann ein Element, das seine Position
    ///    wechseln muss, kurzzeitig DUPLIZIEREN (neu an der Zielposition eingefügt, während das alte
    ///    Vorkommen an seiner ursprünglichen — jetzt verschobenen — Position noch nicht bereinigt ist) — bei
    ///    mehreren solcher Operationen in schneller Folge verlor die ComboBox die Selektion komplett (beide
    ///    Felder zeigten nichts mehr an).
    ///
    /// Diese Methode vermeidet beides: Es werden zuerst nur Elemente ENTFERNT, die in <paramref name="soll"/>
    /// nicht mehr vorkommen (die verbleibenden bilden garantiert eine Teilfolge von <paramref name="soll"/>,
    /// da alle Aufrufe hier stets von derselben festen, sortierten Basisliste filtern), danach werden
    /// ausschließlich fehlende Elemente an ihrer Zielposition eingefügt — kein vorhandenes Element wird je
    /// entfernt und neu eingefügt. Der stets an Index 0 stehende Sentinel-Eintrag (<c>_keinLand</c>/
    /// <c>_keinVerband</c>) bleibt dadurch unangetastet. Siehe <see cref="LandOptionenGefiltert"/>.</summary>
    private static void CollectionInkrementellSynchronisieren<T>(ObservableCollection<T> ziel, IReadOnlyList<T> soll)
    {
        var behalten = new HashSet<T>(soll);
        for (var i = ziel.Count - 1; i >= 0; i--)
            if (!behalten.Contains(ziel[i]))
                ziel.RemoveAt(i);

        var i2 = 0;
        foreach (var item in soll)
        {
            if (i2 < ziel.Count && EqualityComparer<T>.Default.Equals(ziel[i2], item)) { i2++; continue; }
            ziel.Insert(i2, item);
            i2++;
        }
    }

    private static bool IstFifa(Verband? v) => v?.Kurzname == "FIFA";

    /// <summary>Verschiebt eine Collection-Synchronisierung auf den nächsten Dispatcher-Tick statt sie
    /// synchron aus einem ComboBox-Change-Handler heraus auszuführen. Fund #335: Wird
    /// <see cref="LandOptionenGefiltertSynchronisieren"/>/<see cref="VerbandOptionenGefiltertSynchronisieren"/>
    /// synchron aus <see cref="OnVerbandFilterChanged"/>/<see cref="OnLandFilterChanged"/> heraus
    /// aufgerufen, mutiert das die ItemsSource der EINEN ComboBox, während der native WinUI-XAML-Kern noch
    /// mitten in der Verarbeitung des SelectionChanged der ANDEREN ComboBox steckt — führte reproduzierbar
    /// zu einer <c>COMException (0x8000FFFF, "Schwerwiegender Fehler")</c> ohne verwertbaren Managed-Stack.
    /// Über <see cref="DispatcherQueue.TryEnqueue"/> läuft die Mutation erst, nachdem die aktuelle
    /// Bindungsoperation vollständig abgeschlossen ist.</summary>
    private static void CollectionSyncVerzoegern(Action synchronisieren) =>
        DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() => synchronisieren());

    /// <summary>Setzt Verband- und Landfilter atomar statt sequenziell. Bei sequenzieller Zuweisung
    /// (erst VerbandFilter, dann LandFilter) sieht der Zwischenzustand kurzzeitig den neuen Verband mit dem
    /// noch alten Land — <see cref="OnVerbandFilterChanged"/> könnte das Land dann fälschlich als
    /// inkompatibel zurücksetzen, bevor die eigentlich korrekte nachfolgende Land-Zuweisung überhaupt
    /// passiert ist (Fund: Verband-/Land-Feld blieb beim Wechsel zwischen Turniervorlagen sporadisch
    /// falsch). Direktzugriff auf die Backing-Fields umgeht die generierten Setter (und damit
    /// On[Verband|Land]FilterChanged) bewusst, damit beide Werte VOR jeder Change-Reaktion feststehen.</summary>
    private void AuslosungsfilterSetzen(VerbandOption verband, LandOption land)
    {
        var verbandGeaendert = VerbandFilter != verband;
        var landGeaendert = LandFilter != land;
        if (!verbandGeaendert && !landGeaendert) return;

#pragma warning disable MVVMTK0034
        _verbandFilter = verband;
        _landFilter = land;
#pragma warning restore MVVMTK0034

        if (verbandGeaendert) LandOptionenGefiltertSynchronisieren();
        if (landGeaendert) VerbandOptionenGefiltertSynchronisieren();

        OnPropertyChanged(nameof(VerbandFilter));
        OnPropertyChanged(nameof(LandFilter));
        OnPropertyChanged(nameof(VerbandsQuotenSichtbar));
    }

    partial void OnVerbandFilterChanged(VerbandOption value)
    {
        // WinUI kann ComboBox.SelectedItem transient auf null zurückschreiben (TwoWay-Binding) — Fund aus
        // #297-Test: NullReferenceException beim Setzen des Verbands, obwohl der Property-Typ
        // nicht-nullable ist. Explizit auf "Kein Filter" zurücksetzen statt nur früh zurückzukehren, sonst
        // bleibt das Backing-Field null und spätere Lesezugriffe (VerbandsQuotenSichtbar, Speichern, …)
        // crashen ebenfalls.
        if (_populiertGerade > 0) return;
        if (value is null) { VerbandFilter = _keinVerband; return; }
        if (value.Wert is not null && !IstFifa(value.Wert)
            && LandFilter?.Wert is not null && LandFilter.Wert.VerbandId != value.Wert.Id)
            LandFilter = _keinLand;
        CollectionSyncVerzoegern(LandOptionenGefiltertSynchronisieren);
        CollectionSyncVerzoegern(TitelverteidigerFilterAnwenden);
    }

    partial void OnLandFilterChanged(LandOption value)
    {
        // Siehe OnVerbandFilterChanged.
        if (_populiertGerade > 0) return;
        if (value is null) { LandFilter = _keinLand; return; }
        if (value.Wert is not null && VerbandFilter?.Wert is not null && !IstFifa(VerbandFilter.Wert)
            && VerbandFilter.Wert.Id != value.Wert.VerbandId)
            VerbandFilter = _keinVerband;
        CollectionSyncVerzoegern(VerbandOptionenGefiltertSynchronisieren);
        CollectionSyncVerzoegern(TitelverteidigerFilterAnwenden);
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

    // ── Titelverteidiger (Issue #349) ─────────────────────────────────────────
    // Optional; kommt bei Runden mit "Titelverteidiger in Topf 1" unabhängig vom Ranglistenplatz in Topf 1.
    // Die Auswahl ist nach dem Auslosungsfilter (Verband/Land/Mannschaftsart) vorgefiltert — gleiche Semantik
    // wie MannschaftRepository.AlleNachTurnierfilterAsync, FIFA = weltweit. Stabile Collection-Instanz mit
    // inkrementellem Abgleich statt neuer ItemsSource je Filterwechsel (WinUI3-Fallstrick, siehe
    // LandOptionenGefiltert / #335).
    [ObservableProperty] private TitelverteidigerOption _titelverteidiger = _keinTitelverteidiger;
    public ObservableCollection<TitelverteidigerOption> TitelverteidigerOptionenGefiltert { get; } = [_keinTitelverteidiger];

    // Alle Mannschaften nach Name sortiert, je Mannschaft genau eine Option (Referenzabgleich der ComboBox).
    private IReadOnlyList<(TitelverteidigerOption Option, Mannschaft Mannschaft)> _titelverteidigerKandidaten = [];

    partial void OnTitelverteidigerChanged(TitelverteidigerOption value)
    {
        if (value is null) Titelverteidiger = _keinTitelverteidiger;
    }

    partial void OnTeamFilterChanged(TeamfilterOption value)
    {
        if (_populiertGerade > 0 || value is null) return;
        CollectionSyncVerzoegern(TitelverteidigerFilterAnwenden);
    }

    private TitelverteidigerOption TitelverteidigerOptionFuer(int? mannschaftId) =>
        _titelverteidigerKandidaten.FirstOrDefault(k => k.Option.Wert == mannschaftId && mannschaftId is not null).Option
        ?? _keinTitelverteidiger;

    private bool PasstZumAuslosungsfilter(Mannschaft m) =>
        (VerbandFilter?.Wert is null || IstFifa(VerbandFilter.Wert) || m.Land?.VerbandId == VerbandFilter.Wert.Id)
        && (LandFilter?.Wert is null || m.LandId == LandFilter.Wert.Id)
        && (TeamFilter?.Wert ?? MannschaftsartFilter.Alle) switch
        {
            MannschaftsartFilter.Verein             => m.Mannschaftsart == Mannschaftsart.Verein,
            MannschaftsartFilter.Nationalmannschaft => m.Mannschaftsart == Mannschaftsart.Nationalmannschaft,
            _                                       => true
        };

    /// <summary>Gleicht die Auswahlliste mit dem Auslosungsfilter ab. Die aktuelle Auswahl (und optional
    /// <paramref name="zusaetzlich"/>, bevor sie gesetzt wird) bleibt immer enthalten — sonst fände die ComboBox
    /// sie nicht und schriebe null zurück.</summary>
    private void TitelverteidigerOptionenSynchronisieren(TitelverteidigerOption? zusaetzlich = null)
    {
        IReadOnlyList<TitelverteidigerOption> soll =
        [
            _keinTitelverteidiger,
            .. _titelverteidigerKandidaten
                .Where(k => k.Option == Titelverteidiger || k.Option == zusaetzlich || PasstZumAuslosungsfilter(k.Mannschaft))
                .Select(k => k.Option)
        ];
        CollectionInkrementellSynchronisieren(TitelverteidigerOptionenGefiltert, soll);
    }

    /// <summary>Nach einer Filteränderung durch den Nutzer: passt der gewählte Titelverteidiger nicht mehr
    /// zum Filter, wird er zurückgesetzt (Prävention statt Speicher-Validierung), danach die Liste abgeglichen.</summary>
    private void TitelverteidigerFilterAnwenden()
    {
        var aktuell = _titelverteidigerKandidaten.FirstOrDefault(k => k.Option == Titelverteidiger);
        if (aktuell.Mannschaft is not null && !PasstZumAuslosungsfilter(aktuell.Mannschaft))
            Titelverteidiger = _keinTitelverteidiger;
        TitelverteidigerOptionenSynchronisieren();
    }

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
        nameof(Name), nameof(Beschreibung), nameof(Variante), nameof(SaisonOderJahr), nameof(ZielMannschaftsanzahl),
        nameof(Kategorie), nameof(Startdatum), nameof(Enddatum), nameof(LogoDaten),
        nameof(VerbandFilter), nameof(LandFilter), nameof(TeamFilter), nameof(StandardRegelwerk),
        nameof(Titelverteidiger)
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
        TurnierKlonService klonService, MannschaftService mannschaftService)
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
        _mannschaftService = mannschaftService;
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
        var mannschaften = await _mannschaftService.AlleAbrufenMitLandAsync();
        _titelverteidigerKandidaten = mannschaften
            .OrderBy(m => m.Name)
            .Select(m => (new TitelverteidigerOption(m.Name, m.Id), m))
            .ToList();
        TitelverteidigerOptionenSynchronisieren();
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
                Name = Beschreibung = Variante = SaisonOderJahr = ZielMannschaftsanzahl = Startdatum = Enddatum = "";
                Kategorie = TurnierKategorie.Benutzerdefiniert;
                AuslosungsfilterSetzen(_keinVerband, _keinLand);
                TeamFilter = _alleMannschaften;
                StandardRegelwerk = _keinStandardRegelwerk;
                Titelverteidiger = _keinTitelverteidiger;
                TitelverteidigerOptionenSynchronisieren();
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
                Variante = instanz.Variante;
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
                var titelverteidiger = TitelverteidigerOptionFuer(instanz.TitelverteidigerId);
                TitelverteidigerOptionenSynchronisieren(titelverteidiger);
                Titelverteidiger = titelverteidiger;
                TitelverteidigerOptionenSynchronisieren();
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
            Name = Beschreibung = Variante = SaisonOderJahr = ZielMannschaftsanzahl = Startdatum = Enddatum = "";
            Kategorie = TurnierKategorie.Benutzerdefiniert;
            AuslosungsfilterSetzen(_keinVerband, _keinLand);
            TeamFilter = _alleMannschaften;
            StandardRegelwerk = _keinStandardRegelwerk;
            Titelverteidiger = _keinTitelverteidiger;
            TitelverteidigerOptionenSynchronisieren();
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
                    StandardRegelwerkId: standardRwId, Logo: LogoDaten, TitelverteidigerId: Titelverteidiger?.Wert,
                    Variante: Variante);
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
                    StandardRegelwerkId: standardRwId, Logo: LogoDaten, TitelverteidigerId: Titelverteidiger?.Wert,
                    Variante: Variante);
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
