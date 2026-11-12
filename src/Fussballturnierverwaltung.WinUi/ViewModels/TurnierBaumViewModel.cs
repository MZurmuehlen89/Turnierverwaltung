using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>
/// "Turnierbaum"-Bereich des #301-Composite (<see cref="SpielplanUndBaumViewModel"/>) — 1:1 aus
/// Wpf/ViewModels/TurnierBaumViewModel.cs portiert. Verbindungslinien-Zeichnung selbst lebt im
/// Code-Behind der View (Canvas-Overlay, siehe TurnierBaumView.xaml.cs), analog zum WPF-Original.
/// Die Tabellen einer fokussierten Gruppen- oder Ligarunde zeigt das Kind-ViewModel <see cref="Tabellen"/> (#438).
/// #262-Fix (lange Mannschaftsnamen in Tabellenkarten): die Gruppentabellen-/Liga-Tabelle-Karten in
/// der View bekommen ausreichend Breite ohne TextTrimming — die Bracket-Karten selbst behalten die
/// feste 240px-Breite mit Trimming (Zwangslage durch Canvas.Top-Positionierung, keine #262-Stelle).
/// </summary>
public partial class TurnierBaumViewModel : ViewModelBase
{
    private readonly BracketLeser _bracketLeser;

    private const double NODE_H = 148.0;
    private const double GAP_V = 24.0;
    private const double UNIT = NODE_H + GAP_V;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeinTurnierGewaehlt))]
    private TurnierInstanz? _ausgewaehlteInstanz;

    public bool KeinTurnierGewaehlt => AusgewaehlteInstanz is null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigtBracketInhalt))]
    private bool _hatBaum;

    [ObservableProperty] private string? _fehlermeldung;

    [ObservableProperty] private double _fensterCanvasHoehe;

    private readonly List<string> _rundenNamen = [];
    [ObservableProperty] private ObservableCollection<BracketKnotenAnzeige> _knoten = [];

    // ── Gesamtübersicht (Issue #238) ───────────────────────────────────────────

    public const double KOMPAKT_W = 190.0;
    public const double KOMPAKT_H = 52.0;
    private const double GESAMT_GAP_H = 44.0;
    private const double GESAMT_GAP_V = 12.0;
    public const double GESAMT_KOPF_H = 32.0;
    public const double GESAMT_RAND = 16.0;
    /// <summary>Abstand der Kartenbeschriftung (Finale, Spiel um Platz 3) über der Karte.</summary>
    public const double GESAMT_BESCHRIFTUNG_ABSTAND = 22.0;
    private const double GESAMT_UNIT = KOMPAKT_H + GESAMT_GAP_V;

    /// <summary>Umschalter rundenweise ↔ Gesamtübersicht — bewusst nicht gespeichert (Abstimmung zu #238).</summary>
    [ObservableProperty] private bool _istGesamtansicht;

    public double GesamtBreite { get; private set; }
    public double GesamtHoehe { get; private set; }
    public IReadOnlyList<GesamtbaumBeschriftungAnzeige> GesamtBeschriftungen { get; private set; } = [];
    public IReadOnlyList<BracketKnotenAnzeige> GesamtKnoten => Knoten.Where(k => k.IstInGesamtbaum).ToList();

    /// <summary>Die Gesamtübersicht wird im Code-Behind imperativ aufgebaut (Karten, Linien, Beschriftungen) —
    /// dieses Ereignis meldet, dass die Positionen neu berechnet wurden.</summary>
    public event EventHandler? GesamtbaumNeuBerechnet;

    public ICommand GesamtansichtZeigenBefehl { get; }
    public ICommand RundenweiseZeigenBefehl { get; }

    // ── Siegerfeld (#527) ──────────────────────────────────────────────────────

    /// <summary>Höhe des Siegerfelds in der Gesamtübersicht und sein Abstand von dessen Oberkante bis zur Oberkante
    /// der Finalkarte (Siegerfeld, Lücke, Beschriftung „Finale“).</summary>
    public const double SIEGERFELD_H = 88.0;
    public const double SIEGERFELD_UEBER_FINALE = SIEGERFELD_H + 8.0 + GESAMT_BESCHRIFTUNG_ABSTAND;

    /// <summary><c>null</c>, wenn die letzte Runde des Baums mehr als eine Paarung hat (kein Finale).</summary>
    public SiegerfeldAnzeige? Siegerfeld { get; private set; }

    /// <summary>Karte des Finales — das Siegerfeld richtet sich in beiden Ansichten nach ihr.</summary>
    public BracketKnotenAnzeige? FinaleKnoten { get; private set; }

    // ── Rundenweise Navigation (Issue #108/#244) ───────────────────────────────

    // Das Fenster zeigt die Runde AktuellerRundenIndex und — wenn es eine gibt — die folgende.
    [ObservableProperty] private int _aktuellerRundenIndex;

    partial void OnAktuellerRundenIndexChanged(int value) => RundenFensterGeaendert();

    // Alles, was vom Fenster abhängt. Ändert sich das Fenster oder sein Inhalt (anderer Index, andere Knoten,
    // andere Runden), meldet RundenFensterGeaendert diese Eigenschaften — die einzige Stelle dafür.
    private static readonly string[] FENSTER_EIGENSCHAFTEN =
    [
        nameof(LinkeSpalteKnoten), nameof(RechteSpalteKnoten), nameof(LinkeSpalteName), nameof(RechteSpalteName),
        nameof(ZeigtZweiteSpalte), nameof(HatVorherigeRunde), nameof(HatNaechsteRunde),
        nameof(VorherigeRundeName), nameof(NaechsteRundeName)
    ];

    private void RundenFensterGeaendert()
    {
        AktualisiereFensterPositionen();
        foreach (string eigenschaft in FENSTER_EIGENSCHAFTEN)
            OnPropertyChanged(eigenschaft);
        _vorherigeRundeBefehl.NotifyCanExecuteChanged();
        _naechsteRundeBefehl.NotifyCanExecuteChanged();
    }

    private void AktualisiereFensterPositionen()
    {
        var linke = Knoten.Where(k => k.RundenIndex == AktuellerRundenIndex).OrderBy(k => k.Zeile).ToList();
        for (int i = 0; i < linke.Count; i++)
            linke[i].FensterY = i * UNIT + UNIT / 2;

        FensterCanvasHoehe = Math.Max(1, linke.Count) * UNIT + GAP_V;

        if (!ZeigtZweiteSpalte) return;

        var rechte = Knoten.Where(k => k.RundenIndex == FensterEndeIndex).OrderBy(k => k.Zeile).ToList();
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

    private int FensterEndeIndex => Math.Min(AktuellerRundenIndex + 1, Math.Max(0, _rundenNamen.Count - 1));
    private int MaxFensterStartIndex => Math.Max(0, _rundenNamen.Count - 2);

    public bool ZeigtZweiteSpalte => FensterEndeIndex != AktuellerRundenIndex;

    public IReadOnlyList<BracketKnotenAnzeige> LinkeSpalteKnoten =>
        Knoten.Where(k => k.RundenIndex == AktuellerRundenIndex).OrderBy(k => k.Zeile).ToList();

    public IReadOnlyList<BracketKnotenAnzeige> RechteSpalteKnoten =>
        ZeigtZweiteSpalte
            ? Knoten.Where(k => k.RundenIndex == FensterEndeIndex).OrderBy(k => k.Zeile).ToList()
            : [];

    public string LinkeSpalteName =>
        AktuellerRundenIndex >= 0 && AktuellerRundenIndex < _rundenNamen.Count
            ? _rundenNamen[AktuellerRundenIndex]
            : "";

    public string? RechteSpalteName => ZeigtZweiteSpalte ? _rundenNamen[FensterEndeIndex] : null;

    public bool HatVorherigeRunde => AktuellerRundenIndex > 0;
    public bool HatNaechsteRunde => AktuellerRundenIndex < MaxFensterStartIndex;

    public string? VorherigeRundeName => HatVorherigeRunde ? _rundenNamen[AktuellerRundenIndex - 1] : null;
    public string? NaechsteRundeName => HatNaechsteRunde ? _rundenNamen[FensterEndeIndex + 1] : null;

    public ICommand VorherigeRundeBefehl => _vorherigeRundeBefehl;
    public ICommand NaechsteRundeBefehl => _naechsteRundeBefehl;
    private readonly RelayCommand _vorherigeRundeBefehl;
    private readonly RelayCommand _naechsteRundeBefehl;

    // Der Inhalt des Fensters ist neu, auch wenn der Index schon 0 war — dann meldet die Zuweisung nichts.
    private void RundenNavigationZuruecksetzen()
    {
        if (AktuellerRundenIndex != 0)
            AktuellerRundenIndex = 0;
        else
            RundenFensterGeaendert();
    }

    // Nach einem Ergebnis bleibt das Fenster stehen; nur wenn es über die letzte Runde hinausragt, rückt es zurück.
    private void RundenNavigationAktualisieren()
    {
        if (AktuellerRundenIndex > MaxFensterStartIndex)
            AktuellerRundenIndex = MaxFensterStartIndex;
        else
            RundenFensterGeaendert();
    }

    // ── Tabellen der fokussierten Runde ────────────────────────────────────────

    public RundenTabellenViewModel Tabellen { get; }

    public bool ZeigtBracket => !Tabellen.ZeigtTabelle;
    public bool ZeigtBracketInhalt => ZeigtBracket && HatBaum;

    // ── Klick auf eine Karte ──────────────────────────────────────────────────

    public ICommand KnotenAngeklicktBefehl => _knotenAngeklicktBefehl;

    public event EventHandler<int>? RundeAngeklickt;
    public event EventHandler<int>? GruppeAngeklickt;

    private readonly RelayCommand<BracketKnotenAnzeige> _knotenAngeklicktBefehl;

    public TurnierBaumViewModel(BracketLeser bracketLeser, RundenTabellenViewModel tabellen)
    {
        _bracketLeser = bracketLeser;
        Tabellen = tabellen;
        Tabellen.FehlerGemeldet += meldung => Fehlermeldung = meldung;
        Tabellen.GruppeAngeklickt += gruppenNummer => GruppeAngeklickt?.Invoke(this, gruppenNummer);
        Tabellen.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(RundenTabellenViewModel.ZeigtTabelle)) return;
            OnPropertyChanged(nameof(ZeigtBracket));
            OnPropertyChanged(nameof(ZeigtBracketInhalt));
        };

        _knotenAngeklicktBefehl = new RelayCommand<BracketKnotenAnzeige>(k =>
        {
            if (k?.TurnierRundeId is not { } rundeId) return;
            // Issue #238: Die Gesamtübersicht verdeckt den Spielplan — ein Klick kehrt zur rundenweisen Ansicht
            // zurück, damit die angeklickte Runde dort sichtbar wird.
            IstGesamtansicht = false;
            RundeAngeklickt?.Invoke(this, rundeId);
        });
        _vorherigeRundeBefehl = new RelayCommand(() => AktuellerRundenIndex--, () => HatVorherigeRunde);
        _naechsteRundeBefehl = new RelayCommand(() => AktuellerRundenIndex++, () => HatNaechsteRunde);
        GesamtansichtZeigenBefehl = new RelayCommand(() => IstGesamtansicht = true);
        RundenweiseZeigenBefehl = new RelayCommand(() => IstGesamtansicht = false);
    }

    public async Task InstanzSetzenAsync(TurnierInstanz? instanz)
    {
        AusgewaehlteInstanz = instanz;
        await LadeBracketAsync();
    }

    public async Task AktualisiereFokusRundeAsync(TurnierRunde? runde)
    {
        await Tabellen.FokusRundeSetzenAsync(runde);
        if (ZeigtBracket && HatBaum) await AktualisiereBracketErgebnisseAsync();
    }

    private async Task LadeBracketAsync()
    {
        Fehlermeldung = null;
        HatBaum = false;
        Siegerfeld = null;
        FinaleKnoten = null;
        Knoten.Clear();
        _rundenNamen.Clear();
        RundenNavigationZuruecksetzen();

        if (AusgewaehlteInstanz is null) return;

        try
        {
            var runden = await _bracketLeser.BracketLadenAsync(AusgewaehlteInstanz.Id);
            if (runden.Count == 0) return;

            HatBaum = true;
            BerechneBracket(runden, rundenNavigationZuruecksetzen: true);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    private async Task AktualisiereBracketErgebnisseAsync()
    {
        if (AusgewaehlteInstanz is null) return;
        try
        {
            var runden = await _bracketLeser.BracketLadenAsync(AusgewaehlteInstanz.Id);
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
        _rundenNamen.Clear();
        _rundenNamen.AddRange(runden.Select(RundenBezeichnung.Name));
        KnotenAufbauen(runden);
        FinaleKnotenSetzen(runden);

        if (rundenNavigationZuruecksetzen)
            RundenNavigationZuruecksetzen();
        else
            RundenNavigationAktualisieren();

        BerechneGesamtbaum(runden);
    }

    // Das Finale ist die einzige Karte der letzten Runde vor dem Spiel um Platz 3; das Siegerfeld (#527) folgt ihr.
    private void FinaleKnotenSetzen(IReadOnlyList<BracketRunde> runden)
    {
        int finaleIndex = -1;
        for (int ri = 0; ri < runden.Count; ri++)
            if (!runden[ri].IstSpielUmPlatzDrei) finaleIndex = ri;
        var karten = Knoten.Where(k => k.RundenIndex == finaleIndex).ToList();
        FinaleKnoten = karten.Count == 1 ? karten[0] : null;
        Siegerfeld = FinaleKnoten is { } finale ? SiegerfeldAnzeige.Aus(finale) : null;
    }

    private void KnotenAufbauen(IReadOnlyList<BracketRunde> runden)
    {
        var layout = RundenbaumLayout.Berechnen(runden);
        bool mitLabels = RundenBezeichnung.HatFestePaarungen(runden);

        Knoten.Clear();
        for (int ri = 0; ri < runden.Count; ri++)
        {
            foreach (var anzeige in KnotenDerRunde(runden[ri], ri, layout, mitLabels))
                Knoten.Add(anzeige);
        }
    }

    private static IEnumerable<BracketKnotenAnzeige> KnotenDerRunde(
        BracketRunde runde, int rundenIndex, RundenbaumLayoutErgebnis layout, bool mitLabels)
    {
        int? turnierRundeId = runde.Knoten
            .Select(k => k.Spiel?.TurnierRundeId)
            .FirstOrDefault(id => id.HasValue);

        var geordnet = runde.Knoten
            .OrderBy(k => k.Spiel?.Spielnummer ?? int.MaxValue)
            .ToList();

        for (int pos = 0; pos < geordnet.Count; pos++)
        {
            var k = geordnet[pos];
            if (!layout.ZeileJeKnoten.TryGetValue(k.Id, out var zeile)) continue;

            string label = mitLabels ? RundenBezeichnung.KnotenLabel(runde, pos) : "";
            var rueckspiel = k.SpielId is int spielId ? runde.RueckspielJeHinspielId.GetValueOrDefault(spielId) : null;
            yield return BracketKnotenAnzeige.Aus(k, rueckspiel, zeile, rundenIndex, label, turnierRundeId);
        }
    }

    /// <summary>Pixelpositionen der Gesamtübersicht (Issue #238) aus dem gespiegelten Raster von
    /// <see cref="GesamtbaumLayout"/>.</summary>
    private void BerechneGesamtbaum(IReadOnlyList<BracketRunde> runden)
    {
        var layout = GesamtbaumLayout.Berechnen(runden);
        var positionJeKnoten = layout.Positionen.ToDictionary(p => p.KnotenId);

        static double SpaltenX(int spalte) => GESAMT_RAND + spalte * (KOMPAKT_W + GESAMT_GAP_H);
        static double MitteY(double zeile) => GESAMT_KOPF_H + zeile * GESAMT_UNIT;

        foreach (var k in Knoten)
        {
            k.IstInGesamtbaum = positionJeKnoten.TryGetValue(k.Id, out var p);
            if (p is null) continue;
            k.GesamtX = SpaltenX(p.Spalte);
            k.GesamtY = MitteY(p.Zeile) - KOMPAKT_H / 2;
        }

        GesamtBeschriftungen = layout.Beschriftungen
            .Select(b => new GesamtbaumBeschriftungAnzeige(
                SpaltenX(b.Spalte),
                b.Zeile is double z ? MitteY(z) - KOMPAKT_H / 2 - GESAMT_BESCHRIFTUNG_ABSTAND : 4,
                KOMPAKT_W,
                b.Name,
                b.Zeile is not null))
            .ToList();
        GesamtBreite = 2 * GESAMT_RAND + Math.Max(0, layout.SpaltenAnzahl * (KOMPAKT_W + GESAMT_GAP_H) - GESAMT_GAP_H);
        GesamtHoehe = GESAMT_KOPF_H + layout.ZeilenAnzahl * GESAMT_UNIT + GESAMT_RAND;
        PlatzFuerSiegerfeldSchaffen();
        GesamtbaumNeuBerechnet?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Das Siegerfeld steht über dem Finale (#527). Reicht der Platz bis zum oberen Rand nicht (kleine
    /// Bäume), rücken alle Karten samt ihren Beschriftungen nach unten; die Spaltenköpfe bleiben oben.</summary>
    private void PlatzFuerSiegerfeldSchaffen()
    {
        if (Siegerfeld is null || FinaleKnoten is not { IstInGesamtbaum: true } finale) return;

        double versatz = GESAMT_RAND + SIEGERFELD_UEBER_FINALE - finale.GesamtY;
        if (versatz <= 0) return;

        foreach (var k in Knoten.Where(k => k.IstInGesamtbaum))
            k.GesamtY += versatz;
        GesamtBeschriftungen = [.. GesamtBeschriftungen.Select(b => b.GehoertZuKarte ? b with { Y = b.Y + versatz } : b)];
        GesamtHoehe += versatz;
    }
}
