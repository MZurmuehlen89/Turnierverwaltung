using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Spielplan.Eingaben;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>"Runden"-Tab von <see cref="TurnierlisteViewModel"/> (Issue #297) — Rundenliste + Rundenformular
/// (<see cref="Formular"/>, #435) + Übergangsregeln der gewählten Runde (<see cref="UebergangsregelEditor"/>,
/// Issue #348 — vorher eigener Reiter, dadurch Reiterwechsel für abhängige Optionen wie "Paarung fest
/// vorgegeben"); beide Bereiche sind Kind-ViewModels und schreiben in die Daten dieses ViewModels. Alleiniger
/// DATENHALTER für Runden UND Übergangsregeln (<see cref="AlleUebergaenge"/>) — der Editor liest/schreibt die
/// Daten über die öffentlichen Methoden/Property unten, statt eigene Kopien zu halten — EIN "Speichern"
/// committet Runden UND Übergangsregeln gemeinsam über
/// <see cref="TurnierInstanzService.RundenUndUebergaengeErsetzenAsync"/>. Feste Paarungen (Vorbelegung) werden seit
/// #297 unabhängig davon im <see cref="VorbelegungViewModel"/> direkt gegen Spiel/Auslosung verwaltet.</summary>
public partial class RundenViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly TurnierInstanzService _service;
    private readonly UebergangsregelService _uebergangsregelService;
    private readonly TurnierValidator _turnierValidator;
    private readonly UebergangsregelValidator _uebergangsregelValidator;
    private readonly ILogger<RundenViewModel> _logger;
    private int _populiertGerade;

    // Reentrancy-Guard: Instanz-Id, deren Ladevorgang gerade läuft — verhindert, dass ein schneller
    // Instanzwechsel (Klick A, sofort Klick B) den älteren, noch laufenden Ladevorgang anwendet.
    private int _ladevorgangLaufendFuer = -1;

    private readonly List<UebergangsregelBlock> _alleUebergaenge = [];

    /// <summary>Geteilte Übergangsregel-Datenhaltung, gelesen von <see cref="UebergangsregelEditorViewModel"/>.</summary>
    public IReadOnlyList<UebergangsregelBlock> AlleUebergaenge => _alleUebergaenge;

    /// <summary>Feuert bei jeder strukturellen (hinzugefügt/entfernt) oder inhaltlichen (Quelle/Ziel/Zeilen)
    /// Änderung an <see cref="AlleUebergaenge"/> sowie bei jeder Rundendaten-Änderung, die die Kartenanzeige
    /// betrifft — <see cref="UebergangsregelEditorViewModel"/> abonniert dies.</summary>
    public event Action? UebergaengeGeaendert;

    private void UebergaengeGeaendertMelden()
    {
        OnPropertyChanged(nameof(PaarungFestVorgegebenVerfuegbar));
        foreach (var runde in Runden)
            runde.UnstimmigkeitText = UnstimmigkeitErmitteln(runde);
        UebergaengeGeaendert?.Invoke();
    }

    private string? UnstimmigkeitErmitteln(RundeZeile runde) => UebergangsAbdeckung.Unstimmigkeit(
        runde.AusgangsMannschaftsanzahl,
        _alleUebergaenge
            .Where(b => b.QuellRunde == runde)
            .Select(b => b.Zeilen.Sum(z => UebergangsAbdeckung.Plaetze(z.PlatzierungVon, z.PlatzierungBis, z.AnzahlBeste)))
            .ToList());

    /// <summary>Übergangsregel-Bereich im Runden-Detail (Issue #348).</summary>
    public UebergangsregelEditorViewModel UebergangsregelEditor { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatInstanz))]
    [NotifyPropertyChangedFor(nameof(RundenBearbeitbar))]
    [NotifyCanExecuteChangedFor(nameof(NeueRundeCommand))]
    private TurnierInstanz? _aktuelleInstanz;

    public bool HatInstanz => AktuelleInstanz is not null;

    /// <summary>Status-Gating (Issue #297): Rundenstruktur/Übergänge sind nur bearbeitbar, solange das Turnier
    /// noch keine echten Teilnehmer/Ergebnisse hat (Status Geplant oder Vorlage) — analog WPF-Original
    /// <c>TurnierDefinitionViewModel.RundenTabAktiv</c>, um erweitert um den Vorlage-Status.</summary>
    public bool RundenBearbeitbar => AktuelleInstanz is null
        || AktuelleInstanz.Status is TurnierStatus.Geplant or TurnierStatus.Vorlage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RundeEntfernenCommand))]
    [NotifyPropertyChangedFor(nameof(FormularSichtbar))]
    private RundeZeile? _ausgewaehlteRunde;

    partial void OnAusgewaehlteRundeChanged(RundeZeile? value)
    {
        Formular.Zeigen(value, FolgtAufGruppenphase(value));
        OnPropertyChanged(nameof(PaarungFestVorgegebenVerfuegbar));
    }

    public bool FormularSichtbar => AusgewaehlteRunde is not null;

    /// <summary>Rundenformular der gewählten Runde (#435).</summary>
    public RundenFormularViewModel Formular { get; }

    // Einmal je Auswahl bestimmt: die Runden davor ändern sich nicht, solange diese Runde gewählt ist.
    private bool FolgtAufGruppenphase(RundeZeile? zeile) =>
        zeile is not null && Runden.TakeWhile(z => z != zeile).Any(z => z.Rundenart == Rundenart.Gruppenphase);

    /// <summary>Der Nutzer hat Runden oder Übergangsregeln geändert.</summary>
    private void AenderungMerken()
    {
        IsDirty = true;
        PruefenUndMelden();
    }

    /// <summary>Zieht nach, was aus Runden und Übergangsregeln abgeleitet ist: Validierungsfehler,
    /// Unstimmigkeits-Hinweise und die Regelanzeige des Editors (auch Name/Ausgang einer Runde betreffen sie).</summary>
    private void PruefenUndMelden()
    {
        AktualisiereRundenFehler();
        UebergaengeGeaendertMelden();
    }

    /// <summary>Feste Paarungen (Vorbelegung, Issue #277/#297) ergeben nur für eine Runde mit mindestens
    /// einer eingehenden Übergangsregel Sinn — die erste Runde eines Turniers hat noch keine Platzhalter-
    /// Basis und durchläuft in jedem realen Turnier zwingend eine echte Auslosung.</summary>
    public bool PaarungFestVorgegebenVerfuegbar =>
        AusgewaehlteRunde is not null && AlleUebergaenge.Any(b => b.ZielRunde == AusgewaehlteRunde);

    [ObservableProperty] private ObservableCollection<RundeZeile> _runden = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    private bool _isDirty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigeMeldungsSpalte))]
    private string? _fehlermeldung;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatRundenFehler))]
    [NotifyPropertyChangedFor(nameof(ZeigeMeldungsSpalte))]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private IReadOnlyList<string> _rundenFehler = [];

    public bool HatRundenFehler => RundenFehler.Count > 0;
    public bool ZeigeMeldungsSpalte => Fehlermeldung is not null || HatRundenFehler;

    // ── IRibbonAware ──────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Runden";
    public ICommand? NeuBefehl => NeueRundeCommand;
    public ICommand? SpeichernBefehl => SpeichernCommand;
    public ICommand? AbbrechenBefehl => AbbrechenCommand;
    public ICommand? LoeschenBefehl => RundeEntfernenCommand;
    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen => [];

    public RundenViewModel(
        TurnierInstanzService service, UebergangsregelService uebergangsregelService,
        TurnierValidator turnierValidator, UebergangsregelValidator uebergangsregelValidator,
        RundenFormularViewModel formular, ILogger<RundenViewModel> logger)
    {
        _logger = logger;
        _service = service;
        _uebergangsregelService = uebergangsregelService;
        _turnierValidator = turnierValidator;
        _uebergangsregelValidator = uebergangsregelValidator;
        Formular = formular;
        Formular.Geaendert += AenderungMerken;
        UebergangsregelEditor = new UebergangsregelEditorViewModel(this);
    }

    /// <summary>Vom Composite gesetzt, wenn sich die geteilte Instanzauswahl ändert — lädt Runden und
    /// Übergangsregeln der neuen Instanz in einem Rutsch. Wird auch von <see cref="Speichern"/>/
    /// <see cref="Abbrechen"/> für denselben Reload genutzt — die zuvor ausgewählte Runde wird danach
    /// wiederhergestellt (per Id, sonst per Listenposition für eine gerade erst angelegte, noch ungespeicherte
    /// Runde), statt das Formular kommentarlos zu leeren.</summary>
    public async Task InstanzSetzenAsync(TurnierInstanz? instanz)
    {
        using var vorgang = _logger.Vorgang($"Runden: Turnier setzen (Instanz {instanz?.Id})");
        var auswahl = AuswahlMerken();

        AktuelleInstanz = instanz;
        var meineId = instanz?.Id ?? -1;
        _ladevorgangLaufendFuer = meineId;

        if (instanz is null)
        {
            RundenZuruecksetzen([]);
            return;
        }

        try
        {
            await Formular.StammdatenSicherstellenAsync();
            var mitRunden = await _service.FindenNachIdMitRundenAsync(instanz.Id);
            var alleRegeln = await _uebergangsregelService.AlleNachTurnierInstanzIdAsync(instanz.Id);
            if (_ladevorgangLaufendFuer != meineId) return;

            var zeileNachId = RundenUebernehmen(mitRunden?.Runden ?? []);
            UebergaengeUebernehmen(alleRegeln, zeileNachId);
            AuswahlWiederherstellen(auswahl);

            PruefenUndMelden();
            IsDirty = false;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    private (int? Id, int Index) AuswahlMerken() =>
        (AusgewaehlteRunde?.Id, AusgewaehlteRunde is not null ? Runden.IndexOf(AusgewaehlteRunde) : -1);

    // Id-Treffer gibt es nur nach Abbrechen (kein DB-Schreibzugriff, alte Ids bleiben gültig) — nach Speichern
    // hat RundenUndUebergaengeErsetzenAsync ALLE Runden per Löschen+Neuanlegen ersetzt, auch unveränderte
    // bekommen dabei eine neue Id. Der Id-Treffer schlägt dann für JEDE Runde fehl — deshalb per Listenposition
    // weitersuchen, statt nur beim Fehlen einer alten Id.
    private void AuswahlWiederherstellen((int? Id, int Index) vorher)
    {
        RundeZeile? wiederhergestellt = vorher.Id is int id
            ? Runden.FirstOrDefault(z => z.Id == id)
            : null;
        if (wiederhergestellt is null && vorher.Index >= 0 && vorher.Index < Runden.Count)
            wiederhergestellt = Runden[vorher.Index];
        AusgewaehlteRunde = wiederhergestellt;
    }

    /// <summary>Ersetzt die Rundenliste durch die geladenen Runden und liefert die Zeile je Runden-Id.</summary>
    private Dictionary<int, RundeZeile> RundenUebernehmen(IEnumerable<TurnierRunde> geladen)
    {
        var runden = geladen.OrderBy(r => r.Reihenfolge).ToList();
        var zeilen = runden.Select(RundeZuZeile).ToList();
        RundenZuruecksetzen(zeilen);
        return runden.Zip(zeilen, (r, z) => (r.Id, z)).ToDictionary(p => p.Id, p => p.z);
    }

    /// <summary>Baut je Quelle/Ziel-Paar einen Übergangsregel-Block auf; Regeln, deren Runde es nicht (mehr)
    /// gibt, bleiben weg.</summary>
    private void UebergaengeUebernehmen(IEnumerable<Uebergangsregel> alleRegeln, Dictionary<int, RundeZeile> zeileNachId)
    {
        _populiertGerade++;
        try
        {
            var regelnGueltig = alleRegeln.Where(r => zeileNachId.ContainsKey(r.QuellRundeId) && zeileNachId.ContainsKey(r.ZielRundeId));

            foreach (var gruppe in regelnGueltig.GroupBy(r => (r.QuellRundeId, r.ZielRundeId)))
            {
                var quellZeile = zeileNachId[gruppe.Key.QuellRundeId];
                var zielZeile = zeileNachId[gruppe.Key.ZielRundeId];
                var blockZeilen = gruppe.OrderBy(r => r.GruppeIndex ?? 0).Select(r => new UebergangsregelZeile
                {
                    GruppeIndex = r.GruppeIndex,
                    PlatzierungVon = r.PlatzierungVon,
                    PlatzierungBis = r.PlatzierungBis,
                    AnzahlBeste = r.AnzahlBeste
                });
                var block = UebergangsregelBlock.AusBestehendenZeilenErstellen(quellZeile, zielZeile, blockZeilen);
                VerdrahteBlock(block);
                _alleUebergaenge.Add(block);
            }
        }
        finally
        {
            _populiertGerade--;
        }
    }

    private RundeZeile RundeZuZeile(TurnierRunde r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        Rundenart = r.Rundenart,
        EingangsMannschaftsanzahl = r.EingangsMannschaftsanzahl,
        AusgangsMannschaftsanzahl = r.AusgangsMannschaftsanzahl,
        GruppenAnzahl = r.GruppenAnzahl,
        AnzahlSpieltage = RundenKonfiguration.AusJson(r.KonfigurationJson).AnzahlSpieltage,
        MitRueckspiel = r.MitRueckspiel,
        AuswaertstorRegelAktiv = r.AuswaertstorRegelAktiv,
        NeutralerSpielort = r.NeutralerSpielort,
        Ueberraschungsfaktor = r.Ueberraschungsfaktor,
        Regelwerk = r.Regelwerk is not null ? new RegelwerkOption(r.Regelwerk.Name, r.Regelwerk) : RundeZeile.KeinRegelwerk,
        PaarungFestVorgegeben = r.PaarungFestVorgegeben,
        VorgegebeneAuslosungsart = RundenFormularViewModel.AuslosungsartVorgabeOptionFuer(r.VorgegebeneAuslosungsart),
        StandardRangliste = Formular.StandardRanglisteOptionFuer(r.StandardRanglisteId),
        TitelverteidigerInTopf1 = r.TitelverteidigerInTopf1,
        KombinationstabelleJson = r.KombinationstabelleJson,
        VorgegebeneAuslosungsregeln = [.. r.VorgegebeneAuslosungsregeln],
        VorgegebeneGruppenposition = r.VorgegebeneGruppenposition,
        GastgeberGruppen = [.. r.GastgeberGruppen],
        TitelverteidigerGruppe = r.TitelverteidigerGruppe,
        TabellenBereiche = [.. r.TabellenBereiche.OrderBy(b => b.PlatzVon)
            .Select(b => new TabellenBereichEingabe(b.PlatzVon, b.PlatzBis, b.Art, b.Bezeichnung))]
    };

    private void RundenZuruecksetzen(IEnumerable<RundeZeile> runden)
    {
        _populiertGerade++;
        try
        {
            Runden = new ObservableCollection<RundeZeile>(runden);
            _alleUebergaenge.Clear();
            AusgewaehlteRunde = null;
            IsDirty = false;
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }
        PruefenUndMelden();
    }

    // ── Runden-Befehle ────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannNeueRunde))]
    private void NeueRunde()
    {
        var letzte = Runden.LastOrDefault();
        var eingang = letzte?.AusgangsMannschaftsanzahl ?? 8;
        var zeile = new RundeZeile { Name = $"Runde {Runden.Count + 1}", EingangsMannschaftsanzahl = eingang, AusgangsMannschaftsanzahl = eingang };
        Runden.Add(zeile);
        AusgewaehlteRunde = zeile;
        AenderungMerken();
    }
    private bool KannNeueRunde() => HatInstanz;

    [RelayCommand(CanExecute = nameof(KannRundeEntfernen))]
    private void RundeEntfernen()
    {
        if (AusgewaehlteRunde is null) return;
        var entfernt = AusgewaehlteRunde;
        var idx = Runden.IndexOf(entfernt);
        Runden.Remove(entfernt);

        // Regeln, die auf die entfernte Runde verweisen, sind sonst verwaist.
        _alleUebergaenge.RemoveAll(b => b.QuellRunde == entfernt || b.ZielRunde == entfernt);

        AusgewaehlteRunde = Runden.Count > 0 ? Runden[Math.Max(0, idx - 1)] : null;
        AenderungMerken();
    }
    private bool KannRundeEntfernen() => AusgewaehlteRunde is not null;

    // ── Übergangsregel-Datenhaltung (öffentlich, vom Übergangsregel-Editor genutzt) ─────

    public void UebergangHinzufuegen(UebergangsregelBlock block)
    {
        VerdrahteBlock(block);
        _alleUebergaenge.Add(block);
        AenderungMerken();
    }

    public void UebergangEntfernen(UebergangsregelBlock block)
    {
        _alleUebergaenge.Remove(block);
        AenderungMerken();
    }

    private void VerdrahteBlock(UebergangsregelBlock block)
    {
        block.Geaendert += () =>
        {
            if (_populiertGerade == 0) IsDirty = true;
            PruefenUndMelden();
        };
    }

    // ── Validierung ───────────────────────────────────────────────────────────

    /// <summary>Issue #297-Nachzug: der WinUi-Port hatte <c>TurnierRunde.EingangUeberUebergangsregeln</c>
    /// komplett vergessen (weder in <see cref="RundeZeile"/> noch beim Laden/Speichern berücksichtigt) —
    /// dadurch prüfte <see cref="TurnierValidator.RundenValidieren"/> stur die lineare Rundenkette
    /// (Ausgang[i] == Eingang[i+1]), was bei parallelen/zusammenlaufenden Pfaden (z.B. UEFA Champions
    /// League: Ligaphase speist Achtelfinale UND Playoff-Runde parallel) zu falschen Validierungsfehlern
    /// führte (Fund aus #297-Test). Statt das Flag als eigenes, manuell zu pflegendes Formularfeld
    /// nachzuziehen (wie im WPF-Original), wird es hier — analog zu dessen Auto-Set-Logik — direkt aus dem
    /// tatsächlichen Vorhandensein eingehender Übergangsregeln abgeleitet: hat eine Runde mindestens einen
    /// Übergangsregel-Block, der auf sie zielt, ist ihr Eingang per Definition regelbasiert und nicht mehr
    /// die unmittelbar vorangehende Runde.</summary>
    public List<TurnierRundeEingabe> RundenZuEingaben() => Runden.Select(z => new TurnierRundeEingabe(
        Name: z.Name,
        Rundenart: z.Rundenart,
        EingangsMannschaftsanzahl: z.EingangsMannschaftsanzahl,
        AusgangsMannschaftsanzahl: z.AusgangsMannschaftsanzahl,
        RegelwerkId: z.Regelwerk.Wert?.Id,
        MitRueckspiel: z.MitRueckspiel,
        AuswaertstorRegelAktiv: z.AuswaertstorRegelAktiv,
        KonfigurationJson: new RundenKonfiguration(z.AnzahlSpieltage).ZuJson(),
        GruppenAnzahl: z.GruppenAnzahl,
        EingangUeberUebergangsregeln: AlleUebergaenge.Any(b => b.ZielRunde == z),
        PaarungFestVorgegeben: z.PaarungFestVorgegeben,
        VorgegebeneAuslosungsart: z.VorgegebeneAuslosungsart.Wert,
        KombinationstabelleJson: z.KombinationstabelleJson,
        StandardRanglisteId: z.StandardRangliste.Wert,
        TitelverteidigerInTopf1: z.TitelverteidigerInTopf1,
        NeutralerSpielort: z.NeutralerSpielort,
        Ueberraschungsfaktor: z.Ueberraschungsfaktor,
        VorgegebeneAuslosungsregeln: z.VorgegebeneAuslosungsregeln,
        TabellenBereiche: z.TabellenBereiche,
        VorgegebeneGruppenposition: z.VorgegebeneGruppenposition,
        GastgeberGruppen: z.GastgeberGruppen,
        TitelverteidigerGruppe: z.TitelverteidigerGruppe).OhneUnwirksameAngaben()).ToList();

    public List<UebergangsregelPositionsEingabe> UebergangsregelnZuEingaben() => _alleUebergaenge
        .Where(b => b.QuellRunde is not null && b.ZielRunde is not null && b.QuellRunde != b.ZielRunde)
        .SelectMany(b => b.Zeilen.Select(z => new UebergangsregelPositionsEingabe(
            Runden.IndexOf(b.QuellRunde!) + 1, Runden.IndexOf(b.ZielRunde!) + 1,
            z.GruppeIndex, z.PlatzierungVon, z.PlatzierungBis, z.AnzahlBeste)))
        .ToList();

    private void AktualisiereRundenFehler()
    {
        if (Runden.Count == 0) { RundenFehler = []; return; }

        var rundenEingaben = RundenZuEingaben();
        var uebergangsregelEingaben = UebergangsregelnZuEingaben();
        var fehler = new List<string>(_turnierValidator.RundenValidieren(rundenEingaben).Fehler);

        // Nur für die Ganzgraph-Übergangsregel-Validierung nötig — bewusst rein transiente TurnierRunde-
        // Objekte (nie persistiert), da neue, noch ungespeicherte Runden hier ebenfalls mitgeprüft werden
        // müssen und der Validator echte TurnierRunde-Objekte (statt Eingaben) erwartet.
        var rundenFuerValidierung = Runden.Select(z => new TurnierRunde
        {
            Name = z.Name,
            Rundenart = z.Rundenart,
            EingangsMannschaftsanzahl = z.EingangsMannschaftsanzahl,
            AusgangsMannschaftsanzahl = z.AusgangsMannschaftsanzahl,
            GruppenAnzahl = z.GruppenAnzahl
        }).ToList();
        fehler.AddRange(_uebergangsregelValidator.Validieren(rundenFuerValidierung, uebergangsregelEingaben).Fehler);

        RundenFehler = fehler;
    }

    /// <summary>Feuert nach erfolgreichem Speichern — <see cref="VorbelegungViewModel"/> liest Runden
    /// (insbesondere <c>PaarungFestVorgegeben</c>) unabhängig frisch aus der DB und bekommt sonst nichts
    /// vom hier erfolgten Rundenersatz mit (Fund aus #297-Test: Vorbelegung-Tab blieb nach dem Setzen von
    /// "Paarung fest vorgegeben" + Speichern im Runden-Tab leer, weil niemand seinen Reload ausgelöst hat).</summary>
    public event Action<TurnierInstanz>? RundenGespeichert;

    /// <summary>Vom Composite gesetzte Rückfrage vor dem Speichern — der folgende Reload des Vorbelegung-Reiters
    /// verwirft eine begonnene, noch nicht abgeschlossene Vorbelegung, und nach dem Speichern lässt sich das
    /// nicht mehr ablehnen.</summary>
    public Func<Task<bool>>? VorAenderungBestaetigen { get; set; }

    /// <summary>Reiht Speichern und Abbrechen hinter einen Turnierwechsel ein und umgekehrt — über den vom
    /// <see cref="TurnierlisteViewModel"/> gesetzten gemeinsamen Schutz (#416). Nicht wiedereintrittsfähig:
    /// <see cref="InstanzSetzenAsync"/> läuft bereits unter dem Schutz, und <see cref="RundenGespeichert"/>
    /// betritt ihn als Fire-and-Forget selbst — innerhalb einer geschützten Aktion nur auslösen, nie abwarten.</summary>
    public DbContextReentrancySchutz DbContextSchutz { get; set; } = new();

    // ── Ribbon-Befehle ────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        if (AktuelleInstanz is not { } instanz) return;
        if (VorAenderungBestaetigen is { } bestaetigen && !await bestaetigen()) return;

        using var schutz = await DbContextSchutz.BetretenAsync();
        // Hat ein Turnierwechsel die Runden während des Wartens neu geladen, gibt es nichts mehr zu speichern.
        if (AktuelleInstanz != instanz || !KannSpeichern()) return;
        Fehlermeldung = null;
        try
        {
            using var vorgang = _logger.Vorgang($"Runden: speichern (Instanz {instanz.Id}, {Runden.Count} Runden)", "Runden werden gespeichert …");
            using (_logger.Vorgang($"Runden: Runden und Übergangsregeln ersetzen (Instanz {instanz.Id})"))
                await _service.RundenUndUebergaengeErsetzenAsync(
                    instanz.Id, RundenZuEingaben(), UebergangsregelnZuEingaben());
            await InstanzSetzenAsync(instanz);
            RundenGespeichert?.Invoke(instanz);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }
    private bool KannSpeichern() => HatInstanz && IsDirty && !HatRundenFehler;

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private async Task Abbrechen()
    {
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (!KannAbbrechen()) return;
        using var vorgang = _logger.Vorgang($"Runden: abbrechen (Instanz {AktuelleInstanz?.Id})", "Änderungen werden verworfen …");
        await InstanzSetzenAsync(AktuelleInstanz);
    }
    private bool KannAbbrechen() => HatInstanz && IsDirty;

    public bool HatUngespeicherteAenderungen => HatInstanz && IsDirty;
}
