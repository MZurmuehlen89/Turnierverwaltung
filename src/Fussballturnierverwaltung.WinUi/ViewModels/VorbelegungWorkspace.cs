using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Helpers;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>
/// Workspace für die Vorbelegung einer Runde mit <see cref="Auslosungsart.Manuell"/> (Issue #297, Nachfolger
/// von #251 auf Instanzebene) — bewusst eine reine <see cref="ObservableObject"/>-Helferklasse statt
/// ViewModelBase (Precedent: <see cref="TurnierTabKoordinator"/>), gehostet von <see cref="VorbelegungViewModel"/>
/// — eine Instanz je aktuell bearbeiteter Runde, kein DI-Lifetime (per <c>new</c> erzeugt).
///
/// 1:1 aus Wpf/ViewModels/ManuellePaarungWorkspaceViewModel.cs portiert und arbeitet direkt gegen
/// <see cref="Domain.Entitaeten.Spiele.Spiel"/>/<see cref="Auslosung"/> statt gegen die mit #294 entfallene
/// <c>VorlagenPaarung</c>-Entität — funktioniert für Vorlage- UND Turnier-Status identisch (der einzige
/// Status-abhängige Punkt ist die Teilnehmerpool-Quelle in <see cref="LadenAsync"/>: eine Vorlage hat noch
/// keine echten Teilnehmer, daher generische Platzhalter statt echter Mannschaften für die erste Runde ohne
/// eingehende Übergangsregeln). Kein DB-Schreibzugriff vor "Abschließen" — die <see cref="Auslosung"/>-Entität
/// wird erst dabei lazy angelegt.
/// </summary>
public partial class VorbelegungWorkspace : ObservableObject
{
    private readonly AuslosungService _auslosungService;
    private readonly MannschaftService _mannschaftService;
    private readonly SpielplanService _spielplanService;
    private readonly TurnierBaumService _baumService;
    private readonly UebergangsregelService _uebergangsregelService;
    private readonly IDialogService _dialogService;
    private readonly ILogger _logger;
    private readonly DbContextReentrancySchutz _dbContextSchutz;

    private readonly TurnierInstanz _instanz;
    private readonly IReadOnlyList<TurnierRunde> _instanzRunden;

    /// <summary>Bereits vorhandene Manuell-Auslosung dieser Runde (inkl. Ergebnisse/Regeln), oder null,
    /// solange noch nichts abgeschlossen wurde — dann existiert noch keine <see cref="Auslosung"/>-Zeile.</summary>
    private Auslosung? _auslosung;

    public TurnierRunde Runde { get; }

    public bool IstGruppenModus => Runde.Rundenart is Rundenart.Gruppenphase or Rundenart.Liga or Rundenart.SchweizerSystem;
    public bool IstKoModus => !IstGruppenModus;

