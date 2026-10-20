using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.WinUi.Helpers;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

// ViewModel-Zeile für die Einträge-ListView; jede Eigenschaft ist observable, damit
// Inline-Editing (Punkte) sofort den Dirty-Zustand der Rangliste setzt. Rang wird NICHT
// manuell editiert, sondern aus der Position in der Eintraege-Liste abgeleitet (siehe
// RanglistenEintraegeViewModel.AktualisiereRaenge).
public partial class RanglistenEintragZeile : ObservableObject
{
    [ObservableProperty] private Mannschaft _mannschaft = null!;
    [ObservableProperty] private int _rang;
    [ObservableProperty] private int? _punkte;

    // Name des Liga-Abschnitts, der mit dieser Zeile beginnt (Trennband in der Liste, #477); sonst null.
    [ObservableProperty] private string? _abschnittBeginn;

    public byte[]? Wappen => MannschaftsIcon.Fuer(Mannschaft);
}

/// <summary>Eintragsliste des Ranglisten-Editors (#477): Einträge mit Mehrfachmarkierung, Hinzufügen per Suchbox,
/// Umsortieren (↑/↓, „Auf Rang“, Ziehen), Springen per Suche und Abschnittsgrenzen als Trennbänder. Meldet jede
/// Änderung über <see cref="Geaendert"/>; der Editor entscheidet über den Dirty-Zustand.</summary>
public partial class RanglistenEintraegeViewModel : ObservableObject
{
    private static readonly VerbandFilterOption _allesVerband = new("— Alle —", null);
    private int _laedtGerade;
    private int _eigeneAenderung;
    private IReadOnlyList<Mannschaft> _alleMannschaften = [];
    private IReadOnlyList<AbschnittsZeile> _abschnitte = [];
    private IReadOnlyList<RanglistenEintragZeile> _markierte = [];

    public event EventHandler? Geaendert;

    /// <summary>Bittet die Ansicht, genau diese Zeilen zu markieren und die erste sichtbar zu machen (die ListView
    /// bietet für die Mehrfachauswahl keine Bindung).</summary>
    public event EventHandler<IReadOnlyList<RanglistenEintragZeile>>? MarkierungAngefordert;

    public ObservableCollection<RanglistenEintragZeile> Eintraege { get; } = [];

