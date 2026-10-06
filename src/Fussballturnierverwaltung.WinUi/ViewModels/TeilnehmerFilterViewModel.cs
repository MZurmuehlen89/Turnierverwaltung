using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Filter über der Liste der verfügbaren Teams einer Auslosung: Freitext und Land (#344).
/// Kind-ViewModel von <see cref="LostopfArbeitsbereichViewModel"/>, das über <see cref="Geaendert"/> erfährt,
/// wann es seine gefilterte Liste neu abgleichen muss.</summary>
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
         || t.Name.Contains(TeilnehmerFilter, StringComparison.OrdinalIgnoreCase))
        && (_landFilter.LandId is null || t.LandId == _landFilter.LandId);

    // ── Länderfilter für verfügbare Teams (Issue #344) ──────────────────────────
    // Stabile Optionsliste + eine kanonische Option je Land: WinUI3-ComboBoxen gleichen SelectedItem per
    // Referenz ab und vertragen weder getauschte ItemsSource-Instanzen noch Replace-Notifications (#335/#347).
    private static readonly TeilnehmerLandOption _alleLaender = new("Alle Länder", null);
    private readonly Dictionary<int, TeilnehmerLandOption> _landOptionenJeLandId = [];

    public ObservableCollection<TeilnehmerLandOption> LandFilterOptionen { get; } = [_alleLaender];

    public bool ZeigeLandFilter => LandFilterOptionen.Count > 1;

    private TeilnehmerLandOption _landFilter = _alleLaender;
    public TeilnehmerLandOption? LandFilter
    {
        get => _landFilter;
        set
        {
            // null schreibt die ComboBox beim Umbau ihrer Optionen zurück — nie ins Modell übernehmen (#347)
            if (value is null || !SetProperty(ref _landFilter, value)) return;
            Geaendert?.Invoke();
        }
    }

    /// <summary>Turnierwechsel: Filter zurücksetzen.</summary>
    public void Zuruecksetzen()
    {
        TeilnehmerFilter = "";
        LandFilter = _alleLaender;
        LandFilterOptionenSynchronisieren();
    }

    /// <summary>Gleicht die Länderoptionen mit den aktuell verfügbaren Teams ab („Alle Länder“ + vorkommende
    /// Länder alphabetisch). Reiner Merge (erst Überzählige entfernen, dann Fehlende einfügen) — die Restliste
    /// ist stets eine Teilfolge der Zielliste, da beide gleich sortiert sind. Kommt das gewählte Land nicht mehr
    /// vor (z.B. letztes Team in einen Topf gezogen), fällt der Filter auf „Alle Länder“ zurück.</summary>
    public void LandFilterOptionenSynchronisieren()
    {
        var soll = _verfuegbareTeilnehmer
            .Where(t => t.LandId.HasValue)
            .GroupBy(t => t.LandId!.Value)
            .Select(g => LandOptionFuer(g.Key, g.First().LandName))
            .OrderBy(o => o.Anzeige)
            .Prepend(_alleLaender)
            .ToList();

        if (!soll.Contains(_landFilter)) LandFilter = _alleLaender;

        for (var i = LandFilterOptionen.Count - 1; i >= 0; i--)
            if (!soll.Contains(LandFilterOptionen[i])) LandFilterOptionen.RemoveAt(i);
        for (var i = 0; i < soll.Count; i++)
            if (i >= LandFilterOptionen.Count || !ReferenceEquals(LandFilterOptionen[i], soll[i]))
                LandFilterOptionen.Insert(i, soll[i]);

        OnPropertyChanged(nameof(ZeigeLandFilter));
    }

    private TeilnehmerLandOption LandOptionFuer(int landId, string? landName)
    {
        if (!_landOptionenJeLandId.TryGetValue(landId, out var option))
            _landOptionenJeLandId[landId] = option = new TeilnehmerLandOption(landName ?? $"Land {landId}", landId);
        return option;
    }
}
