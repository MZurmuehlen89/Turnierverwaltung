using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

// ── Hilfsklassen für Canvas-Rendering ────────────────────────────────────────

public class BracketKnotenAnzeige
{
    public double X { get; init; }
    public double Y { get; init; }
    public bool IstFreilos { get; init; }
    public string HeimName { get; init; } = "";
    public string AuswaertsName { get; init; } = "";
    public bool HeimIstSieger { get; init; }
    public bool AuswaertsIstSieger { get; init; }
    public bool HatErgebnis { get; init; }
    public string HeimScore { get; init; } = "";
    public string AuswaertsScore { get; init; } = "";
    public string ErgebnisDetail { get; init; } = "";
}

public class BracketRundenkopf
{
    public double X { get; init; }
    public double Breite { get; init; }
    public string Name { get; init; } = "";
}

public class BracketLinie
{
    public double X1 { get; init; }
    public double Y1 { get; init; }
    public double X2 { get; init; }
    public double Y2 { get; init; }
}

// ── ViewModel ─────────────────────────────────────────────────────────────────

public partial class TurnierBaumViewModel : ViewModelBase, IRibbonAware
{
    private readonly TurnierBaumService    _baumService;
    private readonly TurnierInstanzService _instanzService;

    private const double NODE_W  = 185.0;
    private const double NODE_H  = 74.0;
    private const double GAP_H   = 40.0;
    private const double GAP_V   = 14.0;
    private const double ROUND_W = NODE_W + GAP_H;   // 225
    private const double UNIT    = NODE_H + GAP_V;   // 88

