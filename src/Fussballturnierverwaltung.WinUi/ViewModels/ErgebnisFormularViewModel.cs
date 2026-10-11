using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Spiele.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using System.ComponentModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Ergebnis-Formular des Spielplans (#402, aus <see cref="SpielplanViewModel"/> herausgelöst): die
/// Eingabefelder eines Spiels, welche Abschnitte (Verlängerung, Elfmeterschießen) dazu sichtbar sind, und die
/// daraus entstehende Eingabe. Kommt ohne Dienste aus — Laden und Speichern bleiben beim Spielplan.</summary>
public partial class ErgebnisFormularViewModel : ObservableObject
{
    private static readonly HashSet<string> _formFelder =
    [
        nameof(HeimToreRegulaer), nameof(AuswaertsToreRegulaer),
        nameof(HeimToreVerlaengerung), nameof(AuswaertsToreVerlaengerung),
        nameof(HeimElfmetertore), nameof(AuswaertsElfmetertore),
        nameof(HeimHalbzeitTore), nameof(AuswaertsHalbzeitTore)
    ];

    private static readonly HashSet<string> _scoreFelder =
    [
        nameof(HeimToreRegulaer), nameof(AuswaertsToreRegulaer),
        nameof(HeimToreVerlaengerung), nameof(AuswaertsToreVerlaengerung),
    ];

    private int _populiertGerade;
    private IReadOnlyList<VerfahrenArt> _erlaubteVerfahren = [];
    private ErgebnisRahmen _rahmen = ErgebnisRahmen.Leer;
    private EntscheidungArt _entscheidungArt = EntscheidungArt.RegulaereSpielzeit;
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

    // Die Felder "nach Verlängerung" enthalten den Spielstand inklusive regulärer Spielzeit.
    [ObservableProperty] private string _heimToreRegulaer = "0";
    [ObservableProperty] private string _auswaertsToreRegulaer = "0";
    [ObservableProperty] private string _heimToreVerlaengerung = "";
    [ObservableProperty] private string _auswaertsToreVerlaengerung = "";
    [ObservableProperty] private string _heimElfmetertore = "";
    [ObservableProperty] private string _auswaertsElfmetertore = "";
    [ObservableProperty] private string _heimHalbzeitTore = "";
    [ObservableProperty] private string _auswaertsHalbzeitTore = "";

    public bool VerlaengerungAktiv => ErgebnisEntscheidung.VerlaengerungAktiv(_rahmen, Tore);
    public bool ElfmeterAktiv => ErgebnisEntscheidung.ElfmeterAktiv(_rahmen, Tore);

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
            ToreUebernehmen(spiel.Ergebnis);
        }
        finally
        {
            _populiertGerade--;
        }

        AbschnitteMelden();
        IsDirty = false;
    }

    /// <summary>Die Eingaben gelten als gespeichert bzw. verworfen.</summary>
    public void AenderungenVergessen() => IsDirty = false;

    public ErgebnisErfassenEingabe AlsEingabe()
    {
        int heimRegulaer = ParseInt(HeimToreRegulaer);
        int auswaertsRegulaer = ParseInt(AuswaertsToreRegulaer);

        return new(
            heimRegulaer,
            auswaertsRegulaer,
            _entscheidungArt,
            ParseIntNullable(HeimToreVerlaengerung) - heimRegulaer,
            ParseIntNullable(AuswaertsToreVerlaengerung) - auswaertsRegulaer,
            ParseIntNullable(HeimElfmetertore),
            ParseIntNullable(AuswaertsElfmetertore),
            ParseIntNullable(HeimHalbzeitTore),
            ParseIntNullable(AuswaertsHalbzeitTore));
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_populiertGerade > 0 || e.PropertyName is null) return;

        if (_scoreFelder.Contains(e.PropertyName))
        {
            _entscheidungArt = ErgebnisEntscheidung.Art(_rahmen, Tore);
            AbschnitteMelden();
        }

        if (_formFelder.Contains(e.PropertyName))
            IsDirty = true;
    }

    private void ToreUebernehmen(SpielErgebnis? e)
    {
        _entscheidungArt = e?.EntscheidungArt ?? EntscheidungArt.RegulaereSpielzeit;
        HeimToreRegulaer = e?.HeimToreRegulaer.ToString() ?? "";
        AuswaertsToreRegulaer = e?.AuswaertsToreRegulaer.ToString() ?? "";
        HeimToreVerlaengerung = (e?.HeimToreRegulaer + e?.HeimToreVerlaengerung)?.ToString() ?? "";
        AuswaertsToreVerlaengerung = (e?.AuswaertsToreRegulaer + e?.AuswaertsToreVerlaengerung)?.ToString() ?? "";
        HeimElfmetertore = e?.HeimElfmetertore?.ToString() ?? "";
        AuswaertsElfmetertore = e?.AuswaertsElfmetertore?.ToString() ?? "";
        HeimHalbzeitTore = e?.HalbzeitHeimTore?.ToString() ?? "";
        AuswaertsHalbzeitTore = e?.HalbzeitAuswaertsTore?.ToString() ?? "";
    }

    private void AbschnitteMelden()
    {
        OnPropertyChanged(nameof(VerlaengerungAktiv));
        OnPropertyChanged(nameof(ElfmeterAktiv));
    }

    private static int ParseInt(string s) => int.TryParse(s, out var v) ? v : 0;
    private static int? ParseIntNullable(string s) => string.IsNullOrWhiteSpace(s) ? null : int.TryParse(s, out var v) ? v : null;
}
