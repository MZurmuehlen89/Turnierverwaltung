using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Diagnose;
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
using Microsoft.Extensions.Logging;
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

/// <summary>Kopfzeile eines aufklappbaren Turnierkopf-Bereichs (Issue #399): Titel plus Kurzfassung des
/// Inhalts, damit auch zugeklappt erkennbar bleibt, ob etwas festgelegt ist.</summary>
public record AbschnittKopf(string Titel, string Zusammenfassung);

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

/// <summary>Gastgeber-Zeile (Issue #352). Ein <c>null</c> aus der ComboBox kommt nur transient (Auswahl vor
/// <c>Loaded</c> gesetzt, siehe WinUi-CLAUDE.md) und wird ignoriert — entfernt wird eine Zeile über ✕.</summary>
public partial class GastgeberZeile : ObservableObject
{
    private Mannschaft? _mannschaft;
    public Mannschaft? Mannschaft
    {
        get => _mannschaft;
        set { if (value is not null) SetProperty(ref _mannschaft, value); else OnPropertyChanged(); }
    }
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
    private readonly ILogger<TurnierkopfViewModel> _logger;
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
        CollectionSyncVerzoegern(GastgeberFilterAnwenden);
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
        CollectionSyncVerzoegern(GastgeberFilterAnwenden);
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
        CollectionSyncVerzoegern(GastgeberFilterAnwenden);
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

    // ── Gastgeber (Issue #352) ────────────────────────────────────────────────
    // Behalten in Runden mit neutralem Spielort ihren Heimvorteil. Auswahl wie beim Titelverteidiger nach dem
    // Auslosungsfilter vorgefiltert (PasstZumAuslosungsfilter); gewählte Gastgeber bleiben immer enthalten, sonst
    // fände die ComboBox ihre Auswahl nicht. Stabile Collection-Instanzen mit inkrementellem Abgleich
    // (WinUI3-Fallstrick #335), Referenzabgleich der ComboBox über dieselben Mannschaft-Objekte.
    public ObservableCollection<GastgeberZeile> GastgeberZeilen { get; } = [];
    public ObservableCollection<Mannschaft> GastgeberKandidaten { get; } = [];

    // Alle Mannschaften nach Name sortiert.
    private IReadOnlyList<Mannschaft> _alleGastgeberKandidaten = [];

    [RelayCommand]
    private void GastgeberHinzufuegen() => GastgeberZeilen.Add(new GastgeberZeile());

    [RelayCommand]
    private void GastgeberEntfernen(GastgeberZeile zeile) => GastgeberZeilen.Remove(zeile);

