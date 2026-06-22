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
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

// ── Hilfsklassen für Canvas-Rendering ────────────────────────────────────────

public class BracketKnotenAnzeige
{
    public double X { get; init; }
    public double Y { get; init; }
    public bool IstFreilos { get; init; }
    public string Label { get; init; } = "";
    public bool HatLabel => !string.IsNullOrEmpty(Label);
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
    private const double NODE_H  = 90.0;
    private const double GAP_H   = 40.0;
    private const double GAP_V   = 14.0;
    private const double ROUND_W = NODE_W + GAP_H;   // 225
    private const double UNIT    = NODE_H + GAP_V;   // 88

    [ObservableProperty] private ObservableCollection<TurnierInstanz> _instanzen = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeinTurnierGewaehlt))]
    private TurnierInstanz? _ausgewaehlteInstanz;

    public bool KeinTurnierGewaehlt => AusgewaehlteInstanz is null;

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
        var alleKnoten = runden.SelectMany(r => r.Knoten).ToDictionary(k => k.Id);

        // Logik 2 (festes Bracket): sequentielle Platzhalterstruktur → Labels + korrekte Verbindungen
        // Logik 1 (freie Auslosung): beliebige Paarungen → keine Labels, sequentielle Visualisierung
        bool istFest = IstFestesKoBracket(runden);
        var (effLinks, effRechts) = istFest
            ? BerechneEffektiveVerbindungen(runden, alleKnoten)
            : BauSequenzielleVerbindungen(runden, alleKnoten);

        var posMap      = new Dictionary<int, double>();
        double blaetter = 0;

        var wurzel = runden.Last().Knoten.FirstOrDefault();
        if (wurzel != null)
            AssignierePositionen(wurzel, alleKnoten, posMap, ref blaetter, effLinks, effRechts);

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
            string kuerzel = RundenKuerzel(runde.Knoten.Count);
            double x = (runde.RundeNummer - 1) * ROUND_W;

            var geordnet = runde.Knoten
                .OrderBy(k => k.Spiel?.Spielnummer ?? int.MaxValue)
                .ToList();

            for (int pos = 0; pos < geordnet.Count; pos++)
            {
                var k = geordnet[pos];
                if (!posMap.TryGetValue(k.Id, out var centerY)) continue;
                string label = istFest
                    ? (runde.Knoten.Count == 1 ? "F" : $"{kuerzel}{pos + 1}")
                    : "";
                Knoten.Add(BaueKnotenAnzeige(k, x, centerY - NODE_H / 2, label));
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

                int? linksId  = effLinks.GetValueOrDefault(k.Id);
                int? rechtsId = effRechts.GetValueOrDefault(k.Id);

                bool   hatLinks  = linksId.HasValue  && posMap.ContainsKey(linksId.Value);
                bool   hatRechts = rechtsId.HasValue && posMap.ContainsKey(rechtsId.Value);
                double lcyVal    = hatLinks  ? posMap[linksId!.Value]  : 0;
                double rcyVal    = hatRechts ? posMap[rechtsId!.Value] : lcyVal;

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
        ref double blaetter,
        Dictionary<int, int?> effLinks,
        Dictionary<int, int?> effRechts)
    {
        if (posMap.ContainsKey(knoten.Id)) return;

        int? lId = effLinks.GetValueOrDefault(knoten.Id);
        int? rId = effRechts.GetValueOrDefault(knoten.Id);

        if (!lId.HasValue && !rId.HasValue)
        {
            posMap[knoten.Id] = blaetter * UNIT + UNIT / 2;
            blaetter++;
            return;
        }

        if (lId.HasValue && alle.TryGetValue(lId.Value, out var links))
            AssignierePositionen(links, alle, posMap, ref blaetter, effLinks, effRechts);

        if (rId.HasValue && alle.TryGetValue(rId.Value, out var rechts))
            AssignierePositionen(rechts, alle, posMap, ref blaetter, effLinks, effRechts);

        double cy1 = lId.HasValue && posMap.TryGetValue(lId.Value, out var v1) ? v1 : 0;
        double cy2 = rId.HasValue && posMap.TryGetValue(rId.Value, out var v2) ? v2 : cy1;
        posMap[knoten.Id] = (cy1 + cy2) / 2.0;
    }

    // Logik 2: mindestens ein Übergang mit ≥4 Kind-Spielen ist positiv sequentiell bestätigt
    // und kein Übergang ist nicht-sequentiell.
    // Übergänge mit ≤2 Kinder (z.B. HF→Finale) sind trivial sequentiell und kein Beweis für ein festes Bracket.
    private static bool IstFestesKoBracket(IReadOnlyList<BracketRunde> runden)
    {
        bool hatBestaetigtenUebergang = false;

        for (int ri = 1; ri < runden.Count; ri++)
        {
            int kindAnzahl = runden[ri - 1].Knoten.Count;
            if (kindAnzahl < 4) continue; // trivial sequentiell, kein Aussagewert

            var elternSortiert = runden[ri].Knoten
                .OrderBy(k => k.Spiel?.Spielnummer ?? int.MaxValue)
                .ToList();
            string kuerzel = RundenKuerzel(kindAnzahl);

            bool hatBefund = false;
            for (int j = 0; j < elternSortiert.Count; j++)
            {
                var ek = elternSortiert[j];
                if (ek.Spiel is null) continue;

                var heimPos = ExtrahiereKoSiegerPos(ek.Spiel.HeimPlatzhalterName,      kuerzel);
                var ausPos  = ExtrahiereKoSiegerPos(ek.Spiel.AuswaertsPlatzhalterName, kuerzel);

                if (!heimPos.HasValue && !ausPos.HasValue) continue;
                hatBefund = true;

                if (heimPos.HasValue && heimPos.Value != j * 2 + 1) return false;
                if (ausPos.HasValue  && ausPos.Value  != j * 2 + 2) return false;
            }
            if (hatBefund) hatBestaetigtenUebergang = true;
        }

        // Logik 2 nur wenn wir sequentielle Struktur positiv beobachtet haben
        return hatBestaetigtenUebergang;
    }

    // Berechnet effektive Verbindungen für Logik 2: überschreibt gespeicherte Verbindungen mit
    // Platzhalternamen-basierten (korrigiert ggf. alte sequentiell gespeicherte Brackets).
    private static (Dictionary<int, int?> Links, Dictionary<int, int?> Rechts) BerechneEffektiveVerbindungen(
        IReadOnlyList<BracketRunde> runden, Dictionary<int, TurnierBaumKnoten> alleKnoten)
    {
        var effLinks  = alleKnoten.ToDictionary(kv => kv.Key, kv => kv.Value.LinkerKindKnotenId);
        var effRechts = alleKnoten.ToDictionary(kv => kv.Key, kv => kv.Value.RechterKindKnotenId);

        for (int ri = 1; ri < runden.Count; ri++)
        {
            string kuerzel = RundenKuerzel(runden[ri - 1].Knoten.Count);
            var kindNachPos = runden[ri - 1].Knoten
                .OrderBy(k => k.Spiel?.Spielnummer ?? int.MaxValue)
                .Select((k, idx) => (k.Id, pos: idx + 1))
                .ToDictionary(x => x.pos, x => x.Id);

            foreach (var ek in runden[ri].Knoten)
            {
                if (ek.Spiel is null) continue;
                var heimPos = ExtrahiereKoSiegerPos(ek.Spiel.HeimPlatzhalterName,      kuerzel);
                var ausPos  = ExtrahiereKoSiegerPos(ek.Spiel.AuswaertsPlatzhalterName, kuerzel);

                if (heimPos.HasValue && kindNachPos.TryGetValue(heimPos.Value, out var lId)) effLinks[ek.Id]  = lId;
                if (ausPos.HasValue  && kindNachPos.TryGetValue(ausPos.Value,  out var rId)) effRechts[ek.Id] = rId;
            }
        }
        return (effLinks, effRechts);
    }

    // Baut rein sequentielle Verbindungen für Logik 1 (freie Auslosung): ästhetisch saubere
    // Darstellung ohne Anspruch auf korrekte Abhängigkeiten (die Spielkarte zeigt die echten Paarungen).
    private static (Dictionary<int, int?> Links, Dictionary<int, int?> Rechts) BauSequenzielleVerbindungen(
        IReadOnlyList<BracketRunde> runden, Dictionary<int, TurnierBaumKnoten> alleKnoten)
    {
        var effLinks  = alleKnoten.Keys.ToDictionary(id => id, _ => (int?)null);
        var effRechts = alleKnoten.Keys.ToDictionary(id => id, _ => (int?)null);

        for (int ri = 1; ri < runden.Count; ri++)
        {
            var elternSortiert = runden[ri].Knoten
                .OrderBy(k => k.Spiel?.Spielnummer ?? int.MaxValue)
                .ToList();
            var kindSortiert = runden[ri - 1].Knoten
                .OrderBy(k => k.Spiel?.Spielnummer ?? int.MaxValue)
                .ToList();

            for (int j = 0; j < elternSortiert.Count; j++)
            {
                var ek  = elternSortiert[j];
                int li  = j * 2;
                int ri2 = j * 2 + 1;
                if (li  < kindSortiert.Count) effLinks[ek.Id]  = kindSortiert[li].Id;
                if (ri2 < kindSortiert.Count) effRechts[ek.Id] = kindSortiert[ri2].Id;
            }
        }
        return (effLinks, effRechts);
    }

    private static int? ExtrahiereKoSiegerPos(string? name, string kuerzel)
    {
        if (name is null) return null;
        var m = Regex.Match(name, @"^Sieger ([A-Za-z0-9.]+) (\d+)$", RegexOptions.IgnoreCase);
        if (!m.Success || !string.Equals(m.Groups[1].Value, kuerzel, StringComparison.OrdinalIgnoreCase))
            return null;
        return int.TryParse(m.Groups[2].Value, out int nr) ? nr : null;
    }

    private static BracketKnotenAnzeige BaueKnotenAnzeige(TurnierBaumKnoten k, double x, double y, string label)
    {
        if (k.Spiel is null)
            return new BracketKnotenAnzeige { X = x, Y = y, IstFreilos = true, Label = label, HeimName = "–", AuswaertsName = "–" };

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
            Label              = label,
            HeimName           = s.HeimMannschaft?.Name      ?? s.HeimPlatzhalterName      ?? "—",
            AuswaertsName      = s.AuswaertsMannschaft?.Name ?? s.AuswaertsPlatzhalterName ?? "—",
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

    private static string RundenKuerzel(int anzahl) => anzahl switch
    {
        1  => "F",
        2  => "HF",
        4  => "VF",
        8  => "AF",
        16 => "L32-",
        32 => "L64-",
        _  => $"R{anzahl}-"
    };
}
