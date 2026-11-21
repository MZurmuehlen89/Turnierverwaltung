using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Zufall;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>
/// #289: freie Einzelspiel-Simulation als eigenständiger Menüpunkt, 1:1-Port aus
/// Wpf/ViewModels/EinzelspielSimulationsViewModel.cs — braucht anders als
/// <see cref="SimulationsViewModel"/> keinen Turnier/Runde/Spiel-Kontext, nur zwei Mannschaften. Teilt
/// sich die Protokoll-Formatierung mit SimulationsViewModel über Zeitleiste und SpielKopf statt sie
/// zu duplizieren. <see cref="VerbandFilterOption"/> ist die bereits in MannschaftenViewModel
/// definierte, hier wiederverwendete Filter-Option (public record, gleicher Namespace).
///
/// #248: Bei aktiviertem "Mit Rückspiel" ersetzen zwei unabhängige Schritte (Hinspiel/Rückspiel
/// simulieren, je eigener Ribbon-Befehl) die vormals kombinierte Ein-Klick-Simulation beider Beine.
/// Das Hinspiel-Ergebnis ist zusätzlich manuell vorgebbar (Sidebar-Eingabe, dauerhaft sichtbar und
/// vorausgefüllt, nicht nur vor der ersten Simulation — sonst gäbe es nach dem Simulieren keine
/// Korrekturmöglichkeit mehr). Jede Änderung des Hinspiel-Ergebnisses (erneutes Simulieren ODER
/// manuelles Übernehmen) verwirft ein bereits vorhandenes Rückspiel-Ergebnis, da dessen
/// Entscheidungsverfahren vom alten Hinspiel-Aggregat abhing.
///
/// #247: Jedes Spiel mit eigener Uhr (Spiel bzw. Hinspiel, Rückspiel, Wiederholungsspiel) ist eine
/// <see cref="SpielBeinAnsicht"/>, die die View als eigene Karte nebeneinander zeigt.
/// </summary>
public partial class EinzelspielSimulationsViewModel : ViewModelBase, IRibbonAware
{
    private readonly SimulationsService _simulationsService;
    private readonly MannschaftService  _mannschaftService;
    private readonly RegelwerkService   _regelwerkService;
    private readonly VerbandService     _verbandService;
    private readonly Zufallsquelle      _zufallsquelle;
    private readonly RanglisteService   _ranglisteService;

    // Verband und Liga für die Untertitel im Spielkopf (#341, #384); die Liga kommt beim Aktivieren dazu.
    private UntertitelQuellen _untertitel;

    [ObservableProperty] private ObservableCollection<Mannschaft> _alleMannschaften = [];
    [ObservableProperty] private ObservableCollection<Regelwerk>  _alleRegelwerke   = [];
    [ObservableProperty] private ObservableCollection<Verband>    _alleVerbaende    = [];

    private static readonly VerbandFilterOption AllesVerband = new("— Alle —", null);

