using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

// ── Darstellungstypen — 1:1 aus Wpf/ViewModels/AuslosungViewModel.cs portiert, ohne TV-Modus (#303-Backlog)
// und ohne Auslosungsart.Manuell (bleibt beim bestehenden VorbelegungViewModel, #297) ──────────────────────

public class LostopfViewModel : ObservableObject
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public int Reihenfolge { get; init; }
    public ObservableCollection<TeilnehmerAnzeige> Teilnehmer { get; init; } = [];
}

public record ErgebnisAnzeige(int PaarungsNummer, string HeimName, string AuswaertsName, bool IstFreilos,
    byte[]? HeimWappen = null, byte[]? AuswaertsWappen = null);
public record GruppenTeamAnzeige(string Name, byte[]? Wappen);
public record GruppenErgebnisAnzeige(int GruppenNummer, string GruppenName, IReadOnlyList<GruppenTeamAnzeige> Teams);

public class RegelViewModel : ObservableObject
{
    private readonly Func<bool, Task> _callback;
    private bool _istAktiv;
    private bool _istRedundant;

    public AuslosungsRegelArt Regelart { get; init; }
    public string Anzeige { get; init; } = "";
    public string Beschreibung { get; init; } = "";
    public string RedundanzHinweis { get; set; } = "";

    public bool IstAktiv
    {
        get => _istAktiv;
        set { if (SetProperty(ref _istAktiv, value)) _ = _callback(value); }
    }

    public bool IstRedundant
    {
        get => _istRedundant;
        set => SetProperty(ref _istRedundant, value);
    }

    public RegelViewModel(AuslosungsRegelArt art, string anzeige, string beschreibung, bool initial, Func<bool, Task> callback)
    {
        Regelart = art; Anzeige = anzeige; Beschreibung = beschreibung; _istAktiv = initial; _callback = callback;
    }
}

public class VerbandsQuoteAnzeigeViewModel
{
    public string VerbandName { get; init; } = "";
    public int Aktuell { get; init; }
    public int Anzahl { get; init; }

    public bool UeberAnzahl => Aktuell > Anzahl;
    public bool UnterAnzahl => Aktuell < Anzahl;
    public string AnzeigeText => $"{Aktuell} / {Anzahl}";
}

public record AuslosungsartOption(string Anzeige, Auslosungsart Wert);
public record RanglisteOption(string Anzeige, Rangliste? Wert);

// ── TV-Modus: Live-Ziehungsanzeige (#303, Port aus Wpf/ViewModels/AuslosungViewModel.cs, Issue #5) ──
// WinUI3 kennt kein Style.Triggers/DataTrigger (siehe WinUi/CLAUDE.md-Fallstricke) — das WPF-Original graut
// gezogene/unbefüllte Slots per DataTrigger auf den Foreground aus, hier stattdessen direkt gebundene
// *Opacity-Properties (Precedent: SpielberichtControl aus #302).

public class AnimationsGruppenSlot : ObservableObject
{
    public int Position { get; init; }
    private string _anzeige = "…";
    public string Anzeige
    {
        get => _anzeige;
        set { if (SetProperty(ref _anzeige, value)) { OnPropertyChanged(nameof(Befuellt)); OnPropertyChanged(nameof(BefuelltOpacity)); } }
    }
    public bool Befuellt => _anzeige != "…";
    public double BefuelltOpacity => Befuellt ? 1.0 : 0.4;
    private byte[]? _wappen;
    public byte[]? Wappen { get => _wappen; set => SetProperty(ref _wappen, value); }
}

public class AnimationsGruppe : ObservableObject
{
    public int GruppenNummer { get; init; }
    public string GruppenName { get; init; } = "";
    public ObservableCollection<AnimationsGruppenSlot> Slots { get; init; } = [];
}

public class AnimationsPaarung : ObservableObject
{
    public int PaarungsNr { get; init; }
    public bool IstFreilos { get; init; }
    private string _heim = "…";
    public string Heim
    {
        get => _heim;
        set { if (SetProperty(ref _heim, value)) { OnPropertyChanged(nameof(HeimBefuellt)); OnPropertyChanged(nameof(HeimBefuelltOpacity)); } }
    }
    public bool HeimBefuellt => _heim != "…";
    public double HeimBefuelltOpacity => HeimBefuellt ? 1.0 : 0.4;
    private byte[]? _heimWappen;
    public byte[]? HeimWappen { get => _heimWappen; set => SetProperty(ref _heimWappen, value); }
    private string _auswaerts = "…";
    public string Auswaerts
    {
        get => _auswaerts;
        set { if (SetProperty(ref _auswaerts, value)) { OnPropertyChanged(nameof(AuswaertsBefuellt)); OnPropertyChanged(nameof(AuswaertsBefuelltOpacity)); } }
    }
    public bool AuswaertsBefuellt => _auswaerts != "…";
    public double AuswaertsBefuelltOpacity => AuswaertsBefuellt ? 1.0 : 0.4;
    private byte[]? _auswaertsWappen;
    public byte[]? AuswaertsWappen { get => _auswaertsWappen; set => SetProperty(ref _auswaertsWappen, value); }
}

/// <summary>Feste Anzahl Topf-Plätze für die TV-Modus-Anzeige: die Slots werden nie hinzugefügt oder
/// entfernt, damit sich das Layout nicht verschiebt, sobald ein Topf leerer wird — nur Gezogen/Anzeige
/// ändern sich pro Slot.</summary>
public class AnimationsTopfSlot : ObservableObject
{
    public int EintragId { get; init; }
    public string Name { get; init; } = "";
    public byte[]? Wappen { get; init; }
    private bool _gezogen;
    public bool Gezogen
    {
        get => _gezogen;
        set { if (SetProperty(ref _gezogen, value)) { OnPropertyChanged(nameof(Anzeige)); OnPropertyChanged(nameof(GezogenOpacity)); } }
    }
    public string Anzeige => Gezogen ? "…" : Name;
    public double GezogenOpacity => Gezogen ? 0.4 : 1.0;
}

public class AnimationsTopf
{
    public int TopfId { get; init; }
    public string Name { get; init; } = "";
    public ObservableCollection<AnimationsTopfSlot> Slots { get; init; } = [];
}

