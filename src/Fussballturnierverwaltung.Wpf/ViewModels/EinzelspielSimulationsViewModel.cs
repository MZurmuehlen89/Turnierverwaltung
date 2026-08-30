using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

/// <summary>
/// Issue #174: freie Einzelspiel-Simulation als eigenständiger Menüpunkt, aus SimulationsViewModel
/// extrahiert — braucht anders als die Turnierspiel-Simulation keinen Turnier/Runde/Spiel-Kontext,
/// nur zwei Mannschaften. Teilt sich die Protokoll-Formatierung mit SimulationsViewModel über
/// SimulationsProtokollHelfer statt sie zu duplizieren.
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
    [ObservableProperty] private bool       _einzelspielMitRueckspiel;

    // Verband-Filter + Suche je Seite — Heim und Auswaerts unabhaengig voneinander filterbar.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeimMannschaftenGefiltert))]
    private VerbandFilterOption _heimVerbandFilter = AllesVerband;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeimMannschaftenGefiltert))]
    private string _heimSuchbegriff = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AuswaertsMannschaftenGefiltert))]
    private VerbandFilterOption _auswaertsVerbandFilter = AllesVerband;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AuswaertsMannschaftenGefiltert))]
    private string _auswaertsSuchbegriff = "";

    public IEnumerable<Mannschaft> HeimMannschaftenGefiltert => AlleMannschaften
        .Where(m => HeimVerbandFilter.Wert is null || m.Land?.VerbandId == HeimVerbandFilter.Wert.Id)
        .Where(m => string.IsNullOrWhiteSpace(HeimSuchbegriff)
            || m.Name.Contains(HeimSuchbegriff, StringComparison.OrdinalIgnoreCase)
            || m.Kurzname.Contains(HeimSuchbegriff, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<Mannschaft> AuswaertsMannschaftenGefiltert => AlleMannschaften
        .Where(m => AuswaertsVerbandFilter.Wert is null || m.Land?.VerbandId == AuswaertsVerbandFilter.Wert.Id)
        .Where(m => string.IsNullOrWhiteSpace(AuswaertsSuchbegriff)
            || m.Name.Contains(AuswaertsSuchbegriff, StringComparison.OrdinalIgnoreCase)
            || m.Kurzname.Contains(AuswaertsSuchbegriff, StringComparison.OrdinalIgnoreCase));

    public bool EinzelspielKannSimuliert =>
        EinzelspielHeim is not null &&
        EinzelspielAuswaerts is not null &&
        EinzelspielHeim != EinzelspielAuswaerts &&
        !IstLadend;

    // ── Protokoll ─────────────────────────────────────────────────────────────

    [ObservableProperty] private string? _protokollSpielInfo;

    public ObservableCollection<ProtokollErgebnisZeile>   ProtokollZeilen    { get; } = [];
    public ObservableCollection<EinzelspielEreignisZeile> ProtokollEreignisse { get; } = [];

    public bool HatProtokoll           => ProtokollZeilen.Count > 0;
    public bool HatProtokollEreignisse => ProtokollEreignisse.Count > 0;
    public bool ZeigeLeerZustand       => !HatProtokoll;

    // ── Status ────────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EinzelspielKannSimuliert))]
    private bool    _istLadend;

    [ObservableProperty] private string? _fehlermeldung;

    // ── Befehle ───────────────────────────────────────────────────────────────

    public ICommand EinzelspielFreiBefehl { get; }

    // ── IRibbonAware (Issue #174) ─────────────────────────────────────────────
    // Weder Neu/Speichern/Abbrechen/Löschen haben hier eine Entsprechung — Simulieren schreibt
    // nichts in eine DB, es gibt keinen Datensatz zum Anlegen/Speichern/Löschen. HatStandardAktionen
    // UND HatLoeschenAktion daher beide bewusst false (Löschen-Default ist true, siehe
    // [[feedback-composite-vm-aktivierenasync]] — ohne explizites Überschreiben käme der #173-Bug
    // hier zurück, ein command-loser "Löschen"-Button).
    public string   RibbonTabTitel  => "Einzelspiel-Simulation";
    public ICommand? NeuBefehl       => null;
    public ICommand? SpeichernBefehl => null;
    public ICommand? AbbrechenBefehl => null;
    public ICommand? LoeschenBefehl  => null;
    public bool HatStandardAktionen => false;
    public bool HatLoeschenAktion   => false;

    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen =>
    [
        new RibbonGruppe
        {
            Header = "Simulation",
            Aktionen =
            [
                new RibbonAktion
                {
                    Header = "Einzelspiel simulieren",
                    Befehl = EinzelspielFreiBefehl,
                    IconVektorDatei = "whistle.xaml",
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

        EinzelspielFreiBefehl = new AsyncRelayCommand(EinzelspielFreiSimulierenAsync, () => EinzelspielKannSimuliert);
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
                AktualisiereBefehle();
                break;
        }
    }

    // ── Freie Simulation ──────────────────────────────────────────────────────

    private async Task EinzelspielFreiSimulierenAsync()
    {
        if (EinzelspielHeim is null || EinzelspielAuswaerts is null) return;

        var heim      = EinzelspielHeim;
        var auswaerts = EinzelspielAuswaerts;
        var verfahren = EinzelspielRegelwerk?.Entscheidungsverfahren.ToList()
                        ?? (IReadOnlyList<EntscheidungsVerfahren>)[];

        Fehlermeldung = null;
        IstLadend     = true;
        LeerProtokolll();

        await Task.Yield();

        try
        {
            if (EinzelspielMitRueckspiel)
            {
                var rng = new Random();

                // Issue #237 (Fund beim manuellen Test): Hinspiel läuft IMMER ohne Entscheidungsverfahren
                // (leeres verfahren) — ein bei 90 Minuten unentschiedenes Hinspiel braucht für sich allein
                // keine Verlängerung, das entscheidet erst das Aggregat nach dem Rückspiel. Vorherige
                // Fassung wandte "verfahren" blind auf jedes Bein einzeln an.
                var erg1 = _simulationsService.EinzelspielSimulieren(heim, auswaerts, [], SimulationsModus.AusfuehrlicheSimulation, rng);
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

                var (score1, detail1) = SimulationsProtokollHelfer.ScoreUndDetail(erg1);
                var (score2, detail2) = SimulationsProtokollHelfer.ScoreUndDetail(erg2);

                ProtokollSpielInfo = $"{heim.Name} vs. {auswaerts.Name} (Hin & Rück)";
                ProtokollZeilen.Add(new ProtokollErgebnisZeile(
                    "Hinspiel", heim.Name, heimWappen, score1, auswaerts.Name, auswaertsWappen, detail1));
                ProtokollZeilen.Add(new ProtokollErgebnisZeile(
                    "Rückspiel", auswaerts.Name, auswaertsWappen, score2, heim.Name, heimWappen, detail2));
                ProtokollZeilen.Add(new ProtokollErgebnisZeile(
                    "Gesamt", heim.Name, heimWappen, $"{heimGesamt}:{auswaertsGesamt}", auswaerts.Name, auswaertsWappen));
                ProtokollZeilen.Add(ProtokollErgebnisZeile.Text("Weiter", weiter));

                // Issue #237: HINSPIEL/RÜCKSPIEL-Abschnittsköpfe statt der frueheren "Hin"/"Rück"-
                // Minutenpräfixe — sonst waeren die beiden Halbzeit-Trenner (je Bein einer) in der
                // neuen Zeitleisten-Grafik nicht mehr unterscheidbar. Abpfiff-Marke pro Bein wird beim
                // Rückspiel unterdrueckt, stattdessen eine Gesamt-Marke am Ende angehängt.
                ProtokollEreignisse.Add(EinzelspielEreignisZeile.Trenner("HINSPIEL", TrennerArt.Beinabschnitt));
                SimulationsProtokollHelfer.ZeitleisteHinzufuegen(
                    ProtokollEreignisse,
                    erg1.Ereignisse.Select(ev => (ev.Minute, ev.Art, ev.IstHeim)),
                    heim.Name, auswaerts.Name, erg1.EntscheidungArt,
                    erg1.HeimToreRegulaer, erg1.AuswaertsToreRegulaer,
                    erg1.HeimToreVerlaengerung, erg1.AuswaertsToreVerlaengerung,
                    erg1.HeimElfmetertore, erg1.AuswaertsElfmetertore,
                    heimWappen, auswaertsWappen,
                    abpfiffPraefix: "ENDE HINSPIEL");

                ProtokollEreignisse.Add(EinzelspielEreignisZeile.Trenner("RÜCKSPIEL", TrennerArt.Beinabschnitt));
                // Issue #237 (Nutzer-Feedback): Rückspiel-Ereignisse bewusst NICHT mehr auf die feste
                // Heim/Auswärts-Seite des Hinspiels zurückgespiegelt — Auswärts hat im Rückspiel Heimrecht,
                // die Zeitleiste zeigt das (Achsen-links = jeweiliges Heimteam der Zeile, analog zur
                // "Rückspiel: Auswärts – Heim"-Beschriftung im Scoreboard). erg2 wird dafür einfach in
                // seiner eigenen, natürlichen Heim/Auswärts-Rolle übergeben (Auswärts = Heim des Rückspiels).
                SimulationsProtokollHelfer.ZeitleisteHinzufuegen(
                    ProtokollEreignisse,
                    erg2.Ereignisse.Select(ev => (ev.Minute, ev.Art, ev.IstHeim)),
                    auswaerts.Name, heim.Name, erg2.EntscheidungArt,
                    erg2.HeimToreRegulaer, erg2.AuswaertsToreRegulaer,
                    erg2.HeimToreVerlaengerung, erg2.AuswaertsToreVerlaengerung,
                    erg2.HeimElfmetertore, erg2.AuswaertsElfmetertore,
                    auswaertsWappen, heimWappen,
                    anpfiffText: null, abpfiffPraefix: null);
                ProtokollEreignisse.Add(EinzelspielEreignisZeile.Trenner(
                    $"ABPFIFF · Gesamt {heimGesamt}:{auswaertsGesamt}", TrennerArt.Ende));
            }
            else
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

    private void LeerProtokolll()
    {
        ProtokollSpielInfo = null;
        ProtokollZeilen.Clear();
        ProtokollEreignisse.Clear();
        OnPropertyChanged(nameof(HatProtokoll));
        OnPropertyChanged(nameof(HatProtokollEreignisse));
        OnPropertyChanged(nameof(ZeigeLeerZustand));
    }

    private void AktualisiereBefehle()
    {
        ((AsyncRelayCommand)EinzelspielFreiBefehl).NotifyCanExecuteChanged();
    }
}
