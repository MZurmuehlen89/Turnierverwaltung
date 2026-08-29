using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Wpf.Converters;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

// ── Hilfsklassen für Canvas-Rendering ────────────────────────────────────────

public class BracketKnotenAnzeige
{
    public int    Id { get; init; }
    public double X { get; init; }
    // Globale, auf den gesamten Baum bemessene Position (Blätter des ersten Runde im UNIT-Raster,
    // Elternknoten auf dem Mittelwert ihrer Kinder) — gedacht für die künftige Gesamtübersicht (#238),
    // in der alle Runden gleichzeitig sichtbar sind und ein konsistenter Blattabstand über die ganze
    // Breite gebraucht wird. Für die aktuelle Fenster-Navigation (zwei sichtbare Runden) siehe FensterY.
    public double Y { get; init; }
    // Issue #244-Nachbesserung: rein fensterlokale Position, siehe TurnierBaumViewModel.AktualisiereFensterPositionen —
    // Y wäre hier zu sperrig, weil sie den Abstand aller Blätter des GESAMTEN Baums einrechnet (z.B.
    // Halbfinale+Finale würden dann so weit auseinandergezogen wie im vollen 16er-Baum). FensterY zählt
    // stattdessen nur die aktuell sichtbaren Karten und ordnet sie entsprechend kompakt an. Bewusst
    // mutable (kein init) statt eines neuen Objekts, da dieselbe Instanz zwischen Fenstern wiederverwendet wird.
    public double FensterY { get; set; }
    public int    RundenIndex { get; init; }
    public int?   TurnierRundeId { get; init; }
    // Issue #244: für die Verbindungslinien-Zeichnung im Code-Behind (Overlay-Canvas, misst reale
    // Kartenpositionen statt der veralteten Canvas-Konstanten) — verweist auf die Id des jeweils
    // linken/rechten Kind-Knotens (eine Runde früher), null wenn kein Kind vorhanden (Freilos-Slot).
    public int?   LinkerKindKnotenId { get; init; }
    public int?   RechterKindKnotenId { get; init; }
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
    public string Datum { get; init; } = "";
    public bool HatDatum => !string.IsNullOrEmpty(Datum);
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

// Issue #212: Anzeige einer gruppenübergreifenden Rangfolge-Regel (Issue #184, Uebergangsregel.AnzahlBeste
// gesetzt) — analog zur GruppenTabelle-Karte, aber mit den Kandidaten einer festen Platzierung (z.B. "Platz 3")
// über alle Gruppen der Quellrunde hinweg.
public class UebergreifendeRangfolgeAnzeige
{
    public int Platzierung { get; init; }
    public int AnzahlBeste { get; init; }
    public string ZielRundeName { get; init; } = "";
    public IReadOnlyList<TabellenEintrag> Rangfolge { get; init; } = [];
}

// ── ViewModel ─────────────────────────────────────────────────────────────────

public partial class TurnierBaumViewModel : ViewModelBase, IRibbonAware
{
    private readonly TurnierBaumService     _baumService;
    private readonly TabellenService        _tabellenService;
    private readonly UebergangsregelService _uebergangsregelService;

    // Issue #244-Nachbesserung: NODE_H/GAP_V großzügig genug für die höchste Kartenvariante (Label- +
    // Datum-Kopfzeile + zwei Team-Zeilen, real ca. 128px) bemessen — Y wird jetzt direkt als Canvas.Top
    // gebunden (TurnierBaumView.xaml, kein automatisches Stapeln mehr), daher muss der reservierte
    // Platz pro Knoten in jedem Fall reichen, auch wenn kürzere Karten dadurch etwas Leerraum darunter
    // haben. NODE_W an die jetzt feste Kartenbreite (BracketKartenTemplate) angeglichen.
    private const double NODE_W  = 240.0;
    private const double NODE_H  = 148.0;
    private const double GAP_H   = 40.0;
    private const double GAP_V   = 24.0;
    private const double ROUND_W = NODE_W + GAP_H;   // 280
    private const double UNIT    = NODE_H + GAP_V;   // 172

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