/// <summary>
/// "Auslosung"-Tab der Turnierdurchführung (#300, ersetzt den Platzhalter aus #299) — Master-Detail analog
/// Wpf/ViewModels/AuslosungViewModel.cs: Auslosungsliste/Neu-Formular + Workspace (Lostöpfe, Teilnehmerfilter,
/// Verbandsquoten-Spalte, Regeln, Ranglisten-Automatik). Bewusst OHNE TV-Modus/Animation (eigenständiges
/// Backlog-Issue #303) und OHNE <see cref="Auslosungsart.Manuell"/> (bleibt beim bestehenden
/// <see cref="VorbelegungViewModel"/>, #297 — keine Dopplung).
///
/// Anders als das WPF-Original hält dieses ViewModel KEINE eigene Instanzliste/-auswahl — die geteilte
/// Instanzauswahl kommt von <see cref="TurnierdurchfuehrungViewModel"/> über <see cref="InstanzSetzenAsync"/>
/// (Precedent: <see cref="VorbelegungViewModel.InstanzSetzenAsync"/>), analog zum <see cref="TurnierTabKoordinator"/>-
/// Muster aus #297. Bewusst auf Turniere mit Status ≠ <see cref="TurnierStatus.Vorlage"/> beschränkt (siehe
/// <see cref="KannNeu"/>) — anders als Vorbelegung zieht die echte Auslosung primär gegen reale Mannschaften.
/// </summary>
public partial class AuslosungViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly AuslosungService _auslosungService;
    private readonly TurnierInstanzService _instanzService;
    private readonly MannschaftService _mannschaftService;
    private readonly SpielplanService _spielplanService;
    private readonly TurnierBaumService _baumService;
    private readonly RanglisteService _ranglisteService;
    private readonly VerbandsQuoteService _verbandsQuoteService;
    private readonly UebergangsregelService _uebergangsregelService;
    private readonly IDialogService _dialogService;

    private TurnierInstanz? _instanz;
    private Auslosung? _auslosungDetail;
    private bool _ladenAktiv;
    private bool _seitenlisteAktualisierungAktiv;

    // ── Geteilter Instanz-Kontext (siehe Klassendoku) ────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NeuCommand))]
    private bool _keinTurnierGewaehlt = true;

    [ObservableProperty] private ObservableCollection<Auslosung> _auslosungen = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private Auslosung? _ausgewaehlteAuslosung;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeuRundenOptionen))]
    private ObservableCollection<TurnierRunde> _instanzRunden = [];

    /// <summary>Runden mit PaarungFestVorgegeben=true werden im Vorbelegung-Tab verwaltet, nicht hier. Runden
    /// mit bereits vorhandener Auslosung (auch einer im Vorbelegung-Tab abgeschlossenen, hier unsichtbaren
    /// Manuell-Auslosung — siehe <see cref="_belegteRundenIds"/>) werden ebenfalls ausgeblendet: sonst könnte
    /// eine zweite Auslosung für dieselbe Runde angelegt werden, was den Vorbelegung-Tab beim
    /// ToDictionary(a => a.TurnierRundeId)-Aufbau mit einer ArgumentException abstürzen lässt (Issue #320).</summary>
    public IEnumerable<TurnierRunde> NeuRundenOptionen =>
        InstanzRunden.Where(r => !r.PaarungFestVorgegeben && !_belegteRundenIds.Contains(r.Id));

    /// <summary>TurnierRundeId aller Auslosungen der Instanz — inkl. der hier absichtlich ausgeblendeten
    /// Auslosungsart.Manuell (siehe <see cref="LadeAuslosungenGefiltertAsync"/>), damit
    /// <see cref="NeuRundenOptionen"/> auch solche Runden zuverlässig ausschließt (Issue #320).</summary>
    private HashSet<int> _belegteRundenIds = [];

    // ── Neue-Auslosung-Formular ────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigtHinweis))]
    [NotifyCanExecuteChangedFor(nameof(NeuCommand))]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private bool _neuFormAktiv;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private string _neuName = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AuslosungsartHinweis))]
    [NotifyPropertyChangedFor(nameof(AuslosungsartWarnung))]
    [NotifyPropertyChangedFor(nameof(NeuAnzahlLostoepfeBearbeitbar))]
    [NotifyPropertyChangedFor(nameof(NeuAuslosungsartAusgewaehlt))]
    private Auslosungsart _neuAuslosungsart = Auslosungsart.Zufall;

    /// <summary>Wrapper für die ComboBox-Bindung: <c>SelectedItem</c> statt <c>SelectedValue</c>/
    /// <c>SelectedValuePath</c> — Letzteres zeigte in #300-Tests wiederholt einen WinUI3-Fallstrick (Auswahl
    /// erschien nach Reload/Sichtbarkeitswechsel leer, obwohl der zugrunde liegende Wert korrekt war), auch
    /// nach gezielter Nachbenachrichtigung. <c>SelectedItem</c> gegen den Options-Record (Werte-Gleichheit
    /// dank <c>record</c>) ist der im Rest der App bereits bewährte, robustere Mechanismus.</summary>
    public AuslosungsartOption? NeuAuslosungsartAusgewaehlt
    {
        get => NeuAuslosungsartOptionen.FirstOrDefault(o => o.Wert == NeuAuslosungsart);
        set { if (value is not null) NeuAuslosungsart = value.Wert; }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyPropertyChangedFor(nameof(AuslosungsartHinweis))]
    [NotifyPropertyChangedFor(nameof(AuslosungsartWarnung))]
    private TurnierRunde? _neuRunde;

    /// <summary>Gecachte Optionsliste statt einer bei jedem Binding-Zugriff neu berechneten Liste — eine
    /// dauerhaft neue Listeninstanz pro Lesezugriff ist ein bekannter WinUI3-ComboBox-Fallstrick
    /// (siehe WinUi/CLAUDE.md, "ItemsSource pro Fokus neu berechnet"): kombiniert mit
    /// <c>SelectedValuePath</c> kann das zu einem transienten Reset führen, der die eigentlich korrekte
    /// Auswahl visuell verwaist zurücklässt (Fund aus #300-Test — "Auslosungsart erscheint leer trotz
    /// vorheriger Auswahl"). Wird explizit in <see cref="OnNeuRundeChanged"/> aktualisiert.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeuAuslosungsartAusgewaehlt))]
    private IReadOnlyList<AuslosungsartOption> _neuAuslosungsartOptionen = [];

    private string _autoNeuName = "";

    [ObservableProperty] private int _neuAnzahlLostoepfe = 1;

    // ── Auslosung-Detail ──────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigtHinweis))]
    [NotifyCanExecuteChangedFor(nameof(AuslosungAusfuehrenCommand))]
    [NotifyCanExecuteChangedFor(nameof(AuslosungZuruecksetzenCommand))]
    [NotifyCanExecuteChangedFor(nameof(LostoepfeAutomatischBefuellenCommand))]
    [NotifyCanExecuteChangedFor(nameof(AlleToepfeZuruecksetzenCommand))]
    private bool _detailAktiv;

    /// <summary>Platzhalter-Hinweistext ("Turnier und Runde wählen …") — nur solange weder Neu-Formular
    /// noch Detail-Workspace aktiv sind, damit sich nichts überlappt.</summary>
    public bool ZeigtHinweis => !NeuFormAktiv && !DetailAktiv;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AuslosungAusfuehrenCommand))]
    [NotifyCanExecuteChangedFor(nameof(LostoepfeAutomatischBefuellenCommand))]
    [NotifyCanExecuteChangedFor(nameof(AlleToepfeZuruecksetzenCommand))]
    private bool _istBearbeitbar;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailAuslosungsartHinweis))]
    [NotifyPropertyChangedFor(nameof(DetailAuslosungsartWarnung))]
    [NotifyPropertyChangedFor(nameof(DetailAnzahlLostoepfeBearbeitbar))]
    [NotifyPropertyChangedFor(nameof(DetailAuslosungsartAusgewaehlt))]
    [NotifyCanExecuteChangedFor(nameof(AuslosungAusfuehrenCommand))]
    private Auslosungsart _detailAuslosungsart;

    /// <summary>Gecacht, siehe <see cref="NeuAuslosungsartOptionen"/> — aktualisiert in <see cref="LadeDetailAsync"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailAuslosungsartAusgewaehlt))]
    private IReadOnlyList<AuslosungsartOption> _detailAuslosungsartOptionen = [];

    /// <summary>Wrapper für die ComboBox-Bindung, siehe <see cref="NeuAuslosungsartAusgewaehlt"/>.</summary>
    public AuslosungsartOption? DetailAuslosungsartAusgewaehlt
    {
        get => DetailAuslosungsartOptionen.FirstOrDefault(o => o.Wert == DetailAuslosungsart);
        set { if (value is not null) DetailAuslosungsart = value.Wert; }
    }

    [ObservableProperty] private string _detailName = "";
    [ObservableProperty] private int _detailAnzahlLostoepfe = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    private bool _istDetailDirty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstPaarungsergebnis))]
    [NotifyPropertyChangedFor(nameof(ZeigeErgebnis))]
    [NotifyCanExecuteChangedFor(nameof(AuslosungZuruecksetzenCommand))]
    private bool _istAbgeschlossen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstPaarungsergebnis))]
    private bool _istGruppenergebnis;

    public bool IstPaarungsergebnis => IstAbgeschlossen && !IstGruppenergebnis;

    [ObservableProperty] private string _auslosungKopfzeile = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerfuegbareTeilnehmerGefiltert))]
    private ObservableCollection<TeilnehmerAnzeige> _verfuegbareTeilnehmer = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerfuegbareTeilnehmerGefiltert))]
    private string _teilnehmerFilter = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AlleToepfeZuruecksetzenCommand))]
    private ObservableCollection<LostopfViewModel> _lostoepfe = [];

    // ── Automatische Lostopf-Zuordnung via Rangliste (Issue #119) ────────────────
    private static readonly RanglisteOption _keineRangliste = new("— Keine —", null);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RanglisteOptionen))]
    private ObservableCollection<Rangliste> _alleRanglisten = [];

    [ObservableProperty] private RanglisteOption _ausgewaehlteRanglisteOption = _keineRangliste;

    public IReadOnlyList<RanglisteOption> RanglisteOptionen =>
        [_keineRangliste, .. AlleRanglisten.Select(r => new RanglisteOption(r.Name, r))];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private ObservableCollection<RegelViewModel> _regeln = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigeVerbandsQuoten))]
    private ObservableCollection<VerbandsQuoteAnzeigeViewModel> _verbandsQuotenAnzeige = [];

    public bool ZeigeVerbandsQuoten => VerbandsQuotenAnzeige.Count > 0;

    [ObservableProperty] private ObservableCollection<ErgebnisAnzeige> _ergebnisseAnzeige = [];
    [ObservableProperty] private ObservableCollection<GruppenErgebnisAnzeige> _gruppenErgebnisse = [];

    // ── TV-Modus: Schrittweise Ziehungsanimation (#303, Issue #5) ────────────────
    [ObservableProperty] private bool _tvModusAktiv;
    [ObservableProperty] private bool _tvModusManuell;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimationLaeuftNicht))]
    [NotifyPropertyChangedFor(nameof(ZeigeErgebnis))]
    [NotifyCanExecuteChangedFor(nameof(AuslosungZuruecksetzenCommand))]
    [NotifyCanExecuteChangedFor(nameof(AnimationUeberspringenCommand))]
    private bool _animationLaeuft;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnimationWeiterCommand))]
    private bool _animationWartetAufWeiter;
    [ObservableProperty] private string? _animationBannerMannschaft;
    [ObservableProperty] private string? _animationBannerTopf;
    [ObservableProperty] private string? _animationBannerZiel;
    [ObservableProperty] private string? _animationBannerSlot;
    [ObservableProperty] private string? _animationBannerPosition;
    [ObservableProperty] private ObservableCollection<AnimationsTopf> _animationToepfe = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimationIstGruppenmodus))]
    private ObservableCollection<AnimationsGruppe> _animationGruppen = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimationIstPaarungsmodus))]
    private ObservableCollection<AnimationsPaarung> _animationPaarungen = [];

    public bool AnimationIstGruppenmodus  => AnimationGruppen.Count > 0;
    public bool AnimationIstPaarungsmodus => AnimationPaarungen.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimationFortschrittText))]
    private int _animationSchrittIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimationFortschrittText))]
    private int _animationSchrittGesamt;

    public bool AnimationLaeuftNicht => !AnimationLaeuft;
    public string AnimationFortschrittText => $"{AnimationSchrittIndex} / {AnimationSchrittGesamt}";

    /// <summary>Ergebnis-Panel bleibt während der Ziehungsanimation verborgen — das bereits von
    /// LadeDetailAsync geladene Endergebnis wird erst nach Animationsende (bzw. bei "Überspringen" sofort)
    /// sichtbar.</summary>
    public bool ZeigeErgebnis => IstAbgeschlossen && AnimationLaeuftNicht;

    private bool _animationUeberspringen;
    private TaskCompletionSource _weiterSignal = new();
    private static readonly TimeSpan _tvModusSchrittVerzoegerung = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan _tvModusPausenVerzoegerung  = TimeSpan.FromMilliseconds(500);

    /// <summary>Ein einzelner Ziehungsschritt für den TV-Modus: eine Mannschaft aus einem Lostopf, die einem
    /// bereits feststehenden Ziel (Gruppe/Paarung) zugeordnet ist. Das Endergebnis steht durch AusfuehrenAsync
    /// schon fest (holistischer Zuordnungsalgorithmus) — hier wird nur die Reihenfolge/Dramaturgie der
    /// Enthüllung bestimmt.</summary>
    private sealed record ZiehungsSchritt(
        TeilnehmerAnzeige Team, int TopfId, string TopfName, int ZielNummer,
        bool IstGruppenmodus, bool? IstHeim, int? Position, bool IstFreilos);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TeamsStatusText))]
    [NotifyPropertyChangedFor(nameof(TeamsVollstaendig))]
    private int _teamsZugewiesen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TeamsStatusText))]
    [NotifyPropertyChangedFor(nameof(TeamsVollstaendig))]
    private int _teamsBenoetigt;

    public bool TeamsVollstaendig => TeamsBenoetigt > 0 && TeamsZugewiesen == TeamsBenoetigt;

    public string TeamsStatusText => TeamsBenoetigt == 0
        ? $"{TeamsZugewiesen} Teams zugewiesen"
        : TeamsZugewiesen == TeamsBenoetigt
            ? $"✓  Alle {TeamsBenoetigt} Teams zugewiesen"
            : TeamsZugewiesen > TeamsBenoetigt
                ? $"⚠  {TeamsZugewiesen} Teams zugewiesen — {TeamsZugewiesen - TeamsBenoetigt} zu viel (Runde erwartet {TeamsBenoetigt})"
                : $"{TeamsZugewiesen} / {TeamsBenoetigt} Teams zugewiesen — noch {TeamsBenoetigt - TeamsZugewiesen} fehlen";

    // ── Status ────────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    // ── Berechnete Eigenschaften ─────────────────────────────────────────────
    public IEnumerable<TeilnehmerAnzeige> VerfuegbareTeilnehmerGefiltert =>
        string.IsNullOrWhiteSpace(TeilnehmerFilter)
            ? VerfuegbareTeilnehmer
            : VerfuegbareTeilnehmer.Where(t => t.Name.Contains(TeilnehmerFilter, StringComparison.OrdinalIgnoreCase));

    // Auslosungsart.Manuell wird hier nicht angeboten — die Paarungsfestlegung für Runden mit von vornherein
    // feststehenden Paarungen lebt im Vorbelegung-Tab (VorbelegungViewModel/VorbelegungWorkspace, #297).
    private static IReadOnlyList<AuslosungsartOption> AuslosungsartOptionenFuer(Rundenart? rundenart) =>
        Enum.GetValues<Auslosungsart>()
            .Where(a => a != Auslosungsart.Manuell)
            .Where(a => rundenart is null || AuslosungsKompatibilitaet.IstKompatibel(rundenart.Value, a))
            .Select(a => new AuslosungsartOption(AuslosungsartAnzeigeName(a), a))
            .ToList();

    public string AuslosungsartHinweis         => AuslosungsartHinweisText(NeuAuslosungsart);
    public string? AuslosungsartWarnung        => AuslosungsartWarnungText(NeuAuslosungsart, NeuRunde?.Rundenart);
    public string DetailAuslosungsartHinweis   => AuslosungsartHinweisText(DetailAuslosungsart);
    public string? DetailAuslosungsartWarnung  => AuslosungsartWarnungText(DetailAuslosungsart, _auslosungDetail?.TurnierRunde?.Rundenart);

    /// <summary>Zufall/Benutzerdefiniert kennen keine Topf-Struktur und sind auf genau 1 Lostopf begrenzt (Issue #134).</summary>
    private static bool ErlaubtMehrereToepfe(Auslosungsart art) =>
        art is not (Auslosungsart.Zufall or Auslosungsart.Benutzerdefiniert);

    public bool NeuAnzahlLostoepfeBearbeitbar    => ErlaubtMehrereToepfe(NeuAuslosungsart);
    public bool DetailAnzahlLostoepfeBearbeitbar => ErlaubtMehrereToepfe(DetailAuslosungsart);

    private static string AuslosungsartHinweisText(Auslosungsart art) => art switch
    {
        Auslosungsart.Zufall =>
            "Alle Teams werden zufällig gemischt und paarweise zugelost. Passt zu jeder Rundenart. " +
            "Kennt keine Topf-Struktur — auf 1 Lostopf begrenzt.",
        Auslosungsart.NachSetzliste =>
            "Gesetzte Teams (Topf 1) werden gleichmäßig auf Gruppen oder Paare verteilt, " +
            "damit stärkere Teams erst in späteren Runden aufeinandertreffen. " +
            "Typisch für Nationenturniere und nationale Pokal-Auslosungen.",
        Auslosungsart.NachToepfen =>
            "Teams werden manuell auf Töpfe verteilt und dann topfübergreifend zugelost — " +
            "mindestens ein Lostopf ist erforderlich. " +
            "Typisch für UEFA Europa League und internationale Pokal-Wettbewerbe.",
        Auslosungsart.TopfZuordnung =>
            "Jeder Topf belegt genau einen Setzplatz: Topf 1 → Gruppenplatz A, Topf 2 → Gruppenplatz B usw. " +
            "Alle Töpfe müssen gleich groß sein. " +
            "Gängig in der UEFA Champions League (Gruppen- und K.O.-Phase).",
        Auslosungsart.GruppensiegerGegenGruppenzweite =>
            "Gruppensieger werden den Zweitplatzierten einer anderen Gruppe zugelost, " +
            "wobei Teams aus derselben Gruppe nicht aufeinandertreffen dürfen. " +
            "Typisch für K.O.-Runden nach einer Gruppenphase (z. B. WM-Achtelfinale, UEFA-Playoff).",
        Auslosungsart.Benutzerdefiniert =>
            "Teams werden manuell den Losplätzen zugewiesen — keine automatische Verlosung. " +
            "Für besondere Anforderungen oder wenn keine andere Methode passt. " +
            "Kennt keine Topf-Struktur — auf 1 Lostopf begrenzt.",
        _ => ""
    };

    private static string? AuslosungsartWarnungText(Auslosungsart art, Rundenart? rundenart) =>
        (art, rundenart) switch
        {
            (Auslosungsart.NachSetzliste, Rundenart.SchweizerSystem) =>
                "⚠  Im Schweizer System wird die Paarung automatisch anhand der Tabelle berechnet. " +
                "Eine manuelle Setzlisten-Auslosung ist hier in der Regel nicht sinnvoll.",
            (Auslosungsart.NachToepfen, Rundenart.Liga) or
            (Auslosungsart.NachSetzliste, Rundenart.Liga) =>
                "⚠  Liga besteht aus nur einer Gruppe — die Topf-/Setzlisten-Reihenfolge hat hier " +
                "keine Auswirkung, alle Teams landen unabhängig davon in derselben Gruppe.",
            _ => null
        };

    // ── IRibbonAware ──────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Auslosung";
    public ICommand NeuBefehl => NeuCommand;
    public ICommand SpeichernBefehl => SpeichernCommand;
    public ICommand AbbrechenBefehl => AbbrechenCommand;
    public ICommand LoeschenBefehl => LoeschenCommand;

    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen =>
    [
        new RibbonGruppe
        {
            Header = "Auslosung",
            Aktionen =
            [
                new RibbonAktion { Header = "Lostöpfe automatisch befüllen", Befehl = LostoepfeAutomatischBefuellenCommand, IconGlyph = "" },
                new RibbonAktion { Header = "Alle Töpfe leeren", Befehl = AlleToepfeZuruecksetzenCommand, IconGlyph = "" },
                new RibbonAktion
                {
                    Header = "Auslosungs-Regeln",
                    ToggleUnterpunkte = Regeln
                        .Select(r => new RibbonToggleAktion(
                            r.Anzeige, r.IstAktiv, aktiv => r.IstAktiv = aktiv,
                            r.IstRedundant ? $"{r.Beschreibung}\n\n{r.RedundanzHinweis}" : r.Beschreibung))
                        .ToList(),
                    IconGlyph = "",
                },
                new RibbonAktion { Header = "Auslosung durchführen", Befehl = AuslosungAusfuehrenCommand, IconGlyph = "" },
                new RibbonAktion
                {
                    Header = "TV-Modus",
                    Unteraktionen =
                    [
                        new RibbonUnteraktion("Mit TV-Modus", AuslosungMitTvModusAusfuehrenCommand),
                        new RibbonUnteraktion("Mit TV-Modus manuell", AuslosungMitTvModusManuellAusfuehrenCommand),
                    ],
                    IconGlyph = "",
                },
                new RibbonAktion { Header = "Weiter", Befehl = AnimationWeiterCommand, IconGlyph = "" },
                new RibbonAktion { Header = "Rest sofort anzeigen", Befehl = AnimationUeberspringenCommand, IconGlyph = "" },
                new RibbonAktion { Header = "Zurücksetzen", Befehl = AuslosungZuruecksetzenCommand, IconGlyph = "" },
            ],
        },
    ];

    public AuslosungViewModel(
        AuslosungService auslosungService,
        TurnierInstanzService instanzService,
        MannschaftService mannschaftService,
        SpielplanService spielplanService,
        TurnierBaumService baumService,
        RanglisteService ranglisteService,
        VerbandsQuoteService verbandsQuoteService,
        UebergangsregelService uebergangsregelService,
        IDialogService dialogService)
    {
        _auslosungService = auslosungService;
        _instanzService = instanzService;
        _mannschaftService = mannschaftService;
        _spielplanService = spielplanService;
        _baumService = baumService;
        _ranglisteService = ranglisteService;
        _verbandsQuoteService = verbandsQuoteService;
        _uebergangsregelService = uebergangsregelService;
        _dialogService = dialogService;
    }

    /// <summary>Aktualisiert den Turnierbaum nach jeder Spielplan-Änderung automatisch (Issue #320-
    /// Folgefund: der Nutzer soll den Baum nie manuell neu generieren müssen). Für Turniere ganz ohne
    /// K.o.-Runden wirft <see cref="TurnierBaumService.GenerierenAsync"/>, was hier bewusst geschluckt wird
    /// (analog SpielplanViewModel.TurnierBaumAktualisierenAsync).</summary>
    private async Task TurnierBaumAktualisierenAsync(int turnierInstanzId)
    {
        try { await _baumService.GenerierenAsync(turnierInstanzId); }
        catch { /* TurnierBaum-Rebuild ist optional — nie Fehler an den Benutzer weitergeben */ }
    }

    /// <summary>Von <see cref="TurnierdurchfuehrungViewModel"/> einmalig beim Aktivieren geladen — analog
    /// <see cref="TurnierkopfViewModel.StammdatenLadenAsync"/>.</summary>
    public async Task RanglistenLadenAsync()
    {
        var ranglisten = await _ranglisteService.AlleAbrufenAsync();
        AlleRanglisten = new ObservableCollection<Rangliste>(ranglisten);
    }

    /// <summary>Vom Composite gesetzt, wenn sich die geteilte Instanzauswahl ändert (Precedent:
    /// <see cref="VorbelegungViewModel.InstanzSetzenAsync"/>).</summary>
    public async Task InstanzSetzenAsync(TurnierInstanz? instanz)
    {
        _instanz = instanz;
        KeinTurnierGewaehlt = instanz is null;
        Auslosungen.Clear();
        InstanzRunden.Clear();
        VerfuegbareTeilnehmer.Clear();
        TeilnehmerFilter = "";
        DetailAktiv = false;
        NeuFormAktiv = false;
        AusgewaehlteAuslosung = null;
        if (instanz is null) return;

        IstLadend = true;
        try
        {
            Auslosungen = await LadeAuslosungenGefiltertAsync(instanz.Id);
            var detail = await _instanzService.FindenNachIdMitRundenAsync(instanz.Id);
            if (detail is not null)
                InstanzRunden = new ObservableCollection<TurnierRunde>(detail.Runden.OrderBy(r => r.Reihenfolge));
        }
        finally { IstLadend = false; }
    }

    // ── Ribbon-Befehle ────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannNeu))]
    private void Neu()
    {
        NeuFormAktiv        = true;
        DetailAktiv         = false;
        _autoNeuName        = "";
        NeuName             = "";
        NeuAuslosungsart    = Auslosungsart.Zufall;
        NeuRunde            = NeuRundenOptionen.FirstOrDefault();
        NeuAnzahlLostoepfe  = 1;
        Fehlermeldung       = null;
    }

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        Fehlermeldung = null;
        if (NeuFormAktiv)
        {
            if (_instanz is null || NeuRunde is null) return;
            try
            {
                var name    = string.IsNullOrWhiteSpace(NeuName) ? NeuRunde.Name : NeuName.Trim();
                var eingabe = new AuslosungErstellenEingabe(
                    _instanz.Id, NeuRunde.Id, name,
                    NeuAuslosungsart, false);
                var neu = await _auslosungService.ErstellenAsync(eingabe);

                await _auslosungService.LostoepfeSynchronisierenAsync(neu.Id, Math.Max(1, NeuAnzahlLostoepfe));

                NeuFormAktiv = false;

                Auslosungen = await LadeAuslosungenGefiltertAsync(_instanz.Id);
                AusgewaehlteAuslosung = Auslosungen.FirstOrDefault(a => a.Id == neu.Id);
            }
            catch (Exception ex) { Fehlermeldung = ex.Message; }
        }
        else if (IstDetailDirty && _auslosungDetail is not null)
        {
            try
            {
                await _auslosungService.BearbeitenAsync(_auslosungDetail.Id, DetailName, DetailAuslosungsart);
                await _auslosungService.LostoepfeSynchronisierenAsync(_auslosungDetail.Id, DetailAnzahlLostoepfe);
                await LadeDetailAsync(_auslosungDetail.Id);
                await AktualisiereSeitenlisteAsync();
            }
            catch (Exception ex) { Fehlermeldung = ex.Message; }
        }
    }

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private async Task Abbrechen()
    {
        if (NeuFormAktiv)
        {
            NeuFormAktiv = false;
            if (AusgewaehlteAuslosung is not null)
                DetailAktiv = true;
        }
        else if (IstDetailDirty && _auslosungDetail is not null)
        {
            await LadeDetailAsync(_auslosungDetail.Id);
        }
    }

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task Loeschen()
    {
        if (AusgewaehlteAuslosung is null || _instanz is null) return;
        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Auslosung löschen", $"Soll '{AusgewaehlteAuslosung.Name}' wirklich gelöscht werden?");
        if (!bestaetigt) return;
        Fehlermeldung = null;
        try
        {
            await _auslosungService.LoeschenAsync(AusgewaehlteAuslosung.Id);
            Auslosungen = await LadeAuslosungenGefiltertAsync(_instanz.Id);
            AusgewaehlteAuslosung = null;
            DetailAktiv = false;
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    // ── Workspace-Befehle ─────────────────────────────────────────────────────

    /// <summary>Aufgerufen aus dem Drop-Handler des Codebehind (Drag&amp;Drop aus "Verfügbar" auf einen
    /// Lostopf, Precedent: <see cref="Views.VorbelegungPage"/>).</summary>
    public Task TeilnehmerZuTopfHinzufuegenAsync(int topfId, TeilnehmerAnzeige teilnehmer)
        => TeilnehmerZuTopfHinzufuegenAsync(topfId, [teilnehmer]);

    /// <summary>Mehrfachselektions-Variante (Issue #28-Muster aus dem WPF-Original: eine Liste statt eines
    /// einzelnen Teilnehmers deckt Einzel- UND Mehrfachauswahl uniform ab) — aufgerufen aus dem
    /// Rechtsklick-Kontextmenü sowie dem Mehrfach-Drag&amp;Drop im Codebehind. Ein gemeinsamer Reload am Ende
    /// statt pro Teilnehmer, damit die UI nicht mehrfach kurz aufflackert.</summary>
    public async Task TeilnehmerZuTopfHinzufuegenAsync(int topfId, IReadOnlyList<TeilnehmerAnzeige> teilnehmer)
    {
        if (_auslosungDetail is null || teilnehmer.Count == 0) return;
        Fehlermeldung = null;
        try
        {
            foreach (var t in teilnehmer)
            {
                if (t.MannschaftId.HasValue)
                    await _auslosungService.MannschaftZuLostopfHinzufuegenAsync(_auslosungDetail.Id, topfId, t.MannschaftId.Value);
                else
                    await _auslosungService.PlatzhalterZuLostopfHinzufuegenAsync(_auslosungDetail.Id, topfId, t.Name);
            }
            await LadeDetailAsync(_auslosungDetail.Id);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    /// <summary>Rechtsklick-Kontextmenü in einem Lostopf: Eintrag/Einträge zurück in "Verfügbar".</summary>
    public Task TeilnehmerAusTopfEntfernenAsync(int topfId, int eintragId)
        => TeilnehmerAusTopfEntfernenAsync(topfId, [eintragId]);

    /// <summary>Mehrfachselektions-Variante, siehe <see cref="TeilnehmerZuTopfHinzufuegenAsync(int, IReadOnlyList{TeilnehmerAnzeige})"/>.</summary>
    public async Task TeilnehmerAusTopfEntfernenAsync(int topfId, IReadOnlyList<int> eintragIds)
    {
        if (_auslosungDetail is null || eintragIds.Count == 0) return;
        Fehlermeldung = null;
        try
        {
            foreach (var eintragId in eintragIds)
                await _auslosungService.EintragAusLostopfEntfernenAsync(_auslosungDetail.Id, topfId, eintragId);
            await LadeDetailAsync(_auslosungDetail.Id);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    /// <summary>Drag&amp;Drop aus einem Lostopf in einen anderen (Codebehind, Precedent: Wpf
    /// <c>AuslosungViewModel.TeilnehmerZuTopfVerschieben</c>) — <see cref="TeilnehmerZuTopfHinzufuegenAsync(int, IReadOnlyList{TeilnehmerAnzeige})"/>
    /// allein reicht nicht: <c>MannschaftZuLostopfHinzufuegenAsync</c> lehnt eine bereits einem Lostopf
    /// zugeordnete Mannschaft ab, der Quell-Lostopf muss also zuerst geleert werden.</summary>
    public async Task TeilnehmerZuTopfVerschiebenAsync(IReadOnlyList<TeilnehmerAnzeige> teilnehmer, int? vonTopfId, int nachTopfId)
    {
        if (_auslosungDetail is null || teilnehmer.Count == 0) return;
        Fehlermeldung = null;
        try
        {
            foreach (var t in teilnehmer)
            {
                if (vonTopfId.HasValue)
                    await _auslosungService.EintragAusLostopfEntfernenAsync(_auslosungDetail.Id, vonTopfId.Value, t.EintragId);

                if (t.MannschaftId.HasValue)
                    await _auslosungService.MannschaftZuLostopfHinzufuegenAsync(_auslosungDetail.Id, nachTopfId, t.MannschaftId.Value);
                else
                    await _auslosungService.PlatzhalterZuLostopfHinzufuegenAsync(_auslosungDetail.Id, nachTopfId, t.Name);
            }
            await LadeDetailAsync(_auslosungDetail.Id);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    /// <summary>
    /// Verteilt die aktuell noch unzugeordneten Mannschaften (VerfuegbareTeilnehmer, Platzhalter
    /// ausgenommen) anhand der ausgewählten Rangliste auf die Lostöpfe: die bestplatzierten Teams
    /// zuerst in Topf 1, die nächsten in Topf 2 usw. Bereits manuell zugeordnete Teams bleiben
    /// unangetastet — der Button ergänzt nur, was noch in "Verfügbar" steht. Teams ohne
    /// Ranglisten-Eintrag werden ans Ende einsortiert (landen tendenziell im letzten Topf).
    /// </summary>
    [RelayCommand(CanExecute = nameof(KannLostoepfeAutomatischBefuellen))]
    private async Task LostoepfeAutomatischBefuellen()
    {
        if (_auslosungDetail is null) return;
        Fehlermeldung = null;

        var ausgewaehlteRangliste = AusgewaehlteRanglisteOption.Wert;
        if (ausgewaehlteRangliste is null)
        {
            Fehlermeldung = "Bitte zuerst eine Rangliste auswählen.";
            return;
        }
        if (Lostoepfe.Count == 0)
        {
            Fehlermeldung = "Diese Auslosung hat keine Lostöpfe.";
            return;
        }

        try
        {
            var rangliste = await _ranglisteService.FindenNachIdAsync(ausgewaehlteRangliste.Id);
            if (rangliste is null) return;

            var rangProMannschaft = rangliste.Eintraege.ToDictionary(e => e.MannschaftId, e => e.Rang);

            var teamsZuVerteilen = VerfuegbareTeilnehmer
                .Where(t => t.MannschaftId.HasValue)
                .OrderBy(t => rangProMannschaft.TryGetValue(t.MannschaftId!.Value, out var rang) ? rang : int.MaxValue)
                .ThenBy(t => t.Name)
                .ToList();

            if (TeamsBenoetigt > 0)
            {
                int verbleibend = Math.Max(0, TeamsBenoetigt - TeamsZugewiesen);
                teamsZuVerteilen = teamsZuVerteilen.Take(verbleibend).ToList();
            }

            if (teamsZuVerteilen.Count == 0)
            {
                Fehlermeldung = "Keine verfügbaren Mannschaften zum Zuordnen.";
                return;
            }

            var toepfe = Lostoepfe.OrderBy(l => l.Reihenfolge).ToList();

            // Faire Blockgrößen statt Ceiling-Division: bei nicht glatt teilbarer Teamanzahl (z.B. 9 Teams
            // auf 4 Töpfe) ließ Ceiling(9/4)=3 den letzten Topf komplett leer (3,3,3,0 statt 3,2,2,2) —
            // Nutzerfund "automatische Befüllung nicht gleichmäßig". Die ersten `rest` Töpfe bekommen ein
            // Team mehr, den Rang-Tiers-Charakter (beste Teams zuerst in Topf 1 usw.) behält das bei.
            int basisGroesse = teamsZuVerteilen.Count / toepfe.Count;
            int rest = teamsZuVerteilen.Count % toepfe.Count;

            int index = 0;
            for (int topfIndex = 0; topfIndex < toepfe.Count; topfIndex++)
            {
                int groesse = basisGroesse + (topfIndex < rest ? 1 : 0);
                for (int j = 0; j < groesse; j++, index++)
                {
                    await _auslosungService.MannschaftZuLostopfHinzufuegenAsync(
                        _auslosungDetail.Id, toepfe[topfIndex].Id, teamsZuVerteilen[index].MannschaftId!.Value);
                }
            }

            await LadeDetailAsync(_auslosungDetail.Id);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    [RelayCommand(CanExecute = nameof(KannAuslosungAusfuehren))]
    private async Task AuslosungAusfuehren()
    {
        TvModusAktiv = false; TvModusManuell = false;
        await AuslosungDurchfuehrenAsync();
    }

    /// <summary>Ribbon-Unteraktion (Flyout "TV-Modus"): führt wie der Hauptbutton sofort aus, aber mit
    /// TV-Modus-Ziehungsanimation (automatischer Schrittablauf).</summary>
    [RelayCommand(CanExecute = nameof(KannAuslosungAusfuehren))]
    private async Task AuslosungMitTvModusAusfuehren()
    {
        TvModusAktiv = true; TvModusManuell = false;
        await AuslosungDurchfuehrenAsync();
    }

    /// <summary>Ribbon-Unteraktion (Flyout "TV-Modus"): führt wie der Hauptbutton sofort aus, aber mit
    /// TV-Modus-Ziehungsanimation im manuellen Schrittmodus (Klick je Schritt).</summary>
    [RelayCommand(CanExecute = nameof(KannAuslosungAusfuehren))]
    private async Task AuslosungMitTvModusManuellAusfuehren()
    {
        TvModusAktiv = true; TvModusManuell = true;
        await AuslosungDurchfuehrenAsync();
    }

    private async Task AuslosungDurchfuehrenAsync()
    {
        if (_auslosungDetail is null) return;
        Fehlermeldung = null;
        try
        {
            await _auslosungService.AusfuehrenAsync(_auslosungDetail.Id);
            await LadeDetailAsync(_auslosungDetail.Id);

            // Ziehungsschritte bewusst ERST NACH dem Reload aus _auslosungDetail bauen (nicht aus einem vor
            // AusfuehrenAsync genommenen Lostöpfe-Snapshot + dem separaten Rückgabewert von AusfuehrenAsync):
            // ViewModel und AuslosungService teilen denselben DbContext-Scope, ein vorheriger Snapshot kann
            // durch EF-Change-Tracking/Identity-Resolution zwischen den beiden Aufrufen verändert werden.
            // Nach dem Reload stammen Lostöpfe UND Ergebnisse garantiert aus demselben, aktuellen Snapshot —
            // exakt die Daten, die danach auch als "echtes" Ergebnis angezeigt werden (Fund aus #303-Test:
            // TV-Modus-Endanzeige wich bei Gruppenphase nach vorherigem Zurücksetzen vom echten Ergebnis ab).
            if (TvModusAktiv && _auslosungDetail is not null)
            {
                var schritte = ZiehungsSchritteAufbauen(_auslosungDetail.Lostoepfe, _auslosungDetail.Ergebnisse);
                await ZiehungAnimierenAsync(schritte);
            }

            await SpielplanAutomatischGenerierenAsync();
            await AktualisiereSeitenlisteAsync();
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    [RelayCommand(CanExecute = nameof(AnimationWartetAufWeiter))]
    private void AnimationWeiter() => _weiterSignal.TrySetResult();

    [RelayCommand(CanExecute = nameof(AnimationLaeuft))]
    private void AnimationUeberspringen()
    {
        _animationUeberspringen = true;
        _weiterSignal.TrySetResult();
    }

    /// <summary>Baut die Ziehungsreihenfolge für den TV-Modus (Issue #5) auf: analog einer echten Auslosung
    /// wird ein Lostopf vollständig leergezogen, bevor der nächste an die Reihe kommt (Topf-Reihenfolge nach
    /// <see cref="Lostopf.Reihenfolge"/>). Bei Gruppenphase wird zusätzlich pro Gruppe eine von der
    /// Ziehungsreihenfolge unabhängige Position (1..Gruppengröße) verlost — analog historischer
    /// Auslosungsverfahren mit separater Positions-Kugel. Innerhalb eines einzelnen Topfes wird die
    /// Ziehungsreihenfolge gleichmäßig auf alle Zielgruppen verteilt (reihum eine Gruppe nach der anderen,
    /// siehe <see cref="ProGruppeGleichmaessigVerteilen"/>), damit nicht z.B. Gruppe A bereits ihre 2.
    /// Mannschaft aus diesem Topf erhält, während Gruppe B noch gar keine hat — die Umsortierung bleibt aber
    /// strikt auf den jeweiligen Topf beschränkt, ein bereits begonnener Topf wird also weiterhin vollständig
    /// geleert, bevor der nächste beginnt.</summary>
    private static List<ZiehungsSchritt> ZiehungsSchritteAufbauen(
        IEnumerable<Lostopf> lostoepfe, IEnumerable<AuslosungsErgebnis> ergebnisse)
    {
        bool istGruppenmodus = ergebnisse.Any() &&
            ergebnisse.All(e => !e.IstFreilos && e.AuswaertsMannschaftId == null && e.AuswaertsPlatzhalterName == null);

        // Position = tatsächlicher Index des Teams innerhalb seiner Gruppe in `ergebnisse`, NICHT
        // zufällig gewürfelt: SpielplanService.GenerierenAusAuslosungAsync baut den Spielplan (bei
        // Round-Robin/Berger-Rotationsschema reihenfolgeabhängig) exakt aus dieser Reihenfolge —
        // die im TV-Modus gezeigte Position muss mit der tatsächlich gespeicherten übereinstimmen,
        // sonst zeigt die Ziehung eine Position, die mit dem späteren Spielplan nichts zu tun hat.
        var positionProErgebnis = ergebnisse
            .GroupBy(e => e.PaarungsNummer)
            .SelectMany(g => g.Select((e, index) => (Ergebnis: e, Position: index + 1)))
            .ToDictionary(x => x.Ergebnis, x => x.Position);

        bool PasstZuHeim(AuslosungsErgebnis e, LostopfEintrag eintrag) => eintrag.MannschaftId.HasValue
            ? e.HeimMannschaftId == eintrag.MannschaftId
            : e.HeimPlatzhalterName == eintrag.PlatzhalterName;
        bool PasstZuAuswaerts(AuslosungsErgebnis e, LostopfEintrag eintrag) => eintrag.MannschaftId.HasValue
            ? e.AuswaertsMannschaftId == eintrag.MannschaftId
            : e.AuswaertsPlatzhalterName == eintrag.PlatzhalterName;

        var schritte = new List<ZiehungsSchritt>();
        foreach (var topf in lostoepfe.OrderBy(l => l.Reihenfolge))
        {
            var topfSchritte = new List<ZiehungsSchritt>();
            var teamsGemischt = topf.Eintraege.OrderBy(_ => Random.Shared.Next()).ToList();
            foreach (var eintrag in teamsGemischt)
            {
                var team = TeilnehmerAnzeige.AusEintrag(eintrag);
                var treffer = ergebnisse.FirstOrDefault(e => PasstZuHeim(e, eintrag) || PasstZuAuswaerts(e, eintrag));
                if (treffer is null) continue;

                if (istGruppenmodus)
                {
                    int position = positionProErgebnis[treffer];
                    topfSchritte.Add(new ZiehungsSchritt(team, topf.Id, topf.Name, treffer.PaarungsNummer,
                        IstGruppenmodus: true, IstHeim: null, Position: position, IstFreilos: false));
                }
                else
                {
                    bool istHeim = PasstZuHeim(treffer, eintrag);
                    topfSchritte.Add(new ZiehungsSchritt(team, topf.Id, topf.Name, treffer.PaarungsNummer,
                        IstGruppenmodus: false, IstHeim: istHeim, Position: null, IstFreilos: treffer.IstFreilos));
                }
            }

            schritte.AddRange(istGruppenmodus ? ProGruppeGleichmaessigVerteilen(topfSchritte) : topfSchritte);
        }

        return schritte;
    }

    /// <summary>Verteilt die Ziehungsschritte EINES Topfes gleichmäßig auf alle Zielgruppen: reihum wird aus
    /// jeder Gruppen-Warteschlange (in zufälliger Gruppen-Reihenfolge je Runde) je ein Schritt entnommen, bis
    /// alle Warteschlangen leer sind. Verhindert, dass innerhalb eines Topfes eine Gruppe bereits mehrere
    /// Mannschaften erhält, während eine andere noch leer ausgeht.</summary>
    private static List<ZiehungsSchritt> ProGruppeGleichmaessigVerteilen(List<ZiehungsSchritt> topfSchritte)
    {
        var proGruppe = topfSchritte
            .GroupBy(s => s.ZielNummer)
            .Select(g => new Queue<ZiehungsSchritt>(g.OrderBy(_ => Random.Shared.Next())))
            .ToList();

        var ergebnis = new List<ZiehungsSchritt>(topfSchritte.Count);
        bool weitereRunde = true;
        while (weitereRunde)
        {
            weitereRunde = false;
            foreach (var queue in proGruppe.OrderBy(_ => Random.Shared.Next()))
            {
                if (queue.Count == 0) continue;
                ergebnis.Add(queue.Dequeue());
                weitereRunde = true;
            }
        }
        return ergebnis;
    }

    /// <summary>Spielt die Ziehungsschritte nacheinander im UI ab: pro Schritt wird zunächst die gezogene
    /// Mannschaft angezeigt (dabei in der Topf-Anzeige als "gezogen" markiert — die Slots bleiben
    /// zahlenmäßig fix, nur ihr Inhalt wechselt zu "…", damit sich das Layout nicht verschiebt, sobald ein
    /// Topf leerer wird), dann das Ziel (Gruppe/Paarung), bei Gruppenphase zusätzlich die Position — je nach
    /// TvModusManuell entweder automatisch mit Verzögerung oder wartend auf einen "Weiter"-Klick.
    /// AnimationGruppen/AnimationPaarungen werden vorab mit der vollen Anzahl leerer Plätze ("…") aufgebaut
    /// (nicht erst bei der ersten Zuweisung sichtbar) und danach Slot für Slot befüllt. Das bereits von
    /// LadeDetailAsync geladene Endergebnis wird während der Animation nicht verändert — es wird einfach
    /// erst nach Animationsende (bzw. bei "Überspringen" sofort) sichtbar, siehe <see cref="ZeigeErgebnis"/>.</summary>
    private async Task ZiehungAnimierenAsync(List<ZiehungsSchritt> schritte)
    {
        if (schritte.Count == 0) return;

        AnimationLaeuft         = true;
        AnimationSchrittGesamt  = schritte.Count;
        _animationUeberspringen = false;
        _weiterSignal           = new TaskCompletionSource();

        AnimationToepfe = new ObservableCollection<AnimationsTopf>(
            Lostoepfe.OrderBy(l => l.Reihenfolge).Select(l => new AnimationsTopf
            {
                TopfId = l.Id,
                Name   = l.Name,
                Slots  = new ObservableCollection<AnimationsTopfSlot>(
                    l.Teilnehmer.Select(t => new AnimationsTopfSlot { EintragId = t.EintragId, Name = t.Name, Wappen = t.Wappen }))
            }));

        bool istGruppenmodus = schritte[0].IstGruppenmodus;
        if (istGruppenmodus)
        {
            AnimationGruppen = new ObservableCollection<AnimationsGruppe>(
                schritte.GroupBy(s => s.ZielNummer).OrderBy(g => g.Key).Select(g => new AnimationsGruppe
                {
                    GruppenNummer = g.Key,
                    GruppenName   = $"Gruppe {GruppenBuchstabe(g.Key)}",
                    Slots         = new ObservableCollection<AnimationsGruppenSlot>(
                        Enumerable.Range(1, g.Count()).Select(p => new AnimationsGruppenSlot { Position = p }))
                }));
            AnimationPaarungen = [];
        }
        else
        {
            AnimationPaarungen = new ObservableCollection<AnimationsPaarung>(
                schritte.GroupBy(s => s.ZielNummer).OrderBy(g => g.Key).Select(g => new AnimationsPaarung
                {
                    PaarungsNr = g.Key,
                    IstFreilos = g.First().IstFreilos
                }));
            AnimationGruppen = [];
        }

        try
        {
            for (int i = 0; i < schritte.Count; i++)
            {
                if (_animationUeberspringen) break;
                var schritt = schritte[i];
                AnimationSchrittIndex = i + 1;

                var topfSlot = AnimationToepfe
                    .FirstOrDefault(t => t.TopfId == schritt.TopfId)?.Slots
                    .FirstOrDefault(s => s.EintragId == schritt.Team.EintragId);
                if (topfSlot is not null) topfSlot.Gezogen = true;

                AnimationBannerMannschaft = schritt.Team.Name;
                AnimationBannerTopf       = schritt.TopfName;
                AnimationBannerZiel       = null;
                AnimationBannerSlot       = null;
                AnimationBannerPosition   = null;
                await WarteAufSchrittAsync(_tvModusSchrittVerzoegerung);
                if (_animationUeberspringen) break;

                AnimationBannerZiel = schritt.IstGruppenmodus
                    ? $"Gruppe {GruppenBuchstabe(schritt.ZielNummer)}"
                    : $"Paarung {schritt.ZielNummer}";
                AnimationBannerSlot = schritt.IstGruppenmodus
                    ? null
                    : (schritt.IstFreilos ? "Freilos" : (schritt.IstHeim == true ? "Heim" : "Auswärts"));
                await WarteAufSchrittAsync(_tvModusSchrittVerzoegerung);
                if (_animationUeberspringen) break;

                if (schritt.IstGruppenmodus)
                {
                    AnimationBannerPosition = $"Position {schritt.Position}";
                    await WarteAufSchrittAsync(_tvModusSchrittVerzoegerung);
                    if (_animationUeberspringen) break;
                }

                if (schritt.IstGruppenmodus)
                {
                    var gruppe = AnimationGruppen.First(g => g.GruppenNummer == schritt.ZielNummer);
                    var slot = gruppe.Slots[schritt.Position!.Value - 1];
                    slot.Anzeige = schritt.Team.Name;
                    slot.Wappen  = schritt.Team.Wappen;
                }
                else
                {
                    var paarung = AnimationPaarungen.First(p => p.PaarungsNr == schritt.ZielNummer);
                    if (schritt.IstFreilos || schritt.IstHeim == true)
                    {
                        paarung.Heim       = schritt.Team.Name;
                        paarung.HeimWappen = schritt.Team.Wappen;
                    }
                    else
                    {
                        paarung.Auswaerts       = schritt.Team.Name;
                        paarung.AuswaertsWappen = schritt.Team.Wappen;
                    }
                }

                AnimationBannerMannschaft = null;
                AnimationBannerTopf       = null;
                AnimationBannerZiel       = null;
                AnimationBannerSlot       = null;
                AnimationBannerPosition   = null;
                if (i < schritte.Count - 1)
                {
                    await WarteAufSchrittAsync(_tvModusPausenVerzoegerung);
                    if (_animationUeberspringen) break;
                }
            }
        }
        finally
        {
            AnimationLaeuft           = false;
            AnimationWartetAufWeiter  = false;
            AnimationBannerMannschaft = null;
            AnimationBannerTopf       = null;
            AnimationBannerZiel       = null;
            AnimationBannerSlot       = null;
            AnimationBannerPosition   = null;
            AnimationToepfe           = [];
            AnimationGruppen          = [];
            AnimationPaarungen        = [];
        }
    }

    private async Task WarteAufSchrittAsync(TimeSpan verzoegerung)
    {
        if (_animationUeberspringen) return;

        if (TvModusManuell)
        {
            AnimationWartetAufWeiter = true;
            await _weiterSignal.Task;
        }
        else
        {
            await Task.WhenAny(Task.Delay(verzoegerung), _weiterSignal.Task);
        }
        AnimationWartetAufWeiter = false;
        _weiterSignal = new TaskCompletionSource();
    }

    /// <summary>Generiert den Spielplan automatisch, sobald die Auslosung abgeschlossen ist (Issue #19,
    /// analog WPF-Original). Erwartet, dass _auslosungDetail bereits per LadeDetailAsync neu geladen wurde
    /// (inkl. Ergebnisse). Fragt bei bereits vorhandenen Spielen nach, ob sie ersetzt werden sollen.</summary>
    private async Task SpielplanAutomatischGenerierenAsync()
    {
        if (_auslosungDetail is null || _auslosungDetail.Status != AuslosungsStatus.Abgeschlossen) return;

        var rundeId = _auslosungDetail.TurnierRundeId;
        var vorhandeneSpiele = await _spielplanService.SpielplanAbrufenAsync(rundeId);
        if (vorhandeneSpiele.Count > 0)
        {
            var bestaetigt = await _dialogService.BestaetigenAsync(
                "Spielplan neu generieren",
                $"Die Runde hat bereits {vorhandeneSpiele.Count} Spiel(e) (z.B. aus einer früheren Auslosung). " +
                "Soll der bestehende Spielplan gelöscht und aus dieser Auslosung neu generiert werden?");
            if (!bestaetigt) return;

            // Issue #320-Folgefund: kaskadenlos löschen — LoeschenAsync würde selbst sofort wieder die
            // Kaskade auslösen, die bei einer Platzhalter-K.o.-Runde mit weiterhin abgeschlossener
            // Auslosung erneut komplett neu generiert, bevor der folgende GenerierenAusAuslosungAsync-
            // Aufruf noch einmal generiert — macht aus 4 erwarteten Spielen 8 Phantom-Spiele.
            await _spielplanService.RundenSpielplanOhneKaskadeLoeschenAsync(rundeId);
        }

        await _spielplanService.GenerierenAusAuslosungAsync(rundeId, [.. _auslosungDetail.Ergebnisse]);

        // Folgerunden können bereits aufgelöste Platzhalter enthalten, die auf den alten (jetzt ersetzten)
        // Spielplan verwiesen haben (#306) — ohne die Kaskade blieben sie fälschlich stehen.
        await _spielplanService.KaskadeAusloesenAsync(_auslosungDetail.TurnierInstanzId);
        await TurnierBaumAktualisierenAsync(_auslosungDetail.TurnierInstanzId);
    }

    [RelayCommand(CanExecute = nameof(KannAuslosungZuruecksetzen))]
    private async Task AuslosungZuruecksetzen()
    {
        if (_auslosungDetail is null) return;
        Fehlermeldung = null;
        try
        {
            var instanzId = _auslosungDetail.TurnierInstanzId;
            await _auslosungService.ZuruecksetzenAsync(_auslosungDetail.Id);
            await TurnierBaumAktualisierenAsync(instanzId);
            await LadeDetailAsync(_auslosungDetail.Id);
            await AktualisiereSeitenlisteAsync();
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    [RelayCommand]
    private async Task TopfLeeren(LostopfViewModel topf)
    {
        if (_auslosungDetail is null) return;
        Fehlermeldung = null;
        try
        {
            await _auslosungService.LostopfLeerenAsync(_auslosungDetail.Id, topf.Id);
            await LadeDetailAsync(_auslosungDetail.Id);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    [RelayCommand(CanExecute = nameof(KannAlleToepfeZuruecksetzen))]
    private async Task AlleToepfeZuruecksetzen()
    {
        if (_auslosungDetail is null) return;
        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Töpfe zurücksetzen", "Alle Töpfe leeren und alle Teams zurück in die Verfügbar-Liste?");
        if (!bestaetigt) return;
        Fehlermeldung = null;
        try
        {
            await _auslosungService.AlleToepfeZuruecksetzenAsync(_auslosungDetail.Id);
            await LadeDetailAsync(_auslosungDetail.Id);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    // ── Interne Hilfsmethoden ─────────────────────────────────────────────────

    partial void OnAusgewaehlteAuslosungChanged(Auslosung? value) => _ = BeiAuslosungAenderungAsync();

    private async Task BeiAuslosungAenderungAsync()
    {
        // AktualisiereSeitenlisteAsync() tauscht die ListView-ItemsSource (neue Auslosungen-Collection-
        // Instanz) aus, BEVOR sie AusgewaehlteAuslosung passend zur neuen Collection neu zuweist — die
        // TwoWay-SelectedItem-Bindung nullt AusgewaehlteAuslosung dabei transient (WinUI3-Fallstrick, siehe
        // WinUi/CLAUDE.md "Collection.Clear() vor async Abfrage nullt gebundene Selektion"), bevor die
        // korrekte Zuweisung nachkommt. Ohne diesen Guard setzt genau dieses transiente Null DetailAktiv auf
        // false, und da die anschließende korrekte Zuweisung dieselbe Id hat, greift unten der Id-Vergleich
        // und lädt NICHT neu — DetailAktiv bliebe fälschlich false (Fokusverlust, Fund aus #318-Nachtest).
        if (_seitenlisteAktualisierungAktiv) return;

        NeuFormAktiv = false;
        if (AusgewaehlteAuslosung is null) { DetailAktiv = false; return; }

        // AktualisiereSeitenlisteAsync() weist AusgewaehlteAuslosung nach einem Reload der Seitenliste
        // erneut zu (neue Objektinstanz, gleiche Id) — das würde hier ohne diesen Guard einen bereits
        // frisch geladenen Detail-Stand ein zweites Mal nachladen (sichtbares Flackern, Issue #318).
        if (_auslosungDetail?.Id == AusgewaehlteAuslosung.Id) return;

        await LadeDetailAsync(AusgewaehlteAuslosung.Id);
    }

    private async Task LadeDetailAsync(int auslosungId)
    {
        IstLadend    = true;
        _ladenAktiv  = true;
        try
        {
            var detail = await _auslosungService.FindenNachIdAsync(auslosungId);
            if (detail is null) return;
            _auslosungDetail = detail;

            var aktuelleRunde = InstanzRunden.FirstOrDefault(r => r.Id == detail.TurnierRundeId);
            var eingehendeRegeln = aktuelleRunde is not null
                ? await _uebergangsregelService.AlleNachZielRundeIdAsync(aktuelleRunde.Id)
                : [];

            IReadOnlyList<TeilnehmerAnzeige> alleTeilnehmer;
            if (eingehendeRegeln.Count > 0)
            {
                alleTeilnehmer = TeilnehmerPoolHilfsmittel.PlatzhalterAusRegelnGenerieren(eingehendeRegeln);
            }
            else
            {
                var vorherigeRunde = aktuelleRunde is not null
                    ? TeilnehmerPoolHilfsmittel.VorgaengerrundeErmitteln(InstanzRunden, aktuelleRunde)
                    : null;
                if (vorherigeRunde is null)
                {
                    var teams = await _mannschaftService.AlleNachTurnierfilterAsync(
                        _instanz?.VerbandId,
                        _instanz?.LandId,
                        _instanz?.MannschaftsartFilter ?? MannschaftsartFilter.Alle);
                    alleTeilnehmer = teams.Select(TeilnehmerAnzeige.AusMannschaft).ToList();
                }
                else
                {
                    alleTeilnehmer = TeilnehmerPoolHilfsmittel.PlatzhalterOhneRegelnGenerieren(aktuelleRunde!, vorherigeRunde);
                }
            }

            var assignedMannschaftIds = detail.Lostoepfe
                .SelectMany(l => l.Eintraege)
                .Where(e => e.MannschaftId.HasValue)
                .Select(e => e.MannschaftId!.Value)
                .ToHashSet();
            var assignedPlatzhalter = detail.Lostoepfe
                .SelectMany(l => l.Eintraege)
                .Where(e => e.PlatzhalterName != null)
                .Select(e => e.PlatzhalterName!)
                .ToHashSet();

            VerfuegbareTeilnehmer = new ObservableCollection<TeilnehmerAnzeige>(
                alleTeilnehmer
                    .Where(t => t.MannschaftId.HasValue
                        ? !assignedMannschaftIds.Contains(t.MannschaftId.Value)
                        : !assignedPlatzhalter.Contains(t.Name))
                    .OrderBy(t => t.Name));

            Lostoepfe = new ObservableCollection<LostopfViewModel>(
                detail.Lostoepfe.Select(l => new LostopfViewModel
                {
                    Id         = l.Id,
                    Name       = l.Name,
                    Reihenfolge = l.Reihenfolge,
                    Teilnehmer = new ObservableCollection<TeilnehmerAnzeige>(
                        l.Eintraege
                            .OrderBy(e => e.Id)
                            .Select(TeilnehmerAnzeige.AusEintrag))
                }));

            await AktualisiereVerbandsQuotenAnzeigeAsync(detail);

            Regeln = new ObservableCollection<RegelViewModel>(
                Enum.GetValues<AuslosungsRegelArt>()
                .Where(art => art != AuslosungsRegelArt.ManuelleEinschraenkung)
                .Select(art =>
                {
                    var vorh = detail.Regeln.FirstOrDefault(r => r.Regelart == art);
                    return new RegelViewModel(art, RegelAnzeigeName(art), RegelBeschreibung(art), vorh?.IstAktiv ?? false,
                        async aktiv =>
                        {
                            try
                            {
                                if (aktiv) await _auslosungService.RegelHinzufuegenOderAktivierenAsync(auslosungId, art, RegelAnzeigeName(art));
                                else       await _auslosungService.RegelDeaktivierenAsync(auslosungId, art);
                            }
                            catch (Exception ex) { Fehlermeldung = ex.Message; }
                        });
                }));
            AktualisiereRedundanteRegeln(detail.Auslosungsart);

            bool istGruppe = detail.Ergebnisse.Any() &&
                detail.Ergebnisse.All(e => !e.IstFreilos &&
                    e.AuswaertsMannschaftId == null &&
                    e.AuswaertsPlatzhalterName == null);

            IstGruppenergebnis = istGruppe;

            if (istGruppe)
            {
                // Team-Reihenfolge innerhalb der Gruppe bewusst NICHT alphabetisch sortiert: sie muss mit
                // der tatsächlich gespeicherten Reihenfolge übereinstimmen, aus der
                // SpielplanService.GenerierenAusAuslosungAsync den Spielplan generiert (dort ohne eigene
                // Sortierung, ebenfalls direkt aus der PaarungsNummer-Gruppierung) — siehe #303-Diskussion.
                GruppenErgebnisse = new ObservableCollection<GruppenErgebnisAnzeige>(
                    detail.Ergebnisse
                        .GroupBy(e => e.PaarungsNummer)
                        .OrderBy(g => g.Key)
                        .Select(g => new GruppenErgebnisAnzeige(
                            g.Key,
                            $"Gruppe {GruppenBuchstabe(g.Key)}",
                            g.Select(e => new GruppenTeamAnzeige(
                                    e.HeimMannschaft?.Name ?? e.HeimPlatzhalterName ?? "?",
                                    TeilnehmerAnzeige.MannschaftsIcon(e.HeimMannschaft)))
                             .ToList())));
                ErgebnisseAnzeige = [];
            }
            else
            {
                GruppenErgebnisse = [];
                ErgebnisseAnzeige = new ObservableCollection<ErgebnisAnzeige>(
                    detail.Ergebnisse.Select(e => new ErgebnisAnzeige(
                        e.PaarungsNummer,
                        e.HeimMannschaft?.Name ?? e.HeimPlatzhalterName ?? "?",
                        e.AuswaertsMannschaft?.Name ?? e.AuswaertsPlatzhalterName ?? "?",
                        e.IstFreilos,
                        TeilnehmerAnzeige.MannschaftsIcon(e.HeimMannschaft),
                        TeilnehmerAnzeige.MannschaftsIcon(e.AuswaertsMannschaft))));
            }

            TeamsZugewiesen       = detail.Lostoepfe.SelectMany(l => l.Eintraege).Count();
            TeamsBenoetigt        = detail.TurnierRunde?.EingangsMannschaftsanzahl ?? 0;

            IstBearbeitbar        = detail.Status is not (AuslosungsStatus.Abgeschlossen or AuslosungsStatus.Abgebrochen);
            IstAbgeschlossen      = detail.Status == AuslosungsStatus.Abgeschlossen;
            DetailName            = detail.Name;
            DetailAuslosungsart   = detail.Auslosungsart;
            DetailAuslosungsartOptionen = AuslosungsartOptionenFuer(detail.TurnierRunde?.Rundenart);
            // Siehe OnNeuRundeChanged — erzwungene Nachbenachrichtigung gegen den WinUI3-ComboBox-Reset-
            // Fallstrick (Fund aus #300-Test: Auslosungsart erschien nach Anlage einer Auslosung leer).
            OnPropertyChanged(nameof(DetailAuslosungsart));
            DetailAnzahlLostoepfe = Math.Max(1, detail.Lostoepfe.Count);

            AuslosungKopfzeile = $"{detail.Name}  ·  {detail.TurnierRunde.Name}  ·  " +
                                 $"{AuslosungsartAnzeigeName(detail.Auslosungsart)}  ·  " +
                                 $"Status: {StatusAnzeigeName(detail.Status)}";
            DetailAktiv    = true;
            IstDetailDirty = false;
            Fehlermeldung  = null;
        }
        finally
        {
            _ladenAktiv = false;
            IstLadend   = false;
        }
    }

    private async Task AktualisiereSeitenlisteAsync()
    {
        if (_instanz is null) return;
        var neueListe = await LadeAuslosungenGefiltertAsync(_instanz.Id);

        // Guard gegen den transienten Null-Durchlauf, den die TwoWay-SelectedItem-Bindung beim
        // ItemsSource-Tausch auslöst (siehe BeiAuslosungAenderungAsync) — ohne ihn würde dieser Reload den
        // bereits offenen Detail-Workspace kurz schließen (DetailAktiv=false) und nicht wieder öffnen.
        _seitenlisteAktualisierungAktiv = true;
        try
        {
            Auslosungen = neueListe;
            if (_auslosungDetail is not null)
                AusgewaehlteAuslosung = Auslosungen.FirstOrDefault(a => a.Id == _auslosungDetail.Id);
        }
        finally { _seitenlisteAktualisierungAktiv = false; }
    }

    /// <summary>Auslosungsart.Manuell wird hier nicht angezeigt — deren Paarungsfestlegung
    /// lebt ausschließlich im Vorbelegung-Tab.</summary>
    private async Task<ObservableCollection<Auslosung>> LadeAuslosungenGefiltertAsync(int turnierInstanzId)
    {
        var auslosungen = await _auslosungService.AlleNachInstanzAbrufenAsync(turnierInstanzId);

        // Issue #320: _belegteRundenIds bewusst aus der UNGEFILTERTEN Liste (inkl. Manuell) befüllen —
        // NeuRundenOptionen muss auch Runden mit einer hier unsichtbaren Manuell-Auslosung ausblenden.
        _belegteRundenIds = auslosungen.Select(a => a.TurnierRundeId).ToHashSet();
        OnPropertyChanged(nameof(NeuRundenOptionen));

        return new ObservableCollection<Auslosung>(auslosungen.Where(a => a.Auslosungsart != Auslosungsart.Manuell));
    }

    partial void OnNeuRundeChanged(TurnierRunde? value)
    {
        // Reihenfolge bewusst: erst den Zielwert festlegen (falls die aktuelle Art in der neuen Runde nicht
        // mehr kompatibel ist), DANACH die Optionsliste (ItemsSource) neu setzen — minimiert das Zeitfenster,
        // in dem die ComboBox eine ItemsSource ohne passenden Eintrag zur aktuellen Auswahl hätte (siehe
        // Klassendoku bei NeuAuslosungsartOptionen).
        if (value is not null && !AuslosungsKompatibilitaet.IstKompatibel(value.Rundenart, NeuAuslosungsart))
            NeuAuslosungsart = Auslosungsart.Zufall;

        NeuAuslosungsartOptionen = AuslosungsartOptionenFuer(value?.Rundenart);
        // Erzwungene Nachbenachrichtigung: falls WinUI die ComboBox-Auswahl beim ItemsSource-Wechsel
        // transient zurücksetzt und der anschließende Vergleich zufällig denselben Enum-Wert ergibt,
        // unterdrückt CommunityToolkits Gleichheitsprüfung die Benachrichtigung — die ComboBox bliebe dann
        // visuell leer, obwohl NeuAuslosungsart korrekt ist (Fund aus #300-Test).
        OnPropertyChanged(nameof(NeuAuslosungsart));

        if (value is null) return;
        if (string.IsNullOrWhiteSpace(NeuName) || NeuName == _autoNeuName)
        {
            _autoNeuName = value.Name;
            NeuName      = value.Name;
        }
    }

    partial void OnNeuAuslosungsartChanged(Auslosungsart value)
    {
        if (!NeuAnzahlLostoepfeBearbeitbar)
            NeuAnzahlLostoepfe = 1;
    }

    private bool KannNeu()       => !NeuFormAktiv && _instanz is not null && _instanz.Status != TurnierStatus.Vorlage;
    private bool KannSpeichern() => (NeuFormAktiv && NeuRunde is not null)
                                 || (IstDetailDirty && IstBearbeitbar);
    private bool KannAbbrechen() => NeuFormAktiv || IstDetailDirty;

    public bool HatUngespeicherteAenderungen => NeuFormAktiv || IstDetailDirty;
    private bool KannLoeschen()  => AusgewaehlteAuslosung is not null && !NeuFormAktiv;

    private bool KannAuslosungAusfuehren()             => DetailAktiv && IstBearbeitbar;
    private bool KannAuslosungZuruecksetzen()           => DetailAktiv && IstAbgeschlossen && !AnimationLaeuft;
    private bool KannLostoepfeAutomatischBefuellen()    => DetailAktiv && IstBearbeitbar;
    private bool KannAlleToepfeZuruecksetzen()          => DetailAktiv && IstBearbeitbar && Lostoepfe.Any(l => l.Teilnehmer.Count > 0);

    partial void OnDetailNameChanged(string value)           { if (!_ladenAktiv) IstDetailDirty = true; }
    partial void OnDetailAuslosungsartChanged(Auslosungsart value)
    {
        if (!_ladenAktiv) IstDetailDirty = true;
        AktualisiereRedundanteRegeln(value);

        if (!DetailAnzahlLostoepfeBearbeitbar)
            DetailAnzahlLostoepfe = 1;
    }

    private void AktualisiereRedundanteRegeln(Auslosungsart art)
    {
        foreach (var regel in Regeln)
        {
            bool redundant = regel.Regelart switch
            {
                AuslosungsRegelArt.GruppensiegerGegenGruppenzweite =>
                    art == Auslosungsart.GruppensiegerGegenGruppenzweite,
                _ => false
            };
            regel.IstRedundant    = redundant;
            regel.RedundanzHinweis = redundant
                ? "Diese Regel ist bei der gewählten Auslosungsart bereits im Algorithmus enthalten und hat keine zusätzliche Wirkung."
                : "";
        }
    }

    private async Task AktualisiereVerbandsQuotenAnzeigeAsync(Auslosung detail)
    {
        if (_instanz is null) { VerbandsQuotenAnzeige = []; return; }

        // Verbandsquoten gelten laut Domänen-Annahme nur für die initiale Turnierrunde (Zulassung),
        // nicht kumulativ für Folgerunden mit eigenem Losvorgang — siehe [[project_verbandsquote_scope]].
        bool istErsteRunde = InstanzRunden.Count > 0 && InstanzRunden[0].Id == detail.TurnierRundeId;
        if (!istErsteRunde) { VerbandsQuotenAnzeige = []; return; }

        var quoten = await _verbandsQuoteService.AlleNachTurnierInstanzIdAsync(_instanz.Id);
        if (quoten.Count == 0) { VerbandsQuotenAnzeige = []; return; }

        var anzahlProVerband = detail.Lostoepfe
            .SelectMany(l => l.Eintraege)
            .Where(e => e.Mannschaft?.Land is not null)
            .GroupBy(e => e.Mannschaft!.Land!.VerbandId)
            .ToDictionary(g => g.Key, g => g.Count());

        VerbandsQuotenAnzeige = new ObservableCollection<VerbandsQuoteAnzeigeViewModel>(
            quoten.Select(q => new VerbandsQuoteAnzeigeViewModel
            {
                VerbandName = q.Verband?.Name ?? $"Verband {q.VerbandId}",
                Aktuell     = anzahlProVerband.GetValueOrDefault(q.VerbandId),
                Anzahl      = q.Anzahl
            }));
    }

    partial void OnDetailAnzahlLostoepfeChanged(int value)   { if (!_ladenAktiv) IstDetailDirty = true; }

    private static string GruppenBuchstabe(int nr) =>
        nr <= 26 ? ((char)('A' + nr - 1)).ToString() : $"G{nr}";

    // ── Anzeigetexte ──────────────────────────────────────────────────────────

    public static string AuslosungsartAnzeigeName(Auslosungsart a) => a switch
    {
        Auslosungsart.Zufall                          => "Zufällig",
        Auslosungsart.NachSetzliste                   => "Nach Setzliste",
        Auslosungsart.NachToepfen                     => "Nach Töpfen",
        Auslosungsart.TopfZuordnung                   => "Topf-Zuordnung (UCL)",
        Auslosungsart.GruppensiegerGegenGruppenzweite => "Gruppensieger vs. Zweite",
        Auslosungsart.Benutzerdefiniert               => "Benutzerdefiniert",
        Auslosungsart.Manuell                         => "Manuell",
        _                                             => a.ToString()
    };

    private static string StatusAnzeigeName(AuslosungsStatus s) => s switch
    {
        AuslosungsStatus.Vorbereitung  => "Vorbereitung",
        AuslosungsStatus.Laufend       => "Laufend",
        AuslosungsStatus.Abgeschlossen => "Abgeschlossen",
        AuslosungsStatus.Abgebrochen   => "Abgebrochen",
        _                              => s.ToString()
    };

    private static string RegelAnzeigeName(AuslosungsRegelArt art) => art switch
    {
        AuslosungsRegelArt.KeinGleichesLand                => "Kein gleiches Land",
        AuslosungsRegelArt.KeineGleicheGruppe              => "Keine gleiche Gruppe",
        AuslosungsRegelArt.GruppensiegerGegenGruppenzweite => "Gruppensieger vs. Gruppenzweite",
        AuslosungsRegelArt.GesetzteGegenUngesetzte         => "Gesetzte vs. Ungesetzte",
        AuslosungsRegelArt.HeimrechtNachLos                => "Heimrecht nach Los",
        AuslosungsRegelArt.HeimrechtNachSetzliste          => "Heimrecht nach Setzliste",
        AuslosungsRegelArt.FreilosMoeglich                 => "Freilos möglich",
        AuslosungsRegelArt.ManuelleEinschraenkung          => "Manuelle Einschränkung",
        _                                                  => art.ToString()
    };

    private static string RegelBeschreibung(AuslosungsRegelArt art) => art switch
    {
        AuslosungsRegelArt.KeinGleichesLand =>
            "Teams aus demselben Land dürfen nicht in dieselbe Gruppe oder gegeneinander gelost werden. " +
            "Verhindert z. B. nationale Duelle in der Gruppenphase (typisch bei Nationenturnieren).",
        AuslosungsRegelArt.KeineGleicheGruppe =>
            "Teams, die in einer vorangegangenen Gruppenphase bereits in derselben Gruppe waren, " +
            "dürfen nicht erneut aufeinandertreffen. Typisch für UCL/UEL K.O.-Runden.",
        AuslosungsRegelArt.GruppensiegerGegenGruppenzweite =>
            "Gruppensieger werden nur mit Zweitplatzierten einer anderen Gruppe gepaart — " +
            "nie gegeneinander und nie gegen den Zweiten der eigenen Gruppe. " +
            "Strukturregel für K.O.-Runden nach einer Gruppenphase (z. B. WM-Achtelfinale).",
        AuslosungsRegelArt.GesetzteGegenUngesetzte =>
            "Gesetzte Teams (Topf 1) werden ausschließlich mit ungesetzten Teams gepaart. " +
            "Zwei gesetzte Teams treffen nie direkt aufeinander. " +
            "Klassische Setzlisten-Regel für nationale und internationale Pokal-Auslosungen. " +
            "Ergänzt 'NachSetzliste' als Paarungsbeschränkung — die Auslosungsart befüllt die Töpfe, diese Regel steuert die Paarungsbildung.",
        AuslosungsRegelArt.HeimrechtNachLos =>
            "Das Heimrecht wird separat ausgelost: Ein zufällig gezogenes Team erhält das Heimrecht " +
            "für das Hinspiel, das andere für das Rückspiel. " +
            "Nur relevant bei Hin- und Rückspielen.",
        AuslosungsRegelArt.HeimrechtNachSetzliste =>
            "Das höher gesetzte (besser platzierte) Team erhält automatisch das Heimrecht im Rückspiel. " +
            "Bevorzugt Titelverteidiger und Top-Gesetzte — typisch für UEFA-Wettbewerbe.",
        AuslosungsRegelArt.FreilosMoeglich =>
            "Bei ungerader Teamanzahl erhält ein zufällig gezogenes Team ein Freilos und zieht " +
            "kampflos in die nächste Runde ein. Ohne diese Regel schlägt die Auslosung fehl, " +
            "wenn die Anzahl ungerade ist.",
        AuslosungsRegelArt.ManuelleEinschraenkung =>
            "Bestimmte Paarungen sind aus besonderen Gründen ausgeschlossen (z. B. politische " +
            "oder logistische Einschränkungen). Die konkreten Verbote werden separat konfiguriert.",
        _ => ""
    };
}
