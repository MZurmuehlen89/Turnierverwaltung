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
    private int _populiertGerade;
    private readonly List<VerfahrenArt> _erlaubteVerfahren = [];
    private TurnierRunde? _runde;

    public int? SpielId { get; private set; }
    public int? RundeId { get; private set; }

    // Issue #346: K.-o.-Paarung mit Hin- und Rückspiel — im Hinspiel gibt es kein Entscheidungsverfahren,
    // im Rückspiel nur bei ausgeglichenem Gesamtergebnis (Hinspiel-Tore aus Sicht des Hinspiels).
    private bool _formularIstKoHinspiel;
    private (int Heim, int Auswaerts)? _formularHinspielTore;

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

    [ObservableProperty] private bool _isDirty;

    [ObservableProperty] private string _formHeimName = "";
    [ObservableProperty] private string _formAuswaertsName = "";
    [ObservableProperty] private string _formSpielInfo = "";
    [ObservableProperty] private bool _formHatErgebnis;

    [ObservableProperty] private string _heimToreRegulaer = "0";
    [ObservableProperty] private string _auswaertsToreRegulaer = "0";

    [ObservableProperty] private EntscheidungArt _ausgewaehlteEntscheidungArt = EntscheidungArt.RegulaereSpielzeit;

    [ObservableProperty] private string _heimToreVerlaengerung = "";
    [ObservableProperty] private string _auswaertsToreVerlaengerung = "";
    [ObservableProperty] private string _heimElfmetertore = "";
    [ObservableProperty] private string _auswaertsElfmetertore = "";
    [ObservableProperty] private string _heimHalbzeitTore = "";
    [ObservableProperty] private string _auswaertsHalbzeitTore = "";

    private bool IstKoRunde => _runde?.Rundenart is
        Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;

    private bool HatVerlaengerungsVerfahren =>
        _erlaubteVerfahren.Any(v => v is VerfahrenArt.VerlaengerungKlassisch or VerfahrenArt.GoldenGoal or VerfahrenArt.SilverGoal);

    private bool HatElfmeterVerfahren => _erlaubteVerfahren.Any(v => v == VerfahrenArt.Elfmeterschiessen);

    public bool VerlaengerungAktiv =>
        IstKoRunde && HatVerlaengerungsVerfahren && EntscheidungsverfahrenNoetig();

    public bool ElfmeterAktiv
    {
        get
        {
            if (!IstKoRunde || !HatElfmeterVerfahren) return false;
            if (!EntscheidungsverfahrenNoetig()) return false;
            if (!HatVerlaengerungsVerfahren) return true;
            return VerlaengerungToreDifferenz() == 0;
        }
    }

    /// <summary>Tordifferenz (Heim minus Auswärts) allein der in der Verlängerung gefallenen Tore, oder
    /// <c>null</c>, solange der Stand nach Verlängerung nicht vollständig eingegeben ist. Die Felder enthalten
    /// den Spielstand inklusive regulärer Spielzeit — entscheidend ist aber nur, was IN der Verlängerung
    /// fällt: beim Einzelspiel identisch mit dem Vergleich der Spielstände, im K.-o.-Rückspiel mit
    /// ausgeglichenem Gesamtergebnis nicht (reguläre Spielzeit z.B. 3:2, Issue #346).</summary>
    private int? VerlaengerungToreDifferenz()
    {
        if (ParseIntNullable(HeimToreVerlaengerung) is not { } heimV ||
            ParseIntNullable(AuswaertsToreVerlaengerung) is not { } ausV ||
            ParseIntNullable(HeimToreRegulaer) is not { } heimReg ||
            ParseIntNullable(AuswaertsToreRegulaer) is not { } ausReg) return null;

        return (heimV - heimReg) - (ausV - ausReg);
    }

    public void ErlaubteVerfahrenSetzen(IEnumerable<VerfahrenArt> verfahren)
    {
        _erlaubteVerfahren.Clear();
        _erlaubteVerfahren.AddRange(verfahren);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (_populiertGerade == 0 && e.PropertyName is not null)
        {
            if (_scoreFelder.Contains(e.PropertyName))
                EntscheidungNeuBerechnen();

            if (_formFelder.Contains(e.PropertyName))
                IsDirty = true;
        }
    }

    public void FormularAusFuellenMit(
        Spiel spiel, TurnierRunde? runde, string rundenHinweis,
        bool istKoHinspiel, (int Heim, int Auswaerts)? hinspielTore)
    {
        _runde = runde;
        _formularIstKoHinspiel = istKoHinspiel;
        _formularHinspielTore = hinspielTore;
        SpielId = spiel.Id;
        RundeId = runde?.Id;

        _populiertGerade++;
        try
        {
            FormHeimName = spiel.HeimMannschaft?.Name ?? spiel.HeimPlatzhalterName ?? "";
            FormAuswaertsName = spiel.AuswaertsMannschaft?.Name ?? spiel.AuswaertsPlatzhalterName ?? "";
            FormSpielInfo = (spiel.Spieltag.HasValue
                ? $"Spieltag {spiel.Spieltag} · Spiel #{spiel.Spielnummer}"
                : $"Spiel #{spiel.Spielnummer}") + rundenHinweis;
            FormHatErgebnis = spiel.Ergebnis is not null;

            if (spiel.Ergebnis is { } e)
            {
                HeimToreRegulaer = e.HeimToreRegulaer.ToString();
                AuswaertsToreRegulaer = e.AuswaertsToreRegulaer.ToString();
                AusgewaehlteEntscheidungArt = e.EntscheidungArt;
                HeimToreVerlaengerung = e.HeimToreVerlaengerung.HasValue
                    ? (e.HeimToreRegulaer + e.HeimToreVerlaengerung.Value).ToString() : "";
                AuswaertsToreVerlaengerung = e.AuswaertsToreVerlaengerung.HasValue
                    ? (e.AuswaertsToreRegulaer + e.AuswaertsToreVerlaengerung.Value).ToString() : "";
                HeimElfmetertore = e.HeimElfmetertore?.ToString() ?? "";
                AuswaertsElfmetertore = e.AuswaertsElfmetertore?.ToString() ?? "";
                HeimHalbzeitTore = e.HalbzeitHeimTore?.ToString() ?? "";
                AuswaertsHalbzeitTore = e.HalbzeitAuswaertsTore?.ToString() ?? "";
            }
            else
            {
                HeimToreRegulaer = "";
                AuswaertsToreRegulaer = "";
                AusgewaehlteEntscheidungArt = EntscheidungArt.RegulaereSpielzeit;
                HeimToreVerlaengerung = "";
                AuswaertsToreVerlaengerung = "";
                HeimElfmetertore = "";
                AuswaertsElfmetertore = "";
                HeimHalbzeitTore = "";
                AuswaertsHalbzeitTore = "";
            }
        }
        finally
        {
            _populiertGerade--;
        }

        OnPropertyChanged(nameof(VerlaengerungAktiv));
        OnPropertyChanged(nameof(ElfmeterAktiv));
        IsDirty = false;
    }

    public ErgebnisErfassenEingabe BaueEingabe()
    {
        int heimRegulaer = ParseInt(HeimToreRegulaer);
        int auswaertsRegulaer = ParseInt(AuswaertsToreRegulaer);

        int? heimVerlaengerungDelta = ParseIntNullable(HeimToreVerlaengerung) is { } hv ? hv - heimRegulaer : null;
        int? auswaertsVerlaengerungDelta = ParseIntNullable(AuswaertsToreVerlaengerung) is { } av ? av - auswaertsRegulaer : null;

        return new(
            heimRegulaer,
            auswaertsRegulaer,
            AusgewaehlteEntscheidungArt,
            heimVerlaengerungDelta,
            auswaertsVerlaengerungDelta,
            ParseIntNullable(HeimElfmetertore),
            ParseIntNullable(AuswaertsElfmetertore),
            ParseIntNullable(HeimHalbzeitTore),
            ParseIntNullable(AuswaertsHalbzeitTore));
    }

    private static int ParseInt(string s) => int.TryParse(s, out var v) ? v : 0;
    private static int? ParseIntNullable(string s) => string.IsNullOrWhiteSpace(s) ? null : int.TryParse(s, out var v) ? v : null;

    private void EntscheidungNeuBerechnen()
    {
        AusgewaehlteEntscheidungArt = BerechneEntscheidungArt();
        OnPropertyChanged(nameof(VerlaengerungAktiv));
        OnPropertyChanged(nameof(ElfmeterAktiv));
    }

    /// <summary>Ob nach der regulären Spielzeit ein Entscheidungsverfahren fällig ist: im Einzelspiel bei
    /// Remis, im K.-o.-Hinspiel nie, im K.-o.-Rückspiel bei ausgeglichenem Gesamtergebnis (Issue #346).</summary>
    private bool EntscheidungsverfahrenNoetig()
    {
        if (ParseIntNullable(HeimToreRegulaer) is not { } heimReg ||
            ParseIntNullable(AuswaertsToreRegulaer) is not { } ausReg) return false;

        if (_formularIstKoHinspiel) return false;

        if (_formularHinspielTore is { } hin)
            return HinRueckspielWertung.EntscheidungsverfahrenNoetig(
                hin.Heim, hin.Auswaerts, heimReg, ausReg, _runde?.AuswaertstorRegelAktiv == true);

        return heimReg == ausReg;
    }

    private EntscheidungArt BerechneEntscheidungArt()
    {
        if (!IstKoRunde || !EntscheidungsverfahrenNoetig()) return EntscheidungArt.RegulaereSpielzeit;

        bool hatVerl = HatVerlaengerungsVerfahren;
        bool hatElf = HatElfmeterVerfahren;

        if (hatVerl)
        {
            if (VerlaengerungToreDifferenz() is { } differenz)
            {
                if (differenz != 0)
                {
                    var vart = _erlaubteVerfahren.First(v =>
                        v is VerfahrenArt.VerlaengerungKlassisch or VerfahrenArt.GoldenGoal or VerfahrenArt.SilverGoal);
                    return vart switch
                    {
                        VerfahrenArt.GoldenGoal => EntscheidungArt.GoldenGoal,
                        VerfahrenArt.SilverGoal => EntscheidungArt.SilverGoal,
                        _ => EntscheidungArt.Verlaengerung,
                    };
                }
                if (hatElf) return EntscheidungArt.Elfmeterschiessen;
            }

            var vartPending = _erlaubteVerfahren.First(v =>
                v is VerfahrenArt.VerlaengerungKlassisch or VerfahrenArt.GoldenGoal or VerfahrenArt.SilverGoal);
            return vartPending switch
            {
                VerfahrenArt.GoldenGoal => EntscheidungArt.GoldenGoal,
                VerfahrenArt.SilverGoal => EntscheidungArt.SilverGoal,
                _ => EntscheidungArt.Verlaengerung,
            };
        }

        if (hatElf) return EntscheidungArt.Elfmeterschiessen;

        return EntscheidungArt.RegulaereSpielzeit;
    }
}
