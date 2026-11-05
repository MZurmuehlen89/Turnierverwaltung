using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Eine bearbeitbare Zeile der Abschnittstabelle (#477). NumberBoxen liefern double, leer = NaN.</summary>
public partial class AbschnittBearbeitungZeile : ObservableObject
{
    public int? Id { get; init; }

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private double _spielklasse = double.NaN;
    [ObservableProperty] private double _bisPlatz = double.NaN;

    // Berechnet aus dem vorigen Abschnitt.
    [ObservableProperty] private int _vonPlatz = 1;

    /// <summary>Prüffehler dieses Abschnitts (#601) – reine Anzeige.</summary>
    [ObservableProperty] private IReadOnlyList<Feldfehler> _meldungen = [];

    public required IRelayCommand EntfernenCommand { get; init; }

    public RanglistenAbschnittEingabe AlsEingabe() =>
        new(Id, Name, GanzzahlOderNull(Spielklasse), GanzzahlOderNull(BisPlatz));

    // Leer zählt als 0 und fällt damit durch die Prüfregeln.
    private static int GanzzahlOderNull(double wert) => double.IsNaN(wert) ? 0 : (int)Math.Round(wert);
}

/// <summary>Aufklappbarer Bereich „Ligapyramide“ des Ranglisten-Editors (#393, #477): Profigrenze, Pokalsieger (#583)
/// und bearbeitbare Abschnitte mit Live-Prüfung. Meldet Änderungen über <see cref="Geaendert"/> (der Editor entscheidet über den
/// Dirty-Zustand) und geänderte Platzbereiche über <see cref="AbschnitteGeaendert"/> (für die Trennbänder der
/// Eintragsliste).</summary>
public partial class LigapyramideViewModel : ObservableObject
{
    private int _laedtGerade;
    private int _eintragsAnzahl;

    public event EventHandler? Geaendert;
    public event EventHandler? AbschnitteGeaendert;

    // Profigrenze (Spielklasse) der Ligapyramide für „Heimrecht für Unterklassige“ (#393). NumberBox liefert double,
    // leer = NaN = keine Grenze (strikt Klasse gegen Klasse).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Kopf))]
    private double _profigrenze = double.NaN;

    public ObservableCollection<AbschnittBearbeitungZeile> Abschnitte { get; } = [];

    /// <summary>Suchbox „Pokalsieger“ über alle Vereine (#583) — ein Verweis auf die Mannschaft, auch aus einer
    /// Spielklasse, die die Rangliste nicht enthält.</summary>
    public MannschaftsfilterViewModel Pokalsieger { get; } = new(mitAlleOption: false);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Kopf))]
    [NotifyCanExecuteChangedFor(nameof(PokalsiegerEntfernenCommand))]
    private int? _pokalsiegerId;

    public LigapyramideViewModel()
    {
        Pokalsieger.WahlAngefordert = option =>
        {
            PokalsiegerSetzen(option);
            return Task.CompletedTask;
        };
    }

    public bool HatAbschnitte => Abschnitte.Count > 0;

    // Zugeklappt bei jedem Ranglistenwechsel, die Eintragsliste ist die Hauptfläche; die Kopfzeile fasst zusammen,
    // was festgelegt ist.
    [ObservableProperty] private bool _offen;

    public AbschnittKopf Kopf => AbschnittKopf.Aus("LIGAPYRAMIDE",
    [
        HatAbschnitte ? $"{Abschnitte.Count} Abschnitte" : null,
        double.IsNaN(Profigrenze) ? null : $"Profigrenze Spielklasse {(int)Math.Round(Profigrenze)}",
        PokalsiegerId is null ? null : $"Pokalsieger {Pokalsieger.Gewaehlt?.Name ?? $"Id {PokalsiegerId}"}"
    ], "keine Abschnitte");

    /// <summary>Fehler, die das Speichern verhindern (Meldungsleiste).</summary>
    public IReadOnlyList<Feldfehler> Fehler { get; private set; } = [];

    /// <summary>Hinweis, wenn der letzte Abschnitt über die Einträge hinausreicht; Speichern bleibt möglich.</summary>
    public string? Hinweis { get; private set; }

    /// <summary>Die Vereine für die Suchbox „Pokalsieger“.</summary>
    public void StammdatenSetzen(IEnumerable<Mannschaft> mannschaften) =>
        Pokalsieger.TeilnehmerSetzen([.. mannschaften.Where(m => m.Mannschaftsart == Mannschaftsart.Verein)]);

    /// <summary>Stand einer Rangliste übernehmen (leer bei neuer bzw. keiner Rangliste), zugeklappt.</summary>
    public void Laden(Rangliste? rangliste)
    {
        _laedtGerade++;
        try
        {
            Profigrenze = rangliste?.ProfigrenzeSpielklasse ?? double.NaN;
            // Die Id gilt auch, wenn die Suchbox die Mannschaft nicht führt — sonst ginge sie beim Speichern verloren.
            Pokalsieger.Anzeigen(rangliste?.PokalsiegerId is int id ? Pokalsieger.OptionFuer(id) : null);
            PokalsiegerId = rangliste?.PokalsiegerId;
            foreach (var alt in Abschnitte) alt.PropertyChanged -= ZeileGeaendert;
            Abschnitte.Clear();
            foreach (var abschnitt in rangliste?.Abschnitte.OrderBy(a => a.BisPlatz) ?? Enumerable.Empty<RanglistenAbschnitt>())
                ZeileAnhaengen(abschnitt.Id, abschnitt.Name, abschnitt.Spielklasse, abschnitt.BisPlatz);
            Offen = false;
            Neuberechnen();
        }
        finally
        {
            _laedtGerade--;
        }
    }

    /// <summary>Die Eintragsliste meldet ihre Länge — für den Überhang-Hinweis und die Vorgabe neuer Abschnitte.</summary>
    public void EintragsAnzahlSetzen(int anzahl)
    {
        _eintragsAnzahl = anzahl;
        HinweisAktualisieren();
    }

    public IReadOnlyList<RanglistenAbschnittEingabe> Eingaben() => [.. Abschnitte.Select(z => z.AlsEingabe())];

    public IReadOnlyList<AbschnittsZeile> Uebersicht() => AbschnittsRegeln.Uebersicht(Eingaben());

    public int? ProfigrenzeOderNull() => double.IsNaN(Profigrenze) ? null : (int)Math.Round(Profigrenze);

    partial void OnProfigrenzeChanged(double value) => AenderungMelden();

    // ── Pokalsieger (#583) ────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(HatPokalsieger))]
    private void PokalsiegerEntfernen() => PokalsiegerSetzen(null);

    private bool HatPokalsieger() => PokalsiegerId is not null;

    private void PokalsiegerSetzen(MannschaftsOption? option)
    {
        Pokalsieger.Anzeigen(option);
        PokalsiegerId = option?.Id;
    }

    partial void OnPokalsiegerIdChanged(int? value) => AenderungMelden();

    // ── Abschnitte bearbeiten ─────────────────────────────────────────────────

    // Beginnt hinter dem letzten Abschnitt, eine Spielklasse tiefer; reicht bis zum Listenende, sonst ein Platz weit.
    [RelayCommand]
    private void AbschnittHinzufuegen()
    {
        var letzter = Abschnitte.Count > 0 ? Abschnitte[^1].AlsEingabe() : null;
        var bisher = letzter?.BisPlatz ?? 0;
        ZeileAnhaengen(null, "", (letzter?.Spielklasse ?? 0) + 1, Math.Max(_eintragsAnzahl, bisher + 1));
        Neuberechnen();
        AenderungMelden();
        AbschnitteGeaendert?.Invoke(this, EventArgs.Empty);
    }

    private void ZeileAnhaengen(int? id, string name, int spielklasse, int bisPlatz)
    {
        AbschnittBearbeitungZeile? zeile = null;
        zeile = new AbschnittBearbeitungZeile
        {
            Id = id,
            Name = name,
            Spielklasse = spielklasse,
            BisPlatz = bisPlatz,
            EntfernenCommand = new RelayCommand(() => AbschnittEntfernen(zeile!))
        };
        zeile.PropertyChanged += ZeileGeaendert;
        Abschnitte.Add(zeile);
    }

    private void AbschnittEntfernen(AbschnittBearbeitungZeile zeile)
    {
        zeile.PropertyChanged -= ZeileGeaendert;
        Abschnitte.Remove(zeile);
        Neuberechnen();
        AenderungMelden();
        AbschnitteGeaendert?.Invoke(this, EventArgs.Empty);
    }

    private void ZeileGeaendert(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AbschnittBearbeitungZeile.VonPlatz) or nameof(AbschnittBearbeitungZeile.Meldungen)) return;
        Neuberechnen();
        AenderungMelden();
        AbschnitteGeaendert?.Invoke(this, EventArgs.Empty);
    }

    // ── Ableitungen ───────────────────────────────────────────────────────────

    private void Neuberechnen()
    {
        var uebersicht = Uebersicht();
        for (int i = 0; i < Abschnitte.Count; i++)
            Abschnitte[i].VonPlatz = uebersicht[i].VonPlatz;

        Fehler = AbschnittsRegeln.Fehler(Eingaben());
        for (int i = 0; i < Abschnitte.Count; i++)
            Abschnitte[i].Meldungen = [.. Fehler.Where(f => f.Zeile == i)];
        OnPropertyChanged(nameof(Fehler));
        OnPropertyChanged(nameof(HatAbschnitte));
        OnPropertyChanged(nameof(Kopf));
        HinweisAktualisieren();
    }

    private void HinweisAktualisieren()
    {
        Hinweis = AbschnittsRegeln.UeberhangHinweis(Eingaben(), _eintragsAnzahl);
        OnPropertyChanged(nameof(Hinweis));
    }

    private void AenderungMelden()
    {
        if (_laedtGerade == 0) Geaendert?.Invoke(this, EventArgs.Empty);
    }
}
