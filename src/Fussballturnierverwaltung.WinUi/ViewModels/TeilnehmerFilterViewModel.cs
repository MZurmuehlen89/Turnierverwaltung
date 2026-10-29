using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.UI.Dispatching;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Filter über der Liste der verfügbaren Teams einer Auslosung: Freitext, Kontinentalverband (#421) und
/// Land (#344). Welche der beiden Auswahlfilter angeboten werden, folgt aus dem Turnier: der Verband, wenn der
/// Pool Mannschaften mehrerer Verbände enthält (Nationalmannschaften weltweit, Klub-WM), das Land bei
/// Vereinsturnieren über mehrere Länder — beide zusammen bei weltweiten Vereinsturnieren, keiner bei Platzhaltern.
/// Kind-ViewModel von <see cref="LostopfArbeitsbereichViewModel"/>, das über <see cref="Geaendert"/> erfährt,
/// wann es seine gefilterte Liste neu abgleichen muss. Die <see cref="Platzbelegung"/> der Vorbelegung nutzt nur
/// den Freitext: ohne <see cref="OptionenAbgleichen"/> bleiben beide Auswahlfilter leer und lassen alles durch.</summary>
public partial class TeilnehmerFilterViewModel : ObservableObject
{
    private readonly IReadOnlyCollection<TeilnehmerAnzeige> _verfuegbareTeilnehmer;

    public TeilnehmerFilterViewModel(IReadOnlyCollection<TeilnehmerAnzeige> verfuegbareTeilnehmer)
    {
        _verfuegbareTeilnehmer = verfuegbareTeilnehmer;
    }

    public event Action? Geaendert;

    [ObservableProperty] private string _teilnehmerFilter = "";

    partial void OnTeilnehmerFilterChanged(string value) => Geaendert?.Invoke();

    public bool Passt(TeilnehmerAnzeige t) =>
        (string.IsNullOrWhiteSpace(TeilnehmerFilter)
         || t.Name.Contains(TeilnehmerFilter, StringComparison.OrdinalIgnoreCase)
         || t.QualifikantName?.Contains(TeilnehmerFilter, StringComparison.OrdinalIgnoreCase) == true)
        && (_verbandFilter.VerbandId is null || t.VerbandId == _verbandFilter.VerbandId)
        && (_landFilter.LandId is null || t.LandId == _landFilter.LandId);

    // ── Auswahlfilter: Kontinentalverband (#421) und Land (#344) ─────────────────
    // Stabile Optionslisten + eine kanonische Option je Verband/Land: WinUI3-ComboBoxen gleichen SelectedItem per
    // Referenz ab und vertragen weder getauschte ItemsSource-Instanzen noch Replace-Notifications (#335/#347).
    private static readonly TeilnehmerVerbandOption _alleVerbaende = new("Alle Verbände", null);
    private static readonly TeilnehmerLandOption _alleLaender = new("Alle Länder", null);
    private readonly Dictionary<int, TeilnehmerVerbandOption> _verbandOptionenJeVerbandId = [];
    private readonly Dictionary<int, TeilnehmerLandOption> _landOptionenJeLandId = [];
    private bool _landFilterErlaubt = true;

    public ObservableCollection<TeilnehmerVerbandOption> VerbandFilterOptionen { get; } = [_alleVerbaende];
    public ObservableCollection<TeilnehmerLandOption> LandFilterOptionen { get; } = [_alleLaender];

    public bool ZeigeVerbandFilter => VerbandFilterOptionen.Count > 1;

    // Eigener Zustand statt Zählung der Optionen: die Länderliste ist auf den gewählten Verband eingeengt, der
    // Filter soll aber stehen bleiben, auch wenn dieser Verband nur ein Land im Pool hat.
    [ObservableProperty] private bool _zeigeLandFilter;

    private TeilnehmerVerbandOption _verbandFilter = _alleVerbaende;
    public TeilnehmerVerbandOption? VerbandFilter
    {
        get => _verbandFilter;
        set
        {
            // null schreibt die ComboBox beim Umbau ihrer Optionen zurück — nie ins Modell übernehmen (#347)
            if (value is null || !SetProperty(ref _verbandFilter, value)) return;
            LandFilterAnVerbandAnpassen();
            // Die Länderliste erst nach dem laufenden SelectionChanged umbauen: eine ComboBox-ItemsSource aus dem
            // Auswahlwechsel einer anderen ComboBox heraus zu ändern, ließ WinUI abstürzen (#335).
            DispatcherQueue.GetForCurrentThread()?.TryEnqueue(LandFilterOptionenSynchronisieren);
            Geaendert?.Invoke();
        }
    }

    private TeilnehmerLandOption _landFilter = _alleLaender;
    public TeilnehmerLandOption? LandFilter
    {
        get => _landFilter;
        set
        {
            // Siehe VerbandFilter.
            if (value is null || !SetProperty(ref _landFilter, value)) return;
            Geaendert?.Invoke();
        }
    }

    /// <summary>Ein Land aus einem anderen Verband als dem gewählten fällt auf „Alle Länder“ zurück — ohne eigene
    /// Änderungsmeldung, die meldet der Verbandswechsel.</summary>
    private void LandFilterAnVerbandAnpassen()
    {
        if (_verbandFilter.VerbandId is null || _landFilter.LandId is null
            || _landFilter.VerbandId == _verbandFilter.VerbandId) return;
        _landFilter = _alleLaender;
        OnPropertyChanged(nameof(LandFilter));
    }

    /// <summary>Das Land des Länderfilters, <c>null</c> = alle (#573).</summary>
    public int? AktivesLand => _landFilter.LandId;

    /// <summary>Der Verband des Verbandsfilters, <c>null</c> = alle (#573).</summary>
    public int? AktiverVerband => _verbandFilter.VerbandId;

    /// <summary>Klick auf eine Landesquote (#573): filtert auf das Land, ein zweiter Klick hebt den Filter auf. Bietet
    /// der Filter das Land nicht an (alle seine Teams liegen in Töpfen), passiert nichts.</summary>
    public void LandUmschalten(int landId)
    {
        if (_landFilter.LandId == landId)
            LandFilter = _alleLaender;
        else if (LandFilterOptionen.FirstOrDefault(o => o.LandId == landId) is { } option)
            LandFilter = option;
    }

    /// <summary>Klick auf eine Verbandsquote (#573), wie <see cref="LandUmschalten"/>.</summary>
    public void VerbandUmschalten(int verbandId)
    {
        if (_verbandFilter.VerbandId == verbandId)
            VerbandFilter = _alleVerbaende;
        else if (VerbandFilterOptionen.FirstOrDefault(o => o.VerbandId == verbandId) is { } option)
            VerbandFilter = option;
    }

    /// <summary>Turnierwechsel: Filter zurücksetzen.</summary>
    public void Zuruecksetzen()
    {
        TeilnehmerFilter = "";
        VerbandFilter = _alleVerbaende;
        LandFilter = _alleLaender;
        VerbandFilterOptionenSynchronisieren();
        LandFilterOptionenSynchronisieren();
    }

    /// <summary>Zieht die Optionen beider Auswahlfilter den aktuell verfügbaren Teams nach.
    /// <paramref name="mannschaftsart"/> ist die Vorgabe aus dem Turnierkopf: bei reinen
    /// Nationalmannschaftsturnieren stellt jedes Land genau eine Mannschaft, der Länderfilter entfällt.</summary>
    public void OptionenAbgleichen(MannschaftsartFilter mannschaftsart)
    {
        _landFilterErlaubt = mannschaftsart != MannschaftsartFilter.Nationalmannschaft;
        VerbandFilterOptionenSynchronisieren();
        LandFilterOptionenSynchronisieren();
    }

    /// <summary>„Alle Verbände“ + die im Pool vorkommenden Verbände alphabetisch — nur wenn es mehr als einer
    /// ist, sonst gäbe es nichts einzugrenzen. Kommt der gewählte Verband nicht mehr vor, fällt der Filter auf
    /// „Alle Verbände“ zurück.</summary>
    private void VerbandFilterOptionenSynchronisieren()
    {
        var verbaende = _verfuegbareTeilnehmer
            .Where(t => t.VerbandId.HasValue)
            .GroupBy(t => t.VerbandId!.Value)
            .Select(g => VerbandOptionFuer(g.Key, g.First().VerbandName))
            .OrderBy(o => o.Anzeige)
            .ToList();
        List<TeilnehmerVerbandOption> soll = verbaende.Count > 1 ? [_alleVerbaende, .. verbaende] : [_alleVerbaende];

        if (!soll.Contains(_verbandFilter)) VerbandFilter = _alleVerbaende;
        OptionenAngleichen(VerbandFilterOptionen, soll);
        OnPropertyChanged(nameof(ZeigeVerbandFilter));
    }

    /// <summary>„Alle Länder“ + die im Pool vorkommenden Länder des gewählten Verbands alphabetisch — nur bei
    /// mehr als einem Land im gesamten Pool. Kommt das gewählte Land nicht mehr vor (z.B. letztes Team in einen
    /// Topf gezogen), fällt der Filter auf „Alle Länder“ zurück.</summary>
    private void LandFilterOptionenSynchronisieren()
    {
        var laender = _verfuegbareTeilnehmer
            .Where(t => t.LandId.HasValue)
            .GroupBy(t => t.LandId!.Value)
            .Select(g => LandOptionFuer(g.First()))
            .OrderBy(o => o.Anzeige)
            .ToList();
        var zeigen = _landFilterErlaubt && laender.Count > 1;
        List<TeilnehmerLandOption> soll = zeigen
            ? [_alleLaender, .. laender.Where(o => _verbandFilter.VerbandId is null || o.VerbandId == _verbandFilter.VerbandId)]
            : [_alleLaender];

        if (!soll.Contains(_landFilter)) LandFilter = _alleLaender;
        OptionenAngleichen(LandFilterOptionen, soll);
        ZeigeLandFilter = zeigen;
    }

    /// <summary>Reiner Merge: erst Überzählige entfernen, dann Fehlende einfügen — kein vorhandenes Element wird
    /// ersetzt oder verschoben. Setzt voraus, dass der Rest eine Teilfolge von <paramref name="soll"/> ist; das
    /// gilt hier, weil beide Listen alphabetisch sortiert sind.</summary>
    private static void OptionenAngleichen<T>(ObservableCollection<T> optionen, List<T> soll) where T : class
    {
        for (var i = optionen.Count - 1; i >= 0; i--)
            if (!soll.Contains(optionen[i])) optionen.RemoveAt(i);
        for (var i = 0; i < soll.Count; i++)
            if (i >= optionen.Count || !ReferenceEquals(optionen[i], soll[i]))
                optionen.Insert(i, soll[i]);
    }

    private TeilnehmerVerbandOption VerbandOptionFuer(int verbandId, string? verbandName)
    {
        if (!_verbandOptionenJeVerbandId.TryGetValue(verbandId, out var option))
            _verbandOptionenJeVerbandId[verbandId] = option =
                new TeilnehmerVerbandOption(verbandName ?? $"Verband {verbandId}", verbandId);
        return option;
    }

    private TeilnehmerLandOption LandOptionFuer(TeilnehmerAnzeige teilnehmer)
    {
        var landId = teilnehmer.LandId!.Value;
        if (!_landOptionenJeLandId.TryGetValue(landId, out var option))
            _landOptionenJeLandId[landId] = option =
                new TeilnehmerLandOption(teilnehmer.LandName ?? $"Land {landId}", landId, teilnehmer.VerbandId);
        return option;
    }
}
