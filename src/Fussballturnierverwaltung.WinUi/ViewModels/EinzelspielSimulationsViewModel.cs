using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turniere;
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
/// sich die Protokoll-Formatierung mit SimulationsViewModel über SimulationsProtokollHelfer statt sie
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
/// </summary>
public partial class EinzelspielSimulationsViewModel : ViewModelBase, IRibbonAware
{
    private readonly SimulationsService _simulationsService;
    private readonly MannschaftService  _mannschaftService;
    private readonly RegelwerkService   _regelwerkService;
    private readonly VerbandService     _verbandService;

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

    [ObservableProperty] private Regelwerk? _einzelspielRegelwerk;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private bool _einzelspielMitRueckspiel;

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

    public ObservableCollection<ProtokollErgebnisZeile>   ProtokollZeilen    { get; } = [];
    public ObservableCollection<EinzelspielEreignisZeile> ProtokollEreignisse { get; } = [];

    public bool HatProtokoll           => ProtokollZeilen.Count > 0;
    public bool HatProtokollEreignisse => ProtokollEreignisse.Count > 0;
    public bool ZeigeLeerZustand       => !HatProtokoll;

    // ── Protokoll: mit Rückspiel (#248, zweistufig, aufklappbare Karten) ────────

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

    [ObservableProperty] private bool _hinspielOffen;
    [ObservableProperty] private bool _rueckspielOffen;