    // Issue #244-Nachbesserung: Höhe, die die Fenster-Navigation (siehe unten) tatsächlich braucht — nur
    // anhand der aktuell sichtbaren Kartenanzahl, nicht anhand des ganzen Baums wie CanvasHoehe. Verhindert
    // unnötigen Scrollraum/-balken bei kleinen Fenstern (z.B. Halbfinale+Finale).
    [ObservableProperty] private double _fensterCanvasHoehe;

    [ObservableProperty] private ObservableCollection<BracketRundenkopf>   _rundenKoepfe      = [];
    [ObservableProperty] private ObservableCollection<BracketKnotenAnzeige> _knoten            = [];
    [ObservableProperty] private ObservableCollection<BracketLinie>         _verbindungsLinien = [];

    // ── Rundenweise Navigation (Issue #108, an kicker.de-Vorbild angelehnt) ───
    // Statt des gesamten Baums in einem scrollbaren Canvas wird ein Fenster von zwei
    // benachbarten Runden (aktuelle + nächste) als Kartenspalten angezeigt, mit Pfeilen zur
    // Nachbarrunde. Beim Finale (letztes Fenster) rutscht das Fenster auf Halbfinale+Finale,
    // da es kein "danach" mehr gibt — kein Sonderfall nötig, ergibt sich aus der Obergrenze.
    // Der bestehende Canvas/Linien-Rechenweg (X/Y, VerbindungsLinien) bleibt für eine spätere
    // Gesamtübersicht (separates Folge-Issue #238) erhalten, wird hier aber nicht mehr gebunden.
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

    /// <summary>Berechnet für die aktuell sichtbaren zwei Spalten eine rein fensterlokale, kompakte
    /// vertikale Anordnung (FensterY) statt der globalen, auf den gesamten Baum bemessenen Y — sonst
    /// wirkt z.B. ein Halbfinale+Finale-Fenster (nur 3 Karten) unnötig sperrig/leer, weil die globale
    /// Position auf den Blattabstand des ganzen Turnierbaums ausgelegt ist (Nutzer-Feedback). Die linke
    /// Spalte bekommt einfach durchnummerierte UNIT-Slots, die rechte Spalte den Mittelwert der FensterY
    /// ihrer sichtbaren Kind-Knoten (Fallback: Slot-Nummer, falls beide Kinder außerhalb des Fensters
    /// lägen — kommt bei der aktuellen Fenster-Definition [aktuelle+nächste Runde] nicht vor, jedes
    /// Kind eines sichtbaren Eltern-Knotens liegt immer in der aktuell sichtbaren linken Spalte).</summary>
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

    /// <summary>Index der zweiten (rechten) Fensterspalte — identisch zum Startindex, wenn nur eine
    /// Runde existiert (reines Finale-Bracket).</summary>
    private int FensterEndeIndex => Math.Min(AktuellerRundenIndex + 1, Math.Max(0, RundenKoepfe.Count - 1));

    /// <summary>Höchster gültiger Fenster-Startindex, sodass rechts immer eine zweite Spalte Platz hat
    /// (Ausnahme: nur eine Runde insgesamt).</summary>
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
    public bool HatNaechsteRunde  => AktuellerRundenIndex < MaxFensterStartIndex;

    public string? VorherigeRundeName => HatVorherigeRunde ? RundenKoepfe[AktuellerRundenIndex - 1].Name : null;
    public string? NaechsteRundeName  => HatNaechsteRunde  ? RundenKoepfe[FensterEndeIndex + 1].Name : null;

    public ICommand VorherigeRundeBefehl => _vorherigeRundeBefehl;
    public ICommand NaechsteRundeBefehl  => _naechsteRundeBefehl;
    private readonly RelayCommand _vorherigeRundeBefehl;
    private readonly RelayCommand _naechsteRundeBefehl;

    /// <summary>Setzt die Rundennavigation auf das erste Fenster zurück und erzwingt die
    /// Benachrichtigung der abgeleiteten Properties — der CommunityToolkit-Setter überspringt
    /// sonst die Notification, wenn der Index bereits 0 war (z.B. nach Neuladen desselben Baums).</summary>
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

