using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

// ── Hilfsklassen für Canvas-Rendering — 1:1 aus Wpf/ViewModels/TurnierBaumViewModel.cs portiert ────

public class BracketKnotenAnzeige
{
    public int Id { get; init; }
    public double X { get; init; }
    // Globale, auf den gesamten Baum bemessene Position — für die künftige Gesamtübersicht (#238) von
    // Anfang an mitgeführt, auch wenn hier nur die fensterlokale Position (FensterY) gebunden wird.
    public double Y { get; init; }
    public double FensterY { get; set; }
    public int RundenIndex { get; init; }
    public int? TurnierRundeId { get; init; }
    public int? LinkerKindKnotenId { get; init; }
    public int? RechterKindKnotenId { get; init; }
    public bool IstFreilos { get; init; }
    public string Label { get; init; } = "";
    public bool HatLabel => !string.IsNullOrEmpty(Label);
    public string HeimName { get; init; } = "";
    public string AuswaertsName { get; init; } = "";
    public byte[]? HeimWappen { get; init; }
    public byte[]? AuswaertsWappen { get; init; }
    public bool HeimIstSieger { get; init; }
    public bool AuswaertsIstSieger { get; init; }
    public bool HatErgebnis { get; init; }
    public string HeimScore { get; init; } = "";
    public string AuswaertsScore { get; init; } = "";
    public string ErgebnisDetail { get; init; } = "";
    /// <summary>Fußzeile bei Hin-/Rückspiel-Paarungen (Issue #340), z.B. "Hin 1:0 · Rück 3:2".</summary>
    public string PaarungsText { get; init; } = "";
    public bool HatPaarungsText => !string.IsNullOrEmpty(PaarungsText);
    public string Datum { get; init; } = "";
    public bool HatDatum => !string.IsNullOrEmpty(Datum);
}

public class BracketRundenkopf
{
    public double X { get; init; }
    public double Breite { get; init; }
    public string Name { get; init; } = "";
}

public class UebergreifendeRangfolgeAnzeige
{
    public int Platzierung { get; init; }
    public int AnzahlBeste { get; init; }
    public string ZielRundeName { get; init; } = "";
    public IReadOnlyList<TabellenEintrag> Rangfolge { get; init; } = [];
}

/// <summary>
/// "Turnierbaum"-Bereich des #301-Composite (<see cref="SpielplanUndBaumViewModel"/>) — 1:1 aus
/// Wpf/ViewModels/TurnierBaumViewModel.cs portiert. Verbindungslinien-Zeichnung selbst lebt im
/// Code-Behind der View (Canvas-Overlay, siehe TurnierBaumView.xaml.cs), analog zum WPF-Original.
/// #262-Fix (lange Mannschaftsnamen in Tabellenkarten): die Gruppentabellen-/Liga-Tabelle-Karten in
/// der View bekommen ausreichend Breite ohne TextTrimming — die Bracket-Karten selbst behalten die
/// feste 240px-Breite mit Trimming (Zwangslage durch Canvas.Top-Positionierung, keine #262-Stelle).
/// </summary>
public partial class TurnierBaumViewModel : ViewModelBase, IRibbonAware
{
    private readonly TurnierBaumService _baumService;
    private readonly TabellenService _tabellenService;
    private readonly UebergangsregelService _uebergangsregelService;

