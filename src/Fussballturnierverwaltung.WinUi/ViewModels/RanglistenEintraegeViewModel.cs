using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.WinUi.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

// ViewModel-Zeile für die Einträge-ListView; jede Eigenschaft ist observable, damit
// Inline-Editing (Punkte) sofort den Dirty-Zustand der Rangliste setzt. Rang wird NICHT
// manuell editiert, sondern aus der Position in der Eintraege-Liste abgeleitet (siehe
// RanglistenEintraegeViewModel.AktualisiereRaenge) — Reihenfolge per Hoch/Runter-Buttons, analog
// zum bestehenden Verfahren-Muster in RegelwerkeViewModel.
public partial class RanglistenEintragZeile : ObservableObject
{
    [ObservableProperty] private Mannschaft _mannschaft = null!;
    [ObservableProperty] private int _rang;
    [ObservableProperty] private int? _punkte;

    public byte[]? Wappen => MannschaftsIcon.Fuer(Mannschaft);
}

/// <summary>Eintragsliste des Ranglisten-Editors (#477): Einträge, Mannschaftsauswahl zum Hinzufügen und
/// Umsortieren. Meldet jede Änderung über <see cref="Geaendert"/>; der Editor entscheidet über den Dirty-Zustand.</summary>
public partial class RanglistenEintraegeViewModel : ObservableObject
{
    private static readonly VerbandFilterOption _allesVerband = new("— Alle —", null);
    private int _laedtGerade;

