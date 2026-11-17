using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Ein Knopf der Spielweise: Grundausrichtung oder Flexibilität. <see cref="IstGewaehlt"/> hängt TwoWay am
/// RadioButton; wird er gewählt, meldet die Option das ihrer Auswahl.</summary>
public partial class SpielweiseOption(string anzeige, int wert, string automationId, Action<SpielweiseOption> gewaehlt)
    : ObservableObject
{
    public string Anzeige { get; } = anzeige;
    /// <summary>Enum-Wert: Stufe der Grundausrichtung (−2 … 2) bzw. der Flexibilität (−1 … 1).</summary>
    public int Wert { get; } = wert;
    public string AutomationId { get; } = automationId;

    [ObservableProperty] private bool _istGewaehlt;

    partial void OnIstGewaehltChanged(bool value)
    {
        if (value) gewaehlt(this);
    }
}

/// <summary>
/// Abschnitt „Spielweise“ der Mannschaftspflege (#667): Grundausrichtung und Flexibilität als Knöpfe, dazu der
/// Vorschlag aus den Ratings mit „Übernehmen“. Aus dem <see cref="MannschaftenViewModel"/> herausgelöst (SRP); das
/// reicht die Ratings über <see cref="RatingsAktualisieren"/> herein und liest beim Speichern die Werte.
/// </summary>
public partial class SpielweiseAuswahl : ObservableObject
{
    private bool _ladenAktiv;
    private bool _markiertGerade;
    private int _offensivRating;
    private int _defensivRating;

    public IReadOnlyList<SpielweiseOption> Grundausrichtungen { get; }
    public IReadOnlyList<SpielweiseOption> Flexibilitaeten { get; }

    // Nach außen nur lesbar (Tell, don't ask): geändert wird über Waehlen, damit Knöpfe und Geaendert mitziehen.
    private Grundausrichtung _grundausrichtung;
    private Flexibilitaet _flexibilitaet;

    public Grundausrichtung Grundausrichtung
    {
        get => _grundausrichtung;
        private set
        {
            if (!SetProperty(ref _grundausrichtung, value)) return;
            OnPropertyChanged(nameof(WeichtVomVorschlagAb));
            OnPropertyChanged(nameof(VorschlagText));
        }
    }

    public Flexibilitaet Flexibilitaet
    {
        get => _flexibilitaet;
        private set
        {
            if (SetProperty(ref _flexibilitaet, value)) OnPropertyChanged(nameof(FlexibilitaetErklaerung));
        }
    }

    /// <summary>Vom Nutzer geändert – nicht beim Laden über <see cref="Zeigen"/>.</summary>
    public event Action? Geaendert;

    public SpielweiseAuswahl()
    {
        // Nach Wert sortiert: GetValues ordnet negative Werte nach ihrer Bitdarstellung hinter die positiven.
        Grundausrichtungen = Enum.GetValues<Grundausrichtung>().OrderBy(g => (int)g)
            .Select(g => new SpielweiseOption(Anzeige(g), (int)g, $"Mannschaften.Grundausrichtung.{g}",
                o => OptionGewaehlt((Grundausrichtung)o.Wert, Flexibilitaet)))
            .ToList();
        Flexibilitaeten = Enum.GetValues<Flexibilitaet>().OrderBy(f => (int)f)
            .Select(f => new SpielweiseOption(Anzeige(f), (int)f, $"Mannschaften.Flexibilitaet.{f}",
                o => OptionGewaehlt(Grundausrichtung, (Flexibilitaet)o.Wert)))
            .ToList();
        Markieren();
    }

    // ── Vorschlag aus den Ratings ─────────────────────────────────────────

    private Grundausrichtung Vorschlag => SpielweisenVorschlag.AusRatings(_offensivRating, _defensivRating);

    public bool WeichtVomVorschlagAb => Grundausrichtung != Vorschlag;

    public string VorschlagText => WeichtVomVorschlagAb
        ? $"Vorschlag aus den Ratings: {Anzeige(Vorschlag)}"
        : "Entspricht dem Vorschlag aus den Ratings.";

    public string FlexibilitaetErklaerung => Flexibilitaet switch
    {
        Flexibilitaet.Starr    => "Hält an der Grundausrichtung fest und stellt spät um.",
        Flexibilitaet.Flexibel => "Spielt genau, was die Lage verlangt, und stellt schnell um.",
        _                      => "Stellt nach der Lage um und behält die Grundausrichtung zur Hälfte bei."
    };

    public void RatingsAktualisieren(int offensivRating, int defensivRating)
    {
        _offensivRating = offensivRating;
        _defensivRating = defensivRating;
        OnPropertyChanged(nameof(WeichtVomVorschlagAb));
        OnPropertyChanged(nameof(VorschlagText));
    }

    [RelayCommand]
    private void VorschlagUebernehmen() => Waehlen(Vorschlag, Flexibilitaet);

    // ── Auswahl ───────────────────────────────────────────────────────────

    /// <summary>Zeigt die gespeicherten Werte einer Mannschaft, ohne <see cref="Geaendert"/> auszulösen.</summary>
    public void Zeigen(Grundausrichtung grundausrichtung, Flexibilitaet flexibilitaet)
    {
        _ladenAktiv = true;
        try { Waehlen(grundausrichtung, flexibilitaet); }
        finally { _ladenAktiv = false; }
    }

    /// <summary>Ein Knopf wurde gewählt – per Klick; beim Markieren selbst nicht (sonst liefe Markieren verschachtelt
    /// in sich selbst, Befund aus dem KI-Review).</summary>
    private void OptionGewaehlt(Grundausrichtung grundausrichtung, Flexibilitaet flexibilitaet)
    {
        if (!_markiertGerade) Waehlen(grundausrichtung, flexibilitaet);
    }

    private void Waehlen(Grundausrichtung grundausrichtung, Flexibilitaet flexibilitaet)
    {
        bool geaendert = grundausrichtung != Grundausrichtung || flexibilitaet != Flexibilitaet;
        Grundausrichtung = grundausrichtung;
        Flexibilitaet = flexibilitaet;
        Markieren();
        if (geaendert && !_ladenAktiv) Geaendert?.Invoke();
    }

    /// <summary>Genau die Knöpfe der aktuellen Werte sind gewählt – auch wenn der Wert nicht per Klick kam.</summary>
    private void Markieren()
    {
        _markiertGerade = true;
        try
        {
            foreach (var option in Grundausrichtungen)
                option.IstGewaehlt = option.Wert == (int)Grundausrichtung;
            foreach (var option in Flexibilitaeten)
                option.IstGewaehlt = option.Wert == (int)Flexibilitaet;
        }
        finally { _markiertGerade = false; }
    }

    private static string Anzeige(Grundausrichtung grundausrichtung) => grundausrichtung switch
    {
        Grundausrichtung.SehrDefensiv => "sehr defensiv",
        Grundausrichtung.Defensiv     => "defensiv",
        Grundausrichtung.Offensiv     => "offensiv",
        Grundausrichtung.SehrOffensiv => "sehr offensiv",
        _                             => "ausgeglichen"
    };

    private static string Anzeige(Flexibilitaet flexibilitaet) => flexibilitaet switch
    {
        Flexibilitaet.Starr    => "starr",
        Flexibilitaet.Flexibel => "flexibel",
        _                      => "normal"
    };
}
