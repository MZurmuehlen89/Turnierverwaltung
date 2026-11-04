using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.WinUi.Helpers;
using Microsoft.UI.Dispatching;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Auswahl der Spielklasse einer Landes-Quote (#572); <see cref="Nummer"/> null = ganzes Land, mit
/// <see cref="Pokalsieger"/> das Pokalsieger-Kontingent (#583).</summary>
public sealed record SpielklassenOption(string Anzeige, int? Nummer, bool Pokalsieger = false);

/// <summary>Eine Landes-Quoten-Zeile im Turnierkopf (Issue #189, Kontingent seit #572) — lokaler UI-Zustand
/// zwischen Laden und Speichern. Welche Länder und ob „ganzes Land“ angeboten werden, hängt an den übrigen Zeilen;
/// das gleicht <see cref="TurnierQuotenViewModel"/> über <see cref="AuswahlAbgleichen"/> ab.</summary>
public partial class LandesQuoteZeile : ObservableObject
{
    public static readonly SpielklassenOption GanzesLand = new("— ganzes Land —", null);
    public static readonly SpielklassenOption PokalsiegerOption = new("Pokalsieger", null, Pokalsieger: true);

    // Kanonische Optionen des gewählten Landes: die ComboBox gleicht SelectedItem per Referenz ab (#347).
    private IReadOnlyList<SpielklassenOption> _klassenDesLandes = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpielklasseWaehlbar), nameof(Ergibt))]
    private Land? _land;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpielklasseAuswahl), nameof(AbPlatzWaehlbar), nameof(AbPlatzHinweis),
                              nameof(AnzahlWaehlbar), nameof(Ergibt))]
    private SpielklassenOption _spielklasse = GanzesLand;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Ergibt))]
    private int _abPlatz = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Ergibt))]
    private int _anzahl = 1;

    // Stabile Instanzen, abgeglichen per SammlungsAbgleich — nie die ItemsSource tauschen (#335).
    public ObservableCollection<Land> LandOptionen { get; } = [];
    public ObservableCollection<SpielklassenOption> SpielklassenOptionen { get; } = [GanzesLand];

    /// <summary>Bindung der Spielklassen-ComboBox: ein <c>null</c> der ComboBox (ItemsSource-Wechsel, #447) wird
    /// verworfen und die Auswahl auf dem nächsten Tick neu gemeldet.</summary>
    public SpielklassenOption? SpielklasseAuswahl
    {
        get => Spielklasse;
        set
        {
            if (value is not null) Spielklasse = value;
            else DispatcherQueue.GetForCurrentThread()?.TryEnqueue(
                DispatcherQueuePriority.Low, () => OnPropertyChanged(nameof(SpielklasseAuswahl)));
        }
    }

    /// <summary>Prüffehler dieser Zeile für die Feldmarkierung (#595); setzt <see cref="TurnierQuotenViewModel"/>.</summary>
    public IReadOnlyList<Feldfehler> Meldungen { get; private set => SetProperty(ref field, value); } = [];

    // Unverändert nicht neu setzen: die Prüfung läuft bei jedem Tastendruck im Formular.
    public void MeldungenSetzen(IReadOnlyList<Feldfehler> meldungen)
    {
        if (!meldungen.SequenceEqual(Meldungen)) Meldungen = meldungen;
    }

    public bool SpielklasseWaehlbar => Land is not null && LandesQuotenAuswahl.HatPyramide(Land);
    public bool AbPlatzWaehlbar => Spielklasse.Nummer is not null || Spielklasse.Pokalsieger;

    // Der Pokalsieger hat genau einen Platz (#583).
    public bool AnzahlWaehlbar => !Spielklasse.Pokalsieger;

    public string AbPlatzHinweis => Spielklasse.Pokalsieger
        ? "Nachrücken ab Platz der obersten Spielklasse: Plätze davor gelten als schon für einen höheren Wettbewerb vergeben"
        : "ab Platz der Spielklasse";

    /// <summary>Spalte „ergibt“: der Platzbereich des Kontingents, beim Pokalsieger er selbst und der Nachrückbereich.</summary>
    public string Ergibt
    {
        get
        {
            if (Land?.Rangliste is not { } rangliste) return "—";
            if (Spielklasse.Pokalsieger) return PokalsiegerErgibt(rangliste);
            return Spielklasse.Nummer is int nummer
                ? Kontingente.Bereich(rangliste, nummer, AbPlatz, Anzahl)?.Anzeige ?? "passt nicht"
                : "—";
        }
    }

    private string PokalsiegerErgibt(Domain.Entitaeten.Ranglisten.Rangliste rangliste) =>
        Kontingente.Nachrueckbereich(rangliste, AbPlatz) is { } bereich
            ? $"{rangliste.Pokalsieger?.Name ?? "kein Pokalsieger hinterlegt"}, sonst {bereich.Spielklasse.Name} ab Platz {AbPlatz}"
            : "passt nicht";

    /// <summary>Die Zeile als Eingabe; <c>null</c> ohne gewähltes Land (zählt nicht und wird nicht gespeichert).</summary>
    public LandesQuoteZeileEingabe? AlsEingabe() =>
        Land is null ? null : new LandesQuoteZeileEingabe(Land.Id, Anzahl, Spielklasse.Nummer, AbPlatz, Spielklasse.Pokalsieger);

    /// <summary>Gespeicherte Quote übernehmen (Laden): Land, Spielklasse und Startplatz ohne Vorschläge.</summary>
    public void Uebernehmen(Land land, int? spielklasse, int? abPlatz, int anzahl, bool pokalsieger = false)
    {
        Land = land;
        _klassenDesLandes = KlassenDes(land);
        Spielklasse = _klassenDesLandes.FirstOrDefault(k => k.Nummer == spielklasse && k.Pokalsieger == pokalsieger) ?? GanzesLand;
        AbPlatz = abPlatz ?? 1;
        Anzahl = anzahl;
    }

    /// <summary>Gleicht die angebotenen Länder und Spielklassen an die <paramref name="andereZeilen"/> an.
    /// <paramref name="landGewechselt"/>: die Spielklasse folgt dem neuen Land (ganzes Land, wenn möglich, sonst
    /// die höchste Klasse ab dem nächsten freien Platz). Der neue Wert wird gesetzt, bevor der alte aus der Liste
    /// fällt (#447).</summary>
    public void AuswahlAbgleichen(
        IReadOnlyList<Land> alleLaender, IReadOnlyList<LandesQuoteZeileEingabe> andereZeilen, bool landGewechselt)
    {
        SammlungsAbgleich.TeilfolgeAbgleichen(LandOptionen,
            alleLaender.Where(l => l == Land || LandesQuotenAuswahl.LandWaehlbar(l, andereZeilen)).ToList());

        if (landGewechselt)
            _klassenDesLandes = Land is null ? [] : KlassenDes(Land);
        bool ganzesLand = Land is null || LandesQuotenAuswahl.GanzesLandWaehlbar(Land.Id, andereZeilen);
        bool pokalsieger = Land is not null && LandesQuotenAuswahl.PokalsiegerWaehlbar(Land.Id, andereZeilen);
        List<SpielklassenOption> alle = [GanzesLand, .. _klassenDesLandes];
        var soll = alle
            .Where(o => (o != GanzesLand || ganzesLand) && (o != PokalsiegerOption || pokalsieger))
            .ToList();

        SammlungsAbgleich.TeilfolgeErgaenzen(SpielklassenOptionen, alle, soll);
        if (landGewechselt || !soll.Contains(Spielklasse))
            SpielklasseVorschlagen(soll.FirstOrDefault() ?? GanzesLand, andereZeilen);
        SammlungsAbgleich.TeilfolgeAbgleichen(SpielklassenOptionen, soll);
    }

    /// <summary>Neue Spielklasse von Hand: Startplatz hinter den Kontingenten der übrigen Zeilen; beim Pokalsieger
    /// „Nachrücken ab“ hinter denen der obersten Spielklasse (#583).</summary>
    public void StartplatzVorschlagen(IReadOnlyList<LandesQuoteZeileEingabe> andereZeilen)
    {
        if (Land is null) return;
        if (Spielklasse.Pokalsieger)
        {
            Anzahl = 1;
            if (_klassenDesLandes.FirstOrDefault(k => k.Nummer is not null) is { Nummer: int oberste })
                AbPlatz = LandesQuotenAuswahl.NaechsterFreierPlatz(Land.Id, oberste, andereZeilen);
        }
        else if (Spielklasse.Nummer is { } nummer)
            AbPlatz = LandesQuotenAuswahl.NaechsterFreierPlatz(Land.Id, nummer, andereZeilen);
    }

    private void SpielklasseVorschlagen(SpielklassenOption klasse, IReadOnlyList<LandesQuoteZeileEingabe> andereZeilen)
    {
        Spielklasse = klasse;
        AbPlatz = 1;
        StartplatzVorschlagen(andereZeilen);
    }

    // Mit Pyramide zuletzt das Pokalsieger-Kontingent (#583).
    private static IReadOnlyList<SpielklassenOption> KlassenDes(Land land)
    {
        if (land.Rangliste is not { } rangliste || !LandesQuotenAuswahl.HatPyramide(land))
            return [];
        return
        [
            .. Kontingente.Spielklassen(rangliste).Select(k => new SpielklassenOption($"{k.Nummer} · {k.Name}", k.Nummer)),
            PokalsiegerOption,
        ];
    }
}