    public IReadOnlyList<VerbandFilterOption> VerbandFilterOptionen =>
        [AllesVerband, ..AlleVerbaende.Select(v => new VerbandFilterOption(v.Name, v))];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EinzelspielKannSimuliert))]
    private Mannschaft? _einzelspielHeim;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EinzelspielKannSimuliert))]
    private Mannschaft? _einzelspielAuswaerts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private Regelwerk? _einzelspielRegelwerk;

    // #691 E3: der Knopf „Wiederholungsspiel simulieren“ steht nur bei Regelwerken, deren Kette ein Wiederholungsspiel kennt.
    private bool RegelwerkKenntWiederholungsspiel =>
        EinzelspielRegelwerk?.Entscheidungsverfahren.Any(v => v.VerfahrenArt == VerfahrenArt.Wiederholungsspiel) == true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private bool _einzelspielMitRueckspiel;

    // #539: nur mit Rückspiel sichtbar; entscheidet bei ausgeglichenem Gesamtstand vor dem Regelwerk (AD-001).
    [ObservableProperty] private bool _einzelspielAuswaertstorRegel;

    // Verband-Filter + Suche je Seite — Heim und Auswaerts unabhaengig voneinander filterbar.
    // Nullable + On...Changed-Reset (Precedent: MannschaftenViewModel/TurnierkopfViewModel): eine
    // ComboBox setzt ihr SelectedItem transient auf null, sobald ihre ItemsSource neu zugewiesen wird
    // (hier VerbandFilterOptionen, eine computed Property, die bei jedem AktivierenAsync ein neues
    // Array liefert) — ohne den Reset-Handler landet dieses null via TwoWay-Binding im ViewModel und
    // die Gefiltert-Property crasht mit NRE beim ersten Öffnen der Seite.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeimMannschaftenGefiltert))]
    private VerbandFilterOption? _heimVerbandFilter = AllesVerband;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeimMannschaftenGefiltert))]
    private string _heimSuchbegriff = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AuswaertsMannschaftenGefiltert))]
    private VerbandFilterOption? _auswaertsVerbandFilter = AllesVerband;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AuswaertsMannschaftenGefiltert))]
    private string _auswaertsSuchbegriff = "";

    partial void OnHeimVerbandFilterChanged(VerbandFilterOption? value)
    {
        if (value is null) HeimVerbandFilter = AllesVerband;
    }

    partial void OnAuswaertsVerbandFilterChanged(VerbandFilterOption? value)
    {
        if (value is null) AuswaertsVerbandFilter = AllesVerband;
    }

    public IEnumerable<Mannschaft> HeimMannschaftenGefiltert => AlleMannschaften
        .Where(m => HeimVerbandFilter?.Wert is null || m.Land?.VerbandId == HeimVerbandFilter.Wert.Id)
        .Where(m => string.IsNullOrWhiteSpace(HeimSuchbegriff)
            || m.Name.Contains(HeimSuchbegriff, StringComparison.OrdinalIgnoreCase)
            || m.Kurzname.Contains(HeimSuchbegriff, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<Mannschaft> AuswaertsMannschaftenGefiltert => AlleMannschaften
        .Where(m => AuswaertsVerbandFilter?.Wert is null || m.Land?.VerbandId == AuswaertsVerbandFilter.Wert.Id)
        .Where(m => string.IsNullOrWhiteSpace(AuswaertsSuchbegriff)
            || m.Name.Contains(AuswaertsSuchbegriff, StringComparison.OrdinalIgnoreCase)
            || m.Kurzname.Contains(AuswaertsSuchbegriff, StringComparison.OrdinalIgnoreCase));

    public bool EinzelspielKannSimuliert =>
        EinzelspielHeim is not null &&
        EinzelspielAuswaerts is not null &&
        EinzelspielHeim != EinzelspielAuswaerts &&
        !IstLadend;

    // ── Protokoll: ohne Rückspiel (unverändert, SpielberichtControl) ────────────

    [ObservableProperty] private string? _protokollSpielInfo;

    // Issue #341: Spielkopf (kicker-Vorbild) statt der früheren Scoreboard-Zeilenliste; #247: Kopf und
    // Ereignisse in einer Ansicht, die SpielberichtKarte direkt bindet
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatProtokoll))]
    [NotifyPropertyChangedFor(nameof(ZeigeLeerZustand))]
    private SpielBeinAnsicht? _protokoll;

    // #247: das Wiederholungsspiel hat eine eigene Uhr und steht als zweite Karte neben dem Spiel
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatProtokollWiederholung))]
    private SpielBeinAnsicht? _protokollWiederholung;

    public bool HatProtokoll             => Protokoll is not null;
    public bool HatProtokollWiederholung => ProtokollWiederholung is not null;
    public bool ZeigeLeerZustand         => !HatProtokoll;

    // #691: das Wiederholungsspiel ist ein eigener Schritt. Bis es gespielt wird, hält das Spiel (bzw. das Rückspiel) davor
    // an; solange steht an seiner Stelle ein Platzhalter und der Knopf „Wiederholungsspiel simulieren“ ist aktiv.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigeWiederholungAusstehend))]
    private AusstehendesWiederholungsspiel? _ausstehendesWiederholungsspiel;

    public bool ZeigeWiederholungAusstehend => AusstehendesWiederholungsspiel is not null;

    // Die Auswahl, für die das Wiederholungsspiel ansteht: verworfen wird erst bei einer anderen Auswahl, nicht schon beim
    // transienten null, das eine ComboBox beim Neuzuweisen ihrer ItemsSource zurückschreibt (AktivierenAsync, Suchfilter).
    private (Mannschaft? Heim, Mannschaft? Auswaerts, Regelwerk? Regelwerk) _ausstehendBei;

    private void AusstehendesMerken(AusstehendesWiederholungsspiel? ausstehend)
    {
        _ausstehendBei = (EinzelspielHeim, EinzelspielAuswaerts, EinzelspielRegelwerk);
        AusstehendesWiederholungsspiel = ausstehend;
    }

    // ── Protokoll: mit Rückspiel (#248, zweistufig; #247: Karten nebeneinander) ────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RueckspielSimulierenKann))]
    [NotifyPropertyChangedFor(nameof(HatHinspielErgebnis))]
    [NotifyPropertyChangedFor(nameof(ZeigeHinRueckLeerZustand))]
    private SimulationsErgebnis? _hinspielErgebnis;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatRueckspielErgebnis))]
    private SimulationsErgebnis? _rueckspielErgebnis;

    public bool HatHinspielErgebnis   => HinspielErgebnis is not null;
    public bool HatRueckspielErgebnis => RueckspielErgebnis is not null;

    // Manuelle Eingabe: dauerhaft sichtbar (auch nach Simulation), vorausgefüllt mit dem jeweils
    // aktuellen Hinspiel-Ergebnis — sonst gäbe es nach dem ersten Simulieren keine Möglichkeit mehr,
    // das Hinspiel manuell zu korrigieren (Nutzer-Fund bei der Mockup-Abstimmung zu #248).
    [ObservableProperty] private int _hinspielHeimToreEingabe;
    [ObservableProperty] private int _hinspielAuswaertsToreEingabe;

    [ObservableProperty] private SpielBeinAnsicht? _hinspielAnsicht;
    [ObservableProperty] private SpielBeinAnsicht? _rueckspielAnsicht;

    // #247: ein Wiederholungsspiel nach dem Rückspiel (Aggregat unentschieden) steht als dritte Karte daneben
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatRueckspielWiederholung))]
    private SpielBeinAnsicht? _rueckspielWiederholung;

    public bool HatRueckspielWiederholung => RueckspielWiederholung is not null;
    // Issue #341: Gesamtzeile steht schon nach dem Hinspiel ("Ergebnis nach Hinspiel"), Detail trägt den
    // Zusatz ("nach 180 Minuten", "nach Elfmeterschießen (3:4)", …).
    [ObservableProperty] private SpielKopf? _gesamtKopf;
    [ObservableProperty] private string? _siegerText;

    public ObservableCollection<EinzelspielEreignisZeile> HinspielEreignisse   { get; } = [];
    public ObservableCollection<EinzelspielEreignisZeile> RueckspielEreignisse { get; } = [];

    public bool ZeigeHinRueckLeerZustand => !HatHinspielErgebnis;

    // ── Status ────────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EinzelspielKannSimuliert))]
    private bool    _istLadend;

    [ObservableProperty] private string? _fehlermeldung;

    // ── Befehle ───────────────────────────────────────────────────────────────

    public ICommand EinzelspielFreiBefehl        { get; }
    public ICommand HinspielSimulierenBefehl     { get; }
    public ICommand RueckspielSimulierenBefehl   { get; }
    public ICommand HinspielUebernehmenBefehl    { get; }
    public ICommand WiederholungsspielSimulierenBefehl { get; }

    public bool RueckspielSimulierenKann =>
        EinzelspielKannSimuliert && EinzelspielMitRueckspiel && HatHinspielErgebnis;

    // ── IRibbonAware ─────────────────────────────────────────────────────────
    // Weder Neu/Speichern/Abbrechen/Löschen haben hier eine Entsprechung — Simulieren schreibt
    // nichts in eine DB, es gibt keinen Datensatz zum Anlegen/Speichern/Löschen. HatStandardAktionen
    // UND HatLoeschenAktion daher beide bewusst false (Löschen-Default ist true, siehe
    // feedback_composite_vm_aktivierenasync — ohne explizites Überschreiben käme ein command-loser
    // "Löschen"-Button zurück).
    public string   RibbonTabTitel  => "Einzelspiel-Simulation";
    public ICommand? NeuBefehl       => null;
    public ICommand? SpeichernBefehl => null;
    public ICommand? AbbrechenBefehl => null;
    public ICommand? LoeschenBefehl  => null;
    public bool HatStandardAktionen => false;
    public bool HatLoeschenAktion   => false;

    // #248: Bei "Mit Rückspiel" ersetzen zwei unabhängige Buttons den einen kombinierten — die
    // MainViewModel-Weiterleitung reagiert nur auf PropertyChanged(RibbonZusatzGruppen) selbst
    // (nicht auf beliebige andere Properties dieses ViewModels), daher hängt
    // [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))] an _einzelspielMitRueckspiel oben.
    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen =>
    [
        new RibbonGruppe { Header = "Simulation", Aktionen = SimulationsAktionen() },
    ];

    private List<RibbonAktion> SimulationsAktionen()
    {
        List<RibbonAktion> aktionen = EinzelspielMitRueckspiel
            ?
            [
                new RibbonAktion
                {
                    Header = "Hinspiel simulieren",
                    Befehl = HinspielSimulierenBefehl,
                    AutomationId = "Befehlsleiste.HinspielSimulieren",
                    IconGlyph = "",
                },
                new RibbonAktion
                {
                    Header = "Rückspiel simulieren",
                    Befehl = RueckspielSimulierenBefehl,
                    AutomationId = "Befehlsleiste.RueckspielSimulieren",
                    IconGlyph = "",
                },
            ]
            :
            [
                new RibbonAktion
                {
                    Header = "Einzelspiel simulieren",
                    Befehl = EinzelspielFreiBefehl,
                    AutomationId = "Befehlsleiste.EinzelspielSimulieren",
                    IconGlyph = "",
                },
            ];

        if (RegelwerkKenntWiederholungsspiel)
            aktionen.Add(new RibbonAktion
            {
                Header = "Wiederholungsspiel simulieren",
                Befehl = WiederholungsspielSimulierenBefehl,
                AutomationId = "Befehlsleiste.WiederholungsspielSimulieren",
                IconGlyph = "",
            });
        return aktionen;
    }

    // ── Konstruktor ───────────────────────────────────────────────────────────

    public EinzelspielSimulationsViewModel(
        SimulationsService simulationsService,
        MannschaftService  mannschaftService,
        RegelwerkService   regelwerkService,
        VerbandService     verbandService,
        Zufallsquelle      zufallsquelle,
        RanglisteService   ranglisteService)
    {
        _simulationsService = simulationsService;
        _mannschaftService  = mannschaftService;
        _regelwerkService    = regelwerkService;
        _verbandService      = verbandService;
        _zufallsquelle       = zufallsquelle;
        _ranglisteService    = ranglisteService;
        _untertitel          = new UntertitelQuellen(VerbandKurzname, Ligazugehoerigkeit.Keine);

        EinzelspielFreiBefehl      = new AsyncRelayCommand(EinzelspielFreiSimulierenAsync, () => EinzelspielKannSimuliert && !EinzelspielMitRueckspiel);
        HinspielSimulierenBefehl   = new AsyncRelayCommand(HinspielSimulierenAsync, () => EinzelspielKannSimuliert);
        RueckspielSimulierenBefehl = new AsyncRelayCommand(RueckspielSimulierenAsync, () => RueckspielSimulierenKann && !IstLadend);
        HinspielUebernehmenBefehl  = new RelayCommand(HinspielUebernehmen, () => EinzelspielKannSimuliert);
        WiederholungsspielSimulierenBefehl = new AsyncRelayCommand(WiederholungsspielSimulierenAsync, () => ZeigeWiederholungAusstehend && !IstLadend);
    }

    // ── Initialisierung ───────────────────────────────────────────────────────

    public override async Task AktivierenAsync()
    {
        var mannschaftsListe = await _mannschaftService.AlleAbrufenMitLandAsync();
        var regelwerkListe   = await _regelwerkService.AlleAbrufenAsync();
        var verbandListe     = await _verbandService.AlleAbrufenAsync();
        _untertitel = _untertitel with { Ligazugehoerigkeit = await _ranglisteService.LigazugehoerigkeitAbrufenAsync() };

        AlleMannschaften.Clear();
        foreach (var m in mannschaftsListe.OrderBy(m => m.Name)) AlleMannschaften.Add(m);
        OnPropertyChanged(nameof(HeimMannschaftenGefiltert));
        OnPropertyChanged(nameof(AuswaertsMannschaftenGefiltert));

        AlleRegelwerke.Clear();
        foreach (var r in regelwerkListe) AlleRegelwerke.Add(r);

        AlleVerbaende.Clear();
        foreach (var verband in verbandListe.OrderBy(x => x.Name)) AlleVerbaende.Add(verband);
        OnPropertyChanged(nameof(VerbandFilterOptionen));
    }

    // ── Property-Reaktionen ───────────────────────────────────────────────────

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        switch (e.PropertyName)
        {
            case nameof(IstLadend):
            case nameof(EinzelspielHeim):
            case nameof(EinzelspielAuswaerts):
            case nameof(EinzelspielMitRueckspiel):
            case nameof(HinspielErgebnis):
            case nameof(AusstehendesWiederholungsspiel):
                AktualisiereBefehle();
                break;
        }
    }

    // #248: Wechsel von/zu "Mit Rückspiel" setzt den kompletten zweistufigen Zwischenstand zurück —
    // ein Hinspiel-Ergebnis aus dem jeweils anderen Modus ergibt sonst keinen Sinn mehr.
    partial void OnEinzelspielMitRueckspielChanged(bool value)
    {
        LeerProtokoll();
        LeerHinRueckProtokoll();
    }

    // #691 E2: ein ausstehendes Wiederholungsspiel liefe sonst mit alten Teams oder alter Kette.
    partial void OnEinzelspielHeimChanged(Mannschaft? value) => AusstehendesVerwerfen();

    partial void OnEinzelspielAuswaertsChanged(Mannschaft? value) => AusstehendesVerwerfen();

    partial void OnEinzelspielRegelwerkChanged(Regelwerk? value) => AusstehendesVerwerfen();

    // Mit Rückspiel gehen Rückspiel und Ausstehendes, das Hinspiel bleibt; ohne Rückspiel ist das Spiel selbst das erste Spiel.
    private void AusstehendesVerwerfen()
    {
        if (AusstehendesWiederholungsspiel is null || !AuswahlGeaendert()) return;

        if (!EinzelspielMitRueckspiel)
            LeerProtokoll();
        else if (HinspielErgebnis is { } hinspiel && EinzelspielHeim is { } heim && EinzelspielAuswaerts is { } auswaerts)
            RueckspielVerwerfen(heim, auswaerts, hinspiel);
        else
            RueckspielDatenLeeren();
    }

    private bool AuswahlGeaendert() =>
        (EinzelspielHeim is { } heim && heim != _ausstehendBei.Heim)
        || (EinzelspielAuswaerts is { } auswaerts && auswaerts != _ausstehendBei.Auswaerts)
        || (EinzelspielRegelwerk is { } regelwerk && regelwerk != _ausstehendBei.Regelwerk);

    // #539: ein vorhandenes Rückspiel hing von der Regel ab – verwerfen wie bei einem neuen Hinspiel, das Hinspiel bleibt.
    partial void OnEinzelspielAuswaertstorRegelChanged(bool value)
    {
        if (HinspielErgebnis is { } hinspiel && EinzelspielHeim is { } heim && EinzelspielAuswaerts is { } auswaerts)
            RueckspielVerwerfen(heim, auswaerts, hinspiel);
    }

    // ── Freie Simulation (ohne Rückspiel) ────────────────────────────────────

    private async Task EinzelspielFreiSimulierenAsync()
    {
        if (EinzelspielHeim is null || EinzelspielAuswaerts is null) return;

        var heim      = EinzelspielHeim;
        var auswaerts = EinzelspielAuswaerts;
        var verfahren = EinzelspielRegelwerk?.Entscheidungsverfahren.ToList()
                        ?? (IReadOnlyList<EntscheidungsVerfahren>)[];

        Fehlermeldung = null;
        IstLadend     = true;
        LeerProtokoll();

        await Task.Yield();

        try
        {
            // #691: das Wiederholungsspiel ist ein eigener Schritt – hier steht das Spiel bis dorthin da.
            var schritt = _simulationsService.EinzelspielBisWiederholungSimulieren(heim, auswaerts, verfahren, SimulationsModus.AusfuehrlicheSimulation);
            ProtokollAnzeigen(heim, auswaerts, schritt.Ergebnis);
            AusstehendesMerken(schritt.Ausstehend);
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

    // Spielkarte und (falls schon gespielt) die Karte des Wiederholungsspiels zu einem Ergebnis des Einzelspiels.
    private void ProtokollAnzeigen(Mannschaft heim, Mannschaft auswaerts, SimulationsErgebnis erg)
    {
        ProtokollSpielInfo = $"{heim.Name} vs. {auswaerts.Name}";
        ObservableCollection<EinzelspielEreignisZeile> ereignisse = [];
        ZeitleisteFuellen(ereignisse, erg, SpielSeite.Von(heim), SpielSeite.Von(auswaerts));
        ProtokollWiederholung = SpielBeinAnsicht.Wiederholungsspiel(
            erg, erg.Ereignisse.Select(ZeitleistenEreignis.Aus),
            SpielSeite.Von(heim), SpielSeite.Von(auswaerts), neutralerSpielort: false, _untertitel);
        Protokoll = new SpielBeinAnsicht(
            SpielKopf.Aus(null, SpielSeite.Von(heim), SpielSeite.Von(auswaerts), erg, _untertitel), ereignisse);
    }

    // ── Wiederholungsspiel als eigener Schritt (#691) ────────────────────────

    private async Task WiederholungsspielSimulierenAsync()
    {
        if (AusstehendesWiederholungsspiel is not { } ausstehend || EinzelspielHeim is null || EinzelspielAuswaerts is null) return;

        var heim      = EinzelspielHeim;
        var auswaerts = EinzelspielAuswaerts;

        Fehlermeldung = null;
        IstLadend     = true;
        AusstehendesWiederholungsspiel = null; // ein Aufruf trägt es genau einmal aus

        await Task.Yield();

        try
        {
            var erg = ausstehend.Austragen();
            if (EinzelspielMitRueckspiel && HinspielErgebnis is { } hinspiel)
            {
                RueckspielErgebnis = erg;
                RueckspielAnzeigen(SpielSeite.Von(heim), SpielSeite.Von(auswaerts), FreiePaarungGesamt.Bewerten(hinspiel, erg), erg);
            }
            else
                ProtokollAnzeigen(heim, auswaerts, erg);
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

    // ── Hinspiel/Rückspiel unabhängig (#248) ─────────────────────────────────

    private async Task HinspielSimulierenAsync()
    {
        if (EinzelspielHeim is null || EinzelspielAuswaerts is null) return;

        var heim      = EinzelspielHeim;
        var auswaerts = EinzelspielAuswaerts;

        Fehlermeldung = null;
        IstLadend     = true;

        await Task.Yield();

        try
        {
            // Hinspiel läuft immer ohne Entscheidungsverfahren (leeres verfahren) — ein bei 90
            // Minuten unentschiedenes Hinspiel braucht für sich allein keine Verlängerung, das
            // entscheidet erst das Aggregat nach dem Rückspiel (Precedent: WPF-Original, #237).
            var erg = _simulationsService.EinzelspielSimulieren(heim, auswaerts, [], SimulationsModus.AusfuehrlicheSimulation);
            HinspielUebernehmen(erg);
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

    private static void ZeitleisteFuellen(
        ObservableCollection<EinzelspielEreignisZeile> ziel, SimulationsErgebnis erg, SpielSeite heim, SpielSeite auswaerts)
    {
        foreach (var zeile in Zeitleiste.Erstellen(erg, erg.Ereignisse.Select(ZeitleistenEreignis.Aus), heim, auswaerts))
            ziel.Add(zeile);
    }

    // Manuelle Eingabe — teilt sich die Übernahme-Logik mit dem Simulieren-Pfad: ein manuell
    // eingegebenes Ergebnis ist einfach ein SimulationsErgebnis ohne Ereignisse und ohne
    // Verlängerung/Elfmeterschießen (Hinspiel kennt beides ohnehin nicht, s.o.) — dadurch bleibt die
    // Zeitleiste für diesen Fall leer, ganz ohne Sonderbehandlung (Zeitleiste.Erstellen liefert bei
    // Ereignisse.Count==0 nichts).
    private void HinspielUebernehmen()
    {
        var erg = new SimulationsErgebnis(
            HinspielHeimToreEingabe, HinspielAuswaertsToreEingabe,
            null, null, null, null,
            EntscheidungArt.RegulaereSpielzeit, []);
        HinspielUebernehmen(erg);
    }

    private void HinspielUebernehmen(SimulationsErgebnis erg)
    {
        if (EinzelspielHeim is null || EinzelspielAuswaerts is null) return;

        var heim      = EinzelspielHeim;
        var auswaerts = EinzelspielAuswaerts;

        HinspielErgebnis = erg;
        RueckspielDatenLeeren(); // ein verändertes Hinspiel macht ein bestehendes Rückspiel ungültig
        HinspielEreignisse.Clear();

        HinspielHeimToreEingabe      = erg.HeimToreRegulaer;
        HinspielAuswaertsToreEingabe = erg.AuswaertsToreRegulaer;

        ProtokollSpielInfo = $"{heim.Name} vs. {auswaerts.Name} (Hin & Rück)";

        ZeitleisteFuellen(HinspielEreignisse, erg, SpielSeite.Von(heim), SpielSeite.Von(auswaerts));

        HinspielAnsicht = new SpielBeinAnsicht(
            SpielKopf.Aus(
                "Hinspiel", SpielSeite.Von(heim), SpielSeite.Von(auswaerts), erg, _untertitel),
            HinspielEreignisse);
        GesamtKopf = GesamtNachHinspiel(heim, auswaerts, erg);

        OnPropertyChanged(nameof(ZeigeHinRueckLeerZustand));
        AktualisiereBefehle();
    }

    // Hinspiel kennt keine Verlängerung – der Gesamtstand nach dem Hinspiel ist dessen reguläres Ergebnis.
    private SpielKopf GesamtNachHinspiel(Mannschaft heim, Mannschaft auswaerts, SimulationsErgebnis hinspiel) =>
        SpielKopf.Gesamt(SpielSeite.Von(heim), SpielSeite.Von(auswaerts),
            $"{hinspiel.HeimToreRegulaer}:{hinspiel.AuswaertsToreRegulaer}", "Ergebnis nach Hinspiel", _untertitel);

    private void RueckspielDatenLeeren()
    {
        RueckspielErgebnis = null;
        RueckspielAnsicht = null;
        RueckspielWiederholung = null;
        AusstehendesWiederholungsspiel = null;
        SiegerText = null;
        RueckspielEreignisse.Clear();
    }

    // #539: nur das Rückspiel verwerfen – das Hinspiel und die Hinspiel-Eingabefelder bleiben unberührt.
    private void RueckspielVerwerfen(Mannschaft heim, Mannschaft auswaerts, SimulationsErgebnis hinspiel)
    {
        RueckspielDatenLeeren();
        GesamtKopf = GesamtNachHinspiel(heim, auswaerts, hinspiel);
    }

    private async Task RueckspielSimulierenAsync()
    {
        if (EinzelspielHeim is null || EinzelspielAuswaerts is null || HinspielErgebnis is null) return;

        var heim      = EinzelspielHeim;
        var auswaerts = EinzelspielAuswaerts;
        var erg1      = HinspielErgebnis;
        var verfahren = EinzelspielRegelwerk?.Entscheidungsverfahren.ToList()
                        ?? (IReadOnlyList<EntscheidungsVerfahren>)[];

        Fehlermeldung = null;
        IstLadend     = true;

        await Task.Yield();

        try
        {
#pragma warning disable S2234 // Rückspiel: Heim und Auswärts bewusst getauscht
            var schritt = _simulationsService.RueckspielBisWiederholungSimulieren(auswaerts, heim, erg1, verfahren, _zufallsquelle.Erzeugen(), EinzelspielAuswaertstorRegel);
#pragma warning restore S2234
            var erg2 = schritt.Ergebnis;
            RueckspielErgebnis = erg2;
            RueckspielAnzeigen(
                SpielSeite.Von(heim), SpielSeite.Von(auswaerts),
                FreiePaarungGesamt.Bewerten(erg1, erg2, wiederholungAusstehend: schritt.Ausstehend is not null), erg2);
            AusstehendesMerken(schritt.Ausstehend);
            if (schritt.Ausstehend is not null) SiegerText = null; // #691: solange das Wiederholungsspiel aussteht, gibt es keinen Sieger – auch kein „Unentschieden“
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

    // Gesamtkopf, Sieger und die Rückspiel-Karte; das Rückspiel hat vertauschte Rollen (dessen Heim = Hinspiel-Auswärts).
    private void RueckspielAnzeigen(SpielSeite heim, SpielSeite auswaerts, FreiePaarungGesamt gesamt, SimulationsErgebnis erg2)
    {
        string? sieger = gesamt.Sieger switch
        {
            PaarungSieger.Heim      => heim.Name,
            PaarungSieger.Auswaerts => auswaerts.Name,
            _                       => null
        };

        GesamtKopf = SpielKopf.Gesamt(heim, auswaerts, $"{gesamt.HeimTore}:{gesamt.AuswaertsTore}", gesamt.Zusatz, _untertitel);
        SiegerText = sieger is null ? "Unentschieden" : $"Sieger: {sieger}";

        RueckspielEreignisse.Clear();
#pragma warning disable S2234 // Rückspiel: Heim und Auswärts bewusst getauscht
        ZeitleisteFuellen(RueckspielEreignisse, erg2, auswaerts, heim);
        RueckspielAnsicht = new SpielBeinAnsicht(SpielKopf.Aus("Rückspiel", auswaerts, heim, erg2, _untertitel), RueckspielEreignisse);
        RueckspielWiederholung = SpielBeinAnsicht.Wiederholungsspiel(
            erg2, erg2.Ereignisse.Select(ZeitleistenEreignis.Aus), auswaerts, heim, neutralerSpielort: false, _untertitel);
#pragma warning restore S2234
    }

    private void LeerProtokoll()
    {
        ProtokollSpielInfo = null;
        Protokoll = null;
        ProtokollWiederholung = null;
        AusstehendesWiederholungsspiel = null;
    }

    // Mannschaften kommen ohne Verband (AlleAbrufenMitLandAsync) — Kurzname über die ohnehin geladene
    // Verbandsliste nachschlagen (Untertitel im Spielkopf, #341).
    private string? VerbandKurzname(Mannschaft mannschaft) =>
        AlleVerbaende.FirstOrDefault(v => v.Id == mannschaft.Land?.VerbandId)?.Kurzname;

    private void LeerHinRueckProtokoll()
    {
        HinspielErgebnis = null;
        RueckspielErgebnis = null;
        HinspielAnsicht = null;
        RueckspielAnsicht = null;
        RueckspielWiederholung = null;
        AusstehendesWiederholungsspiel = null;
        GesamtKopf = null;
        SiegerText = null;
        HinspielEreignisse.Clear();
        RueckspielEreignisse.Clear();
        HinspielHeimToreEingabe = 0;
        HinspielAuswaertsToreEingabe = 0;
        OnPropertyChanged(nameof(ZeigeHinRueckLeerZustand));
    }

    private void AktualisiereBefehle()
    {
        ((AsyncRelayCommand)EinzelspielFreiBefehl).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)HinspielSimulierenBefehl).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)RueckspielSimulierenBefehl).NotifyCanExecuteChanged();
        ((RelayCommand)HinspielUebernehmenBefehl).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)WiederholungsspielSimulierenBefehl).NotifyCanExecuteChanged();
    }
}
