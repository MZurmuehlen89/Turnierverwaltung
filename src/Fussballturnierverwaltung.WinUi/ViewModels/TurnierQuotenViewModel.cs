using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

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

/// <summary>Die geprüften Quoten-Eingaben eines Speichervorgangs: zwischen Prüfen und Ersetzen liegt das
/// Speichern der Basisdaten, ersetzt wird genau das, was geprüft wurde.</summary>
public record TurnierQuotenEingaben(
    IReadOnlyList<VerbandsQuoteZeileEingabe> VerbandsQuoten,
    IReadOnlyList<LandesQuoteZeileEingabe> LandesQuoten,
    int? EingangsKapazitaet);

/// <summary>Quoten-Bereich des Turnierkopfs (#434): Verband- und Landes-Quoten einer gespeicherten
/// Turnierinstanz samt Kopfzeile des aufklappbaren Bereichs. Ob die beiden Quotenarten sichtbar sind, hängt am
/// Formularzustand und am Auslosungsfilter — das teilt <see cref="TurnierkopfViewModel"/> über
/// <see cref="SichtbarkeitSetzen"/> mit.</summary>
public partial class TurnierQuotenViewModel : ObservableObject
{
    private readonly VerbandsQuoteService _verbandsQuoteService;
    private readonly VerbandsQuoteValidator _verbandsQuoteValidator;
    private readonly LandesQuoteService _landesQuoteService;
    private readonly LandesQuoteValidator _landesQuoteValidator;
    private int _populiertGerade;

    /// <summary>Der Nutzer hat eine Quoten-Zeile hinzugefügt, entfernt oder geändert.</summary>
    public event Action? Geaendert;

    // ── Verband-Quoten (Issue #161/#154, hier #297) ──────────────────────────
    [ObservableProperty] private ObservableCollection<VerbandsQuoteZeile> _quotenZeilen = [];
    [ObservableProperty] private IReadOnlyList<Verband> _alleVerbaende = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KapazitaetsHinweisSichtbar))]
    private int? _ersteRundeEingang;

    public bool KapazitaetsHinweisSichtbar => ErsteRundeEingang.HasValue;

    [ObservableProperty] private bool _verbandsQuotenSichtbar;

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
            if (_populiertGerade == 0) Geaendert?.Invoke();
        };
        foreach (var z in QuotenZeilen) z.PropertyChanged += OnQuotenZeileGeaendert;
    }

    private void OnQuotenZeileGeaendert(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(QuotenKopf));
        if (_populiertGerade == 0) Geaendert?.Invoke();
    }

    // ── Landes-Quoten (Issue #189) ────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<LandesQuoteZeile> _landesQuotenZeilen = [];
    [ObservableProperty] private IReadOnlyList<Land> _alleLaender = [];

    [ObservableProperty] private bool _landesQuotenSichtbar;

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
            if (_populiertGerade == 0) Geaendert?.Invoke();
        };
        foreach (var z in LandesQuotenZeilen) z.PropertyChanged += OnLandesQuoteZeileGeaendert;
    }

    private void OnLandesQuoteZeileGeaendert(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(QuotenKopf));
        if (_populiertGerade == 0) Geaendert?.Invoke();
    }

    // ── Aufklappbarer Bereich (Issue #399) ────────────────────────────────────
    [ObservableProperty] private bool _quotenOffen;

    public bool QuotenSichtbar => VerbandsQuotenSichtbar || LandesQuotenSichtbar;

    // Ohne Verband-Quoten rückt die Landes-Quote in die linke Spalte.
    public int LandesQuotenSpalte => VerbandsQuotenSichtbar ? 2 : 0;

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

    public void AufklappzustandSetzen() => QuotenOffen = QuotenZeilen.Count > 0 || LandesQuotenZeilen.Count > 0;

    public TurnierQuotenViewModel(
        VerbandsQuoteService verbandsQuoteService, VerbandsQuoteValidator verbandsQuoteValidator,
        LandesQuoteService landesQuoteService, LandesQuoteValidator landesQuoteValidator)
    {
        _verbandsQuoteService = verbandsQuoteService;
        _verbandsQuoteValidator = verbandsQuoteValidator;
        _landesQuoteService = landesQuoteService;
        _landesQuoteValidator = landesQuoteValidator;
        QuotenZeilenAbonnieren();
        LandesQuotenZeilenAbonnieren();
    }

    public void StammdatenSetzen(IReadOnlyList<Verband> verbaende, IReadOnlyList<Land> laender)
    {
        AlleVerbaende = verbaende;
        AlleLaender = laender;
    }

    public void SichtbarkeitSetzen(bool verbandsQuoten, bool landesQuoten)
    {
        VerbandsQuotenSichtbar = verbandsQuoten;
        LandesQuotenSichtbar = landesQuoten;
    }

    public void Leeren()
    {
        QuotenZeilen = [];
        LandesQuotenZeilen = [];
        ErsteRundeEingang = null;
    }

    public async Task LadenAsync(int turnierInstanzId, int? ersteRundeEingang)
    {
        ErsteRundeEingang = ersteRundeEingang;
        await QuotenLadenAsync(turnierInstanzId);
        await LandesQuotenLadenAsync(turnierInstanzId);
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

    /// <summary>Wandelt die Zeilen in Eingaben um und prüft sie; wirft <see cref="ValidationException"/>, bevor
    /// irgendetwas gespeichert ist.</summary>
    public TurnierQuotenEingaben GepruefteEingaben()
    {
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

        return new(quotenZeilen, landesQuotenZeilen, ErsteRundeEingang);
    }

    public async Task ErsetzenAsync(int turnierInstanzId, TurnierQuotenEingaben eingaben)
    {
        await _verbandsQuoteService.QuotenErsetzenAsync(turnierInstanzId, eingaben.VerbandsQuoten, eingaben.EingangsKapazitaet);
        await _landesQuoteService.QuotenErsetzenAsync(turnierInstanzId, eingaben.LandesQuoten, eingaben.EingangsKapazitaet);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        switch (e.PropertyName)
        {
            case nameof(QuotenZeilen) or nameof(LandesQuotenZeilen):
                OnPropertyChanged(nameof(QuotenKopf));
                break;
            case nameof(VerbandsQuotenSichtbar) or nameof(LandesQuotenSichtbar):
                OnPropertyChanged(nameof(QuotenSichtbar));
                OnPropertyChanged(nameof(LandesQuotenSpalte));
                OnPropertyChanged(nameof(QuotenKopf));
                break;
        }
    }
}
