using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Wpf.Navigation;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

// ── Darstellungstypen ────────────────────────────────────────────────────────

public record TeilnehmerAnzeige(int? MannschaftId, string Name, string? Kuerzel, int EintragId = 0, int? LandId = null)
{
    public bool IstPlatzhalter => !MannschaftId.HasValue;

    public static TeilnehmerAnzeige AusMannschaft(Mannschaft m) =>
        new(m.Id, m.Name, m.Land?.Kurzname, LandId: m.LandId);

    public static TeilnehmerAnzeige AusPlatzhalter(string name) =>
        new(null, name, null);

    public static TeilnehmerAnzeige AusEintrag(LostopfEintrag e) =>
        e.MannschaftId.HasValue
            ? new(e.MannschaftId, e.Mannschaft?.Name ?? "?", e.Mannschaft?.Land?.Kurzname, e.Id, e.Mannschaft?.LandId)
            : new(null, e.PlatzhalterName ?? "?", null, e.Id);
}

public record ErgebnisAnzeige(int PaarungsNummer, string HeimName, string AuswaertsName, bool IstFreilos);
public record GruppenErgebnisAnzeige(int GruppenNummer, string GruppenName, IReadOnlyList<string> Teams);

public class ManuellerKoSlot : ObservableObject
{
    private TeilnehmerAnzeige? _team;
    public int PaarungsNr { get; init; }
    public bool IstHeim { get; init; }
    public int? QuellTopfId { get; private set; }

    public TeilnehmerAnzeige? Team
    {
        get => _team;
        private set
        {
            if (SetProperty(ref _team, value))
            {
                OnPropertyChanged(nameof(IstLeer));
                OnPropertyChanged(nameof(AnzeigeName));
            }
        }
    }
    public bool IstLeer => Team is null;
    public string AnzeigeName => Team?.Name ?? "…";

    public void Besetzen(TeilnehmerAnzeige team, int topfId) { Team = team; QuellTopfId = topfId; }
    public (TeilnehmerAnzeige Team, int TopfId)? Leeren()
    {
        if (Team is null) return null;
        var ergebnis = (Team, QuellTopfId!.Value);
        Team = null; QuellTopfId = null;
        return ergebnis;
    }
}

public record ManuelleKoPaarungAnzeige(int PaarungsNr, ManuellerKoSlot HeimSlot, ManuellerKoSlot AuswaertsSlot);

public class ManuellerGruppenSlot : ObservableObject
{
    private TeilnehmerAnzeige? _team;
    public int GruppenNr { get; init; }
    public int SlotNr { get; init; }
    public int? QuellTopfId { get; private set; }

    public TeilnehmerAnzeige? Team
    {
        get => _team;
        private set
        {
            if (SetProperty(ref _team, value))
            {
                OnPropertyChanged(nameof(IstLeer));
                OnPropertyChanged(nameof(AnzeigeName));
            }
        }
    }
    public bool IstLeer => Team is null;
    public string AnzeigeName => Team?.Name ?? "…";

    public void Besetzen(TeilnehmerAnzeige team, int topfId) { Team = team; QuellTopfId = topfId; }
    public (TeilnehmerAnzeige Team, int TopfId)? Leeren()
    {
        if (Team is null) return null;
        var ergebnis = (Team, QuellTopfId!.Value);
        Team = null; QuellTopfId = null;
        return ergebnis;
    }
}

public class ManuelleGruppe : ObservableObject
{
    public int GruppenNr { get; init; }
    public string GruppenName { get; init; } = "";
    public ObservableCollection<ManuellerGruppenSlot> Slots { get; init; } = [];
}

public class LostopfViewModel : ObservableObject
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public int Reihenfolge { get; init; }
    public ObservableCollection<TeilnehmerAnzeige> Teilnehmer { get; init; } = [];
}

// ── TV-Modus: Live-Ziehungsanzeige (Issue #5) ────────────────────────────────

public class AnimationsGruppenSlot : ObservableObject
{
    public int Position { get; init; }
    private string _anzeige = "…";
    public string Anzeige
    {
        get => _anzeige;
        set { if (SetProperty(ref _anzeige, value)) OnPropertyChanged(nameof(Befuellt)); }
    }
    public bool Befuellt => _anzeige != "…";
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
    public string Heim { get => _heim; set => SetProperty(ref _heim, value); }
    private string _auswaerts = "…";
    public string Auswaerts { get => _auswaerts; set => SetProperty(ref _auswaerts, value); }
}

/// <summary>Feste Anzahl Topf-Plätze für die TV-Modus-Anzeige: die Slots werden nie hinzugefügt
/// oder entfernt, damit sich das Layout nicht verschiebt, sobald ein Topf leerer wird — nur
/// Gezogen/Anzeige ändern sich pro Slot.</summary>
public class AnimationsTopfSlot : ObservableObject
{
    public int EintragId { get; init; }
    public string Name { get; init; } = "";
    private bool _gezogen;
    public bool Gezogen
    {
        get => _gezogen;
        set { if (SetProperty(ref _gezogen, value)) OnPropertyChanged(nameof(Anzeige)); }
    }
    public string Anzeige => Gezogen ? "…" : Name;
}

public class AnimationsTopf
{
    public int TopfId { get; init; }
    public string Name { get; init; } = "";
    public ObservableCollection<AnimationsTopfSlot> Slots { get; init; } = [];
}

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
    public int Max { get; init; }
    public int? Min { get; init; }

    public bool UeberMax => Aktuell > Max;
    public bool UnterMin => Min.HasValue && Aktuell < Min.Value;
    public string AnzeigeText => Min.HasValue ? $"{Aktuell} / {Max} (min. {Min})" : $"{Aktuell} / {Max}";
}

public record AuslosungsartOption(string Anzeige, Auslosungsart Wert);
public record RanglisteOption(string Anzeige, Rangliste? Wert);

// ── Haupt-ViewModel ─────────────────────────────────────────────────────────

