using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Helpers;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

public record VerbandOption(string Anzeige, Verband? Wert);
public record LandOption(string Anzeige, Land? Wert);
public record TeamfilterOption(string Anzeige, MannschaftsartFilter Wert);

/// <summary>Auslosungsfilter des Turnierkopfs (#434): Verband, Land und Mannschaftsart, die festlegen, welche
/// Mannschaften für das Turnier in Frage kommen. Verband und Land schränken sich gegenseitig ein. Was aus einer
/// Änderung folgt (ungespeichert, Sichtbarkeit der Quoten, Auswahllisten für Titelverteidiger und Gastgeber),
/// entscheidet <see cref="TurnierkopfViewModel"/> — es hört auf <see cref="Geaendert"/> und liest
/// <see cref="Wert"/>.</summary>
public partial class AuslosungsfilterViewModel : ObservableObject
{
    private readonly VerbandService _verbandService;
    private readonly LandService _landService;
    private readonly ILogger<AuslosungsfilterViewModel> _logger;
    private int _reaktionenAusgesetzt;

    private static readonly VerbandOption _keinVerband = new("— Kein Filter —", null);
    private static readonly LandOption _keinLand = new("— Kein Filter —", null);
    private static readonly TeamfilterOption _alleMannschaften = new("Alle Mannschaften", MannschaftsartFilter.Alle);

    /// <summary>Verband, Land oder Mannschaftsart hat einen neuen Wert — durch den Nutzer oder durch
    /// <see cref="Setzen"/>.</summary>
    public event Action? Geaendert;

    [ObservableProperty] private VerbandOption _verbandFilter = _keinVerband;
    [ObservableProperty] private LandOption _landFilter = _keinLand;
    [ObservableProperty] private TeamfilterOption _teamFilter = _alleMannschaften;

    // Stammdaten, einmal je Seitenaufruf geladen; die Optionslisten sind die feste, sortierte Grundlage der
    // gefilterten Auswahllisten.
    public IReadOnlyList<Verband> Verbaende { get; private set; } = [];
    public IReadOnlyList<Land> Laender { get; private set; } = [];
    private IReadOnlyList<VerbandOption> _verbandOptionen = [];
    private IReadOnlyList<LandOption> _landOptionen = [];

    /// <summary>Der Filter als Wert, FIFA als "weltweit" aufgelöst.</summary>
    public Auslosungsfilter Wert => new(
        IstFifa(VerbandFilter?.Wert) ? null : VerbandFilter?.Wert?.Id,
        LandFilter?.Wert?.Id,
        TeamFilter?.Wert ?? MannschaftsartFilter.Alle);

    // Was gespeichert wird — der gewählte Verband bleibt dabei auch als FIFA erhalten.
    public int? VerbandId => VerbandFilter?.Wert?.Id;
    public int? LandId => LandFilter?.Wert?.Id;
    public MannschaftsartFilter Mannschaftsart => TeamFilter.Wert;

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
    // aktualisieren den Inhalt dieser EINEN Instanz inkrementell (SammlungsAbgleich.TeilfolgeAbgleichen), die
    // ItemsSource-Referenz selbst wird nie ausgetauscht.
    public ObservableCollection<LandOption> LandOptionenGefiltert { get; } = [];
    public ObservableCollection<VerbandOption> VerbandOptionenGefiltert { get; } = [];

    private void LandOptionenGefiltertSynchronisieren() =>
        SammlungsAbgleich.TeilfolgeAbgleichen(LandOptionenGefiltert, LandOptionenSoll());

    private void VerbandOptionenGefiltertSynchronisieren() =>
        SammlungsAbgleich.TeilfolgeAbgleichen(VerbandOptionenGefiltert, VerbandOptionenSoll());

    private IReadOnlyList<LandOption> LandOptionenSoll() => VerbandFilter?.Wert is null || IstFifa(VerbandFilter.Wert)
        ? _landOptionen
        : [_keinLand, .. _landOptionen.Where(o => o.Wert?.VerbandId == VerbandFilter.Wert.Id)];

    private IReadOnlyList<VerbandOption> VerbandOptionenSoll() => LandFilter?.Wert is null
        ? _verbandOptionen
        : [_keinVerband, .. _verbandOptionen.Where(o => o.Wert?.Id == LandFilter.Wert.VerbandId || IstFifa(o.Wert))];

    private static bool IstFifa(Verband? v) => v?.Kurzname == "FIFA";