    [ObservableProperty] private bool _istLadend;
    [ObservableProperty] private string? _fehlermeldung;
    [ObservableProperty] private string? _manuellesFehler;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstBearbeitbar))]
    [NotifyCanExecuteChangedFor(nameof(AbschliessenCommand))]
    [NotifyCanExecuteChangedFor(nameof(ZuruecksetzenCommand))]
    private bool _istAbgeschlossen;

    public bool IstBearbeitbar => !IstAbgeschlossen;

    // Beide Listen bleiben stabile Instanzen und werden nur abgeglichen (SammlungsAbgleich, analog Auslosung
    // #388): eine bei jedem Zugriff neu berechnete Where-Aufzählung meldete bei aktivem Namensfilter weder
    // Platzieren noch Zurücklegen an die ListView (#392).
    public ObservableCollection<TeilnehmerAnzeige> VerfuegbareTeilnehmer { get; } = [];
    public ObservableCollection<TeilnehmerAnzeige> VerfuegbareTeilnehmerGefiltert { get; } = [];

    [ObservableProperty] private string _teilnehmerFilter = "";

    /// <summary>Ausgangsreihenfolge des Teilnehmerpools (Schlüssel → Rang) — ein zurückgelegtes Team kehrt an
    /// seine ursprüngliche Position zurück statt ans Listenende (Platzhalter bewusst nicht alphabetisch).</summary>
    private Dictionary<(int?, string), int> _ausgangsRang = [];

    partial void OnTeilnehmerFilterChanged(string value) => GefilterteTeilnehmerAbgleichen();

    private static (int?, string) TeilnehmerSchluessel(TeilnehmerAnzeige t) =>
        (t.MannschaftId, t.MannschaftId.HasValue ? "" : t.Name);

    private void GefilterteTeilnehmerAbgleichen() =>
        SammlungsAbgleich.Abgleichen(
            VerfuegbareTeilnehmerGefiltert,
            VerfuegbareTeilnehmer
                .Where(t => string.IsNullOrWhiteSpace(TeilnehmerFilter)
                            || t.Name.Contains(TeilnehmerFilter, StringComparison.OrdinalIgnoreCase))
                .ToList(),
            TeilnehmerSchluessel);

    [ObservableProperty] private ObservableCollection<ManuelleKoPaarungAnzeige> _koPaarungen = [];
    [ObservableProperty] private ObservableCollection<ManuelleGruppe> _gruppen = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TeamsStatusText))]
    [NotifyPropertyChangedFor(nameof(TeamsVollstaendig))]
    private int _teamsZugewiesen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TeamsStatusText))]
    [NotifyPropertyChangedFor(nameof(TeamsVollstaendig))]
    private int _teamsBenoetigt;

    public bool TeamsVollstaendig => TeamsBenoetigt > 0 && TeamsZugewiesen == TeamsBenoetigt;

    /// <summary>Gibt "Paarung abschließen" erst frei, wenn nichts mehr fehlt — statt eine lückenhafte Belegung
    /// erst beim Abschließen abzulehnen (Gruppen) bzw. Paarungen ohne Heimteam stillschweigend wegzulassen
    /// (K.o.). Gruppen: alle Plätze besetzt. K.o.: alle erwarteten Teams platziert und keine Paarung, die nur
    /// ein Auswärtsteam hat — ein einzelnes Team (Freilos bei ungerader Anzahl) steht auf dem Heimplatz.</summary>
    public bool BelegungVollstaendig => IstGruppenModus
        ? TeamsZugewiesen > 0 && Gruppen.All(g => g.Slots.All(s => !s.IstLeer))
        : TeamsZugewiesen > 0
          && (TeamsBenoetigt == 0 || TeamsZugewiesen == TeamsBenoetigt)
          && KoPaarungen.All(p => !p.HeimSlot.IstLeer || p.AuswaertsSlot.IstLeer);

    public string TeamsStatusText => TeamsBenoetigt == 0
        ? $"{TeamsZugewiesen} Teams zugewiesen"
        : TeamsZugewiesen == TeamsBenoetigt
            ? $"✓  Alle {TeamsBenoetigt} Teams zugewiesen"
            : TeamsZugewiesen > TeamsBenoetigt
                ? $"⚠  {TeamsZugewiesen} Teams zugewiesen — {TeamsZugewiesen - TeamsBenoetigt} zu viel (Runde erwartet {TeamsBenoetigt})"
                : $"{TeamsZugewiesen} / {TeamsBenoetigt} Teams zugewiesen — noch {TeamsBenoetigt - TeamsZugewiesen} fehlen";

    /// <summary>Meldet dem Host (<see cref="VorbelegungViewModel"/>) eine Änderung, die dessen
    /// Runden-Statusliste betrifft (Abschließen/Zurücksetzen).</summary>
    public event Action? Geaendert;

    public bool HatUngespeicherteAenderungen => !IstAbgeschlossen && TeamsZugewiesen > 0;

    public VorbelegungWorkspace(
        AuslosungService auslosungService,
        MannschaftService mannschaftService,
        SpielplanService spielplanService,
        TurnierBaumService baumService,
        UebergangsregelService uebergangsregelService,
        IDialogService dialogService,
        ILogger logger,
        DbContextReentrancySchutz dbContextSchutz,
        TurnierInstanz instanz,
        TurnierRunde runde,
        IReadOnlyList<TurnierRunde> instanzRunden)
    {
        _logger = logger;
        _dbContextSchutz = dbContextSchutz;
        _auslosungService = auslosungService;
        _mannschaftService = mannschaftService;
        _spielplanService = spielplanService;
        _baumService = baumService;
        _uebergangsregelService = uebergangsregelService;
        _dialogService = dialogService;
        _instanz = instanz;
        Runde = runde;
        _instanzRunden = instanzRunden;
        VerfuegbareTeilnehmer.CollectionChanged += (_, _) => GefilterteTeilnehmerAbgleichen();
    }

    /// <summary>Lädt die Runde unter dem gemeinsamen Schutz (#416) — für den Aufruf von außerhalb einer
    /// geschützten Aktion (Rundenwechsel). Abschließen/Zurücksetzen halten den Schutz schon und rufen
    /// <see cref="LadenAsync"/> direkt auf.</summary>
    public async Task GeschuetztLadenAsync()
    {
        using var schutz = await _dbContextSchutz.BetretenAsync();
        await LadenAsync();
    }

    private async Task LadenAsync()
    {
        using var vorgang = _logger.Vorgang($"Vorbelegung: Runde laden (Runde {Runde.Id})");
        IstLadend = true;
        Fehlermeldung = null;
        ManuellesFehler = null;
        try
        {
            var alle = await _auslosungService.AlleNachInstanzAbrufenAsync(_instanz.Id);
            var bestehende = alle.FirstOrDefault(a => a.TurnierRundeId == Runde.Id);
            _auslosung = bestehende is not null ? await _auslosungService.FindenNachIdAsync(bestehende.Id) : null;

            IstAbgeschlossen = _auslosung?.Status == AuslosungsStatus.Abgeschlossen;

            var eingehendeRegeln = await _uebergangsregelService.AlleNachZielRundeIdAsync(Runde.Id);
            var platzhalter = TeilnehmerPoolHilfsmittel.PlatzhalterAusVorrunden(
                _instanzRunden, Runde, eingehendeRegeln, Kombinationstabelle.AusJson(Runde.KombinationstabelleJson));
            var alleTeilnehmer = platzhalter ?? await TeilnehmerOhneVorgaengerAsync();

            TeamsBenoetigt = platzhalter?.Count ?? Runde.EingangsMannschaftsanzahl;
            SlotsAufbauen(alleTeilnehmer, TeamsBenoetigt);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
        finally { IstLadend = false; }
    }

    /// <summary>Teilnehmer einer Runde ohne Vorgänger. Issue #297: "echte Auslosung mit Mannschaften nur bei
    /// Status != Vorlage" — eine Vorlage hat noch keine echten Teilnehmer, daher generische Platzhalter statt einer
    /// fachlich unpassenden echten Mannschaftsliste.</summary>
    private async Task<IReadOnlyList<TeilnehmerAnzeige>> TeilnehmerOhneVorgaengerAsync()
    {
        if (_instanz.Status == TurnierStatus.Vorlage)
            return Enumerable.Range(1, Runde.EingangsMannschaftsanzahl)
                .Select(i => TeilnehmerAnzeige.AusPlatzhalter($"Team {i}")).ToList();

        var teams = await _mannschaftService.AlleNachTurnierfilterAsync(
            _instanz.VerbandId, _instanz.LandId, _instanz.MannschaftsartFilter);
        return teams.Select(TeilnehmerAnzeige.AusMannschaft).ToList();
    }

    private void SlotsAufbauen(IReadOnlyList<TeilnehmerAnzeige> alleTeilnehmer, int teamAnzahl)
    {
        if (IstGruppenModus)
        {
            if (Runde.Rundenart == Rundenart.Gruppenphase)
            {
                int gruppenAnzahl = Runde.GruppenAnzahl ?? 1;
                int slotsProGruppe = teamAnzahl > 0 && gruppenAnzahl > 0 ? teamAnzahl / gruppenAnzahl : 4;
                Gruppen = new ObservableCollection<ManuelleGruppe>(
                    Enumerable.Range(1, gruppenAnzahl).Select(gNr => new ManuelleGruppe
                    {
                        GruppenNr   = gNr,
                        GruppenName = $"Gruppe {GruppenHerkunft.Buchstabe(gNr)}",
                        Slots       = new ObservableCollection<ManuellerGruppenSlot>(
                            Enumerable.Range(1, slotsProGruppe).Select(sNr => new ManuellerGruppenSlot
                            {
                                GruppenNr = gNr,
                                SlotNr    = sNr
                            }))
                    }));
            }
            else
            {
                int slotAnzahl = Math.Max(teamAnzahl, 1);
                Gruppen = new ObservableCollection<ManuelleGruppe>(
                [
                    new ManuelleGruppe
                    {
                        GruppenNr   = 1,
                        GruppenName = "Teilnehmer",
                        Slots       = new ObservableCollection<ManuellerGruppenSlot>(
                            Enumerable.Range(1, slotAnzahl).Select(sNr => new ManuellerGruppenSlot
                            {
                                GruppenNr = 1,
                                SlotNr    = sNr
                            }))
                    }
                ]);
            }
            KoPaarungen = [];
        }
        else
        {
            int paarAnzahl = teamAnzahl > 0 ? (int)Math.Ceiling(teamAnzahl / 2.0) : 1;
            KoPaarungen = new ObservableCollection<ManuelleKoPaarungAnzeige>(
                Enumerable.Range(1, paarAnzahl).Select(nr => new ManuelleKoPaarungAnzeige(
                    nr,
                    new ManuellerKoSlot { PaarungsNr = nr, IstHeim = true },
                    new ManuellerKoSlot { PaarungsNr = nr, IstHeim = false })));
            Gruppen = [];
        }

        if (IstAbgeschlossen && _auslosung is not null)
        {
            SlotsAusErgebnissenBefuellen(_auslosung.Ergebnisse);
            _ausgangsRang = [];
            SammlungsAbgleich.Abgleichen(VerfuegbareTeilnehmer, [], TeilnehmerSchluessel);
        }
        else
        {
            // Platzhalter (z.B. "Platz 9 (Ligaphase)" … "Platz 24 (Ligaphase)") kommen aus den Generatoren
            // in TeilnehmerPoolHilfsmittel bereits in der richtigen (numerischen) Reihenfolge — ein
            // Alphabet-Sort nach Name reißt sie auseinander ("Platz 10" vor "Platz 9", Fund aus #297-Test).
            // Nur echte Mannschaftslisten (Erste-Runde-ohne-Vorgänger-Fall) profitieren vom Alphabet-Sort.
            var sortiert = alleTeilnehmer.All(t => t.IstPlatzhalter)
                ? alleTeilnehmer.ToList()
                : alleTeilnehmer.OrderBy(t => t.Name).ToList();
            _ausgangsRang = sortiert
                .Select((t, i) => (Schluessel: TeilnehmerSchluessel(t), Rang: i))
                .GroupBy(x => x.Schluessel)
                .ToDictionary(g => g.Key, g => g.First().Rang);
            SammlungsAbgleich.Abgleichen(VerfuegbareTeilnehmer, sortiert, TeilnehmerSchluessel);
        }
        AktualisiereTeamsZugewiesen();
    }

    private void SlotsAusErgebnissenBefuellen(IEnumerable<AuslosungsErgebnis> ergebnisse)
    {
        if (IstGruppenModus)
        {
            foreach (var e in ergebnisse)
            {
                var slot = Gruppen.FirstOrDefault(g => g.GruppenNr == e.PaarungsNummer)?.Slots.FirstOrDefault(s => s.IstLeer);
                slot?.Besetzen(TeamAnzeigeAusErgebnisHeim(e));
            }
            return;
        }

        foreach (var e in ergebnisse)
        {
            var paarung = KoPaarungen.FirstOrDefault(p => p.PaarungsNr == e.PaarungsNummer);
            if (paarung is null) continue;
            paarung.HeimSlot.Besetzen(TeamAnzeigeAusErgebnisHeim(e));
            if (!e.IstFreilos && (e.AuswaertsMannschaftId.HasValue || e.AuswaertsPlatzhalterName is not null))
                paarung.AuswaertsSlot.Besetzen(TeamAnzeigeAusErgebnisAuswaerts(e));
        }
    }

    private static TeilnehmerAnzeige TeamAnzeigeAusErgebnisHeim(AuslosungsErgebnis e) =>
        e.HeimMannschaftId.HasValue
            ? new TeilnehmerAnzeige(e.HeimMannschaftId, e.HeimMannschaft?.Name ?? "?", e.HeimMannschaft?.Land?.Kurzname,
                LandId: e.HeimMannschaft?.LandId, Wappen: TeilnehmerAnzeige.MannschaftsIcon(e.HeimMannschaft))
            : TeilnehmerAnzeige.AusPlatzhalter(e.HeimPlatzhalterName ?? "?");

    private static TeilnehmerAnzeige TeamAnzeigeAusErgebnisAuswaerts(AuslosungsErgebnis e) =>
        e.AuswaertsMannschaftId.HasValue
            ? new TeilnehmerAnzeige(e.AuswaertsMannschaftId, e.AuswaertsMannschaft?.Name ?? "?", e.AuswaertsMannschaft?.Land?.Kurzname,
                LandId: e.AuswaertsMannschaft?.LandId, Wappen: TeilnehmerAnzeige.MannschaftsIcon(e.AuswaertsMannschaft))
            : TeilnehmerAnzeige.AusPlatzhalter(e.AuswaertsPlatzhalterName ?? "?");

    // ── Öffentliche Methoden: Slot-Zuweisung (Drag&Drop/Doppelklick-Ziel im View-Code-behind) ──────

    public string? TeamInKoSlotPlatzieren(TeilnehmerAnzeige team, ManuellerKoSlot slot)
    {
        ManuellesFehler = null;
        if (!VerfuegbareTeilnehmer.Contains(team)) return null; // bereits platziert — nie doppelt besetzen (#392)
        if (HatRegelVerletzung(team, slot.PaarungsNr, istHeim: slot.IstHeim, out var fehler))
        {
            ManuellesFehler = fehler;
            return fehler;
        }
        VerfuegbareTeilnehmer.Remove(team);
        slot.Besetzen(team);
        AktualisiereTeamsZugewiesen();
        return null;
    }

    public void TeamAusKoSlotEntfernen(ManuellerKoSlot slot)
    {
        var team = slot.Leeren();
        if (team is not null) ZurueckZuVerfuegbar(team);
        ManuellesFehler = null;
        AktualisiereTeamsZugewiesen();
    }

    public string? TeamInGruppenSlotPlatzieren(TeilnehmerAnzeige team, ManuellerGruppenSlot slot)
    {
        ManuellesFehler = null;
        if (!VerfuegbareTeilnehmer.Contains(team)) return null; // bereits platziert — nie doppelt besetzen (#392)
        if (HatRegelVerletzungGruppe(team, slot.GruppenNr, out var fehler))
        {
            ManuellesFehler = fehler;
            return fehler;
        }
        VerfuegbareTeilnehmer.Remove(team);
        slot.Besetzen(team);
        AktualisiereTeamsZugewiesen();
        return null;
    }

    public void TeamAusGruppenSlotEntfernen(ManuellerGruppenSlot slot)
    {
        var team = slot.Leeren();
        if (team is not null) ZurueckZuVerfuegbar(team);
        ManuellesFehler = null;
        AktualisiereTeamsZugewiesen();
    }

    private void ZurueckZuVerfuegbar(TeilnehmerAnzeige team)
    {
        var schluessel = TeilnehmerSchluessel(team);
        if (VerfuegbareTeilnehmer.Any(t => TeilnehmerSchluessel(t) == schluessel)) return;

        // An der Ausgangsposition einsortieren; Unbekannte (nicht aus dem Pool) ans Ende
        var rang = _ausgangsRang.GetValueOrDefault(schluessel, int.MaxValue);
        var index = 0;
        while (index < VerfuegbareTeilnehmer.Count &&
               _ausgangsRang.GetValueOrDefault(TeilnehmerSchluessel(VerfuegbareTeilnehmer[index]), int.MaxValue) <= rang)
            index++;
        VerfuegbareTeilnehmer.Insert(index, team);
    }

    public ManuellerKoSlot? NaechsterFreierKoSlot() =>
        KoPaarungen.SelectMany(p => new[] { p.HeimSlot, p.AuswaertsSlot }).FirstOrDefault(s => s.IstLeer);

    public ManuellerGruppenSlot? NaechsterFreierGruppenSlot() =>
        Gruppen.SelectMany(g => g.Slots).FirstOrDefault(s => s.IstLeer);

    private void AktualisiereTeamsZugewiesen()
    {
        TeamsZugewiesen = IstGruppenModus
            ? Gruppen.SelectMany(g => g.Slots).Count(s => !s.IstLeer)
            : KoPaarungen.SelectMany(p => new[] { p.HeimSlot, p.AuswaertsSlot }).Count(s => !s.IstLeer);

        // Bewusst bei jedem Aufruf statt über die Zähler-Property: zwei Züge können dieselbe Anzahl ergeben
        // und trotzdem die Vollständigkeit ändern (z.B. Team vom Auswärts- auf den Heimplatz).
        OnPropertyChanged(nameof(BelegungVollstaendig));
        AbschliessenCommand.NotifyCanExecuteChanged();
    }

    private bool RegelAktiv(AuslosungsRegelArt art) =>
        _auslosung?.Regeln.Any(r => r.IstAktiv && r.Regelart == art) == true;

    // Prüft „Kein gleiches Land“ und „Keine gleiche Gruppe“ (#331) — die Gruppen-Herkunft steckt im
    // Platzhalternamen („1. Gruppe A“), echte Mannschaften gibt es hier nur in der ersten Runde ohne Vorgänger.
    private string? RegelVerletzung(TeilnehmerAnzeige neuTeam, TeilnehmerAnzeige anderes, string beziehung)
    {
        if (RegelAktiv(AuslosungsRegelArt.KeinGleichesLand) &&
            neuTeam.LandId.HasValue && neuTeam.LandId == anderes.LandId)
            return $"Regel 'Kein gleiches Land': '{neuTeam.Name}' und '{anderes.Name}' " +
                   $"sind aus demselben Land und dürfen nicht {beziehung}.";

        if (RegelAktiv(AuslosungsRegelArt.KeineGleicheGruppe) &&
            GruppenHerkunft.AusPlatzhalter(neuTeam.Name) is { } herkunftNeu &&
            GruppenHerkunft.AusPlatzhalter(anderes.Name) is { } herkunftAnderes &&
            herkunftNeu.Gruppe == herkunftAnderes.Gruppe)
            return $"Regel 'Keine gleiche Gruppe': '{neuTeam.Name}' und '{anderes.Name}' " +
                   $"waren in derselben Gruppe und dürfen nicht {beziehung}.";

        return null;
    }

    private bool HatRegelVerletzung(TeilnehmerAnzeige neuTeam, int paarungsNr, bool istHeim, out string? fehler)
    {
        fehler = null;
        var paarung = KoPaarungen.FirstOrDefault(p => p.PaarungsNr == paarungsNr);
        if (paarung is null) return false;
        var gegnerSlot = istHeim ? paarung.AuswaertsSlot : paarung.HeimSlot;
        if (gegnerSlot.IstLeer) return false;
        fehler = RegelVerletzung(neuTeam, gegnerSlot.Team!, "gepaart werden");
        return fehler is not null;
    }

    private bool HatRegelVerletzungGruppe(TeilnehmerAnzeige neuTeam, int gruppenNr, out string? fehler)
    {
        fehler = null;
        var gruppe = Gruppen.FirstOrDefault(g => g.GruppenNr == gruppenNr);
        if (gruppe is null) return false;
        fehler = gruppe.Slots
            .Where(s => !s.IstLeer)
            .Select(s => RegelVerletzung(neuTeam, s.Team!, "in dieselbe Gruppe"))
            .FirstOrDefault(f => f is not null);
        return fehler is not null;
    }

    // ── Abschließen / Zurücksetzen ───────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannAbschliessen))]
    private async Task Abschliessen()
    {
        Fehlermeldung = null;
        ManuellesFehler = null;
        try
        {
            List<ManuellePaarungEingabe> paarungen;
            if (IstGruppenModus)
            {
                var alleSlots = Gruppen
                    .SelectMany(g => g.Slots.Select(s => (g.GruppenNr, s)))
                    .Where(x => !x.s.IstLeer)
                    .ToList();
                if (alleSlots.Count == 0)
                {
                    ManuellesFehler = "Bitte mindestens eine Zuweisung vornehmen.";
                    return;
                }
                var leereSlots = Gruppen.Sum(g => g.Slots.Count(s => s.IstLeer));
                if (leereSlots > 0)
                {
                    ManuellesFehler = $"Es sind noch {leereSlots} Gruppenplatz/-plätze unbesetzt. " +
                                      "Bitte alle Plätze befüllen, bevor die Paarung abgeschlossen wird.";
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
                var allePaarungen = KoPaarungen.Where(p => !p.HeimSlot.IstLeer).ToList();
                if (allePaarungen.Count == 0)
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

            // Schon die Rückfrage liest den Spielplan — der Schutz gilt ab hier bis nach dem Neuladen (#416).
            using var schutz = await _dbContextSchutz.BetretenAsync();
            if (!await ErgebnisVerlustBestaetigtAsync()) return;

            // Erst ab hier messen — die Rückfrage davor wartet auf den Nutzer.
            using var vorgang = _logger.Vorgang($"Vorbelegung: Paarung abschließen (Runde {Runde.Id}, {paarungen.Count} Einträge)");

            _auslosung ??= await _auslosungService.ErstellenAsync(new AuslosungErstellenEingabe(
                _instanz.Id, Runde.Id, Runde.Name, Auslosungsart.Manuell, IstAutomatisch: false));

            // Der Service ersetzt dabei auch den Spielplan der Runde und zieht die Folgerunden nach (#411).
            using (_logger.Vorgang($"Vorbelegung: Paarungen speichern, Spielplan generieren (Auslosung {_auslosung.Id})"))
                await _auslosungService.ManuelleErgebnisseAbschliessenAsync(_auslosung.Id, paarungen);
            await TurnierBaumAktualisierenAsync();
            await LadenAsync();
            Geaendert?.Invoke();
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    [RelayCommand(CanExecute = nameof(KannZuruecksetzen))]
    private async Task Zuruecksetzen()
    {
        if (_auslosung is null) return;
        Fehlermeldung = null;
        try
        {
            using var schutz = await _dbContextSchutz.BetretenAsync();
            using var vorgang = _logger.Vorgang($"Vorbelegung: zurücksetzen (Runde {Runde.Id}, Auslosung {_auslosung.Id})");
            using (_logger.Vorgang($"Vorbelegung: Auslosung zurücksetzen (Auslosung {_auslosung.Id})"))
                await _auslosungService.ZuruecksetzenAsync(_auslosung.Id);
            await TurnierBaumAktualisierenAsync();
            await LadenAsync();
            Geaendert?.Invoke();
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    private bool KannAbschliessen() => IstBearbeitbar && BelegungVollstaendig;
    private bool KannZuruecksetzen() => IstAbgeschlossen;

    private Task<bool> ErgebnisVerlustBestaetigtAsync() =>
        SpielplanErsetzenRueckfrage.ErgebnisVerlustBestaetigtAsync(
            _spielplanService, _dialogService, Runde.Id, "Paarung abschließen", "Das Abschließen", "abschließen");

    private async Task TurnierBaumAktualisierenAsync()
    {
        using var vorgang = _logger.Vorgang($"Vorbelegung: Turnierbaum aktualisieren (Instanz {_instanz.Id})");
        await _baumService.NachSpielplanAenderungAktualisierenAsync(_instanz.Id);
    }
}