    private void GastgeberZeilenAbonnieren()
    {
        GastgeberZeilen.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null)
                foreach (GastgeberZeile z in e.NewItems)
                    z.PropertyChanged += (_, _) =>
                    {
                        OnPropertyChanged(nameof(TitelGastgeberKopf));
                        if (_populiertGerade > 0) return;
                        IsDirty = true;
                        // Eine zuvor nur wegen der Auswahl enthaltene Mannschaft fällt wieder heraus.
                        CollectionSyncVerzoegern(() => GastgeberOptionenSynchronisieren());
                    };
            OnPropertyChanged(nameof(TitelGastgeberKopf));
            if (_populiertGerade > 0) return;
            IsDirty = true;
            CollectionSyncVerzoegern(() => GastgeberOptionenSynchronisieren());
        };
    }

    private void GastgeberSetzen(IEnumerable<int> mannschaftIds)
    {
        _populiertGerade++;
        try
        {
            var gastgeber = _alleGastgeberKandidaten.Where(m => mannschaftIds.Contains(m.Id)).ToList();
            GastgeberZeilen.Clear();
            GastgeberOptionenSynchronisieren(gastgeber);
            foreach (var m in gastgeber)
                GastgeberZeilen.Add(new GastgeberZeile { Mannschaft = m });
        }
        finally
        {
            _populiertGerade--;
        }
    }

    /// <summary>Gleicht die Auswahlliste mit dem Auslosungsfilter ab. Gewählte Gastgeber (und optional
    /// <paramref name="zusaetzlich"/>, bevor sie gesetzt werden) bleiben immer enthalten.</summary>
    private void GastgeberOptionenSynchronisieren(IReadOnlyCollection<Mannschaft>? zusaetzlich = null)
    {
        var gewaehlt = GastgeberZeilen.Select(z => z.Mannschaft).OfType<Mannschaft>().ToHashSet();
        if (zusaetzlich is not null) gewaehlt.UnionWith(zusaetzlich);
        IReadOnlyList<Mannschaft> soll = _alleGastgeberKandidaten
            .Where(m => gewaehlt.Contains(m) || PasstZumAuslosungsfilter(m))
            .ToList();
        CollectionInkrementellSynchronisieren(GastgeberKandidaten, soll);
    }

    /// <summary>Nach einer Filteränderung durch den Nutzer: Gastgeber, die nicht mehr zum Filter passen, werden
    /// entfernt (Prävention statt Speicher-Validierung, analog <see cref="TitelverteidigerFilterAnwenden"/>),
    /// danach die Liste abgeglichen.</summary>
    private void GastgeberFilterAnwenden()
    {
        foreach (var zeile in GastgeberZeilen.Where(z => z.Mannschaft is not null && !PasstZumAuslosungsfilter(z.Mannschaft)).ToList())
            GastgeberZeilen.Remove(zeile);
        GastgeberOptionenSynchronisieren();
    }

    private List<int> GastgeberIds() => GastgeberZeilen
        .Where(z => z.Mannschaft is not null)
        .Select(z => z.Mannschaft!.Id)
        .Distinct()
        .ToList();

    // ── Status-Anzeige/-Auswahl ───────────────────────────────────────────────
    [ObservableProperty] private TurnierStatusOption _status = new(TurnierStatusZuTextConverter.StatusAnzeigeName(TurnierStatus.Geplant), TurnierStatus.Geplant);

    public IReadOnlyList<TurnierStatusOption> StatusOptionen =>
        Enum.GetValues<TurnierStatus>().Select(s => new TurnierStatusOption(TurnierStatusZuTextConverter.StatusAnzeigeName(s), s)).ToList();

    /// <summary>Vom Composite gesetzt: der Status, mit dem "Neu" das Formular vorbelegt — er folgt dem
    /// Turniere/Vorlagen-Umschalter der Liste, sonst entstünde in der Vorlagen-Ansicht ein Turnier, das dort
    /// nach dem Speichern gar nicht auftaucht (#382).</summary>
    public TurnierStatus StatusFuerNeueInstanz { get; set; } = TurnierStatus.Geplant;

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
        // Die Instanz vor dem Warten festhalten: bis der Schutz frei ist, kann ein Turnierwechsel
        // AktuelleInstanz bereits umgesetzt haben.
        if (AktuelleInstanz is not { } instanz) return;
        Fehlermeldung = null;
        try
        {
            if (!await AenderungBestaetigtAsync())
            {
                StatusAuswahlZuruecksetzen();
                return;
            }

            using var schutz = await DbContextSchutz.BetretenAsync();
            using var vorgang = _logger.Vorgang($"Turnierkopf: Status ändern (Instanz {instanz.Id}, {neuerStatus})");
            await _service.StatusAendernAsync(instanz.Id, neuerStatus);
            InstanzGeaendert?.Invoke(instanz);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
            StatusAuswahlZuruecksetzen();
        }
    }

    private void StatusAuswahlZuruecksetzen()
    {
        if (AktuelleInstanz is null) return;
        _populiertGerade++;
        try { Status = StatusOptionen.First(o => o.Wert == AktuelleInstanz.Status); }
        finally { _populiertGerade--; }
    }

    /// <summary>Vom Composite gesetzte Rückfrage vor jeder Aktion, die <see cref="InstanzGeaendert"/> auslöst —
    /// der folgende Reload aller Reiter verwirft eine begonnene, noch nicht abgeschlossene Vorbelegung, und
    /// nach der Aktion lässt sich das nicht mehr ablehnen. "Löschen" fragt nicht zusätzlich: seine eigene
    /// Rückfrage nennt bereits, dass alles zum Turnier Gehörende verloren geht.</summary>
    public Func<Task<bool>>? VorAenderungBestaetigen { get; set; }

    private Task<bool> AenderungBestaetigtAsync() => VorAenderungBestaetigen?.Invoke() ?? Task.FromResult(true);

    /// <summary>Reiht die Aktionen dieses Reiters (Speichern, Statuswechsel, Löschen, Abbrechen) hinter einen
    /// Turnierwechsel ein und umgekehrt — über den vom <see cref="TurnierlisteViewModel"/> gesetzten gemeinsamen
    /// Schutz (#416). Nicht wiedereintrittsfähig: <see cref="InstanzSetzenAsync"/> läuft bereits unter dem
    /// Schutz, und <see cref="InstanzGeaendert"/> betritt ihn als Fire-and-Forget selbst — innerhalb einer
    /// geschützten Aktion nur auslösen, nie abwarten.</summary>
    public DbContextReentrancySchutz DbContextSchutz { get; set; } = new();

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
            OnPropertyChanged(nameof(QuotenKopf));
            if (_populiertGerade == 0) IsDirty = true;
        };
        foreach (var z in QuotenZeilen) z.PropertyChanged += OnQuotenZeileGeaendert;
    }

    private void OnQuotenZeileGeaendert(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(QuotenKopf));
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
            OnPropertyChanged(nameof(QuotenKopf));
            if (_populiertGerade == 0) IsDirty = true;
        };
        foreach (var z in LandesQuotenZeilen) z.PropertyChanged += OnLandesQuoteZeileGeaendert;
    }

    private void OnLandesQuoteZeileGeaendert(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(QuotenKopf));
        if (_populiertGerade == 0) IsDirty = true;
    }

    // ── Aufklappbare Bereiche (Issue #399) ────────────────────────────────────
    // Titelverteidiger + Gastgeber und die Quoten sind selten genutzt und liegen deshalb in aufklappbaren
    // Bereichen. Aufgeklappt wird beim Turnierwechsel, wenn der Bereich Werte enthält; danach bleibt der
    // Zustand dem Nutzer überlassen (Speichern/Abbrechen laden dasselbe Turnier neu und klappen nichts um).
    [ObservableProperty] private bool _titelGastgeberOffen;
    [ObservableProperty] private bool _quotenOffen;

    public bool QuotenSichtbar => VerbandsQuotenSichtbar || LandesQuotenSichtbar;

    // Ohne Verband-Quoten rückt die Landes-Quote in die linke Spalte.
    public int LandesQuotenSpalte => VerbandsQuotenSichtbar ? 2 : 0;

    public AbschnittKopf TitelGastgeberKopf
    {
        get
        {
            var teile = new List<string>();
            if (Titelverteidiger?.Wert is not null) teile.Add($"Titelverteidiger: {Titelverteidiger.Anzeige}");
            var gastgeber = GastgeberZeilen.Select(z => z.Mannschaft).OfType<Mannschaft>().Distinct().ToList();
            if (gastgeber.Count is > 0 and <= 3) teile.Add($"Gastgeber: {string.Join(", ", gastgeber.Select(m => m.Name))}");
            else if (gastgeber.Count > 3) teile.Add($"Gastgeber: {gastgeber.Count}");
            return new("TITELVERTEIDIGER & GASTGEBER", teile.Count > 0 ? string.Join(" · ", teile) : "nicht festgelegt");
        }
    }

    public AbschnittKopf QuotenKopf
    {
        get
        {
            var teile = new List<string>();
            var verband = QuotenZeilen.Where(z => z.Verband is not null).ToList();
            if (VerbandsQuotenSichtbar && verband.Count > 0)
                teile.Add($"Verband: {verband.Count} (Summe {verband.Sum(z => z.Anzahl)})");
            var land = LandesQuotenZeilen.Where(z => z.Land is not null).ToList();
            if (LandesQuotenSichtbar && land.Count > 0)
                teile.Add($"Land: {land.Count} (Summe {land.Sum(z => z.Anzahl)})");
            return new("QUOTEN", teile.Count > 0 ? string.Join(" · ", teile) : "nicht festgelegt");
        }
    }

    private void AufklappzustandSetzen()
    {
        TitelGastgeberOffen = Titelverteidiger?.Wert is not null || GastgeberZeilen.Count > 0;
        QuotenOffen = QuotenZeilen.Count > 0 || LandesQuotenZeilen.Count > 0;
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
        TurnierKlonService klonService, MannschaftService mannschaftService,
        ILogger<TurnierkopfViewModel> logger)
    {
        _logger = logger;
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
        GastgeberZeilenAbonnieren();
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
        _alleGastgeberKandidaten = mannschaften.OrderBy(m => m.Name).ToList();
        GastgeberOptionenSynchronisieren();
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
        using var vorgang = _logger.Vorgang($"Turnierkopf: Turnier setzen (Instanz {instanz?.Id})");
        var instanzGewechselt = AktuelleInstanz?.Id != instanz?.Id || IstNeuModus;
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
                GastgeberSetzen([]);
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

            if (instanz is null)
            {
                AufklappzustandSetzen();
                return;
            }

            IstLadend = true;
            try
            {
                var detail = await _service.FindenNachIdMitRundenAsync(instanz.Id);
                ErsteRundeEingang = detail?.Runden.OrderBy(r => r.Reihenfolge).FirstOrDefault()?.EingangsMannschaftsanzahl;
                GastgeberSetzen(detail?.Gastgeber.Select(g => g.MannschaftId).ToList() ?? []);
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
                if (instanzGewechselt) AufklappzustandSetzen();
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
            Status = StatusOptionen.First(o => o.Wert == StatusFuerNeueInstanz);
            LogoDaten = null;
            QuotenZeilen = [];
            LandesQuotenZeilen = [];
            GastgeberSetzen([]);
            ErsteRundeEingang = null;
            Fehlermeldung = null;
            AufklappzustandSetzen();
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
        // Vor dem Öffnen fragen — der Erstellen-Dialog ist selbst ein ContentDialog, nur einer kann offen sein.
        if (!await AenderungBestaetigtAsync()) return;

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
        if (!await AenderungBestaetigtAsync()) return;

        using var schutz = await DbContextSchutz.BetretenAsync();
        // Hat ein Turnierwechsel das Formular während des Wartens neu befüllt, gibt es nichts mehr zu speichern.
        if (!KannSpeichern()) return;

        Fehlermeldung = null;
        try
        {
            using var vorgang = _logger.Vorgang($"Turnierkopf: speichern (Instanz {AktuelleInstanz?.Id})");
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
                    Variante: Variante, GastgeberIds: GastgeberIds());
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
                    Variante: Variante, GastgeberIds: GastgeberIds());
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
    private async Task Abbrechen()
    {
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (!KannAbbrechen()) return;
        using var vorgang = _logger.Vorgang($"Turnierkopf: abbrechen (Instanz {AktuelleInstanz?.Id})");
        await InstanzSetzenAsync(IstNeuModus ? null : AktuelleInstanz);
    }

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task Loeschen()
    {
        // Gelöscht wird die Instanz, die die Rückfrage genannt hat — auch wenn ein Turnierwechsel
        // AktuelleInstanz umsetzt, bis der Schutz frei ist.
        if (AktuelleInstanz is not { } instanz) return;
        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Turnier löschen",
            $"Soll '{instanz.Name}' wirklich gelöscht werden?\n\nDabei werden alle Spiele, Auslosungen und Simulationsläufe mitgelöscht.");
        if (!bestaetigt) return;

        using var schutz = await DbContextSchutz.BetretenAsync();
        Fehlermeldung = null;
        try
        {
            using var vorgang = _logger.Vorgang($"Turnierkopf: Turnier löschen (Instanz {instanz.Id})");
            await _service.LoeschenAsync(instanz.Id);
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
                fehler.Add("Mannschaftsanzahl muss eine Zahl ≥ 2 sein.");
        }

        if (!string.IsNullOrWhiteSpace(Startdatum) && ParseJahr(Startdatum) is null)
            fehler.Add("Startjahr muss eine Jahreszahl zwischen 1800 und 2100 sein.");
        if (!string.IsNullOrWhiteSpace(Enddatum) && ParseJahr(Enddatum) is null)
            fehler.Add("Endjahr muss eine Jahreszahl zwischen 1800 und 2100 sein.");

        fehler.AddRange(_validator.BasisdatenValidieren(
            Name, SaisonOderJahr, zielAnzahl, ParseJahr(Startdatum), ParseJahr(Enddatum), LogoDaten).Fehler);

        FormularFehler = fehler;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        switch (e.PropertyName)
        {
            case nameof(Titelverteidiger):
                OnPropertyChanged(nameof(TitelGastgeberKopf));
                break;
            case nameof(QuotenZeilen) or nameof(LandesQuotenZeilen):
                OnPropertyChanged(nameof(QuotenKopf));
                break;
            case nameof(VerbandsQuotenSichtbar) or nameof(LandesQuotenSichtbar):
                OnPropertyChanged(nameof(QuotenSichtbar));
                OnPropertyChanged(nameof(LandesQuotenSpalte));
                OnPropertyChanged(nameof(QuotenKopf));
                break;
        }
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