    [ObservableProperty] private ObservableCollection<TurnierInstanz> _instanzen = [];
    [ObservableProperty] private TurnierInstanz? _ausgewaehlteInstanz;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatBaum))]
    private bool _hatBaumIntern;
    public bool HatBaum => HatBaumIntern;

    [ObservableProperty] private bool    _istLadend;
    [ObservableProperty] private string? _fehlermeldung;
    [ObservableProperty] private string? _statusMeldung;

    [ObservableProperty] private double _canvasBreite;
    [ObservableProperty] private double _canvasHoehe;

    [ObservableProperty] private ObservableCollection<BracketRundenkopf>   _rundenKoepfe      = [];
    [ObservableProperty] private ObservableCollection<BracketKnotenAnzeige> _knoten            = [];
    [ObservableProperty] private ObservableCollection<BracketLinie>         _verbindungsLinien = [];

    public string  RibbonTabTitel  => "Turnierbaum";
    public ICommand NeuBefehl       => _neuBefehl;
    public ICommand SpeichernBefehl => _speichernBefehl;
    public ICommand AbbrechenBefehl => _abbrechenBefehl;
    public ICommand LoeschenBefehl  => _loeschenBefehl;

    private readonly AsyncRelayCommand _neuBefehl;
    private readonly AsyncRelayCommand _speichernBefehl;
    private readonly RelayCommand      _abbrechenBefehl;
    private readonly AsyncRelayCommand _loeschenBefehl;

    public TurnierBaumViewModel(TurnierBaumService baumService, TurnierInstanzService instanzService)
    {
        _baumService    = baumService;
        _instanzService = instanzService;

        _neuBefehl       = new AsyncRelayCommand(GenerierenAsync,       () => AusgewaehlteInstanz != null);
        _speichernBefehl = new AsyncRelayCommand(SiegerFortsteiernAsync, () => AusgewaehlteInstanz != null && HatBaum);
        _abbrechenBefehl = new RelayCommand(() => { }, () => false);
        _loeschenBefehl  = new AsyncRelayCommand(BaumLoeschenAsync,      () => AusgewaehlteInstanz != null && HatBaum);
    }

    public override async Task AktivierenAsync()
    {
        var liste = await _instanzService.AlleAbrufenAsync();
        Instanzen.Clear();
        foreach (var i in liste)
            Instanzen.Add(i);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(AusgewaehlteInstanz))
            _ = LadeBracketAsync();
    }

    private async Task LadeBracketAsync()
    {
        Fehlermeldung = null;
        StatusMeldung = null;
        HatBaumIntern = false;
        Knoten.Clear();
        VerbindungsLinien.Clear();
        RundenKoepfe.Clear();
        AktualisiereBefehle();

        if (AusgewaehlteInstanz is null) return;

        IstLadend = true;
        try
        {
            var runden = await _baumService.BracketLadenAsync(AusgewaehlteInstanz.Id);
            if (runden.Count == 0)
            {
                StatusMeldung = "Noch kein Turnierbaum vorhanden. Klicke 'Neu' zum Generieren.";
                return;
            }

            HatBaumIntern = true;
            BerechneBracket(runden);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstLadend = false;
            AktualisiereBefehle();
        }
    }

    private void BerechneBracket(IReadOnlyList<BracketRunde> runden)
    {
        var alleKnoten  = runden.SelectMany(r => r.Knoten).ToDictionary(k => k.Id);
        var posMap      = new Dictionary<int, double>();
        double blaetter = 0;

        var wurzel = runden.Last().Knoten.FirstOrDefault();
        if (wurzel != null)
            AssignierePositionen(wurzel, alleKnoten, posMap, ref blaetter);

        foreach (var k in runden.First().Knoten.Where(k => !posMap.ContainsKey(k.Id)))
        {
            posMap[k.Id] = blaetter * UNIT + UNIT / 2;
            blaetter++;
        }

        CanvasHoehe  = Math.Max(1, blaetter) * UNIT + GAP_V;
        CanvasBreite = runden.Count * ROUND_W - GAP_H;

        RundenKoepfe.Clear();
        for (int ri = 0; ri < runden.Count; ri++)
            RundenKoepfe.Add(new BracketRundenkopf
            {
                X      = ri * ROUND_W,
                Breite = NODE_W,
                Name   = RundenAnzeigeName(runden[ri].Knoten.Count)
            });

        Knoten.Clear();
        foreach (var runde in runden)
        {
            double x = (runde.RundeNummer - 1) * ROUND_W;
            foreach (var k in runde.Knoten)
            {
                if (!posMap.TryGetValue(k.Id, out var centerY)) continue;
                Knoten.Add(BaueKnotenAnzeige(k, x, centerY - NODE_H / 2));
            }
        }

        VerbindungsLinien.Clear();
        foreach (var runde in runden.Skip(1))
        {
            double px          = (runde.RundeNummer - 1) * ROUND_W;
            double midX        = px - GAP_H / 2.0;
            double kindRechtsX = (runde.RundeNummer - 2) * ROUND_W + NODE_W;

            foreach (var k in runde.Knoten)
            {
                if (!posMap.TryGetValue(k.Id, out var pcy)) continue;

                bool   hatLinks  = k.LinkerKindKnotenId.HasValue  && posMap.ContainsKey(k.LinkerKindKnotenId.Value);
                bool   hatRechts = k.RechterKindKnotenId.HasValue && posMap.ContainsKey(k.RechterKindKnotenId.Value);
                double lcyVal    = hatLinks  ? posMap[k.LinkerKindKnotenId!.Value]  : 0;
                double rcyVal    = hatRechts ? posMap[k.RechterKindKnotenId!.Value] : lcyVal;

                if (hatLinks)
                    VerbindungsLinien.Add(new BracketLinie { X1 = kindRechtsX, Y1 = lcyVal, X2 = midX, Y2 = lcyVal });
                if (hatRechts)
                    VerbindungsLinien.Add(new BracketLinie { X1 = kindRechtsX, Y1 = rcyVal, X2 = midX, Y2 = rcyVal });

                if (hatLinks || hatRechts)
                {
                    double top = Math.Min(lcyVal, rcyVal);
                    double bot = Math.Max(lcyVal, rcyVal);
                    if (top < bot)
                        VerbindungsLinien.Add(new BracketLinie { X1 = midX, Y1 = top, X2 = midX, Y2 = bot });
                    VerbindungsLinien.Add(new BracketLinie { X1 = midX, Y1 = pcy, X2 = px, Y2 = pcy });
                }
            }
        }
    }

    private static void AssignierePositionen(
        TurnierBaumKnoten knoten,
        Dictionary<int, TurnierBaumKnoten> alle,
        Dictionary<int, double> posMap,
        ref double blaetter)
    {
        if (posMap.ContainsKey(knoten.Id)) return;

        bool istBlatt = knoten.LinkerKindKnotenId == null && knoten.RechterKindKnotenId == null;
        if (istBlatt)
        {
            posMap[knoten.Id] = blaetter * UNIT + UNIT / 2;
            blaetter++;
            return;
        }

        if (knoten.LinkerKindKnotenId.HasValue && alle.TryGetValue(knoten.LinkerKindKnotenId.Value, out var links))
            AssignierePositionen(links, alle, posMap, ref blaetter);

        if (knoten.RechterKindKnotenId.HasValue && alle.TryGetValue(knoten.RechterKindKnotenId.Value, out var rechts))
            AssignierePositionen(rechts, alle, posMap, ref blaetter);

        double cy1 = knoten.LinkerKindKnotenId.HasValue
            && posMap.TryGetValue(knoten.LinkerKindKnotenId.Value, out var v1) ? v1 : 0;
        double cy2 = knoten.RechterKindKnotenId.HasValue
            && posMap.TryGetValue(knoten.RechterKindKnotenId.Value, out var v2) ? v2 : cy1;
        posMap[knoten.Id] = (cy1 + cy2) / 2.0;
    }

    private static BracketKnotenAnzeige BaueKnotenAnzeige(TurnierBaumKnoten k, double x, double y)
    {
        if (k.Spiel is null)
            return new BracketKnotenAnzeige { X = x, Y = y, IstFreilos = true, HeimName = "–", AuswaertsName = "–" };

        var s     = k.Spiel;
        var e     = s.Ergebnis;
        bool hatE = e is not null;

        string detail = hatE ? e!.EntscheidungArt switch
        {
            EntscheidungArt.Verlaengerung     => "n.V.",
            EntscheidungArt.GoldenGoal        => "G.G.",
            EntscheidungArt.SilverGoal        => "S.G.",
            EntscheidungArt.Elfmeterschiessen => $"i.E. ({e.HeimElfmetertore}:{e.AuswaertsElfmetertore})",
            EntscheidungArt.Auswaertstorregel => "A.T.",
            _                                 => ""
        } : "";

        return new BracketKnotenAnzeige
        {
            X                  = x,
            Y                  = y,
            IstFreilos         = false,
            HeimName           = s.HeimMannschaft?.Name      ?? "—",
            AuswaertsName      = s.AuswaertsMannschaft?.Name ?? "—",
            HatErgebnis        = hatE,
            HeimScore          = hatE ? e!.HeimToreGesamt.ToString()      : "",
            AuswaertsScore     = hatE ? e!.AuswaertsToreGesamt.ToString() : "",
            ErgebnisDetail     = detail,
            HeimIstSieger      = hatE && e!.SiegerMannschaftId == s.HeimMannschaftId,
            AuswaertsIstSieger = hatE && e!.SiegerMannschaftId == s.AuswaertsMannschaftId
        };
    }

    private async Task GenerierenAsync()
    {
        if (AusgewaehlteInstanz is null) return;
        Fehlermeldung = null;
        StatusMeldung = null;
        IstLadend = true;
        try
        {
            await _baumService.GenerierenAsync(AusgewaehlteInstanz.Id);
            await LadeBracketAsync();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
            IstLadend = false;
        }
    }

    private async Task SiegerFortsteiernAsync()
    {
        if (AusgewaehlteInstanz is null) return;
        Fehlermeldung = null;
        try
        {
            int anzahl = await _baumService.SiegerFortsteiernAsync(AusgewaehlteInstanz.Id);
            StatusMeldung = anzahl == 0
                ? "Keine neuen Sieger zum Fortschreiben gefunden."
                : $"{anzahl} Spiel(e) mit Sieger aktualisiert.";
            await LadeBracketAsync();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    private async Task BaumLoeschenAsync()
    {
        if (AusgewaehlteInstanz is null) return;
        Fehlermeldung = null;
        try
        {
            await _baumService.LoeschenAsync(AusgewaehlteInstanz.Id);
            HatBaumIntern = false;
            Knoten.Clear();
            VerbindungsLinien.Clear();
            RundenKoepfe.Clear();
            StatusMeldung = "Turnierbaum wurde gelöscht.";
            AktualisiereBefehle();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    private void AktualisiereBefehle()
    {
        _neuBefehl.NotifyCanExecuteChanged();
        _speichernBefehl.NotifyCanExecuteChanged();
        _loeschenBefehl.NotifyCanExecuteChanged();
    }

    private static string RundenAnzeigeName(int anzahl) => anzahl switch
    {
        1  => "Finale",
        2  => "Halbfinale",
        4  => "Viertelfinale",
        8  => "Achtelfinale",
        16 => "Letzte 32",
        32 => "Letzte 64",
        _  => $"{anzahl} Spiele"
    };
}