public partial class AuslosungViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly AuslosungService _auslosungService;
    private readonly TurnierInstanzService _instanzService;
    private readonly MannschaftService _mannschaftService;
    private readonly SpielplanService _spielplanService;
    private readonly RanglisteService _ranglisteService;
    private readonly VerbandsQuoteService _verbandsQuoteService;
    private readonly AusgewaehlteTurnierInstanzService _turnierAuswahlService;

    private Auslosung? _auslosungDetail;

    // ── Linke Spalte ──────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<TurnierInstanz> _instanzen = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NeuCommand))]
    [NotifyPropertyChangedFor(nameof(KeinTurnierGewaehlt))]
    private TurnierInstanz? _ausgewaehlteInstanz;

    [ObservableProperty] private ObservableCollection<Auslosung> _auslosungen = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private Auslosung? _ausgewaehlteAuslosung;

    [ObservableProperty] private ObservableCollection<TurnierRunde> _instanzRunden = [];

    // ── Neue-Auslosung-Formular ────────────────────────────────────────────────
    [ObservableProperty]
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
    private Auslosungsart _neuAuslosungsart = Auslosungsart.Zufall;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyPropertyChangedFor(nameof(AuslosungsartHinweis))]
    [NotifyPropertyChangedFor(nameof(AuslosungsartWarnung))]
    private TurnierRunde? _neuRunde;

    private string _autoNeuName = "";

    [ObservableProperty] private int _neuAnzahlLostoepfe = 1;

    // ── Auslosung-Detail ──────────────────────────────────────────────────────
    [ObservableProperty] private bool _detailAktiv;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstManuellerModus))]
    [NotifyPropertyChangedFor(nameof(IstBearbeitbarNichtManuell))]
    [NotifyPropertyChangedFor(nameof(IstManuellKoModus))]
    [NotifyPropertyChangedFor(nameof(IstManuellGruppenModus))]
    private bool _istBearbeitbar;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailAuslosungsartHinweis))]
    [NotifyPropertyChangedFor(nameof(DetailAuslosungsartWarnung))]
    [NotifyPropertyChangedFor(nameof(IstManuellerModus))]
    [NotifyPropertyChangedFor(nameof(IstBearbeitbarNichtManuell))]
    [NotifyPropertyChangedFor(nameof(IstManuellKoModus))]
    [NotifyPropertyChangedFor(nameof(IstManuellGruppenModus))]
    [NotifyPropertyChangedFor(nameof(DetailAnzahlLostoepfeBearbeitbar))]
    private Auslosungsart _detailAuslosungsart;

    [ObservableProperty] private string _detailName = "";
    [ObservableProperty] private int _detailAnzahlLostoepfe = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    private bool _istDetailDirty;

    private bool _ladenAktiv;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstPaarungsergebnis))]
    [NotifyPropertyChangedFor(nameof(ZeigeGruppenErgebnis))]
    [NotifyPropertyChangedFor(nameof(ZeigePaarungsergebnis))]
    private bool _istAbgeschlossen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstPaarungsergebnis))]
    [NotifyPropertyChangedFor(nameof(ZeigeGruppenErgebnis))]
    [NotifyPropertyChangedFor(nameof(ZeigePaarungsergebnis))]
    private bool _istGruppenergebnis;

    public bool IstPaarungsergebnis => IstAbgeschlossen && !IstGruppenergebnis;

    /// <summary>Das statische Endergebnis (GruppenErgebnisse/ErgebnisseAnzeige) bleibt während der
    /// TV-Modus-Animation im Hintergrund unverändert (bereits von LadeDetailAsync geladen) — sichtbar
    /// wird es erst, wenn die Animation fertig ist bzw. übersprungen wurde. Während der Animation
    /// zeigt AnimationGruppen/AnimationPaarungen die Live-Ziehungsanzeige.</summary>
    public bool ZeigeGruppenErgebnis   => IstGruppenergebnis && AnimationLaeuftNicht;
    public bool ZeigePaarungsergebnis  => IstPaarungsergebnis && AnimationLaeuftNicht;

    [ObservableProperty] private string _auslosungKopfzeile = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerfuegbareTeilnehmerGefiltert))]
    private ObservableCollection<TeilnehmerAnzeige> _verfuegbareTeilnehmer = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerfuegbareTeilnehmerGefiltert))]
    private string _teilnehmerFilter = "";

    [ObservableProperty] private TeilnehmerAnzeige? _ausgewaehlterVerfuegbarTeilnehmer;

    [ObservableProperty] private ObservableCollection<LostopfViewModel> _lostoepfe = [];

    // ── Automatische Lostopf-Zuordnung via Rangliste (Issue #119) ────────────────
    private static readonly RanglisteOption _keineRangliste = new("— Keine —", null);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RanglisteOptionen))]
    private ObservableCollection<Rangliste> _alleRanglisten = [];

    [ObservableProperty] private RanglisteOption _ausgewaehlteRanglisteOption = _keineRangliste;

    public IReadOnlyList<RanglisteOption> RanglisteOptionen =>
        [_keineRangliste, .. AlleRanglisten.Select(r => new RanglisteOption(r.Name, r))];

    [ObservableProperty] private ObservableCollection<RegelViewModel> _regeln = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigeVerbandsQuoten))]
    private ObservableCollection<VerbandsQuoteAnzeigeViewModel> _verbandsQuotenAnzeige = [];

    public bool ZeigeVerbandsQuoten => VerbandsQuotenAnzeige.Count > 0;
    [ObservableProperty] private ObservableCollection<ErgebnisAnzeige> _ergebnisseAnzeige = [];
    [ObservableProperty] private ObservableCollection<GruppenErgebnisAnzeige> _gruppenErgebnisse = [];

    // ── TV-Modus: Schrittweise Ziehungsanimation (Issue #5) ──────────────────
    [ObservableProperty] private bool _tvModusAktiv;
    [ObservableProperty] private bool _tvModusManuell;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimationLaeuftNicht))]
    [NotifyPropertyChangedFor(nameof(ZeigeGruppenErgebnis))]
    [NotifyPropertyChangedFor(nameof(ZeigePaarungsergebnis))]
    private bool _animationLaeuft;

    [ObservableProperty] private bool _animationWartetAufWeiter;
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

    private bool _animationUeberspringen;
    private TaskCompletionSource _weiterSignal = new();
    private static readonly TimeSpan _tvModusSchrittVerzoegerung = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan _tvModusPausenVerzoegerung  = TimeSpan.FromMilliseconds(500);

    /// <summary>Ein einzelner Ziehungsschritt für den TV-Modus: eine Mannschaft aus einem Lostopf,
    /// die einem bereits feststehenden Ziel (Gruppe/Paarung) zugeordnet ist. Das Endergebnis steht
    /// durch AusfuehrenAsync schon fest (holistischer Zuordnungsalgorithmus, siehe Issue #5-Staleness-
    /// Check) — hier wird nur die Reihenfolge/Dramaturgie der Enthüllung bestimmt. Die Position
    /// innerhalb einer Gruppe wird bewusst unabhängig von der Ziehungsreihenfolge zufällig vergeben
    /// (wie bei historischen Auslosungsverfahren mit separater Positions-Kugel).</summary>
    private sealed record ZiehungsSchritt(
        TeilnehmerAnzeige Team, int TopfId, string TopfName, int ZielNummer,
        bool IstGruppenmodus, bool? IstHeim, int? Position, bool IstFreilos);

    // ── Manuelle Auslosung ────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<ManuelleKoPaarungAnzeige> _manuelleKoPaarungen = [];
    [ObservableProperty] private ObservableCollection<ManuelleGruppe> _manuelleGruppen = [];
    [ObservableProperty] private string? _manuellesFehler;

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

    // ── Manueller Modus ───────────────────────────────────────────────────────
    public bool IstManuellerModus          => IstBearbeitbar && DetailAuslosungsart == Auslosungsart.Manuell;
    public bool IstBearbeitbarNichtManuell => IstBearbeitbar && !IstManuellerModus;
    public bool IstManuellKoModus          => IstManuellerModus && _auslosungDetail?.TurnierRunde?.Rundenart != Rundenart.Gruppenphase;
    public bool IstManuellGruppenModus     => IstManuellerModus && _auslosungDetail?.TurnierRunde?.Rundenart == Rundenart.Gruppenphase;

    // ── Status ────────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    // ── Berechnete Eigenschaften ─────────────────────────────────────────────
    public bool KeinTurnierGewaehlt => AusgewaehlteInstanz is null;

    public IEnumerable<TeilnehmerAnzeige> VerfuegbareTeilnehmerGefiltert =>
        string.IsNullOrWhiteSpace(TeilnehmerFilter)
            ? VerfuegbareTeilnehmer
            : VerfuegbareTeilnehmer.Where(t =>
                t.Name.Contains(TeilnehmerFilter, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<AuslosungsartOption> AuslosungsartOptionen =>
        Enum.GetValues<Auslosungsart>()
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
        Auslosungsart.Manuell =>
            "Vollständig manuelle Auslosung ohne Automation: Sie bestimmen jede Paarung (KO-Runde) " +
            "oder Gruppenzuordnung (Gruppenphase) selbst. " +
            "Die konfigurierten Auslosungsregeln (z. B. 'Kein gleiches Land') werden bei jeder Eingabe geprüft, " +
            "damit keine ungültige Paarung entstehen kann.",
        _ => ""
    };

    private static string? AuslosungsartWarnungText(Auslosungsart art, Rundenart? rundenart) =>
        (art, rundenart) switch
        {
            (Auslosungsart.GruppensiegerGegenGruppenzweite, Rundenart.Liga) or
            (Auslosungsart.GruppensiegerGegenGruppenzweite, Rundenart.Gruppenphase) or
            (Auslosungsart.GruppensiegerGegenGruppenzweite, Rundenart.TopfLiga) or
            (Auslosungsart.GruppensiegerGegenGruppenzweite, Rundenart.SchweizerSystem) =>
                "⚠  Diese Auslosungsart setzt voraus, dass bereits eine Gruppenphase gespielt wurde. " +
                "Sie ist für K.O.-Runden gedacht, die auf eine Gruppenphase folgen.",
            (Auslosungsart.TopfZuordnung, Rundenart.Gruppenphase) =>
                "⚠  Für eine klassische Gruppenphase verwenden Sie 'Nach Töpfen' — " +
                "dabei wird ein Team pro Topf in jede Gruppe gelost. " +
                "'Topf-Zuordnung' ist für die UCL-Ligaphase (direkte Gegnerbestimmung) gedacht.",
            (Auslosungsart.TopfZuordnung, Rundenart.KoRunde) or
            (Auslosungsart.TopfZuordnung, Rundenart.Finale) or
            (Auslosungsart.TopfZuordnung, Rundenart.SpielUmPlatzDrei) =>
                "⚠  Topf-Zuordnung weist Teams direkt Gruppenplätzen zu. " +
                "Für K.O.-Runden ist 'Nach Töpfen' oder 'Zufällig' besser geeignet.",
            (Auslosungsart.NachSetzliste, Rundenart.SchweizerSystem) =>
                "⚠  Im Schweizer System wird die Paarung automatisch anhand der Tabelle berechnet. " +
                "Eine manuelle Setzlisten-Auslosung ist hier in der Regel nicht sinnvoll.",
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
                new RibbonAktion
                {
                    Header = "Alle Töpfe leeren",
                    Befehl = AlleToepfeZuruecksetzenCommand,
                    IconGlyph = "",
                },
            ],
        },
    ];

    public AuslosungViewModel(
        AuslosungService auslosungService,
        TurnierInstanzService instanzService,
        MannschaftService mannschaftService,
        SpielplanService spielplanService,
        RanglisteService ranglisteService,
        VerbandsQuoteService verbandsQuoteService,
        AusgewaehlteTurnierInstanzService turnierAuswahlService)
    {
        _auslosungService     = auslosungService;
        _instanzService       = instanzService;
        _mannschaftService    = mannschaftService;
        _spielplanService     = spielplanService;
        _ranglisteService     = ranglisteService;
        _verbandsQuoteService = verbandsQuoteService;
        _turnierAuswahlService = turnierAuswahlService;
    }

    public override async Task AktivierenAsync()
    {
        IstLadend = true;
        try
        {
            var instanzen  = await _instanzService.AlleAbrufenAsync();
            var ranglisten = await _ranglisteService.AlleAbrufenAsync();
            Instanzen     = new ObservableCollection<TurnierInstanz>(instanzen);
            AlleRanglisten = new ObservableCollection<Rangliste>(ranglisten);

            AusgewaehlteInstanz = Instanzen.FirstOrDefault(
                i => i.Id == _turnierAuswahlService.TurnierInstanzId);
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
        NeuRunde            = InstanzRunden.FirstOrDefault();
        NeuAnzahlLostoepfe  = 1;
        Fehlermeldung       = null;
    }

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        Fehlermeldung = null;
        if (NeuFormAktiv)
        {
            if (AusgewaehlteInstanz is null || NeuRunde is null) return;
            try
            {
                var name    = string.IsNullOrWhiteSpace(NeuName) ? NeuRunde.Name : NeuName.Trim();
                var eingabe = new AuslosungErstellenEingabe(
                    AusgewaehlteInstanz.Id, NeuRunde.Id, name,
                    NeuAuslosungsart, false);
                var neu = await _auslosungService.ErstellenAsync(eingabe);

                await _auslosungService.LostoepfeSynchronisierenAsync(neu.Id, Math.Max(1, NeuAnzahlLostoepfe));

                NeuFormAktiv = false;

                var auslosungen = await _auslosungService.AlleNachInstanzAbrufenAsync(AusgewaehlteInstanz.Id);
                Auslosungen = new ObservableCollection<Auslosung>(auslosungen);
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
        if (AusgewaehlteAuslosung is null) return;
        var r = MessageBox.Show($"Soll '{AusgewaehlteAuslosung.Name}' wirklich gelöscht werden?",
            "Auslosung löschen", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;
        Fehlermeldung = null;
        try
        {
            await _auslosungService.LoeschenAsync(AusgewaehlteAuslosung.Id);
            var auslosungen = await _auslosungService.AlleNachInstanzAbrufenAsync(AusgewaehlteInstanz!.Id);
            Auslosungen = new ObservableCollection<Auslosung>(auslosungen);
            AusgewaehlteAuslosung = null;
            DetailAktiv = false;
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    // ── Workspace-Befehle ─────────────────────────────────────────────────────

    [RelayCommand]
    private async Task TeilnehmerZuTopfHinzufuegen(int topfId)
    {
        if (_auslosungDetail is null || AusgewaehlterVerfuegbarTeilnehmer is null) return;
        Fehlermeldung = null;
        var t = AusgewaehlterVerfuegbarTeilnehmer;
        try
        {
            if (t.MannschaftId.HasValue)
                await _auslosungService.MannschaftZuLostopfHinzufuegenAsync(_auslosungDetail.Id, topfId, t.MannschaftId.Value);
            else
                await _auslosungService.PlatzhalterZuLostopfHinzufuegenAsync(_auslosungDetail.Id, topfId, t.Name);
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
    [RelayCommand]
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

            // Runde nicht mit mehr Teams befüllen als sie tatsächlich benötigt (TeamsBenoetigt=0
            // bedeutet: keine bekannte Obergrenze, dann alle verfügbaren Teams verteilen).
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
            int potGroesse = (int)Math.Ceiling(teamsZuVerteilen.Count / (double)toepfe.Count);

            for (int i = 0; i < teamsZuVerteilen.Count; i++)
            {
                var topf = toepfe[Math.Min(i / potGroesse, toepfe.Count - 1)];
                await _auslosungService.MannschaftZuLostopfHinzufuegenAsync(
                    _auslosungDetail.Id, topf.Id, teamsZuVerteilen[i].MannschaftId!.Value);
            }

            await LadeDetailAsync(_auslosungDetail.Id);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    [RelayCommand]
    private async Task AuslosungAusfuehren()
    {
        if (_auslosungDetail is null) return;
        Fehlermeldung = null;
        try
        {
            var vorherigeLostoepfe = _auslosungDetail.Lostoepfe;
            var ergebnisse = await _auslosungService.AusfuehrenAsync(_auslosungDetail.Id);
            var schritte = TvModusAktiv
                ? ZiehungsSchritteAufbauen(vorherigeLostoepfe, ergebnisse)
                : null;

            await LadeDetailAsync(_auslosungDetail.Id);

            if (schritte is not null)
                await ZiehungAnimierenAsync(schritte);

            await SpielplanAutomatischGenerierenAsync();
            await AktualisiereSeitenlisteAsync();
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    [RelayCommand]
    private void AnimationWeiter() => _weiterSignal.TrySetResult();

    [RelayCommand]
    private void AnimationUeberspringen()
    {
        _animationUeberspringen = true;
        _weiterSignal.TrySetResult();
    }

    /// <summary>Baut die Ziehungsreihenfolge für den TV-Modus (Issue #5) auf: analog einer echten
    /// Auslosung wird ein Lostopf vollständig leergezogen, bevor der nächste an die Reihe kommt
    /// (Topf-Reihenfolge nach <see cref="Lostopf.Reihenfolge"/>). Bei Gruppenphase wird zusätzlich
    /// pro Gruppe eine von der Ziehungsreihenfolge unabhängige Position (1..Gruppengröße) verlost —
    /// analog historischer Auslosungsverfahren mit separater Positions-Kugel. Innerhalb eines
    /// einzelnen Topfes wird die Ziehungsreihenfolge gleichmäßig auf alle Zielgruppen verteilt
    /// (reihum eine Gruppe nach der anderen, siehe <see cref="ProGruppeGleichmaessigVerteilen"/>),
    /// damit nicht z.B. Gruppe A bereits ihre 2. Mannschaft aus diesem Topf erhält, während Gruppe B
    /// noch gar keine hat — die Umsortierung bleibt aber strikt auf den jeweiligen Topf beschränkt,
    /// ein bereits begonnener Topf wird also weiterhin vollständig geleert, bevor der nächste
    /// beginnt.</summary>
    private static List<ZiehungsSchritt> ZiehungsSchritteAufbauen(
        IEnumerable<Lostopf> lostoepfe, IReadOnlyList<AuslosungsErgebnis> ergebnisse)
    {
        bool istGruppenmodus = ergebnisse.Any() &&
            ergebnisse.All(e => !e.IstFreilos && e.AuswaertsMannschaftId == null && e.AuswaertsPlatzhalterName == null);

        var positionenProGruppe = new Dictionary<int, Queue<int>>();
        if (istGruppenmodus)
        {
            foreach (var gruppe in ergebnisse.GroupBy(e => e.PaarungsNummer))
            {
                var positionen = Enumerable.Range(1, gruppe.Count()).OrderBy(_ => Random.Shared.Next()).ToList();
                positionenProGruppe[gruppe.Key] = new Queue<int>(positionen);
            }
        }

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
                    int position = positionenProGruppe[treffer.PaarungsNummer].Dequeue();
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

    /// <summary>Verteilt die Ziehungsschritte EINES Topfes gleichmäßig auf alle Zielgruppen: reihum
    /// wird aus jeder Gruppen-Warteschlange (in zufälliger Gruppen-Reihenfolge je Runde) je ein
    /// Schritt entnommen, bis alle Warteschlangen leer sind. Verhindert, dass innerhalb eines
    /// Topfes eine Gruppe bereits mehrere Mannschaften erhält, während eine andere noch leer
    /// ausgeht.</summary>
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

    /// <summary>Spielt die Ziehungsschritte nacheinander im UI ab: pro Schritt wird zunächst die
    /// gezogene Mannschaft angezeigt (dabei in der Topf-Anzeige als "gezogen" markiert — die Slots
    /// bleiben zahlenmäßig fix, nur ihr Inhalt wechselt zu "…", damit sich das Layout nicht
    /// verschiebt, sobald ein Topf leerer wird), dann das Ziel (Gruppe/Paarung), bei Gruppenphase
    /// zusätzlich die Position — je nach TvModusManuell entweder automatisch mit Verzögerung oder
    /// wartend auf einen "Weiter"-Klick. AnimationGruppen/AnimationPaarungen werden vorab mit der
    /// vollen Anzahl leerer Plätze ("…") aufgebaut (nicht erst bei der ersten Zuweisung sichtbar)
    /// und danach Slot für Slot befüllt. Das bereits von LadeDetailAsync geladene Endergebnis
    /// (GruppenErgebnisse/ErgebnisseAnzeige) wird während der Animation nicht verändert — es wird
    /// einfach erst nach Animationsende (bzw. bei "Überspringen" sofort) sichtbar, siehe
    /// AnimationLaeuftNicht-Bindung in der View.</summary>
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
                    l.Teilnehmer.Select(t => new AnimationsTopfSlot { EintragId = t.EintragId, Name = t.Name }))
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

                // Mannschaft wird "aus dem Topf gezogen" — Slot bleibt bestehen, wird nur als
                // gezogen markiert (kein Remove, damit sich die Topf-Anzeige nicht verschiebt).
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
                    gruppe.Slots[schritt.Position!.Value - 1].Anzeige = schritt.Team.Name;
                }
                else
                {
                    var paarung = AnimationPaarungen.First(p => p.PaarungsNr == schritt.ZielNummer);
                    if (schritt.IstFreilos || schritt.IstHeim == true) paarung.Heim = schritt.Team.Name;
                    else paarung.Auswaerts = schritt.Team.Name;
                }

                // Vollständig gezogene Mannschaft: Banner leeren (kurzer "blank screen"), dann Pause
                // vor der nächsten Mannschaft — sowohl automatisch (kurze Verzögerung) als auch
                // manuell (wartet auf "Weiter") auf Nutzerwunsch entkoppelt von den Reveal-Schritten.
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

    /// <summary>Generiert den Spielplan automatisch, sobald die Auslosung abgeschlossen ist - der manuelle
    /// "Neu generieren"-Klick im Spielplan entfällt dadurch (Issue #19). Erwartet, dass _auslosungDetail
    /// bereits per LadeDetailAsync neu geladen wurde (inkl. Ergebnisse).</summary>
    private async Task SpielplanAutomatischGenerierenAsync()
    {
        if (_auslosungDetail is null || _auslosungDetail.Status != AuslosungsStatus.Abgeschlossen) return;
        await _spielplanService.GenerierenAusAuslosungAsync(
            _auslosungDetail.TurnierRundeId, [.. _auslosungDetail.Ergebnisse]);
    }

    // ── Öffentliche Methoden: Manuelle Slot-Zuweisung ────────────────────────

    public string? TeamInKoSlotPlatzieren(TeilnehmerAnzeige team, int quellTopfId, ManuellerKoSlot slot)
    {
        ManuellesFehler = null;
        if (HatKeinGleichesLandVerletzung(team, slot.PaarungsNr, istHeim: slot.IstHeim, out var fehler))
        {
            ManuellesFehler = fehler;
            return fehler;
        }
        EntferneAusTopf(team, quellTopfId);
        slot.Besetzen(team, quellTopfId);
        return null;
    }

    public void TeamAusKoSlotEntfernen(ManuellerKoSlot slot)
    {
        var ergebnis = slot.Leeren();
        if (ergebnis is not null) GibeAnTopfZurueck(ergebnis.Value.Team, ergebnis.Value.TopfId);
        ManuellesFehler = null;
    }

    public string? TeamInGruppenSlotPlatzieren(TeilnehmerAnzeige team, int quellTopfId, ManuellerGruppenSlot slot)
    {
        ManuellesFehler = null;
        if (HatKeinGleichesLandVerletzungGruppe(team, slot.GruppenNr, out var fehler))
        {
            ManuellesFehler = fehler;
            return fehler;
        }
        EntferneAusTopf(team, quellTopfId);
        slot.Besetzen(team, quellTopfId);
        return null;
    }

    public void TeamAusGruppenSlotEntfernen(ManuellerGruppenSlot slot)
    {
        var ergebnis = slot.Leeren();
        if (ergebnis is not null) GibeAnTopfZurueck(ergebnis.Value.Team, ergebnis.Value.TopfId);
        ManuellesFehler = null;
    }

    private void EntferneAusTopf(TeilnehmerAnzeige team, int topfId)
    {
        var topf    = Lostoepfe.FirstOrDefault(t => t.Id == topfId);
        var eintrag = topf?.Teilnehmer.FirstOrDefault(t =>
            (t.EintragId > 0 && t.EintragId == team.EintragId) || t.Name == team.Name);
        if (eintrag is not null) topf!.Teilnehmer.Remove(eintrag);
    }

    private void GibeAnTopfZurueck(TeilnehmerAnzeige team, int topfId)
    {
        var topf = Lostoepfe.FirstOrDefault(t => t.Id == topfId);
        if (topf is null) return;
        bool bereitsVorhanden = topf.Teilnehmer.Any(t =>
            (t.EintragId > 0 && t.EintragId == team.EintragId) || t.Name == team.Name);
        if (!bereitsVorhanden) topf.Teilnehmer.Add(team);
    }

    public ManuellerKoSlot? NaechsterFreierKoSlot() =>
        ManuelleKoPaarungen.SelectMany(p => new[] { p.HeimSlot, p.AuswaertsSlot }).FirstOrDefault(s => s.IstLeer);

    public ManuellerGruppenSlot? NaechsterFreierGruppenSlot() =>
        ManuelleGruppen.SelectMany(g => g.Slots).FirstOrDefault(s => s.IstLeer);

    [RelayCommand]
    private async Task ManuelleAuslosungAbschliessen()
    {
        if (_auslosungDetail is null) return;
        ManuellesFehler = null;
        Fehlermeldung = null;
        try
        {
            List<ManuellePaarungEingabe> paarungen;
            if (IstManuellGruppenModus)
            {
                var alleSlots = ManuelleGruppen
                    .SelectMany(g => g.Slots.Select(s => (g.GruppenNr, s)))
                    .Where(x => !x.s.IstLeer)
                    .ToList();
                if (!alleSlots.Any())
                {
                    ManuellesFehler = "Bitte mindestens eine Zuweisung vornehmen.";
                    return;
                }
                var leereSlots = ManuelleGruppen.Sum(g => g.Slots.Count(s => s.IstLeer));
                if (leereSlots > 0)
                {
                    ManuellesFehler = $"Es sind noch {leereSlots} Gruppenplatz/-plätze unbesetzt. " +
                                      "Bitte alle Plätze befüllen, bevor die Auslosung abgeschlossen wird.";
                    return;
                }
                paarungen = alleSlots.Select(x => new ManuellePaarungEingabe(
                    HeimMannschaftId:         x.s.Team!.MannschaftId,
                    HeimPlatzhalterName:      x.s.Team!.MannschaftId is null ? x.s.Team!.Name : null,
                    AuswaertsMannschaftId:    null,
                    AuswaertsPlatzhalterName: null,
                    PaarungsNummer:           x.GruppenNr
                )).ToList();
            }
            else
            {
                var allePaarungen = ManuelleKoPaarungen
                    .Where(p => !p.HeimSlot.IstLeer)
                    .ToList();
                if (!allePaarungen.Any())
                {
                    ManuellesFehler = "Bitte mindestens eine Paarung befüllen.";
                    return;
                }
                paarungen = allePaarungen.Select(p => new ManuellePaarungEingabe(
                    HeimMannschaftId:         p.HeimSlot.Team!.MannschaftId,
                    HeimPlatzhalterName:      p.HeimSlot.Team!.MannschaftId is null ? p.HeimSlot.Team!.Name : null,
                    AuswaertsMannschaftId:    p.AuswaertsSlot.Team?.MannschaftId,
                    AuswaertsPlatzhalterName: p.AuswaertsSlot.Team?.MannschaftId is null ? p.AuswaertsSlot.Team?.Name : null,
                    PaarungsNummer:           p.PaarungsNr
                )).ToList();
            }
            await _auslosungService.ManuelleErgebnisseAbschliessenAsync(_auslosungDetail.Id, paarungen);
            await LadeDetailAsync(_auslosungDetail.Id);
            await SpielplanAutomatischGenerierenAsync();
            await AktualisiereSeitenlisteAsync();
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    private bool HatKeinGleichesLandVerletzung(TeilnehmerAnzeige neuTeam, int paarungsNr, bool istHeim, out string? fehler)
    {
        fehler = null;
        if (_auslosungDetail?.Regeln.Any(r => r.IstAktiv && r.Regelart == AuslosungsRegelArt.KeinGleichesLand) != true)
            return false;
        if (!neuTeam.LandId.HasValue) return false;
        var paarung = ManuelleKoPaarungen.FirstOrDefault(p => p.PaarungsNr == paarungsNr);
        if (paarung is null) return false;
        var gegnerSlot = istHeim ? paarung.AuswaertsSlot : paarung.HeimSlot;
        if (gegnerSlot.IstLeer || !gegnerSlot.Team!.LandId.HasValue) return false;
        if (gegnerSlot.Team!.LandId == neuTeam.LandId)
        {
            fehler = $"Regel 'Kein gleiches Land': '{neuTeam.Name}' und '{gegnerSlot.Team!.Name}' " +
                     "sind aus demselben Land und dürfen nicht gepaart werden.";
            return true;
        }
        return false;
    }

    private bool HatKeinGleichesLandVerletzungGruppe(TeilnehmerAnzeige neuTeam, int gruppenNr, out string? fehler)
    {
        fehler = null;
        if (_auslosungDetail?.Regeln.Any(r => r.IstAktiv && r.Regelart == AuslosungsRegelArt.KeinGleichesLand) != true)
            return false;
        if (!neuTeam.LandId.HasValue) return false;
        var gruppe = ManuelleGruppen.FirstOrDefault(g => g.GruppenNr == gruppenNr);
        if (gruppe is null) return false;
        var konflikt = gruppe.Slots.FirstOrDefault(s => !s.IstLeer && s.Team!.LandId == neuTeam.LandId);
        if (konflikt is not null)
        {
            fehler = $"Regel 'Kein gleiches Land': '{neuTeam.Name}' und '{konflikt.Team!.Name}' " +
                     "sind aus demselben Land und dürfen nicht in dieselbe Gruppe.";
            return true;
        }
        return false;
    }

    [RelayCommand]
    private async Task AuslosungZuruecksetzen()
    {
        if (_auslosungDetail is null) return;
        Fehlermeldung = null;
        try
        {
            await _auslosungService.ZuruecksetzenAsync(_auslosungDetail.Id);
            await LadeDetailAsync(_auslosungDetail.Id);
            await AktualisiereSeitenlisteAsync();
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    // ── Öffentliche Methoden für D&D und Kontextmenü ─────────────────────────

    public async Task TeilnehmerZuTopfVerschieben(TeilnehmerAnzeige teilnehmer, int? vonTopfId, int nachTopfId)
    {
        if (_auslosungDetail is null) return;
        Fehlermeldung = null;
        try
        {
            if (vonTopfId.HasValue)
                await _auslosungService.EintragAusLostopfEntfernenAsync(_auslosungDetail.Id, vonTopfId.Value, teilnehmer.EintragId);

            if (teilnehmer.MannschaftId.HasValue)
                await _auslosungService.MannschaftZuLostopfHinzufuegenAsync(_auslosungDetail.Id, nachTopfId, teilnehmer.MannschaftId.Value);
            else
                await _auslosungService.PlatzhalterZuLostopfHinzufuegenAsync(_auslosungDetail.Id, nachTopfId, teilnehmer.Name);

            await LadeDetailAsync(_auslosungDetail.Id);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    public async Task TeilnehmerZuVerfuegbarZurueck(TeilnehmerAnzeige teilnehmer, int vonTopfId)
    {
        if (_auslosungDetail is null) return;
        Fehlermeldung = null;
        try
        {
            await _auslosungService.EintragAusLostopfEntfernenAsync(_auslosungDetail.Id, vonTopfId, teilnehmer.EintragId);
            await LadeDetailAsync(_auslosungDetail.Id);
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

    [RelayCommand]
    private async Task AlleToepfeZuruecksetzen()
    {
        if (_auslosungDetail is null) return;
        var result = System.Windows.MessageBox.Show(
            "Alle Töpfe leeren und alle Teams zurück in die Verfügbar-Liste?",
            "Töpfe zurücksetzen",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes) return;
        Fehlermeldung = null;
        try
        {
            await _auslosungService.AlleToepfeZuruecksetzenAsync(_auslosungDetail.Id);
            await LadeDetailAsync(_auslosungDetail.Id);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    // ── Interne Hilfsmethoden ─────────────────────────────────────────────────

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(AusgewaehlteInstanz))
        {
            _turnierAuswahlService.TurnierInstanzId = AusgewaehlteInstanz?.Id;
            _ = BeiInstanzAenderungAsync();
        }
        if (e.PropertyName == nameof(AusgewaehlteAuslosung))
            _ = BeiAuslosungAenderungAsync();
    }

    private async Task BeiInstanzAenderungAsync()
    {
        Auslosungen.Clear();
        InstanzRunden.Clear();
        VerfuegbareTeilnehmer.Clear();
        TeilnehmerFilter = "";
        DetailAktiv  = false;
        NeuFormAktiv = false;
        if (AusgewaehlteInstanz is null) return;

        IstLadend = true;
        try
        {
            var auslosungen = await _auslosungService.AlleNachInstanzAbrufenAsync(AusgewaehlteInstanz.Id);
            Auslosungen = new ObservableCollection<Auslosung>(auslosungen);

            var detail = await _instanzService.FindenNachIdMitRundenAsync(AusgewaehlteInstanz.Id);
            if (detail is not null)
                InstanzRunden = new ObservableCollection<TurnierRunde>(detail.Runden.OrderBy(r => r.Reihenfolge));
        }
        finally { IstLadend = false; }
    }

    private async Task BeiAuslosungAenderungAsync()
    {
        NeuFormAktiv = false;
        if (AusgewaehlteAuslosung is null) { DetailAktiv = false; return; }
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

            ManuelleKoPaarungen = [];
            ManuelleGruppen     = [];
            ManuellesFehler     = null;

            // Determine round order to decide real-teams vs placeholders
            var aktuelleRunde = InstanzRunden.FirstOrDefault(r => r.Id == detail.TurnierRundeId);
            var vorherigeRunde = aktuelleRunde is not null
                ? InstanzRunden.Where(r => r.Reihenfolge < aktuelleRunde.Reihenfolge)
                               .OrderByDescending(r => r.Reihenfolge)
                               .FirstOrDefault()
                : null;

            IReadOnlyList<TeilnehmerAnzeige> alleTeilnehmer;
            if (vorherigeRunde is null)
            {
                var teams = await _mannschaftService.AlleNachTurnierfilterAsync(
                    AusgewaehlteInstanz?.VerbandId,
                    AusgewaehlteInstanz?.MannschaftsartFilter ?? MannschaftsartFilter.Alle);
                alleTeilnehmer = teams.Select(TeilnehmerAnzeige.AusMannschaft).ToList();
            }
            else
            {
                alleTeilnehmer = PlatzhalterGenerieren(aktuelleRunde!, vorherigeRunde);
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
                    // Zuordnungsreihenfolge (nach Eintrags-Id, nicht alphabetisch) — damit die
                    // automatische Zuordnung nach Rangliste (Issue #119) im Topf erkennbar bleibt.
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

            // Detect whether results are group assignments (Gruppenphase) or match pairings
            bool istGruppe = detail.Ergebnisse.Any() &&
                detail.Ergebnisse.All(e => !e.IstFreilos &&
                    e.AuswaertsMannschaftId == null &&
                    e.AuswaertsPlatzhalterName == null);

            IstGruppenergebnis = istGruppe;

            if (istGruppe)
            {
                GruppenErgebnisse = new ObservableCollection<GruppenErgebnisAnzeige>(
                    detail.Ergebnisse
                        .GroupBy(e => e.PaarungsNummer)
                        .OrderBy(g => g.Key)
                        .Select(g => new GruppenErgebnisAnzeige(
                            g.Key,
                            $"Gruppe {GruppenBuchstabe(g.Key)}",
                            g.Select(e => e.HeimMannschaft?.Name ?? e.HeimPlatzhalterName ?? "?")
                             .OrderBy(n => n)
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
                        e.IstFreilos)));
            }

            TeamsZugewiesen       = detail.Lostoepfe.SelectMany(l => l.Eintraege).Count();
            TeamsBenoetigt        = detail.TurnierRunde?.EingangsMannschaftsanzahl ?? 0;

            // IstBearbeitbar muss vor ManuelleSlotsAufbauen gesetzt werden, da IstManuellerModus davon abhängt.
            IstBearbeitbar        = detail.Status is not (AuslosungsStatus.Abgeschlossen or AuslosungsStatus.Abgebrochen);
            IstAbgeschlossen      = detail.Status == AuslosungsStatus.Abgeschlossen;
            DetailName            = detail.Name;
            DetailAuslosungsart   = detail.Auslosungsart;
            DetailAnzahlLostoepfe = Math.Max(1, detail.Lostoepfe.Count);

            // Explizit statt nur über den OnDetailAuslosungsartChanged-Seiteneffekt aufrufen: Der
            // [ObservableProperty]-Setter feuert nur bei einer tatsächlichen Werteänderung — bleibt
            // die Auslosungsart über ein Neuladen hinweg unverändert (z. B. nach 'Zurücksetzen' einer
            // Manuell-Auslosung), würde der Slot-Aufbau sonst übersprungen und die Anzeige leer bleiben.
            ManuelleSlotsAufbauen();

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

    private static IReadOnlyList<TeilnehmerAnzeige> PlatzhalterGenerieren(
        TurnierRunde aktuelleRunde, TurnierRunde vorherigeRunde)
    {
        int anzahl = aktuelleRunde.EingangsMannschaftsanzahl;

        if (vorherigeRunde.Rundenart == Rundenart.Gruppenphase && vorherigeRunde.GruppenAnzahl.HasValue)
        {
            var gruppen  = vorherigeRunde.GruppenAnzahl.Value;
            var proGruppe = vorherigeRunde.AusgangsMannschaftsanzahl / gruppen;
            var liste = new List<TeilnehmerAnzeige>();
            for (int platz = 1; platz <= proGruppe; platz++)
            {
                for (int g = 0; g < gruppen; g++)
                {
                    var buchstabe = (char)('A' + g);
                    liste.Add(TeilnehmerAnzeige.AusPlatzhalter($"{platz}. Gruppe {buchstabe}"));
                }
            }
            return liste.Take(anzahl).ToList();
        }

        if (vorherigeRunde.Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei)
        {
            var spiele  = vorherigeRunde.EingangsMannschaftsanzahl / 2;
            var kuerzel = KoPlatzhalterKuerzel(vorherigeRunde);
            string prefix = aktuelleRunde.Rundenart == Rundenart.SpielUmPlatzDrei ? "Verlierer" : "Sieger";
            return Enumerable.Range(1, Math.Min(spiele, anzahl))
                .Select(i => TeilnehmerAnzeige.AusPlatzhalter($"{prefix} {kuerzel} {i}"))
                .ToList();
        }

        return Enumerable.Range(1, anzahl)
            .Select(i => TeilnehmerAnzeige.AusPlatzhalter($"Platz {i} ({vorherigeRunde.Name})"))
            .ToList();
    }

    private static string KoPlatzhalterKuerzel(TurnierRunde runde) => runde.Rundenart switch
    {
        Rundenart.Finale           => "Finale",
        Rundenart.SpielUmPlatzDrei => "3.Platz",
        _                          => SpielplanViewModel.KoKuerzel(runde.EingangsMannschaftsanzahl) ?? runde.Name
    };

    private async Task AlleTeamsZuErstemTopfHinzufuegenAsync(int auslosungId, TurnierRunde runde)
    {
        var detail = await _auslosungService.FindenNachIdAsync(auslosungId);
        var ersterTopf = detail?.Lostoepfe.FirstOrDefault();
        if (ersterTopf is null) return;

        var aktuelleRunde  = InstanzRunden.FirstOrDefault(r => r.Id == runde.Id);
        var vorherigeRunde = aktuelleRunde is not null
            ? InstanzRunden.Where(r => r.Reihenfolge < aktuelleRunde.Reihenfolge)
                           .OrderByDescending(r => r.Reihenfolge)
                           .FirstOrDefault()
            : null;

        if (vorherigeRunde is null)
        {
            var teams = await _mannschaftService.AlleNachTurnierfilterAsync(
                AusgewaehlteInstanz?.VerbandId,
                AusgewaehlteInstanz?.MannschaftsartFilter ?? MannschaftsartFilter.Alle);
            var zuFuellende = runde.EingangsMannschaftsanzahl > 0
                ? teams.Take(runde.EingangsMannschaftsanzahl)
                : (IEnumerable<Mannschaft>)teams;
            foreach (var team in zuFuellende)
                await _auslosungService.MannschaftZuLostopfHinzufuegenAsync(auslosungId, ersterTopf.Id, team.Id);
        }
        else if (aktuelleRunde is not null)
        {
            var platzhalter = PlatzhalterGenerieren(aktuelleRunde, vorherigeRunde);
            foreach (var p in platzhalter)
                await _auslosungService.PlatzhalterZuLostopfHinzufuegenAsync(auslosungId, ersterTopf.Id, p.Name);
        }
    }

    private async Task AktualisiereSeitenlisteAsync()
    {
        if (AusgewaehlteInstanz is null) return;
        var auslosungen = await _auslosungService.AlleNachInstanzAbrufenAsync(AusgewaehlteInstanz.Id);
        Auslosungen = new ObservableCollection<Auslosung>(auslosungen);
        if (_auslosungDetail is not null)
            AusgewaehlteAuslosung = Auslosungen.FirstOrDefault(a => a.Id == _auslosungDetail.Id);
    }

    partial void OnNeuRundeChanged(TurnierRunde? value)
    {
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

    private bool KannNeu()       => !NeuFormAktiv && AusgewaehlteInstanz is not null;
    private bool KannSpeichern() => (NeuFormAktiv && NeuRunde is not null)
                                 || (IstDetailDirty && IstBearbeitbar);
    private bool KannAbbrechen() => NeuFormAktiv || IstDetailDirty;

    public bool HatUngespeicherteAenderungen => NeuFormAktiv || IstDetailDirty;
    private bool KannLoeschen()  => AusgewaehlteAuslosung is not null && !NeuFormAktiv;

    partial void OnDetailNameChanged(string value)           { if (!_ladenAktiv) IstDetailDirty = true; }
    partial void OnDetailAuslosungsartChanged(Auslosungsart value)
    {
        if (!_ladenAktiv) IstDetailDirty = true;
        AktualisiereRedundanteRegeln(value);
        ManuelleSlotsAufbauen();

        // Zufall/Benutzerdefiniert erlauben nur 1 Lostopf (Issue #134). Die eigentliche Reduktion der
        // gespeicherten Lostöpfe passiert erst beim Speichern via LostoepfeSynchronisierenAsync — das
        // wirft dort bewusst einen Fehler, wenn überzählige Töpfe noch Teams enthalten, statt Teams
        // automatisch zusammenzuführen.
        if (!DetailAnzahlLostoepfeBearbeitbar)
            DetailAnzahlLostoepfe = 1;
    }

    /// <summary>Baut ManuelleGruppen/ManuelleKoPaarungen (die Slot-Anzeige für Auslosungsart 'Manuell')
    /// aus der zugehörigen TurnierRunde neu auf. Muss sowohl beim Laden einer Auslosung laufen als auch,
    /// wenn der Nutzer die Auslosungsart interaktiv auf 'Manuell' umschaltet (sonst bleibt der Bereich
    /// sichtbar, aber leer, bis einmal gespeichert und neu geladen wurde).</summary>
    private void ManuelleSlotsAufbauen()
    {
        if (DetailAuslosungsart != Auslosungsart.Manuell || _auslosungDetail is null ||
            _auslosungDetail.Status is AuslosungsStatus.Abgeschlossen or AuslosungsStatus.Abgebrochen)
        {
            ManuelleKoPaarungen = [];
            ManuelleGruppen     = [];
            return;
        }

        int teamAnzahl = _auslosungDetail.TurnierRunde?.EingangsMannschaftsanzahl ?? 0;
        if (_auslosungDetail.TurnierRunde?.Rundenart == Rundenart.Gruppenphase)
        {
            int gruppenAnzahl = _auslosungDetail.TurnierRunde.GruppenAnzahl ?? 1;
            int slotsProGruppe = teamAnzahl > 0 && gruppenAnzahl > 0 ? teamAnzahl / gruppenAnzahl : 4;
            ManuelleGruppen = new ObservableCollection<ManuelleGruppe>(
                Enumerable.Range(1, gruppenAnzahl).Select(gNr => new ManuelleGruppe
                {
                    GruppenNr   = gNr,
                    GruppenName = $"Gruppe {GruppenBuchstabe(gNr)}",
                    Slots       = new ObservableCollection<ManuellerGruppenSlot>(
                        Enumerable.Range(1, slotsProGruppe).Select(sNr => new ManuellerGruppenSlot
                        {
                            GruppenNr = gNr,
                            SlotNr    = sNr
                        }))
                }));
            ManuelleKoPaarungen = [];
        }
        else
        {
            int paarAnzahl = teamAnzahl > 0 ? (int)Math.Ceiling(teamAnzahl / 2.0) : 1;
            ManuelleKoPaarungen = new ObservableCollection<ManuelleKoPaarungAnzeige>(
                Enumerable.Range(1, paarAnzahl).Select(nr => new ManuelleKoPaarungAnzeige(
                    nr,
                    new ManuellerKoSlot { PaarungsNr = nr, IstHeim = true },
                    new ManuellerKoSlot { PaarungsNr = nr, IstHeim = false })));
            ManuelleGruppen = [];
        }
        OnPropertyChanged(nameof(IstManuellKoModus));
        OnPropertyChanged(nameof(IstManuellGruppenModus));
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
        if (AusgewaehlteInstanz is null) { VerbandsQuotenAnzeige = []; return; }

        var quoten = await _verbandsQuoteService.AlleNachTurnierInstanzIdAsync(AusgewaehlteInstanz.Id);
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
                Max         = q.MaxAnzahl,
                Min         = q.MinAnzahl
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
