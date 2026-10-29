using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.WinUi.Helpers;
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

    /// <summary>Der Nutzer hat eine Quoten-Zeile hinzugefügt, entfernt oder geändert. Laden und Leeren melden
    /// nichts.</summary>
    public event Action? Geaendert;

    private bool _befuelltGerade;

    // Läuft der Abgleich der Landes-Auswahl, lösen seine eigenen Änderungen keinen weiteren aus.
    private bool _landesAbgleichLaeuft;

    // ── Verband-Quoten (Issue #161/#154, hier #297) ──────────────────────────
    public ObservableCollection<VerbandsQuoteZeile> VerbandsQuotenZeilen { get; } = [];
    public IReadOnlyList<Verband> AlleVerbaende { get; private set => SetProperty(ref field, value); } = [];
    public bool VerbandsQuotenSichtbar { get; private set => SetProperty(ref field, value); }

    [RelayCommand]
    private void VerbandsQuoteHinzufuegen() => VerbandsQuotenZeilen.Add(new VerbandsQuoteZeile());

    [RelayCommand]
    private void VerbandsQuoteEntfernen(VerbandsQuoteZeile zeile) => VerbandsQuotenZeilen.Remove(zeile);

    // ── Landes-Quoten (Issue #189) ────────────────────────────────────────────
    public ObservableCollection<LandesQuoteZeile> LandesQuotenZeilen { get; } = [];
    public IReadOnlyList<Land> AlleLaender { get; private set => SetProperty(ref field, value); } = [];
    public bool LandesQuotenSichtbar { get; private set => SetProperty(ref field, value); }

    [RelayCommand]
    private void LandesQuoteHinzufuegen()
    {
        LandesQuotenZeilen.Add(new LandesQuoteZeile());
        LandesAuswahlAbgleichen(null);
    }

    [RelayCommand]
    private void LandesQuoteEntfernen(LandesQuoteZeile zeile)
    {
        LandesQuotenZeilen.Remove(zeile);
        LandesAuswahlAbgleichen(null);
    }

    // ── Eingangskapazität der ersten Runde ────────────────────────────────────
    // Obergrenze für die Summe je Quotenart; ohne Runden gibt es keine.
    public int? ErsteRundeEingang { get; private set => SetProperty(ref field, value); }

    public bool KapazitaetsHinweisSichtbar => ErsteRundeEingang.HasValue;

    // Freie Plätze (#572): Eingang der ersten Runde ohne die Summe der Quoten; ohne Runden kein Hinweis.
    public string? VerbandsFreiePlaetze => FreiePlaetze(VerbandsQuotenEingaben().Sum(q => q.Anzahl));
    public string? LandesFreiePlaetze => FreiePlaetze(LandesQuotenEingaben().Sum(q => q.Anzahl));

    private string? FreiePlaetze(int summe) => ErsteRundeEingang is { } eingang
        ? (eingang - summe == 1 ? "1 freier Platz" : $"{eingang - summe} freie Plätze")
        : null;

    // ── Aufklappbarer Bereich (Issue #399) ────────────────────────────────────
    [ObservableProperty] private bool _offen;

    public bool Sichtbar => VerbandsQuotenSichtbar || LandesQuotenSichtbar;

    public AbschnittKopf Kopf => AbschnittKopf.Aus("QUOTEN",
    [
        VerbandsQuotenSichtbar ? KopfTeil("Verband", VerbandsQuotenEingaben().Select(q => q.Anzahl).ToList()) : null,
        LandesQuotenSichtbar ? KopfTeil("Land", LandesQuotenEingaben().Select(q => q.Anzahl).ToList()) : null
    ]);

    private static string? KopfTeil(string quotenart, IReadOnlyList<int> anzahlen) =>
        anzahlen.Count > 0 ? $"{quotenart}: {anzahlen.Count} (Summe {anzahlen.Sum()})" : null;

    public TurnierQuotenViewModel(
        VerbandsQuoteService verbandsQuoteService, VerbandsQuoteValidator verbandsQuoteValidator,
        LandesQuoteService landesQuoteService, LandesQuoteValidator landesQuoteValidator)
    {
        _verbandsQuoteService = verbandsQuoteService;
        _verbandsQuoteValidator = verbandsQuoteValidator;
        _landesQuoteService = landesQuoteService;
        _landesQuoteValidator = landesQuoteValidator;
        Beobachten(VerbandsQuotenZeilen);
        Beobachten(LandesQuotenZeilen, OnLandesZeileGeaendert);
    }

    public void StammdatenSetzen(IReadOnlyList<Verband> verbaende, IReadOnlyList<Land> laender)
    {
        AlleVerbaende = verbaende;
        AlleLaender = laender;
        LandesAuswahlAbgleichen(null);
    }

    public void SichtbarkeitSetzen(bool verbandsQuoten, bool landesQuoten)
    {
        VerbandsQuotenSichtbar = verbandsQuoten;
        LandesQuotenSichtbar = landesQuoten;
    }

    public void AufklappzustandSetzen() => Offen = VerbandsQuotenZeilen.Count > 0 || LandesQuotenZeilen.Count > 0;

    public void Leeren()
    {
        Befuellen(VerbandsQuotenZeilen, []);
        Befuellen(LandesQuotenZeilen, []);
        ErsteRundeEingang = null;
    }

    public async Task LadenAsync(int turnierInstanzId, int? ersteRundeEingang)
    {
        ErsteRundeEingang = ersteRundeEingang;
        var verbandsQuoten = await _verbandsQuoteService.AlleNachTurnierInstanzIdAsync(turnierInstanzId);
        Befuellen(VerbandsQuotenZeilen,
            verbandsQuoten.Select(q => new VerbandsQuoteZeile { Verband = q.Verband, Anzahl = q.Anzahl }));
        var landesQuoten = await _landesQuoteService.AlleNachTurnierInstanzIdAsync(turnierInstanzId);
        // Das Land aus AlleLaender (mit Rangliste), damit Spielklassen und Auswahllisten dieselbe Instanz sehen.
        Befuellen(LandesQuotenZeilen, landesQuoten.Select(q =>
        {
            var zeile = new LandesQuoteZeile();
            zeile.Uebernehmen(AlleLaender.FirstOrDefault(l => l.Id == q.LandId) ?? q.Land, q.Spielklasse, q.AbPlatz, q.Anzahl);
            return zeile;
        }));
        LandesAuswahlAbgleichen(null);
    }

    /// <summary>Wandelt die Zeilen in Eingaben um und prüft sie; wirft <see cref="ValidationException"/>, bevor
    /// irgendetwas gespeichert ist.</summary>
    public TurnierQuotenEingaben GepruefteEingaben()
    {
        var eingaben = new TurnierQuotenEingaben(VerbandsQuotenEingaben(), LandesQuotenEingaben(), ErsteRundeEingang);
        ValidationException.WerfenWennUngueltig(
            _verbandsQuoteValidator.Validieren(eingaben.VerbandsQuoten, eingaben.EingangsKapazitaet));
        ValidationException.WerfenWennUngueltig(
            _landesQuoteValidator.Validieren(eingaben.LandesQuoten, AlleLaender, eingaben.EingangsKapazitaet));
        return eingaben;
    }

    public async Task ErsetzenAsync(int turnierInstanzId, TurnierQuotenEingaben eingaben)
    {
        await _verbandsQuoteService.QuotenErsetzenAsync(turnierInstanzId, eingaben.VerbandsQuoten, eingaben.EingangsKapazitaet);
        await _landesQuoteService.QuotenErsetzenAsync(turnierInstanzId, eingaben.LandesQuoten, eingaben.EingangsKapazitaet);
    }

    // Zeilen ohne gewählten Verband bzw. ohne gewähltes Land zählen nicht und werden nicht gespeichert.
    private List<VerbandsQuoteZeileEingabe> VerbandsQuotenEingaben() => VerbandsQuotenZeilen
        .Where(z => z.Verband is not null)
        .Select(z => new VerbandsQuoteZeileEingabe(z.Verband!.Id, z.Anzahl))
        .ToList();

    private List<LandesQuoteZeileEingabe> LandesQuotenEingaben() => AndereEingaben(null);

    private List<LandesQuoteZeileEingabe> AndereEingaben(LandesQuoteZeile? ausser) => LandesQuotenZeilen
        .Where(z => z != ausser)
        .Select(z => z.AlsEingabe())
        .OfType<LandesQuoteZeileEingabe>()
        .ToList();

    // ── Landes-Auswahl (#572) ─────────────────────────────────────────────────

    /// <summary>Land oder Spielklasse einer Zeile geändert: die übrigen Zeilen bieten danach andere Länder bzw.
    /// „ganzes Land“ an. Läuft verzögert, denn die Änderung kommt aus dem SelectionChanged einer ComboBox (#335).</summary>
    private void OnLandesZeileGeaendert(object? sender, PropertyChangedEventArgs e)
    {
        if (_befuelltGerade || _landesAbgleichLaeuft || sender is not LandesQuoteZeile zeile)
            return;
        if (e.PropertyName == nameof(LandesQuoteZeile.Land))
            SammlungsAbgleich.Verzoegert(() => LandesAuswahlAbgleichen(zeile));
        else if (e.PropertyName == nameof(LandesQuoteZeile.Spielklasse))
            SammlungsAbgleich.Verzoegert(() =>
            {
                zeile.StartplatzVorschlagen(AndereEingaben(zeile));
                LandesAuswahlAbgleichen(null);
            });
    }

    /// <summary>Gleicht alle Zeilen an; die Zeile mit neuem Land zuerst, damit die übrigen ihre neue Spielklasse sehen.</summary>
    private void LandesAuswahlAbgleichen(LandesQuoteZeile? landGewechselt)
    {
        _landesAbgleichLaeuft = true;
        try
        {
            landGewechselt?.AuswahlAbgleichen(AlleLaender, AndereEingaben(landGewechselt), landGewechselt: true);
            foreach (var zeile in LandesQuotenZeilen.Where(z => z != landGewechselt))
                zeile.AuswahlAbgleichen(AlleLaender, AndereEingaben(zeile), landGewechselt: false);
        }
        finally
        {
            _landesAbgleichLaeuft = false;
        }
    }

    /// <summary>Änderungen an der Zeilenliste (Zeile dazu oder weg, Wert einer Zeile) frischen die Kopfzeile auf
    /// und werden als Nutzeränderung gemeldet.</summary>
    private void Beobachten<TZeile>(ObservableCollection<TZeile> liste, PropertyChangedEventHandler? zusaetzlich = null)
        where TZeile : INotifyPropertyChanged
    {
        liste.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null)
                foreach (INotifyPropertyChanged zeile in e.NewItems)
                {
                    zeile.PropertyChanged += OnZeileGeaendert;
                    if (zusaetzlich is not null) zeile.PropertyChanged += zusaetzlich;
                }
            ZeilenAenderungMelden();
        };
    }

    /// <summary>Ersetzt den Inhalt einer Zeilenliste beim Laden oder Leeren; die Kopfzeile frischt auf, eine
    /// Nutzeränderung wird nicht gemeldet.</summary>
    private void Befuellen<TZeile>(ObservableCollection<TZeile> liste, IEnumerable<TZeile> zeilen)
    {
        _befuelltGerade = true;
        try
        {
            liste.Clear();
            foreach (var zeile in zeilen) liste.Add(zeile);
        }
        finally
        {
            _befuelltGerade = false;
        }
    }

    private void OnZeileGeaendert(object? sender, PropertyChangedEventArgs e) => ZeilenAenderungMelden();

    private void ZeilenAenderungMelden()
    {
        OnPropertyChanged(nameof(Kopf));
        OnPropertyChanged(nameof(VerbandsFreiePlaetze));
        OnPropertyChanged(nameof(LandesFreiePlaetze));
        if (_befuelltGerade) return;
        Geaendert?.Invoke();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        switch (e.PropertyName)
        {
            case nameof(VerbandsQuotenSichtbar) or nameof(LandesQuotenSichtbar):
                OnPropertyChanged(nameof(Sichtbar));
                OnPropertyChanged(nameof(Kopf));
                break;
            case nameof(ErsteRundeEingang):
                OnPropertyChanged(nameof(KapazitaetsHinweisSichtbar));
                OnPropertyChanged(nameof(VerbandsFreiePlaetze));
                OnPropertyChanged(nameof(LandesFreiePlaetze));
                break;
        }
    }
}