    private const double NODE_W = 240.0;
    private const double NODE_H = 148.0;
    private const double GAP_H = 40.0;
    private const double GAP_V = 24.0;
    private const double ROUND_W = NODE_W + GAP_H;
    private const double UNIT = NODE_H + GAP_V;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeinTurnierGewaehlt))]
    private TurnierInstanz? _ausgewaehlteInstanz;

    public bool KeinTurnierGewaehlt => AusgewaehlteInstanz is null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatBaum))]
    [NotifyPropertyChangedFor(nameof(ZeigtBracketInhalt))]
    private bool _hatBaumIntern;
    public bool HatBaum => HatBaumIntern;

    [ObservableProperty] private bool _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    [ObservableProperty] private double _canvasBreite;
    [ObservableProperty] private double _canvasHoehe;
    [ObservableProperty] private double _fensterCanvasHoehe;

    [ObservableProperty] private ObservableCollection<BracketRundenkopf> _rundenKoepfe = [];
    [ObservableProperty] private ObservableCollection<BracketKnotenAnzeige> _knoten = [];

    // ── Rundenweise Navigation (Issue #108/#244) ───────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LinkeSpalteKnoten))]
    [NotifyPropertyChangedFor(nameof(RechteSpalteKnoten))]
    [NotifyPropertyChangedFor(nameof(LinkeSpalteName))]
    [NotifyPropertyChangedFor(nameof(RechteSpalteName))]
    [NotifyPropertyChangedFor(nameof(ZeigtZweiteSpalte))]
    [NotifyPropertyChangedFor(nameof(HatVorherigeRunde))]
    [NotifyPropertyChangedFor(nameof(HatNaechsteRunde))]
    [NotifyPropertyChangedFor(nameof(VorherigeRundeName))]
    [NotifyPropertyChangedFor(nameof(NaechsteRundeName))]
    private int _aktuellerRundenIndex;

    partial void OnAktuellerRundenIndexChanged(int value)
    {
        AktualisiereFensterPositionen();
        _vorherigeRundeBefehl.NotifyCanExecuteChanged();
        _naechsteRundeBefehl.NotifyCanExecuteChanged();
    }

    private void AktualisiereFensterPositionen()
    {
        var linke = Knoten.Where(k => k.RundenIndex == AktuellerRundenIndex).OrderBy(k => k.Y).ToList();
        for (int i = 0; i < linke.Count; i++)
            linke[i].FensterY = i * UNIT + UNIT / 2;

        FensterCanvasHoehe = Math.Max(1, linke.Count) * UNIT + GAP_V;

        if (!ZeigtZweiteSpalte) return;

        var rechte = Knoten.Where(k => k.RundenIndex == FensterEndeIndex).OrderBy(k => k.Y).ToList();
        for (int i = 0; i < rechte.Count; i++)
        {
            var eltern = rechte[i];
            var kindPositionen = new List<double>();
            if (eltern.LinkerKindKnotenId is { } lid && linke.FirstOrDefault(k => k.Id == lid) is { } l)
                kindPositionen.Add(l.FensterY);
            if (eltern.RechterKindKnotenId is { } rid && linke.FirstOrDefault(k => k.Id == rid) is { } r)
                kindPositionen.Add(r.FensterY);
            eltern.FensterY = kindPositionen.Count > 0 ? kindPositionen.Average() : i * UNIT + UNIT / 2;
        }
    }

    private int FensterEndeIndex => Math.Min(AktuellerRundenIndex + 1, Math.Max(0, RundenKoepfe.Count - 1));
    private int MaxFensterStartIndex => Math.Max(0, RundenKoepfe.Count - 2);

    public bool ZeigtZweiteSpalte => FensterEndeIndex != AktuellerRundenIndex;

    public IReadOnlyList<BracketKnotenAnzeige> LinkeSpalteKnoten =>
        Knoten.Where(k => k.RundenIndex == AktuellerRundenIndex).OrderBy(k => k.Y).ToList();

    public IReadOnlyList<BracketKnotenAnzeige> RechteSpalteKnoten =>
        ZeigtZweiteSpalte
            ? Knoten.Where(k => k.RundenIndex == FensterEndeIndex).OrderBy(k => k.Y).ToList()
            : [];

    public string LinkeSpalteName =>
        AktuellerRundenIndex >= 0 && AktuellerRundenIndex < RundenKoepfe.Count
            ? RundenKoepfe[AktuellerRundenIndex].Name
            : "";

    public string? RechteSpalteName => ZeigtZweiteSpalte ? RundenKoepfe[FensterEndeIndex].Name : null;

    public bool HatVorherigeRunde => AktuellerRundenIndex > 0;
    public bool HatNaechsteRunde => AktuellerRundenIndex < MaxFensterStartIndex;

    public string? VorherigeRundeName => HatVorherigeRunde ? RundenKoepfe[AktuellerRundenIndex - 1].Name : null;
    public string? NaechsteRundeName => HatNaechsteRunde ? RundenKoepfe[FensterEndeIndex + 1].Name : null;

    public ICommand VorherigeRundeBefehl => _vorherigeRundeBefehl;
    public ICommand NaechsteRundeBefehl => _naechsteRundeBefehl;
    private readonly RelayCommand _vorherigeRundeBefehl;
    private readonly RelayCommand _naechsteRundeBefehl;

    private void RundenNavigationZuruecksetzen()
    {
        AktuellerRundenIndex = 0;
        AktualisiereFensterPositionen();
        OnPropertyChanged(nameof(AktuellerRundenIndex));
        OnPropertyChanged(nameof(LinkeSpalteKnoten));
        OnPropertyChanged(nameof(RechteSpalteKnoten));
        OnPropertyChanged(nameof(LinkeSpalteName));
        OnPropertyChanged(nameof(RechteSpalteName));
        OnPropertyChanged(nameof(ZeigtZweiteSpalte));
        OnPropertyChanged(nameof(HatVorherigeRunde));
        OnPropertyChanged(nameof(HatNaechsteRunde));
        OnPropertyChanged(nameof(VorherigeRundeName));
        OnPropertyChanged(nameof(NaechsteRundeName));
        _vorherigeRundeBefehl.NotifyCanExecuteChanged();
        _naechsteRundeBefehl.NotifyCanExecuteChanged();
    }

    private void RundenNavigationAktualisieren()
    {
        if (AktuellerRundenIndex > MaxFensterStartIndex)
        {
            AktuellerRundenIndex = MaxFensterStartIndex;
            return;
        }
        AktualisiereFensterPositionen();
        OnPropertyChanged(nameof(LinkeSpalteKnoten));
        OnPropertyChanged(nameof(RechteSpalteKnoten));
    }

    // ── Gruppenphase-/Liga-Darstellung ─────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigtBracket))]
    [NotifyPropertyChangedFor(nameof(ZeigtBracketInhalt))]
    private bool _zeigtGruppenphase;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigtBracket))]
    [NotifyPropertyChangedFor(nameof(ZeigtBracketInhalt))]
    private bool _zeigtLigaTabelle;

    [ObservableProperty]
    private bool _zeigtBuchholzSpalte;

    public bool ZeigtBracket => !ZeigtGruppenphase && !ZeigtLigaTabelle;
    public bool ZeigtBracketInhalt => ZeigtBracket && HatBaum;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatGruppenTabellenAnzeige))]
    [NotifyPropertyChangedFor(nameof(AnzeigeGruppenTabellen))]
    private ObservableCollection<GruppenTabelle> _gruppenTabellenAnzeige = [];
    public bool HatGruppenTabellenAnzeige => GruppenTabellenAnzeige.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatLigaTabelleAnzeige))]
    private ObservableCollection<TabellenEintrag> _ligaTabelleAnzeige = [];
    public bool HatLigaTabelleAnzeige => LigaTabelleAnzeige.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatUebergreifendeRangfolgenAnzeige))]
    private ObservableCollection<UebergreifendeRangfolgeAnzeige> _uebergreifendeRangfolgenAnzeige = [];
    public bool HatUebergreifendeRangfolgenAnzeige => UebergreifendeRangfolgenAnzeige.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnzeigeGruppenTabellen))]
    private int? _fokussierteGruppenNummer;

    public IReadOnlyList<GruppenTabelle> AnzeigeGruppenTabellen =>
        FokussierteGruppenNummer is { } nr
            ? GruppenTabellenAnzeige.Where(g => g.GruppenNummer == nr).ToList()
            : GruppenTabellenAnzeige;

    public void FokussiereGruppe(int? gruppenNummer) => FokussierteGruppenNummer = gruppenNummer;

    // ── Tabellenstand-Snapshot "Bis Spieltag" ────────────────────────────────

    private TurnierRunde? _fokusRunde;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TabellenSpieltagOptionen))]
    [NotifyPropertyChangedFor(nameof(HatVerfuegbareSpieltage))]
    private ObservableCollection<int> _verfuegbareSpieltage = [];

    public IReadOnlyList<int> TabellenSpieltagOptionen => new[] { 0 }.Concat(VerfuegbareSpieltage).ToList();
    public bool HatVerfuegbareSpieltage => VerfuegbareSpieltage.Count > 0;

    [ObservableProperty] private int? _tabellenBisSpieltag;

    public int TabellenBisSpieltagAuswahl
    {
        get => TabellenBisSpieltag ?? 0;
        set => TabellenBisSpieltag = value == 0 ? null : value;
    }

    partial void OnTabellenBisSpieltagChanged(int? value)
    {
        OnPropertyChanged(nameof(TabellenBisSpieltagAuswahl));
        if (_fokusRunde is not null) _ = TabelleNeuLadenAsync();
    }

    public void ZeigeAktuellenStand() => TabellenBisSpieltag = null;

    [ObservableProperty] private TabellenAnsicht _ausgewaehlteLigaAnsicht = TabellenAnsicht.Gesamt;
    public bool ZeigtHinRueckrundenTabs => _fokusRunde?.MitRueckspiel == true;
    public ICommand LigaAnsichtWaehlenBefehl => _ligaAnsichtWaehlenBefehl;
    private readonly RelayCommand<TabellenAnsicht?> _ligaAnsichtWaehlenBefehl;

    partial void OnAusgewaehlteLigaAnsichtChanged(TabellenAnsicht value)
    {
        if (_fokusRunde is not null) _ = TabelleNeuLadenAsync();
    }

    private async Task TabelleNeuLadenAsync()
    {
        if (_fokusRunde is null) return;
        Fehlermeldung = null;
        try
        {
            if (ZeigtGruppenphase)
            {
                var tabellen = await _tabellenService.GruppenTabellenBerechnenAsync(_fokusRunde.Id, TabellenBisSpieltag);
                GruppenTabellenAnzeige = new ObservableCollection<GruppenTabelle>(tabellen);
                await UebergreifendeRangfolgenLadenAsync();
            }
            else if (ZeigtLigaTabelle)
            {
                var tabelle = await _tabellenService.TabelleBerechnenAsync(_fokusRunde.Id, AusgewaehlteLigaAnsicht, TabellenBisSpieltag);
                LigaTabelleAnzeige = new ObservableCollection<TabellenEintrag>(tabelle);
            }
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    private async Task UebergreifendeRangfolgenLadenAsync()
    {
        if (_fokusRunde is null) { UebergreifendeRangfolgenAnzeige = []; return; }

        var regeln = (await _uebergangsregelService.AlleNachQuellRundeIdAsync(_fokusRunde.Id))
            .Where(r => r.AnzahlBeste is not null)
            .ToList();

        var anzeigen = new List<UebergreifendeRangfolgeAnzeige>();
        foreach (var regel in regeln)
        {
            var rangfolge = await _tabellenService.UebergreifendeRangfolgeBerechnenAsync(regel.QuellRundeId, regel.PlatzierungVon);
            var qualifikanten = await _uebergangsregelService.QualifikantenErmittelnAsync(regel.Id);

            if (qualifikanten.Values.All(v => v.HasValue))
            {
                var qualifizierteIds = qualifikanten.Values.Select(v => v!.Value).ToHashSet();
                foreach (var eintrag in rangfolge)
                {
                    eintrag.Qualifikationsstatus = qualifizierteIds.Contains(eintrag.Mannschaft.Id)
                        ? Qualifikationsstatus.SicherQualifiziert
                        : Qualifikationsstatus.SicherAusgeschieden;
                    eintrag.SicherPositioniert = true;
                }
            }

            anzeigen.Add(new UebergreifendeRangfolgeAnzeige
            {
                Platzierung = regel.PlatzierungVon,
                AnzahlBeste = regel.AnzahlBeste!.Value,
                ZielRundeName = regel.ZielRunde.Name,
                Rangfolge = rangfolge
            });
        }

        UebergreifendeRangfolgenAnzeige = new ObservableCollection<UebergreifendeRangfolgeAnzeige>(anzeigen);
    }

    // Ribbon-Standardaktionen bewusst komplett ausgeblendet (#311): "Baum generieren"/"Sieger
    // fortschreiben"/"Baum löschen" waren toter Code — beide Vorgänge laufen bereits automatisch nach
    // jeder Ergebnisänderung (SpielplanViewModel.TurnierBaumAktualisierenAsync), "Baum löschen" ist
    // ebenfalls überflüssig, da der Baum beim nächsten Ergebnis ohnehin neu generiert wird.
    public string RibbonTabTitel => "Turnierbaum";
    public ICommand? NeuBefehl => null;
    public ICommand? SpeichernBefehl => null;
    public ICommand AbbrechenBefehl => _abbrechenBefehl;
    public ICommand? LoeschenBefehl => null;
    public ICommand KnotenAngeklicktBefehl => _knotenAngeklicktBefehl;
    public ICommand GruppeAngeklicktBefehl => _gruppeAngeklicktBefehl;
    public bool HatStandardAktionen => false;
    public bool HatLoeschenAktion => false;
    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen => [];

    public event EventHandler<int>? RundeAngeklickt;
    public event EventHandler<int>? GruppeAngeklickt;

    private readonly RelayCommand _abbrechenBefehl;
    private readonly RelayCommand<BracketKnotenAnzeige> _knotenAngeklicktBefehl;
    private readonly RelayCommand<GruppenTabelle> _gruppeAngeklicktBefehl;

    public TurnierBaumViewModel(TurnierBaumService baumService, TabellenService tabellenService, UebergangsregelService uebergangsregelService)
    {
        _baumService = baumService;
        _tabellenService = tabellenService;
        _uebergangsregelService = uebergangsregelService;

        _abbrechenBefehl = new RelayCommand(() => { }, () => false);
        _knotenAngeklicktBefehl = new RelayCommand<BracketKnotenAnzeige>(
            k => { if (k?.TurnierRundeId is { } rundeId) RundeAngeklickt?.Invoke(this, rundeId); });
        _gruppeAngeklicktBefehl = new RelayCommand<GruppenTabelle>(
            g => { if (g is not null) GruppeAngeklickt?.Invoke(this, g.GruppenNummer); });
        _ligaAnsichtWaehlenBefehl = new RelayCommand<TabellenAnsicht?>(a => AusgewaehlteLigaAnsicht = a ?? TabellenAnsicht.Gesamt);
        _vorherigeRundeBefehl = new RelayCommand(() => AktuellerRundenIndex--, () => HatVorherigeRunde);
        _naechsteRundeBefehl = new RelayCommand(() => AktuellerRundenIndex++, () => HatNaechsteRunde);
    }

    public override Task AktivierenAsync() => Task.CompletedTask;

    public async Task InstanzSetzenAsync(TurnierInstanz? instanz)
    {
        AusgewaehlteInstanz = instanz;
        await LadeBracketAsync();
    }

    public async Task AktualisiereFokusRundeAsync(TurnierRunde? runde)
    {
        bool istEchterRundenwechsel = runde?.Id != _fokusRunde?.Id;
        if (istEchterRundenwechsel)
            FokussierteGruppenNummer = null;

        _fokusRunde = null;
        TabellenBisSpieltag = null;
        AusgewaehlteLigaAnsicht = TabellenAnsicht.Gesamt;
        VerfuegbareSpieltage = [];
        _fokusRunde = runde;
        OnPropertyChanged(nameof(ZeigtHinRueckrundenTabs));

        var rundenart = runde?.Rundenart;
        bool istGruppenphase = rundenart == Rundenart.Gruppenphase;
        bool istLigaArtig = rundenart is Rundenart.Liga or Rundenart.TopfLiga or Rundenart.SchweizerSystem;

        ZeigtGruppenphase = istGruppenphase;
        ZeigtLigaTabelle = istLigaArtig;
        ZeigtBuchholzSpalte = rundenart == Rundenart.SchweizerSystem;

        if (!istGruppenphase && !istLigaArtig)
        {
            if (HatBaum) await AktualisiereBracketErgebnisseAsync();
            return;
        }

        Fehlermeldung = null;
        try
        {
            var verfuegbareSpieltage = await _tabellenService.VerfuegbareSpieltageAsync(runde!.Id);
            VerfuegbareSpieltage = new ObservableCollection<int>(verfuegbareSpieltage);

            await TabelleNeuLadenAsync();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e) => base.OnPropertyChanged(e);

    private async Task LadeBracketAsync()
    {
        Fehlermeldung = null;
        HatBaumIntern = false;
        Knoten.Clear();
        RundenKoepfe.Clear();
        RundenNavigationZuruecksetzen();

        if (AusgewaehlteInstanz is null) return;

        IstLadend = true;
        try
        {
            var runden = await _baumService.BracketLadenAsync(AusgewaehlteInstanz.Id);
            if (runden.Count == 0) return;

            HatBaumIntern = true;
            BerechneBracket(runden, rundenNavigationZuruecksetzen: true);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstLadend = false;
        }
    }

    private async Task AktualisiereBracketErgebnisseAsync()
    {
        if (AusgewaehlteInstanz is null) return;
        try
        {
            var runden = await _baumService.BracketLadenAsync(AusgewaehlteInstanz.Id);
            if (runden.Count == 0) return;
            BerechneBracket(runden, rundenNavigationZuruecksetzen: false);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    private void BerechneBracket(IReadOnlyList<BracketRunde> runden, bool rundenNavigationZuruecksetzen)
    {
        var alleKnoten = runden.SelectMany(r => r.Knoten).ToDictionary(k => k.Id);

        var effLinks = alleKnoten.ToDictionary(kv => kv.Key, kv => kv.Value.LinkerKindKnotenId);
        var effRechts = alleKnoten.ToDictionary(kv => kv.Key, kv => kv.Value.RechterKindKnotenId);

        bool istFest = runden.Skip(1).Any(r => r.Knoten.Any(k =>
            k.Spiel is not null && (k.Spiel.HeimPlatzhalterName is not null || k.Spiel.AuswaertsPlatzhalterName is not null)));

        var posMap = new Dictionary<int, double>();
        double blaetter = 0;

        // Issue #385: Das Spiel um Platz 3 steht hinter dem Finale, gehört aber nicht zur Baumkette.
        var wurzel = runden.LastOrDefault(r => !r.IstSpielUmPlatzDrei)?.Knoten.FirstOrDefault();
        if (wurzel != null)
            AssignierePositionen(wurzel, alleKnoten, posMap, ref blaetter, effLinks, effRechts);

        // Knoten ohne Weg zur Wurzel (Direkteinsteiger, Spiel um Platz 3) in jeder Runde anhängen, nicht nur in
        // der ersten — sonst fehlen sie in der Anzeige.
        foreach (var k in runden.SelectMany(r => r.Knoten).Where(k => !posMap.ContainsKey(k.Id)))
        {
            posMap[k.Id] = blaetter * UNIT + UNIT / 2;
            blaetter++;
        }

        CanvasHoehe = Math.Max(1, blaetter) * UNIT + GAP_V;
        CanvasBreite = runden.Count * ROUND_W - GAP_H;

        RundenKoepfe.Clear();
        for (int ri = 0; ri < runden.Count; ri++)
            RundenKoepfe.Add(new BracketRundenkopf
            {
                X = ri * ROUND_W,
                Breite = NODE_W,
                Name = runden[ri].RundenName ?? RundenAnzeigeName(runden[ri].Knoten.Count)
            });

        Knoten.Clear();
        for (int ri = 0; ri < runden.Count; ri++)
        {
            var runde = runden[ri];
            string kuerzel = RundenKuerzelAusName(runde.RundenName) ?? RundenKuerzel(runde.Knoten.Count);
            double x = ri * ROUND_W;

            int? turnierRundeId = runde.Knoten
                .Select(k => k.Spiel?.TurnierRundeId)
                .FirstOrDefault(id => id.HasValue);

            var geordnet = runde.Knoten
                .OrderBy(k => k.Spiel?.Spielnummer ?? int.MaxValue)
                .ToList();

            for (int pos = 0; pos < geordnet.Count; pos++)
            {
                var k = geordnet[pos];
                if (!posMap.TryGetValue(k.Id, out var centerY)) continue;
                string label = istFest
                    ? (runde.Knoten.Count == 1 ? kuerzel.TrimEnd('-') : $"{kuerzel}{pos + 1}")
                    : "";
                var rueckspiel = k.SpielId is int spielId ? runde.RueckspielJeHinspielId.GetValueOrDefault(spielId) : null;
                Knoten.Add(BaueKnotenAnzeige(k, rueckspiel, x, centerY - NODE_H / 2, ri, label,
                    effLinks.GetValueOrDefault(k.Id), effRechts.GetValueOrDefault(k.Id), turnierRundeId));
            }
        }

        if (rundenNavigationZuruecksetzen)
            RundenNavigationZuruecksetzen();
        else
            RundenNavigationAktualisieren();
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

    private static BracketKnotenAnzeige BaueKnotenAnzeige(TurnierBaumKnoten k, Spiel? rueckspiel, double x, double y,
        int rundenIndex, string label, int? linkerKindKnotenId, int? rechterKindKnotenId, int? turnierRundeId = null)
    {
        if (k.Spiel is null)
            return new BracketKnotenAnzeige { Id = k.Id, X = x, Y = y, RundenIndex = rundenIndex, TurnierRundeId = turnierRundeId, LinkerKindKnotenId = linkerKindKnotenId, RechterKindKnotenId = rechterKindKnotenId, IstFreilos = true, Label = label, HeimName = "–", AuswaertsName = "–" };

        var s = k.Spiel;
        var e = s.Ergebnis;
        bool hatE = e is not null;

        string detail = hatE
            ? SpielErgebnisFormatierer.DetailText(
                e!.EntscheidungArt,
                e.HeimToreRegulaer, e.AuswaertsToreRegulaer,
                e.HeimToreVerlaengerung, e.AuswaertsToreVerlaengerung,
                e.HeimElfmetertore, e.AuswaertsElfmetertore)
            : "";
        string heimScore      = hatE ? e!.HeimToreGesamt.ToString()      : "";
        string auswaertsScore = hatE ? e!.AuswaertsToreGesamt.ToString() : "";
        int? siegerId = e?.SiegerMannschaftId;
        string paarungsText = "";

        // Issue #340: Hin-/Rückspiel-Paarung — Gesamtergebnis beider Spiele, Sieger aus dem Rückspiel (das
        // Hinspiel-Ergebnis trägt nur den Sieger des Einzelspiels). Ohne Rückspiel-Ergebnis steht der Stand
        // nach dem Hinspiel ohne Sieger-Markierung.
        if (rueckspiel is not null && hatE)
        {
            var re = rueckspiel.Ergebnis;
            paarungsText = SpielErgebnisFormatierer.PaarungsEinzelergebnisse(e!, re);
            detail = re is not null ? SpielErgebnisFormatierer.PaarungsEntscheidung(re) : "";
            siegerId = re?.SiegerMannschaftId;
            if (re is not null)
            {
                var (teamA, teamB) = HinRueckspielWertung.GesamtTore(e!, re);
                heimScore      = teamA.ToString();
                auswaertsScore = teamB.ToString();
            }
        }

        return new BracketKnotenAnzeige
        {
            Id = k.Id,
            X = x,
            Y = y,
            RundenIndex = rundenIndex,
            TurnierRundeId = turnierRundeId ?? s.TurnierRundeId,
            LinkerKindKnotenId = linkerKindKnotenId,
            RechterKindKnotenId = rechterKindKnotenId,
            IstFreilos = false,
            Label = label,
            HeimName = s.HeimMannschaft?.Name ?? s.HeimPlatzhalterName ?? "—",
            AuswaertsName = s.AuswaertsMannschaft?.Name ?? s.AuswaertsPlatzhalterName ?? "—",
            HeimWappen = TeilnehmerAnzeige.MannschaftsIcon(s.HeimMannschaft),
            AuswaertsWappen = TeilnehmerAnzeige.MannschaftsIcon(s.AuswaertsMannschaft),
            HatErgebnis = hatE,
            HeimScore = heimScore,
            AuswaertsScore = auswaertsScore,
            ErgebnisDetail = detail,
            PaarungsText = paarungsText,
            HeimIstSieger = siegerId is not null && siegerId == s.HeimMannschaftId,
            AuswaertsIstSieger = siegerId is not null && siegerId == s.AuswaertsMannschaftId,
            Datum = s.Datum?.ToString("ddd, dd.MM.yy", CultureInfo.GetCultureInfo("de-DE")) ?? ""
        };
    }

    /// <summary>Kürzel der Knoten-Labels aus dem echten Rundennamen (Issue #365), z.B. "Playoff-Runde" → "PO",
    /// "Achtelfinale" → "AF". Unbekannte Namen: Anfangsbuchstaben der Wörter. <c>null</c> ohne Namen — dann greift
    /// der Rückfall über die Spielanzahl.</summary>
    private static string? RundenKuerzelAusName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        string normiert = name.Trim().ToLowerInvariant();
        string? bekannt = normiert switch
        {
            "finale" => "F",
            "halbfinale" => "HF",
            "viertelfinale" => "VF",
            "achtelfinale" => "AF",
            "sechzehntelfinale" => "L32-",
            _ when normiert.Contains("playoff") || normiert.Contains("play-off") => "PO",
            _ => null
        };
        if (bekannt is not null) return bekannt;

        string initialen = string.Concat(name
            .Split([' ', '-', '.'], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0])));
        if (initialen.Length == 0) return null;
        return char.IsDigit(initialen[^1]) ? initialen + "-" : initialen;
    }

    private static string RundenAnzeigeName(int anzahl) => anzahl switch
    {
        1 => "Finale",
        2 => "Halbfinale",
        4 => "Viertelfinale",
        8 => "Achtelfinale",
        16 => "Letzte 32",
        32 => "Letzte 64",
        _ => $"{anzahl} Spiele"
    };

    private static string RundenKuerzel(int anzahl) => anzahl switch
    {
        1 => "F",
        2 => "HF",
        4 => "VF",
        8 => "AF",
        16 => "L32-",
        32 => "L64-",
        _ => $"R{anzahl}-"
    };
}