    /// <summary>Suchbox „Mannschaft hinzufügen“ über die Mannschaften, die noch nicht in der Liste stehen.</summary>
    public MannschaftsfilterViewModel Hinzufuegen { get; } = new(mitAlleOption: false);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MannschaftVerbandFilterAuswahl))]
    private VerbandFilterOption _mannschaftVerbandFilter = _allesVerband;

    // NumberBox liefert double, leer = NaN.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AufRangVerschiebenCommand))]
    private double _zielRang = double.NaN;

    [ObservableProperty] private string _suchText = "";
    [ObservableProperty] private bool _sucheOhneTreffer;

    public RanglistenEintraegeViewModel()
    {
        Eintraege.CollectionChanged += EintraegeGeaendert;
        Hinzufuegen.WahlAngefordert = option =>
        {
            MannschaftHinzufuegen(option);
            return Task.CompletedTask;
        };
    }

    // ── Verbandsfilter für das Hinzufügen ─────────────────────────────────────

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

    // Kanonische Options-Instanzen: die ComboBox gleicht SelectedItem per Referenz ab (#347).
    public IReadOnlyList<VerbandFilterOption> VerbandFilterOptionen { get; private set; } = [_allesVerband];

    partial void OnMannschaftVerbandFilterChanged(VerbandFilterOption value) => HinzufuegbareAktualisieren();

    private void HinzufuegbareAktualisieren() => Hinzufuegen.TeilnehmerSetzen(
    [
        .. _alleMannschaften
            .Where(m => Eintraege.All(e => e.Mannschaft.Id != m.Id))
            .Where(m => MannschaftVerbandFilter.Wert is null || m.Land?.VerbandId == MannschaftVerbandFilter.Wert.Id)
    ]);

    // ── Anzeige ───────────────────────────────────────────────────────────────

    public string EintraegeUeberschrift => $"EINTRÄGE ({Eintraege.Count})";

    public string? MarkierungText => _markierte.Count > 1 ? $"{_markierte.Count} markiert" : null;

    // ── Laden ─────────────────────────────────────────────────────────────────

    public void StammdatenSetzen(IEnumerable<Mannschaft> mannschaften, IEnumerable<Verband> verbaende)
    {
        _alleMannschaften = [.. mannschaften];
        VerbandFilterOptionen = [_allesVerband, .. verbaende.OrderBy(v => v.Name).Select(v => new VerbandFilterOption(v.Name, v))];
        OnPropertyChanged(nameof(VerbandFilterOptionen));
        MannschaftVerbandFilter = VerbandFilterOptionen
            .FirstOrDefault(o => o.Wert?.Id == MannschaftVerbandFilter.Wert?.Id) ?? _allesVerband;
        HinzufuegbareAktualisieren();
    }

    /// <summary>Füllt die Liste mit den Einträgen einer Rangliste (leer bei neuer bzw. keiner Rangliste).</summary>
    public void Laden(IEnumerable<RanglistenEintragZeile> zeilen, IReadOnlyList<AbschnittsZeile> abschnitte)
    {
        _laedtGerade++;
        _eigeneAenderung++;
        try
        {
            foreach (var alt in Eintraege) alt.PropertyChanged -= OnEintragGeaendert;
            Eintraege.Clear();
            foreach (var zeile in zeilen)
            {
                zeile.PropertyChanged += OnEintragGeaendert;
                Eintraege.Add(zeile);
            }
            _abschnitte = abschnitte;
            _markierte = [];
            OnPropertyChanged(nameof(MarkierungText));
            AktualisiereRaenge();
            MannschaftVerbandFilter = _allesVerband;
            HinzufuegbareAktualisieren();
            ZielRang = double.NaN;
            SuchText = "";
            SucheOhneTreffer = false;
        }
        finally
        {
            _eigeneAenderung--;
            _laedtGerade--;
        }
    }

    // Meldet Inline-Änderungen (Punkte) als Änderung der Rangliste.
    private void OnEintragGeaendert(object? sender, PropertyChangedEventArgs e)
    {
        if (_laedtGerade == 0 && e.PropertyName == nameof(RanglistenEintragZeile.Punkte))
            Geaendert?.Invoke(this, EventArgs.Empty);
    }

    // Ziehen mit der Maus ordnet die Liste selbst um (Entfernen + Einfügen) — dann Ränge nachziehen und melden.
    private void EintraegeGeaendert(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(EintraegeUeberschrift));
        if (_eigeneAenderung > 0) return;
        AktualisiereRaenge();
        Geaendert?.Invoke(this, EventArgs.Empty);
    }

    // Rang ergibt sich immer aus der Position in der Liste (1-basiert) — keine manuelle Eingabe, keine
    // Lücken/Duplikate möglich. Zugleich die Abschnittsbänder an die erste Zeile jedes Abschnitts.
    private void AktualisiereRaenge()
    {
        for (int i = 0; i < Eintraege.Count; i++)
        {
            var rang = i + 1;
            Eintraege[i].Rang = rang;
            Eintraege[i].AbschnittBeginn = _abschnitte.FirstOrDefault(a => a.VonPlatz == rang) is { } abschnitt
                ? $"{abschnitt.Name} · Spielklasse {abschnitt.Spielklasse}"
                : null;
        }
        BefehleAktualisieren();
    }

    // ── Markierung ────────────────────────────────────────────────────────────

    /// <summary>Die Ansicht meldet die markierten Zeilen (Auswahl der ListView).</summary>
    public void MarkierungSetzen(IEnumerable<RanglistenEintragZeile> zeilen)
    {
        var menge = zeilen.ToHashSet();
        _markierte = [.. Eintraege.Where(menge.Contains)];
        OnPropertyChanged(nameof(MarkierungText));
        BefehleAktualisieren();
    }

    private HashSet<RanglistenEintragZeile> MarkierteMenge() => [.. _markierte];

    private void Markieren(IReadOnlyList<RanglistenEintragZeile> zeilen) => MarkierungAngefordert?.Invoke(this, zeilen);

    private void BefehleAktualisieren()
    {
        EintraegeHochCommand.NotifyCanExecuteChanged();
        EintraegeRunterCommand.NotifyCanExecuteChanged();
        AufRangVerschiebenCommand.NotifyCanExecuteChanged();
        EintraegeEntfernenCommand.NotifyCanExecuteChanged();
    }

    // ── Umsortieren ───────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannHoch))]
    private void EintraegeHoch() => ReihenfolgeAnwenden(EintragsReihenfolge.Hoch(Eintraege, MarkierteMenge()));

    [RelayCommand(CanExecute = nameof(KannRunter))]
    private void EintraegeRunter() => ReihenfolgeAnwenden(EintragsReihenfolge.Runter(Eintraege, MarkierteMenge()));

    [RelayCommand(CanExecute = nameof(KannAufRang))]
    private void AufRangVerschieben() =>
        ReihenfolgeAnwenden(EintragsReihenfolge.AufRang(Eintraege, MarkierteMenge(), (int)Math.Round(ZielRang)));

    // Überträgt die neue Reihenfolge per Move auf dieselben Zeilen und markiert danach wieder dieselben — die
    // ListView verliert die Auswahl verschobener Zeilen.
    private void ReihenfolgeAnwenden(IReadOnlyList<RanglistenEintragZeile> neu)
    {
        var markierte = _markierte;
        _eigeneAenderung++;
        try
        {
            for (int i = 0; i < neu.Count; i++)
            {
                var alt = Eintraege.IndexOf(neu[i]);
                if (alt != i) Eintraege.Move(alt, i);
            }
        }
        finally
        {
            _eigeneAenderung--;
        }
        AktualisiereRaenge();
        Geaendert?.Invoke(this, EventArgs.Empty);
        Markieren(markierte);
    }

    private bool KannHoch() => _markierte.Count > 0 && !EintragsReihenfolge.Hoch(Eintraege, MarkierteMenge()).SequenceEqual(Eintraege);
    private bool KannRunter() => _markierte.Count > 0 && !EintragsReihenfolge.Runter(Eintraege, MarkierteMenge()).SequenceEqual(Eintraege);
    private bool KannAufRang() => _markierte.Count > 0 && !double.IsNaN(ZielRang);

    // ── Hinzufügen / Entfernen ────────────────────────────────────────────────

    // Hinter dem letzten markierten Eintrag, sonst am Ende; der neue Eintrag ist danach markiert, so lassen sich
    // mehrere nacheinander in Reihenfolge einfügen.
    private void MannschaftHinzufuegen(MannschaftsOption? option)
    {
        if (option is null || _alleMannschaften.FirstOrDefault(m => m.Id == option.Id) is not { } mannschaft) return;

        var zeile = new RanglistenEintragZeile { Mannschaft = mannschaft };
        zeile.PropertyChanged += OnEintragGeaendert;
        var position = EintragsReihenfolge.EinfuegenNach(Eintraege, MarkierteMenge(), zeile).ToList().IndexOf(zeile);

        _eigeneAenderung++;
        try { Eintraege.Insert(position, zeile); }
        finally { _eigeneAenderung--; }

        AktualisiereRaenge();
        HinzufuegbareAktualisieren();
        Hinzufuegen.Anzeigen(null);
        Geaendert?.Invoke(this, EventArgs.Empty);
        Markieren([zeile]);
    }

    [RelayCommand(CanExecute = nameof(KannEntfernen))]
    private void EintraegeEntfernen()
    {
        _eigeneAenderung++;
        try
        {
            foreach (var zeile in _markierte)
            {
                zeile.PropertyChanged -= OnEintragGeaendert;
                Eintraege.Remove(zeile);
            }
        }
        finally
        {
            _eigeneAenderung--;
        }
        _markierte = [];
        OnPropertyChanged(nameof(MarkierungText));
        AktualisiereRaenge();
        HinzufuegbareAktualisieren();
        Geaendert?.Invoke(this, EventArgs.Empty);
    }

    private bool KannEntfernen() => _markierte.Count > 0;

    // ── Suchen (springt zum Treffer, filtert nicht — die Ränge bleiben sichtbar) ─

    partial void OnSuchTextChanged(string value) => TrefferMarkieren(0);

    /// <summary>Enter in der Suche: nächster Treffer hinter dem markierten Eintrag.</summary>
    public void WeiterSuchen()
    {
        var start = _markierte.Count > 0 ? Eintraege.IndexOf(_markierte[0]) + 1 : 0;
        TrefferMarkieren(start);
    }

    private void TrefferMarkieren(int startIndex)
    {
        var text = SuchText.Trim();
        if (text.Length == 0 || Eintraege.Count == 0)
        {
            SucheOhneTreffer = false;
            return;
        }
        var treffer = EintragsReihenfolge.NaechsterTreffer(
            Eintraege, z => MannschaftsSuche.Treffer([z.Mannschaft], text).Count > 0, startIndex);
        SucheOhneTreffer = treffer is null;
        if (treffer is int index) Markieren([Eintraege[index]]);
    }
}