    [ObservableProperty] private ProtokollErgebnisZeile? _hinspielZeile;
    [ObservableProperty] private ProtokollErgebnisZeile? _rueckspielZeile;
    [ObservableProperty] private ProtokollErgebnisZeile? _gesamtZeile;
    [ObservableProperty] private ProtokollErgebnisZeile? _weiterZeile;

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
        new RibbonGruppe
        {
            Header = "Simulation",
            Aktionen = EinzelspielMitRueckspiel
                ?
                [
                    new RibbonAktion
                    {
                        Header = "Hinspiel simulieren",
                        Befehl = HinspielSimulierenBefehl,
                        IconGlyph = "",
                    },
                    new RibbonAktion
                    {
                        Header = "Rückspiel simulieren",
                        Befehl = RueckspielSimulierenBefehl,
                        IconGlyph = "",
                    },
                ]
                :
                [
                    new RibbonAktion
                    {
                        Header = "Einzelspiel simulieren",
                        Befehl = EinzelspielFreiBefehl,
                        IconGlyph = "",
                    },
                ],
        },
    ];

    // ── Konstruktor ───────────────────────────────────────────────────────────

    public EinzelspielSimulationsViewModel(
        SimulationsService simulationsService,
        MannschaftService  mannschaftService,
        RegelwerkService   regelwerkService,
        VerbandService     verbandService)
    {
        _simulationsService = simulationsService;
        _mannschaftService  = mannschaftService;
        _regelwerkService    = regelwerkService;
        _verbandService      = verbandService;

        EinzelspielFreiBefehl      = new AsyncRelayCommand(EinzelspielFreiSimulierenAsync, () => EinzelspielKannSimuliert && !EinzelspielMitRueckspiel);
        HinspielSimulierenBefehl   = new AsyncRelayCommand(HinspielSimulierenAsync, () => EinzelspielKannSimuliert);
        RueckspielSimulierenBefehl = new AsyncRelayCommand(RueckspielSimulierenAsync, () => RueckspielSimulierenKann && !IstLadend);
        HinspielUebernehmenBefehl  = new RelayCommand(HinspielUebernehmen, () => EinzelspielKannSimuliert);
    }

    // ── Initialisierung ───────────────────────────────────────────────────────

    public override async Task AktivierenAsync()
    {
        var mannschaftsListe = await _mannschaftService.AlleAbrufenMitLandAsync();
        var regelwerkListe   = await _regelwerkService.AlleAbrufenAsync();
        var verbandListe     = await _verbandService.AlleAbrufenAsync();

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
            var erg = _simulationsService.EinzelspielSimulieren(heim, auswaerts, verfahren, SimulationsModus.AusfuehrlicheSimulation);
            var (score, detail) = SimulationsProtokollHelfer.ScoreUndDetail(erg);
            byte[]? heimWappen      = SimulationsProtokollHelfer.MannschaftsIcon(heim);
            byte[]? auswaertsWappen = SimulationsProtokollHelfer.MannschaftsIcon(auswaerts);
            ProtokollSpielInfo = $"{heim.Name} vs. {auswaerts.Name}";
            ProtokollZeilen.Add(new ProtokollErgebnisZeile(
                null,
                heim.Name, heimWappen,
                score,
                auswaerts.Name, auswaertsWappen,
                detail));

            SimulationsProtokollHelfer.ZeitleisteHinzufuegen(
                ProtokollEreignisse,
                erg.Ereignisse.Select(ev => (ev.Minute, ev.Art, ev.IstHeim)),
                heim.Name, auswaerts.Name, erg.EntscheidungArt,
                erg.HeimToreRegulaer, erg.AuswaertsToreRegulaer,
                erg.HeimToreVerlaengerung, erg.AuswaertsToreVerlaengerung,
                erg.HeimElfmetertore, erg.AuswaertsElfmetertore,
                heimWappen, auswaertsWappen);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstLadend = false;
            AktualisiereBefehle();
            OnPropertyChanged(nameof(HatProtokoll));
            OnPropertyChanged(nameof(HatProtokollEreignisse));
            OnPropertyChanged(nameof(ZeigeLeerZustand));
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

    // Manuelle Eingabe — teilt sich die Übernahme-Logik mit dem Simulieren-Pfad: ein manuell
    // eingegebenes Ergebnis ist einfach ein SimulationsErgebnis ohne Ereignisse und ohne
    // Verlängerung/Elfmeterschießen (Hinspiel kennt beides ohnehin nicht, s.o.) — dadurch bleibt die
    // Zeitleiste für diesen Fall leer, ganz ohne Sonderbehandlung (ZeitleisteHinzufuegen steigt bei
    // Ereignisse.Count==0 früh aus).
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
        RueckspielErgebnis = null; // ein verändertes Hinspiel macht ein bestehendes Rückspiel ungültig
        RueckspielZeile = null;
        GesamtZeile = null;
        WeiterZeile = null;
        RueckspielEreignisse.Clear();
        HinspielEreignisse.Clear();

        HinspielHeimToreEingabe      = erg.HeimToreRegulaer;
        HinspielAuswaertsToreEingabe = erg.AuswaertsToreRegulaer;

        byte[]? heimWappen      = SimulationsProtokollHelfer.MannschaftsIcon(heim);
        byte[]? auswaertsWappen = SimulationsProtokollHelfer.MannschaftsIcon(auswaerts);
        var (score, detail) = SimulationsProtokollHelfer.ScoreUndDetail(erg);

        ProtokollSpielInfo = $"{heim.Name} vs. {auswaerts.Name} (Hin & Rück)";
        HinspielZeile = new ProtokollErgebnisZeile(
            "Hinspiel", heim.Name, heimWappen, score, auswaerts.Name, auswaertsWappen, detail);

        SimulationsProtokollHelfer.ZeitleisteHinzufuegen(
            HinspielEreignisse,
            erg.Ereignisse.Select(ev => (ev.Minute, ev.Art, ev.IstHeim)),
            heim.Name, auswaerts.Name, erg.EntscheidungArt,
            erg.HeimToreRegulaer, erg.AuswaertsToreRegulaer,
            erg.HeimToreVerlaengerung, erg.AuswaertsToreVerlaengerung,
            erg.HeimElfmetertore, erg.AuswaertsElfmetertore,
            heimWappen, auswaertsWappen);

        // Hinspiel steht fest → aufgeklappt; ein vorheriges Rückspiel ist jetzt ungültig → eingeklappt.
        HinspielOffen   = true;
        RueckspielOffen = false;

        OnPropertyChanged(nameof(HatHinspielEreignisse));
        OnPropertyChanged(nameof(HatRueckspielEreignisse));
        OnPropertyChanged(nameof(ZeigeHinRueckLeerZustand));
        AktualisiereBefehle();
    }

    public bool HatHinspielEreignisse   => HinspielEreignisse.Count > 0;
    public bool HatRueckspielEreignisse => RueckspielEreignisse.Count > 0;

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
            var rng = new Random();
            var erg2 = _simulationsService.EinzelspielSimulieren(auswaerts, heim, [], SimulationsModus.AusfuehrlicheSimulation, rng);

            int heimGesamtRegulaer      = erg1.HeimToreRegulaer      + erg2.AuswaertsToreRegulaer;
            int auswaertsGesamtRegulaer = erg1.AuswaertsToreRegulaer + erg2.HeimToreRegulaer;

            // Aggregat nach 180 Minuten unentschieden → Rückspiel geht gemäß Regelwerk in die
            // Verlängerung (und ggf. Elfmeterschießen) — angewandt auf erg2, da die Verlängerung
            // real im Stadion des Rückspiels gespielt würde. Rollen bei EntscheidungsverfahrenAnwenden
            // entsprechen erg2s eigener Heim/Auswärts-Zuordnung (auswaerts spielt zu Hause).
            if (heimGesamtRegulaer == auswaertsGesamtRegulaer && verfahren.Count > 0)
            {
                var (hv, gv, he, ge, art, extras) =
                    SimulationsEngine.EntscheidungsverfahrenAnwenden(auswaerts, heim, verfahren, ausfuehrlich: true, rng);
                erg2 = erg2 with
                {
                    HeimToreVerlaengerung = hv, AuswaertsToreVerlaengerung = gv,
                    HeimElfmetertore = he, AuswaertsElfmetertore = ge,
                    EntscheidungArt = art,
                    Ereignisse = [.. erg2.Ereignisse, .. extras],
                };
            }

            RueckspielErgebnis = erg2;

            int heimGesamt      = heimGesamtRegulaer      + (erg2.AuswaertsToreVerlaengerung ?? 0);
            int auswaertsGesamt = auswaertsGesamtRegulaer + (erg2.HeimToreVerlaengerung ?? 0);

            string weiter;
            if (heimGesamt != auswaertsGesamt)
                weiter = heimGesamt > auswaertsGesamt ? heim.Name : auswaerts.Name;
            else if (erg2 is { EntscheidungArt: EntscheidungArt.Elfmeterschiessen, HeimElfmetertore: not null, AuswaertsElfmetertore: not null })
                weiter = erg2.HeimElfmetertore > erg2.AuswaertsElfmetertore ? auswaerts.Name : heim.Name; // erg2 "Heim" = auswaerts
            else
                weiter = "Unentschieden";

            byte[]? heimWappen      = SimulationsProtokollHelfer.MannschaftsIcon(heim);
            byte[]? auswaertsWappen = SimulationsProtokollHelfer.MannschaftsIcon(auswaerts);
            var (score2, detail2) = SimulationsProtokollHelfer.ScoreUndDetail(erg2);

            RueckspielZeile = new ProtokollErgebnisZeile(
                "Rückspiel", auswaerts.Name, auswaertsWappen, score2, heim.Name, heimWappen, detail2);
            GesamtZeile = new ProtokollErgebnisZeile(
                "Gesamt", heim.Name, heimWappen, $"{heimGesamt}:{auswaertsGesamt}", auswaerts.Name, auswaertsWappen);
            WeiterZeile = ProtokollErgebnisZeile.Text("Weiter", weiter);

            RueckspielEreignisse.Clear();
            SimulationsProtokollHelfer.ZeitleisteHinzufuegen(
                RueckspielEreignisse,
                erg2.Ereignisse.Select(ev => (ev.Minute, ev.Art, ev.IstHeim)),
                auswaerts.Name, heim.Name, erg2.EntscheidungArt,
                erg2.HeimToreRegulaer, erg2.AuswaertsToreRegulaer,
                erg2.HeimToreVerlaengerung, erg2.AuswaertsToreVerlaengerung,
                erg2.HeimElfmetertore, erg2.AuswaertsElfmetertore,
                auswaertsWappen, heimWappen);

            // Rückspiel steht fest → aufgeklappt, Hinspiel klappt automatisch ein (Nutzer-Vorgabe #248).
            HinspielOffen   = false;
            RueckspielOffen = true;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstLadend = false;
            AktualisiereBefehle();
            OnPropertyChanged(nameof(HatRueckspielEreignisse));
        }
    }

    private void LeerProtokoll()
    {
        ProtokollSpielInfo = null;
        ProtokollZeilen.Clear();
        ProtokollEreignisse.Clear();
        OnPropertyChanged(nameof(HatProtokoll));
        OnPropertyChanged(nameof(HatProtokollEreignisse));
        OnPropertyChanged(nameof(ZeigeLeerZustand));
    }

    private void LeerHinRueckProtokoll()
    {
        HinspielErgebnis = null;
        RueckspielErgebnis = null;
        HinspielZeile = null;
        RueckspielZeile = null;
        GesamtZeile = null;
        WeiterZeile = null;
        HinspielEreignisse.Clear();
        RueckspielEreignisse.Clear();
        HinspielHeimToreEingabe = 0;
        HinspielAuswaertsToreEingabe = 0;
        HinspielOffen = false;
        RueckspielOffen = false;
        OnPropertyChanged(nameof(HatHinspielEreignisse));
        OnPropertyChanged(nameof(HatRueckspielEreignisse));
        OnPropertyChanged(nameof(ZeigeHinRueckLeerZustand));
    }

    private void AktualisiereBefehle()
    {
        ((AsyncRelayCommand)EinzelspielFreiBefehl).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)HinspielSimulierenBefehl).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)RueckspielSimulierenBefehl).NotifyCanExecuteChanged();
        ((RelayCommand)HinspielUebernehmenBefehl).NotifyCanExecuteChanged();
    }
}