    /// <summary>Setzt Verband- und Landfilter atomar statt sequenziell. Bei sequenzieller Zuweisung
    /// (erst VerbandFilter, dann LandFilter) sieht der Zwischenzustand kurzzeitig den neuen Verband mit dem
    /// noch alten Land — <see cref="OnVerbandFilterChanged"/> könnte das Land dann fälschlich als
    /// inkompatibel zurücksetzen, bevor die eigentlich korrekte nachfolgende Land-Zuweisung überhaupt
    /// passiert ist (Fund: Verband-/Land-Feld blieb beim Wechsel zwischen Turniervorlagen sporadisch
    /// falsch). Direktzugriff auf die Backing-Fields umgeht die generierten Setter (und damit
    /// On[Verband|Land]FilterChanged) bewusst, damit beide Werte VOR jeder Change-Reaktion feststehen.
    ///
    /// Die Listen werden in zwei Schritten abgeglichen, die Meldung dazwischen: erst ergänzen, was den neuen
    /// Werten fehlt, dann die ComboBoxen umstellen, dann entfernen, was nicht mehr passt. Entfernt der Abgleich
    /// einen Eintrag, den seine ComboBox noch als Auswahl zeigt, schreibt sie <c>null</c> zurück — hier bei
    /// ausgesetzten Reaktionen, also ohne Korrektur: per Log belegt blieb „Land“ nach „Abbrechen“ leer, wenn
    /// zuvor Verband und Land geändert waren (#447).</summary>
    private void AuslosungsfilterSetzen(VerbandOption verband, LandOption land)
    {
        if (VerbandFilter == verband && LandFilter == land) return;

#pragma warning disable MVVMTK0034
        _verbandFilter = verband;
        _landFilter = land;
#pragma warning restore MVVMTK0034

        var landSoll = LandOptionenSoll();
        var verbandSoll = VerbandOptionenSoll();
        SammlungsAbgleich.TeilfolgeErgaenzen(LandOptionenGefiltert, _landOptionen, landSoll);
        SammlungsAbgleich.TeilfolgeErgaenzen(VerbandOptionenGefiltert, _verbandOptionen, verbandSoll);

        OnPropertyChanged(nameof(VerbandFilter));
        OnPropertyChanged(nameof(LandFilter));

        SammlungsAbgleich.TeilfolgeAbgleichen(LandOptionenGefiltert, landSoll);
        SammlungsAbgleich.TeilfolgeAbgleichen(VerbandOptionenGefiltert, verbandSoll);
    }

    partial void OnVerbandFilterChanged(VerbandOption value)
    {
        // WinUI kann ComboBox.SelectedItem transient auf null zurückschreiben (TwoWay-Binding) — Fund aus
        // #297-Test: NullReferenceException beim Setzen des Verbands, obwohl der Property-Typ
        // nicht-nullable ist. Explizit auf "Kein Filter" zurücksetzen statt nur früh zurückzukehren, sonst
        // bleibt das Backing-Field null und spätere Lesezugriffe (Sichtbarkeit der Quoten, Speichern, …)
        // crashen ebenfalls.
        if (_reaktionenAusgesetzt > 0) return;
        if (value is null) { VerbandFilter = _keinVerband; return; }
        if (value.Wert is not null && !IstFifa(value.Wert)
            && LandFilter?.Wert is not null && LandFilter.Wert.VerbandId != value.Wert.Id)
            LandFilter = _keinLand;
        SammlungsAbgleich.Verzoegert(LandOptionenGefiltertSynchronisieren);
    }

    partial void OnLandFilterChanged(LandOption value)
    {
        // Siehe OnVerbandFilterChanged.
        if (_reaktionenAusgesetzt > 0) return;
        if (value is null) { LandFilter = _keinLand; return; }
        if (value.Wert is not null && VerbandFilter?.Wert is not null && !IstFifa(VerbandFilter.Wert)
            && VerbandFilter.Wert.Id != value.Wert.VerbandId)
            VerbandFilter = _keinVerband;
        SammlungsAbgleich.Verzoegert(VerbandOptionenGefiltertSynchronisieren);
    }

    public IReadOnlyList<TeamfilterOption> TeamfilterOptionen =>
        [_alleMannschaften,
         new("Nur Vereine", MannschaftsartFilter.Verein),
         new("Nur Nationalmannschaften", MannschaftsartFilter.Nationalmannschaft)];

    public AuslosungsfilterViewModel(
        VerbandService verbandService, LandService landService, ILogger<AuslosungsfilterViewModel> logger)
    {
        _verbandService = verbandService;
        _landService = landService;
        _logger = logger;
    }

    public async Task StammdatenLadenAsync()
    {
        using (_logger.Vorgang("Turnierkopf: Verbände abfragen"))
            Verbaende = await _verbandService.AlleAbrufenAsync();
        _verbandOptionen = [_keinVerband, .. Verbaende.Select(v => new VerbandOption(v.Name, v))];
        VerbandOptionenGefiltertSynchronisieren();
        using (_logger.Vorgang("Turnierkopf: Länder abfragen"))
            Laender = await _landService.AlleAbrufenAsync();
        _landOptionen = [_keinLand, .. Laender.OrderBy(l => l.Name).Select(l => new LandOption(l.Name, l))];
        LandOptionenGefiltertSynchronisieren();
    }

    /// <summary>Solange das Eltern-ViewModel ein Turnier lädt, bleiben Rückschreibungen der ComboBoxen ohne
    /// Folgen (kein Zurücksetzen des jeweils anderen Filters, kein Listenabgleich) — so lange, wie dort auch
    /// „ungespeichert“ unterdrückt ist. Jeder Aufruf braucht sein <see cref="ReaktionenFortsetzen"/>.</summary>
    public void ReaktionenAussetzen() => _reaktionenAusgesetzt++;

    public void ReaktionenFortsetzen() => _reaktionenAusgesetzt--;

    /// <summary>Befüllt den Filter beim Laden eines Turniers; <c>null</c> steht für „Kein Filter“.</summary>
    public void Setzen(int? verbandId, int? landId, MannschaftsartFilter mannschaftsart)
    {
        AuslosungsfilterSetzen(
            _verbandOptionen.FirstOrDefault(o => o.Wert?.Id == verbandId) ?? _keinVerband,
            _landOptionen.FirstOrDefault(o => o.Wert?.Id == landId) ?? _keinLand);
        TeamFilter = TeamfilterOptionen.FirstOrDefault(o => o.Wert == mannschaftsart) ?? _alleMannschaften;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(VerbandFilter) or nameof(LandFilter) or nameof(TeamFilter))
            Geaendert?.Invoke();
    }
}