    /// <summary>Aktualisiert die Rundennavigation nach einem reinen Ergebnis-Refresh (Bracket-Struktur
    /// unverändert) — hält den Index, klemmt ihn nur bei Bedarf auf den gültigen Bereich, und
    /// benachrichtigt beide Spalten immer neu, da sich die Knoten-Instanzen (Score, Sieger) geändert
    /// haben, auch wenn der Index gleich bleibt.</summary>
    private void RundenNavigationAktualisieren()
    {
        if (AktuellerRundenIndex > MaxFensterStartIndex)
        {
            AktuellerRundenIndex = MaxFensterStartIndex; // löst über NotifyPropertyChangedFor bereits alle Notifications aus
            return;
        }
        AktualisiereFensterPositionen();
        OnPropertyChanged(nameof(LinkeSpalteKnoten));
        OnPropertyChanged(nameof(RechteSpalteKnoten));
    }

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

    // Issue #212: gruppenübergreifende Rangfolge-Regeln (Issue #184) der fokussierten Gruppenphase-Runde —
    // zusätzlich zu den Gruppen-Karten oben, eine Karte je Regel mit AnzahlBeste gesetzt.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatUebergreifendeRangfolgenAnzeige))]
    private ObservableCollection<UebergreifendeRangfolgeAnzeige> _uebergreifendeRangfolgenAnzeige = [];
    public bool HatUebergreifendeRangfolgenAnzeige => UebergreifendeRangfolgenAnzeige.Count > 0;

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

