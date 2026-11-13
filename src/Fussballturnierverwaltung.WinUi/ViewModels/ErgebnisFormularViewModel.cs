using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Spiele.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using System.ComponentModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Ergebnis-Leiste des Spielplans (#402, aus <see cref="SpielplanViewModel"/> herausgelöst; seit #391 eine
/// Leiste unter der Spielliste): die Eingabefelder eines Spiels in der Reihenfolge des Spielverlaufs, welche
/// Abschnitte (Verlängerung, Elfmeterschießen, Losentscheid) dazu sichtbar sind, und die daraus entstehende Eingabe.
/// Kommt ohne Dienste aus — Laden und Speichern bleiben beim Spielplan.</summary>
public partial class ErgebnisFormularViewModel : ObservableObject
{
    // Felder, deren Änderung die Entscheidung und die sichtbaren Abschnitte neu ableitet.
    private static readonly HashSet<string> _entscheidungsFelder =
    [
        nameof(HeimToreRegulaer), nameof(AuswaertsToreRegulaer),
        nameof(HeimToreVerlaengerung), nameof(AuswaertsToreVerlaengerung),
        nameof(SilverGoalNachErsterHaelfte), nameof(LosGewinnerHeim),
    ];

    private static readonly HashSet<string> _formFelder =
    [
        .. _entscheidungsFelder,
        nameof(HeimElfmetertore), nameof(AuswaertsElfmetertore),
    ];

    private int _populiertGerade;
    private IReadOnlyList<VerfahrenArt> _erlaubteVerfahren = [];
    private ErgebnisRahmen _rahmen = ErgebnisRahmen.Leer;
    private EntscheidungArt _entscheidungArt = EntscheidungArt.RegulaereSpielzeit;
    // #391: Der Halbzeitstand hat kein Eingabefeld mehr; der gespeicherte wird beim Speichern fortgeschrieben.
    private SpielErgebnis? _gespeichert;
    private bool _isDirty;

    /// <summary>Spiel und Runde, für die das Formular zuletzt befüllt wurde.</summary>
    public int? SpielId { get; private set; }
    public int? RundeId { get; private set; }

    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    [ObservableProperty] private string _heimName = "";
    [ObservableProperty] private string _auswaertsName = "";
    [ObservableProperty] private string _spielInfo = "";
    [ObservableProperty] private bool _hatErgebnis;
    [ObservableProperty] private bool _istPlatzhalterSpiel;

    // Die Felder "nach Verlängerung" enthalten den Spielstand inklusive regulärer Spielzeit.
    [ObservableProperty] private string _heimToreRegulaer = "0";
    [ObservableProperty] private string _auswaertsToreRegulaer = "0";
    [ObservableProperty] private string _heimToreVerlaengerung = "";
    [ObservableProperty] private string _auswaertsToreVerlaengerung = "";
    [ObservableProperty] private string _heimElfmetertore = "";
    [ObservableProperty] private string _auswaertsElfmetertore = "";
    [ObservableProperty] private bool _silverGoalNachErsterHaelfte;
    [ObservableProperty] private bool? _losGewinnerHeim;

    public bool VerlaengerungAktiv => ErgebnisEntscheidung.VerlaengerungAktiv(_rahmen, Tore);
    public bool ElfmeterAktiv => ErgebnisEntscheidung.ElfmeterAktiv(_rahmen, Tore);
    public bool SilverGoalWaehlbar => ErgebnisEntscheidung.SilverGoalWaehlbar(_rahmen, Tore);
    public bool LosAktiv => ErgebnisEntscheidung.LosArt(_rahmen, Tore) is not null;

    public string LosUeberschrift => ErgebnisEntscheidung.LosArt(_rahmen, Tore) == EntscheidungArt.Muenzwurf
        ? "MÜNZWURF FÜR"
        : "LOS FIEL AUF";

    /// <summary>Was an der eingegebenen Verlängerung nicht stimmen kann (#391), sonst <c>null</c>.</summary>
    public string? EingabeFehler => ErgebnisEntscheidung.VerlaengerungFehler(_rahmen, Tore);

    /// <summary>Speichern geht nur mit stimmiger Eingabe und — falls fällig — gewähltem Losgewinner.</summary>
    public bool KannSpeichern =>
        !IstPlatzhalterSpiel && EingabeFehler is null && (!LosAktiv || LosGewinnerHeim is not null);

    public bool ZeigtEingabe => !IstPlatzhalterSpiel;
    public bool ZeigtErgebnisHinweis => HatErgebnis && !IsDirty;

    // Die beiden Knöpfe der Los-Zeile schreiben in denselben Wert; ein abgewählter Knopf ändert nichts.
    public bool LosFuerHeim
    {
        get => LosGewinnerHeim == true;
        set { if (value) LosGewinnerHeim = true; }
    }

    public bool LosFuerAuswaerts
    {
        get => LosGewinnerHeim == false;
        set { if (value) LosGewinnerHeim = false; }
    }

    private TorEingabe Tore => new(
        ParseIntNullable(HeimToreRegulaer), ParseIntNullable(AuswaertsToreRegulaer),
        ParseIntNullable(HeimToreVerlaengerung), ParseIntNullable(AuswaertsToreVerlaengerung));

    /// <summary>Entscheidungsverfahren des Regelwerks der Runde, in dessen Reihenfolge.</summary>
    public void ErlaubteVerfahrenSetzen(IEnumerable<VerfahrenArt> verfahren)
    {
        _erlaubteVerfahren = verfahren.ToList();
        _rahmen = _rahmen with { ErlaubteVerfahren = _erlaubteVerfahren };
    }

    /// <summary>Zeigt <paramref name="spiel"/> mit seinem Ergebnis, sofern vorhanden.
    /// <paramref name="hinspielTore"/> (aus Sicht des Hinspiels) nur im K.-o.-Rückspiel mit eingetragenem
    /// Hinspiel.</summary>
    public void AusFuellenMit(
        Spiel spiel, TurnierRunde? runde, string rundenHinweis, (int Heim, int Auswaerts)? hinspielTore)
    {
        SpielId = spiel.Id;
        RundeId = runde?.Id;
        _gespeichert = spiel.Ergebnis;
        _rahmen = new ErgebnisRahmen(
            IstKoRunde: runde is not null && HinRueckspielWertung.IstKoRunde(runde),
            IstKoHinspiel: runde is not null && HinRueckspielWertung.IstKoHinspiel(spiel, runde),
            hinspielTore,
            AuswaertstorRegelAktiv: runde?.AuswaertstorRegelAktiv == true,
            _erlaubteVerfahren);

        _populiertGerade++;
        try
        {
            HeimName = spiel.HeimMannschaft?.Name ?? spiel.HeimPlatzhalterName ?? "";
            AuswaertsName = spiel.AuswaertsMannschaft?.Name ?? spiel.AuswaertsPlatzhalterName ?? "";
            SpielInfo = (spiel.Spieltag.HasValue
                ? $"Spieltag {spiel.Spieltag} · Spiel #{spiel.Spielnummer}"
                : $"Spiel #{spiel.Spielnummer}") + rundenHinweis;
            HatErgebnis = spiel.Ergebnis is not null;
            IstPlatzhalterSpiel = spiel.HeimMannschaftId is null || spiel.AuswaertsMannschaftId is null;
            ToreUebernehmen(spiel.Ergebnis, spiel.HeimMannschaftId);
        }
        finally
        {
            _populiertGerade--;
        }

        IsDirty = false;
        AbschnitteMelden();
    }

    /// <summary>Die Eingaben gelten als gespeichert bzw. verworfen.</summary>
    public void AenderungenVergessen() => IsDirty = false;

    public ErgebnisErfassenEingabe AlsEingabe()
    {
        int heimRegulaer = ParseInt(HeimToreRegulaer);
        int auswaertsRegulaer = ParseInt(AuswaertsToreRegulaer);
        var (halbzeitHeim, halbzeitAuswaerts) =
            ErgebnisEntscheidung.HalbzeitstandBehalten(_gespeichert, heimRegulaer, auswaertsRegulaer);
        // Nur sichtbare Abschnitte zählen — ein ausgeblendetes Feld behält sonst einen veralteten Wert.
        bool verlaengerung = VerlaengerungAktiv;
        bool elfmeter = ElfmeterAktiv;

        return new(
            heimRegulaer,
            auswaertsRegulaer,
            _entscheidungArt,
            verlaengerung ? ParseIntNullable(HeimToreVerlaengerung) - heimRegulaer : null,
            verlaengerung ? ParseIntNullable(AuswaertsToreVerlaengerung) - auswaertsRegulaer : null,
            elfmeter ? ParseIntNullable(HeimElfmetertore) : null,
            elfmeter ? ParseIntNullable(AuswaertsElfmetertore) : null,
            halbzeitHeim,
            halbzeitAuswaerts,
            LosAktiv ? LosGewinnerHeim : null);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(IsDirty)) OnPropertyChanged(nameof(ZeigtErgebnisHinweis));
        if (_populiertGerade > 0 || e.PropertyName is null) return;

        if (_entscheidungsFelder.Contains(e.PropertyName))
        {
            _entscheidungArt = ErgebnisEntscheidung.Art(
                _rahmen, Tore, SilverGoalNachErsterHaelfte && SilverGoalWaehlbar);
            AbschnitteMelden();
        }

        if (_formFelder.Contains(e.PropertyName))
            IsDirty = true;
    }

    private void ToreUebernehmen(SpielErgebnis? e, int? heimId)
    {
        _entscheidungArt = e?.EntscheidungArt ?? EntscheidungArt.RegulaereSpielzeit;
        HeimToreRegulaer = e?.HeimToreRegulaer.ToString() ?? "";
        AuswaertsToreRegulaer = e?.AuswaertsToreRegulaer.ToString() ?? "";
        HeimToreVerlaengerung = (e?.HeimToreRegulaer + e?.HeimToreVerlaengerung)?.ToString() ?? "";
        AuswaertsToreVerlaengerung = (e?.AuswaertsToreRegulaer + e?.AuswaertsToreVerlaengerung)?.ToString() ?? "";
        HeimElfmetertore = e?.HeimElfmetertore?.ToString() ?? "";
        AuswaertsElfmetertore = e?.AuswaertsElfmetertore?.ToString() ?? "";
        SilverGoalNachErsterHaelfte = e?.EntscheidungArt == EntscheidungArt.SilverGoal;
        LosGewinnerHeim = e?.EntscheidungArt is EntscheidungArt.Losentscheid or EntscheidungArt.Muenzwurf
            ? e.SiegerMannschaftId is { } sieger ? sieger == heimId : null
            : null;
    }

    private void AbschnitteMelden()
    {
        OnPropertyChanged(nameof(VerlaengerungAktiv));
        OnPropertyChanged(nameof(ElfmeterAktiv));
        OnPropertyChanged(nameof(SilverGoalWaehlbar));
        OnPropertyChanged(nameof(LosAktiv));
        OnPropertyChanged(nameof(LosUeberschrift));
        OnPropertyChanged(nameof(LosFuerHeim));
        OnPropertyChanged(nameof(LosFuerAuswaerts));
        OnPropertyChanged(nameof(EingabeFehler));
        OnPropertyChanged(nameof(KannSpeichern));
        OnPropertyChanged(nameof(ZeigtEingabe));
    }

    private static int ParseInt(string s) => int.TryParse(s, out var v) ? v : 0;
    private static int? ParseIntNullable(string s) => string.IsNullOrWhiteSpace(s) ? null : int.TryParse(s, out var v) ? v : null;
}