    public event EventHandler? Geaendert;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerfuegbareMannschaftenGefiltert))]
    private ObservableCollection<RanglistenEintragZeile> _eintraege = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EintragEntfernenCommand))]
    [NotifyCanExecuteChangedFor(nameof(EintragHochCommand))]
    [NotifyCanExecuteChangedFor(nameof(EintragRunterCommand))]
    private RanglistenEintragZeile? _ausgewaehlterEintrag;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerfuegbareMannschaftenGefiltert))]
    private ObservableCollection<Mannschaft> _alleMannschaften = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerbandFilterOptionen))]
    private ObservableCollection<Verband> _alleVerbaende = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerfuegbareMannschaftenGefiltert))]
    [NotifyPropertyChangedFor(nameof(MannschaftVerbandFilterAuswahl))]
    private VerbandFilterOption _mannschaftVerbandFilter = _allesVerband;

    // WinUI-ComboBox schreibt beim Laden bzw. beim Tausch der ItemsSource null über TwoWay zurück
    // (siehe RundenFormularViewModel.RundenartAuswahl, #377). Deshalb bindet sie an diese Nullable-Hülle:
    // null wird verworfen und die Anzeige danach wiederhergestellt.
    public VerbandFilterOption? MannschaftVerbandFilterAuswahl
    {
        get => MannschaftVerbandFilter;
        set
        {
            if (value is not null) MannschaftVerbandFilter = value;
            else NachComboBoxResetWiederherstellen();
        }
    }

    private void NachComboBoxResetWiederherstellen() =>
        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(
            Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
            () => OnPropertyChanged(nameof(MannschaftVerbandFilterAuswahl)));

    // Kanonische Options-Instanzen: die ComboBox gleicht SelectedItem per Referenz ab (#347),
    // daher nur bei geänderter Verbandsliste neu aufbauen und die Auswahl auf die neue Instanz umhängen.
    private IReadOnlyList<VerbandFilterOption> _verbandFilterOptionen = [_allesVerband];

    partial void OnAlleVerbaendeChanged(ObservableCollection<Verband> value)
    {
        _verbandFilterOptionen = [_allesVerband, .. value.Select(v => new VerbandFilterOption(v.Name, v))];
        MannschaftVerbandFilter = _verbandFilterOptionen
            .FirstOrDefault(o => o.Wert?.Id == MannschaftVerbandFilter.Wert?.Id) ?? _allesVerband;
    }

    partial void OnEintraegeChanged(ObservableCollection<RanglistenEintragZeile>? oldValue,
        ObservableCollection<RanglistenEintragZeile> newValue)
    {
        if (oldValue is not null) oldValue.CollectionChanged -= EintraegeGeaendert;
        newValue.CollectionChanged += EintraegeGeaendert;
        OnPropertyChanged(nameof(EintraegeUeberschrift));
    }

    private void EintraegeGeaendert(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        OnPropertyChanged(nameof(EintraegeUeberschrift));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerfuegbareMannschaftenGefiltert))]
    private string _mannschaftSuchbegriff = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EintragHinzufuegenCommand))]
    private Mannschaft? _neueMannschaft;

    // Einträge werden per Add/Remove bearbeitet (nicht neu zugewiesen) — daher folgt die Überschrift
    // CollectionChanged, siehe OnEintraegeChanged.
    public string EintraegeUeberschrift => $"EINTRÄGE ({Eintraege.Count})";

    public IReadOnlyList<VerbandFilterOption> VerbandFilterOptionen => _verbandFilterOptionen;

    public IEnumerable<Mannschaft> VerfuegbareMannschaftenGefiltert => AlleMannschaften
        .Where(m => Eintraege.All(e => e.Mannschaft.Id != m.Id))
        .Where(m => MannschaftVerbandFilter.Wert is null || m.Land?.VerbandId == MannschaftVerbandFilter.Wert.Id)
        .Where(m => string.IsNullOrWhiteSpace(MannschaftSuchbegriff)
            || m.Name.Contains(MannschaftSuchbegriff, StringComparison.OrdinalIgnoreCase));

    public void StammdatenSetzen(IEnumerable<Mannschaft> mannschaften, IEnumerable<Verband> verbaende)
    {
        AlleMannschaften = new ObservableCollection<Mannschaft>(mannschaften);
        AlleVerbaende = new ObservableCollection<Verband>(verbaende.OrderBy(v => v.Name));
    }

    /// <summary>Füllt die Liste mit den Einträgen einer Rangliste (leer bei neuer bzw. keiner Rangliste).</summary>
    public void Laden(IEnumerable<RanglistenEintragZeile> zeilen)
    {
        _laedtGerade++;
        try
        {
            Eintraege = new ObservableCollection<RanglistenEintragZeile>(zeilen);
            AbonniereEintraegeAenderungen();
            AktualisiereRaenge();
            NeueMannschaft = null;
            MannschaftSuchbegriff = "";
            MannschaftVerbandFilter = _allesVerband;
        }
        finally
        {
            _laedtGerade--;
        }
    }

    // Meldet Inline-Änderungen (Punkte) als Änderung der Rangliste.
    private void AbonniereEintraegeAenderungen()
    {
        foreach (var zeile in Eintraege)
            zeile.PropertyChanged += OnEintragGeaendert;
    }

    private void OnEintragGeaendert(object? sender, PropertyChangedEventArgs e)
    {
        if (_laedtGerade == 0)
            Geaendert?.Invoke(this, EventArgs.Empty);
    }

    // Rang ergibt sich immer aus der Position in der Liste (1-basiert) — keine
    // manuelle Eingabe, keine Lücken/Duplikate möglich.
    private void AktualisiereRaenge()
    {
        for (int i = 0; i < Eintraege.Count; i++)
            Eintraege[i].Rang = i + 1;
        OnPropertyChanged(nameof(VerfuegbareMannschaftenGefiltert));
    }

    // ── Eintrag-Befehle ───────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannEintragHinzufuegen))]
    private void EintragHinzufuegen()
    {
        if (NeueMannschaft is null) return;
        var neu = new RanglistenEintragZeile { Mannschaft = NeueMannschaft, Rang = Eintraege.Count + 1 };
        neu.PropertyChanged += OnEintragGeaendert;
        Eintraege.Add(neu);
        AusgewaehlterEintrag = neu;
        NeueMannschaft = null;
        AktualisiereRaenge();
        Geaendert?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(KannEintragEntfernen))]
    private void EintragEntfernen()
    {
        if (AusgewaehlterEintrag is null) return;
        Eintraege.Remove(AusgewaehlterEintrag);
        AusgewaehlterEintrag = null;
        AktualisiereRaenge();
        Geaendert?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(KannEintragHoch))]
    private void EintragHoch()
    {
        if (AusgewaehlterEintrag is null) return;
        var idx = Eintraege.IndexOf(AusgewaehlterEintrag);
        if (idx <= 0) return;
        Eintraege.Move(idx, idx - 1);
        AktualisiereRaenge();
        Geaendert?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(KannEintragRunter))]
    private void EintragRunter()
    {
        if (AusgewaehlterEintrag is null) return;
        var idx = Eintraege.IndexOf(AusgewaehlterEintrag);
        if (idx < 0 || idx >= Eintraege.Count - 1) return;
        Eintraege.Move(idx, idx + 1);
        AktualisiereRaenge();
        Geaendert?.Invoke(this, EventArgs.Empty);
    }

    private bool KannEintragHinzufuegen() => NeueMannschaft is not null;
    private bool KannEintragEntfernen() => AusgewaehlterEintrag is not null;
    private bool KannEintragHoch() => AusgewaehlterEintrag is not null && Eintraege.IndexOf(AusgewaehlterEintrag) > 0;
    private bool KannEintragRunter() => AusgewaehlterEintrag is not null
        && Eintraege.IndexOf(AusgewaehlterEintrag) < Eintraege.Count - 1;
}