    /// <summary>Lädt die Kandidaten-Rangfolge je gruppenübergreifender Übergangsregel (Issue #184) der
    /// fokussierten Quellrunde. Kein bisSpieltag-Snapshot (Issue #141) — <see cref="TabellenService.UebergreifendeRangfolgeBerechnenAsync"/>
    /// unterstützt das nicht, analog zur bewusst konservativen Gesamtrunden-Gate-Logik in
    /// <c>SpielplanService.BesteNAufloesenAsync</c> (keine historische Ansicht für diesen Sonderfall nötig).
    /// Die Qualifikationsstatus-Ampel wird erst gesetzt, wenn <see cref="UebergangsregelService.QualifikantenErmittelnAsync"/>
    /// für ALLE AnzahlBeste-Plätze ein Ergebnis liefert (Quellrunde vollständig abgeschlossen) — bis dahin bleibt
    /// sie auf "Offen", da ein optimistischer Cross-Group-Vorgriff wie bei #202/#206 hier bewusst nicht
    /// implementiert ist.</summary>
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
                Platzierung   = regel.PlatzierungVon,
                AnzahlBeste   = regel.AnzahlBeste!.Value,
                ZielRundeName = regel.ZielRunde.Name,
                Rangfolge     = rangfolge
            });
        }

        UebergreifendeRangfolgenAnzeige = new ObservableCollection<UebergreifendeRangfolgeAnzeige>(anzeigen);
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

    public TurnierBaumViewModel(TurnierBaumService baumService, TabellenService tabellenService, UebergangsregelService uebergangsregelService)
    {
        _baumService             = baumService;
        _tabellenService         = tabellenService;
        _uebergangsregelService  = uebergangsregelService;

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
        _vorherigeRundeBefehl    = new RelayCommand(() => AktuellerRundenIndex--, () => HatVorherigeRunde);
        _naechsteRundeBefehl     = new RelayCommand(() => AktuellerRundenIndex++, () => HatNaechsteRunde);
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

        if (!istGruppenphase && !istLigaArtig)
        {
            // K.-o.-Bracket: Ergebnisse neu laden, damit Simulationen (Einzelspiel oder Batch, Issue #108-
            // Nachbesserung) sich sofort in den Bracket-Karten (Score, Sieger-Hervorhebung) niederschlagen —
            // ohne die Rundennavigation zurückzusetzen, siehe AktualisiereBracketErgebnisseAsync.
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
        RundenNavigationZuruecksetzen();
        AktualisiereBefehle();

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
            AktualisiereBefehle();
        }
    }

    /// <summary>Lädt die Bracket-Knoten neu (Score, Sieger-Hervorhebung) nach einer Ergebnis-/
    /// Simulationsänderung, ohne die aktuell betrachtete Runde umzuschalten — im Unterschied zu
    /// <see cref="LadeBracketAsync"/>, das für einen echten Neu-/Erstladevorgang gedacht ist.</summary>
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

        // Issue #241: LinkerKindKnotenId/RechterKindKnotenId kommen jetzt zuverlässig aus der DB —
        // TurnierBaumService.GenerierenAsync setzt sie entweder aus der echten Spiel.HeimQuellSpielId-
        // Verknüpfung (festes K.o.-Bracket) oder aus dem sequentiellen Fallback (freie Auslosung mit
        // direkt bekannten Teams). Kein Text-Raten mehr nötig hier.
        var effLinks  = alleKnoten.ToDictionary(kv => kv.Key, kv => kv.Value.LinkerKindKnotenId);
        var effRechts = alleKnoten.ToDictionary(kv => kv.Key, kv => kv.Value.RechterKindKnotenId);

        // "VF1"/"AF3"-Kürzel-Labels ergeben nur bei einem echten, platzhalterbasierten K.o.-Bracket Sinn,
        // nicht bei frei ausgelosten Runden mit direkt bekannten Teams (keine Labels dort).
        bool istFest = runden.Skip(1).Any(r => r.Knoten.Any(k =>
            k.Spiel is not null && (k.Spiel.HeimPlatzhalterName is not null || k.Spiel.AuswaertsPlatzhalterName is not null)));

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
                Knoten.Add(BaueKnotenAnzeige(k, x, centerY - NODE_H / 2, runde.RundeNummer - 1, label,
                    effLinks.GetValueOrDefault(k.Id), effRechts.GetValueOrDefault(k.Id), turnierRundeId));
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

    private static BracketKnotenAnzeige BaueKnotenAnzeige(TurnierBaumKnoten k, double x, double y, int rundenIndex, string label,
        int? linkerKindKnotenId, int? rechterKindKnotenId, int? turnierRundeId = null)
    {
        if (k.Spiel is null)
            return new BracketKnotenAnzeige { Id = k.Id, X = x, Y = y, RundenIndex = rundenIndex, TurnierRundeId = turnierRundeId, LinkerKindKnotenId = linkerKindKnotenId, RechterKindKnotenId = rechterKindKnotenId, IstFreilos = true, Label = label, HeimName = "–", AuswaertsName = "–" };

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
            Id                 = k.Id,
            X                  = x,
            Y                  = y,
            RundenIndex        = rundenIndex,
            TurnierRundeId     = turnierRundeId ?? s.TurnierRundeId,
            LinkerKindKnotenId = linkerKindKnotenId,
            RechterKindKnotenId = rechterKindKnotenId,
            IstFreilos         = false,
            Label              = label,
            HeimName           = s.HeimMannschaft?.Name      ?? s.HeimPlatzhalterName      ?? "—",
            AuswaertsName      = s.AuswaertsMannschaft?.Name ?? s.AuswaertsPlatzhalterName ?? "—",
            HeimWappen         = MannschaftWappenErmittlung.Bytes(s.HeimMannschaft),
            AuswaertsWappen    = MannschaftWappenErmittlung.Bytes(s.AuswaertsMannschaft),
            HatErgebnis        = hatE,
            HeimScore          = hatE ? e!.HeimToreGesamt.ToString()      : "",
            AuswaertsScore     = hatE ? e!.AuswaertsToreGesamt.ToString() : "",
            ErgebnisDetail     = detail,
            HeimIstSieger      = hatE && e!.SiegerMannschaftId == s.HeimMannschaftId,
            AuswaertsIstSieger = hatE && e!.SiegerMannschaftId == s.AuswaertsMannschaftId,
            Datum              = s.Datum?.ToString("ddd, dd.MM.yy", CultureInfo.GetCultureInfo("de-DE")) ?? ""
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
            RundenNavigationZuruecksetzen();
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
