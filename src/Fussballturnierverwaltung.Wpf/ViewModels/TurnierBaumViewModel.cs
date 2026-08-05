using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turnierbaum;
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
    public int?   TurnierRundeId { get; init; }
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
    private readonly TurnierBaumService _baumService;
    private readonly TabellenService    _tabellenService;

    private const double NODE_W  = 185.0;
    private const double NODE_H  = 90.0;
    private const double GAP_H   = 40.0;
    private const double GAP_V   = 14.0;
    private const double ROUND_W = NODE_W + GAP_H;   // 225
    private const double UNIT    = NODE_H + GAP_V;   // 88

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeinTurnierGewaehlt))]
    private TurnierInstanz? _ausgewaehlteInstanz;

    public bool KeinTurnierGewaehlt => AusgewaehlteInstanz is null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatBaum))]
    [NotifyPropertyChangedFor(nameof(ZeigtBracketInhalt))]
    private bool _hatBaumIntern;
    public bool HatBaum => HatBaumIntern;

    [ObservableProperty] private bool    _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    [ObservableProperty] private double _canvasBreite;
    [ObservableProperty] private double _canvasHoehe;

    [ObservableProperty] private ObservableCollection<BracketRundenkopf>   _rundenKoepfe      = [];
    [ObservableProperty] private ObservableCollection<BracketKnotenAnzeige> _knoten            = [];
    [ObservableProperty] private ObservableCollection<BracketLinie>         _verbindungsLinien = [];

    // ── Gruppenphase-/Liga-Darstellung (Issue #137, Phase A von #33) ──────────
    // Zeigt, je nach Rundenart der aktuell im Spielplan fokussierten Runde, statt des
    // K.-o.-Brackets Gruppen-Boxen oder eine Gesamttabelle. Unabhängig vom Bracket-Ladepfad
    // (TabellenService statt TurnierBaumService), da Gruppenphase/Liga keinen persistenten
    // Baum benötigen — die Tabelle wird direkt aus den Spielen berechnet.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigtBracket))]
    [NotifyPropertyChangedFor(nameof(ZeigtBracketInhalt))]
    private bool _zeigtGruppenphase;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigtBracket))]
    [NotifyPropertyChangedFor(nameof(ZeigtBracketInhalt))]
    private bool _zeigtLigaTabelle;

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

    // ── Bidirektionale Fokus-Reaktivität (Issue #138, Phase B von #33) ────────
    // Kopplungsmodell (nach manuellem Testdurchlauf überarbeitet): primär an
    // SpielplanVm.AusgewaehlteGruppe (bewusster, klebriger Filter) gekoppelt; nur wenn dort kein
    // Filter aktiv ist, folgt der Fokus automatisch der volatilen SpielplanVm.AusgewaehlteSpiel-
    // Selektion (sekundäre Automatik). Siehe TurnierBereichViewModel.AktualisiereGruppenFokus.
    // Rein clientseitige Filterung der bereits geladenen GruppenTabellenAnzeige, kein erneuter
    // TabellenService-Aufruf nötig — daher synchron statt async.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnzeigeGruppenTabellen))]
    private int? _fokussierteGruppenNummer;

    public IReadOnlyList<GruppenTabelle> AnzeigeGruppenTabellen =>
        FokussierteGruppenNummer is { } nr
            ? GruppenTabellenAnzeige.Where(g => g.GruppenNummer == nr).ToList()
            : GruppenTabellenAnzeige;

    /// <summary>Wird von TurnierBereichViewModel synchron bei jeder Änderung von
    /// SpielplanVm.AusgewaehlteGruppe/AusgewaehlteSpiel aufgerufen (kein DB-Zugriff, daher
    /// unproblematisch als direkter Property-Changed-Handler statt sequenzielles Delegate).</summary>
    public void FokussiereGruppe(int? gruppenNummer) => FokussierteGruppenNummer = gruppenNummer;

    // ── Tabellenstand-Snapshot "Bis Spieltag" (Issue #141) ────────────────────
    // Eigenständig aus TabellenService geladen (kein Cross-VM-Zugriff auf SpielplanVm nötig,
    // da TabelleBerechnenAsync/GruppenTabellenBerechnenAsync bereits einen bisSpieltag-Parameter
    // unterstützen). Die Auswahl ist auf Spieltage mit mindestens einem Ergebnis begrenzt.
    private TurnierRunde? _fokusRunde;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TabellenSpieltagOptionen))]
    [NotifyPropertyChangedFor(nameof(HatVerfuegbareSpieltage))]
    private ObservableCollection<int> _verfuegbareSpieltage = [];

    public IReadOnlyList<int> TabellenSpieltagOptionen =>
        new[] { 0 }.Concat(VerfuegbareSpieltage).ToList();

    /// <summary>Steuert die Sichtbarkeit des Tabellenstand-Snapshot-Dropdowns — ausgeblendet, solange
    /// noch kein Spieltag mindestens ein Ergebnis hat (Dropdown hätte sonst nur "Aktuell" zur Wahl).</summary>
    public bool HatVerfuegbareSpieltage => VerfuegbareSpieltage.Count > 0;

    [ObservableProperty] private int? _tabellenBisSpieltag;

    /// <summary>UI-Wrapper um <see cref="TabellenBisSpieltag"/> für das ComboBox-Binding: 0 = "Aktuell"
    /// (Sentinel statt null, siehe LigaBisSpieltagAuswahl in SpielplanViewModel für dasselbe Muster).</summary>
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

    /// <summary>Wird von TurnierBereichViewModel aufgerufen, sobald die Spieltag-Navigation im
    /// Spielplan links benutzt wird — verhindert, dass eine veraltete historische Tabelle unbemerkt
    /// stehen bleibt, während in der Liste weitergeblättert wird. Kein DB-Zugriff, wenn der Snapshot
    /// bereits auf "Aktuell" steht (CommunityToolkit-Setter überspringt No-Op-Änderungen).</summary>
    public void ZeigeAktuellenStand() => TabellenBisSpieltag = null;

    // Liga-Ansicht-Tabs (Gesamt/Hinrunde/Rückrunde/Heim/Auswärts, Issue #95) — beim Umzug der
    // Liga-Tabelle ins rechte Panel (#141) hierher mitgenommen, damit die Funktionalität erhalten bleibt.
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

    public string  RibbonTabTitel  => "Turnierbaum";
    public ICommand NeuBefehl       => _neuBefehl;
    public ICommand SpeichernBefehl => _speichernBefehl;
    public ICommand AbbrechenBefehl => _abbrechenBefehl;
    public ICommand LoeschenBefehl  => _loeschenBefehl;
    public ICommand KnotenAngeklicktBefehl => _knotenAngeklicktBefehl;
    public ICommand GruppeAngeklicktBefehl => _gruppeAngeklicktBefehl;

    // Issue #163: vormalige Inline-Toolbar (Generieren/Sieger fortschreiben/Baum löschen) als
    // eigene Ribbon-Gruppe — die 4 Standard-CRUD-Slots (NeuBefehl usw. oben) bleiben weiterhin
    // exklusiv an SpielplanViewModel verdrahtet (TurnierBereichViewModel.IRibbonAware), daher
    // hier bewusst eigenständige Buttons statt eine Wiederverwendung dieser Slots.
    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen =>
    [
        new RibbonGruppe
        {
            Header = "Turnierbaum",
            Aktionen =
            [
                new RibbonAktion { Header = "Baum generieren", Befehl = _neuBefehl, IconVektorDatei = "elements_hierarchy.xaml" },
                new RibbonAktion { Header = "Sieger fortschreiben", Befehl = _speichernBefehl, IconVektorDatei = "trophy.xaml" },
                new RibbonAktion { Header = "Baum löschen", Befehl = _loeschenBefehl, IconGlyph = "" },
            ],
        },
    ];

    public event EventHandler<int>? RundeAngeklickt;
    public event EventHandler<int>? GruppeAngeklickt;

    private readonly AsyncRelayCommand                    _neuBefehl;
    private readonly AsyncRelayCommand                    _speichernBefehl;
    private readonly RelayCommand                         _abbrechenBefehl;
    private readonly AsyncRelayCommand                    _loeschenBefehl;
    private readonly RelayCommand<BracketKnotenAnzeige>  _knotenAngeklicktBefehl;
    private readonly RelayCommand<GruppenTabelle>        _gruppeAngeklicktBefehl;

    public TurnierBaumViewModel(TurnierBaumService baumService, TabellenService tabellenService)
    {
        _baumService     = baumService;
        _tabellenService = tabellenService;

        // ZeigtBracket-Gating (Issue #163): die 3 Baum-Aktionen ergeben nur im K.-o.-Bracket Sinn
        // (Liga/Gruppenphase zeigen stattdessen eine Tabelle) — vormals über die Sichtbarkeit der
        // Inline-Toolbar in TurnierBaumView.xaml geregelt, jetzt im Ribbon per CanExecute-Graying
        // statt Ein-/Ausblenden der Buttons selbst (siehe AktualisiereFokusRundeAsync).
        _neuBefehl               = new AsyncRelayCommand(GenerierenAsync,       () => AusgewaehlteInstanz != null && ZeigtBracket);
        _speichernBefehl         = new AsyncRelayCommand(SiegerFortsteiernAsync, () => AusgewaehlteInstanz != null && HatBaum && ZeigtBracket);
        _abbrechenBefehl         = new RelayCommand(() => { }, () => false);
        _loeschenBefehl          = new AsyncRelayCommand(BaumLoeschenAsync,      () => AusgewaehlteInstanz != null && HatBaum && ZeigtBracket);
        _knotenAngeklicktBefehl  = new RelayCommand<BracketKnotenAnzeige>(
            k => { if (k?.TurnierRundeId is { } rundeId) RundeAngeklickt?.Invoke(this, rundeId); });
        _gruppeAngeklicktBefehl  = new RelayCommand<GruppenTabelle>(
            g => { if (g is not null) GruppeAngeklickt?.Invoke(this, g.GruppenNummer); });
        _ligaAnsichtWaehlenBefehl = new RelayCommand<TabellenAnsicht?>(a => AusgewaehlteLigaAnsicht = a ?? TabellenAnsicht.Gesamt);
    }

    public override Task AktivierenAsync() => Task.CompletedTask;

    public async Task LadeInstanzAsync(TurnierInstanz? instanz)
    {
        AusgewaehlteInstanz = instanz;
        await LadeBracketAsync();
    }

    /// <summary>Wird von TurnierBereichViewModel sequenziell nach jedem Rundenwechsel im Spielplan
    /// aufgerufen (nicht als eigenständiges Fire-and-Forget-Event, siehe [[feedback-dbcontext-concurrency]]).
    /// Bei Gruppenphase/Liga/TopfLiga/SchweizerSystem wird statt des K.-o.-Brackets die passende
    /// Tabelle angezeigt; bei K.-o.-Runden (oder keiner Runde) bleibt das Bracket unverändert sichtbar.</summary>
    public async Task AktualisiereFokusRundeAsync(TurnierRunde? runde)
    {
        // Issue #171-Nachbesserung: TabellenNeuLadenAsync (SpielplanViewModel) loest dieses Delegate
        // mittlerweile bei JEDER Ergebnis-/Simulations-Aenderung aus, nicht nur bei echten Runden-
        // wechseln (Issue #137) — ohne diese Unterscheidung sprang ein aktiver Gruppenfokus bei jeder
        // Gruppe/Spieltag-Aktion kurz auf die Gesamtuebersicht zurueck und wieder zurueck (Flackern).
        bool istEchterRundenwechsel = runde?.Id != _fokusRunde?.Id;
        if (istEchterRundenwechsel)
            FokussierteGruppenNummer = null;

        // _fokusRunde erst auf null setzen, dann Snapshot/Ansicht zurücksetzen — die Changed-Handler
        // lösen sonst (wegen _fokusRunde != null) einen überflüssigen Reload für die alte Runde aus,
        // kurz bevor unten ohnehin frisch für die neue Runde geladen wird.
        _fokusRunde = null;
        TabellenBisSpieltag    = null;
        AusgewaehlteLigaAnsicht = TabellenAnsicht.Gesamt;
        VerfuegbareSpieltage   = [];
        _fokusRunde = runde;
        OnPropertyChanged(nameof(ZeigtHinRueckrundenTabs));

        var rundenart = runde?.Rundenart;
        bool istGruppenphase = rundenart == Rundenart.Gruppenphase;
        bool istLigaArtig    = rundenart is Rundenart.Liga or Rundenart.TopfLiga or Rundenart.SchweizerSystem;

        ZeigtGruppenphase = istGruppenphase;
        ZeigtLigaTabelle  = istLigaArtig;
        AktualisiereBefehle();

        if (!istGruppenphase && !istLigaArtig) return;

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

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        // Kein fire-and-forget für AusgewaehlteInstanz — Laden erfolgt sequenziell via LadeInstanzAsync()
    }

    private async Task LadeBracketAsync()
    {
        Fehlermeldung = null;
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
            if (runden.Count == 0) return;

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
                    ? (runde.Knoten.Count == 1 ? "F" : $"{kuerzel}{pos + 1}")
                    : "";
                Knoten.Add(BaueKnotenAnzeige(k, x, centerY - NODE_H / 2, label, turnierRundeId));
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

    private static BracketKnotenAnzeige BaueKnotenAnzeige(TurnierBaumKnoten k, double x, double y, string label, int? turnierRundeId = null)
    {
        if (k.Spiel is null)
            return new BracketKnotenAnzeige { X = x, Y = y, TurnierRundeId = turnierRundeId, IstFreilos = true, Label = label, HeimName = "–", AuswaertsName = "–" };

        var s     = k.Spiel;
        var e     = s.Ergebnis;
        bool hatE = e is not null;

        string detail = hatE
            ? SpielErgebnisFormatierer.DetailText(
                e!.EntscheidungArt,
                e.HeimToreRegulaer, e.AuswaertsToreRegulaer,
                e.HeimToreVerlaengerung, e.AuswaertsToreVerlaengerung,
                e.HeimElfmetertore, e.AuswaertsElfmetertore)
            : "";

        return new BracketKnotenAnzeige
        {
            X                  = x,
            Y                  = y,
            TurnierRundeId     = turnierRundeId ?? s.TurnierRundeId,
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
            await _baumService.SiegerFortsteiernAsync(AusgewaehlteInstanz.Id);
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
