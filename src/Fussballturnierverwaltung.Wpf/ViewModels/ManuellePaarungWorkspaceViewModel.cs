using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using System.Collections.ObjectModel;
using System.Windows;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

// ── Anzeigetypen für die Slot-Grids (Issue #251, aus AuslosungViewModel verschoben — ohne den dortigen
// Lostopf-Zwischenspeicher: hier wird direkt aus "Verfügbar" in einen Slot gezogen, siehe
// ManuellePaarungWorkspaceViewModel) ──

public class ManuellerKoSlot : ObservableObject
{
    private TeilnehmerAnzeige? _team;
    public int PaarungsNr { get; init; }
    public bool IstHeim { get; init; }

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

    public void Besetzen(TeilnehmerAnzeige team) => Team = team;
    public TeilnehmerAnzeige? Leeren()
    {
        var team = Team;
        Team = null;
        return team;
    }
}

public record ManuelleKoPaarungAnzeige(int PaarungsNr, ManuellerKoSlot HeimSlot, ManuellerKoSlot AuswaertsSlot);

public class ManuellerGruppenSlot : ObservableObject
{
    private TeilnehmerAnzeige? _team;
    public int GruppenNr { get; init; }
    public int SlotNr { get; init; }

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

    public void Besetzen(TeilnehmerAnzeige team) => Team = team;
    public TeilnehmerAnzeige? Leeren()
    {
        var team = Team;
        Team = null;
        return team;
    }
}

public class ManuelleGruppe : ObservableObject
{
    public int GruppenNr { get; init; }
    public string GruppenName { get; init; } = "";
    public ObservableCollection<ManuellerGruppenSlot> Slots { get; init; } = [];
}

/// <summary>
/// Workspace für die Paarungsfestlegung einer Runde mit <see cref="Auslosungsart.Manuell"/> (Issue #251)
/// — bewusst eine reine <see cref="ObservableObject"/>-Helferklasse statt ViewModelBase (Precedent:
/// <see cref="TurnierTabKoordinator"/>, <c>UebergangsregelBlock</c> in RundenEditorViewModel.cs), gehostet
/// von <see cref="PaarungsfestlegungViewModel"/> — eine Instanz je aktuell bearbeiteter Runde.
///
/// Ursprünglich Teil von AuslosungViewModel (Auslosungsart.Manuell-Zweig), von dort vollständig entfernt
/// (kein Nebeneinander zweier Wege zum selben Ziel, siehe Plan). Vereinfacht gegenüber dem Original: kein
/// Lostopf-Zwischenspeicher mehr — der diente dort nur als lokale Zwischenablage ohne algorithmische
/// Bedeutung für <see cref="AuslosungService.ManuelleErgebnisseAbschliessenAsync"/>; hier wird direkt aus
/// "Verfügbar" in einen Slot gezogen. Dadurch entsteht bis zum Klick auf "Abschließen" auch kein
/// DB-Schreibzugriff — die <see cref="Auslosung"/>-Entität wird erst dabei angelegt (lazy).
/// </summary>
public partial class ManuellePaarungWorkspaceViewModel : ObservableObject
{
    private readonly AuslosungService _auslosungService;
    private readonly MannschaftService _mannschaftService;
    private readonly SpielplanService _spielplanService;
    private readonly UebergangsregelService _uebergangsregelService;

    private readonly TurnierInstanz _instanz;
    private readonly IReadOnlyList<TurnierRunde> _instanzRunden;

    /// <summary>Bereits vorhandene Manuell-Auslosung dieser Runde (inkl. Ergebnisse/Regeln), oder null,
    /// solange noch nichts abgeschlossen wurde — dann existiert noch keine <see cref="Auslosung"/>-Zeile.</summary>
    private Auslosung? _auslosung;

    public TurnierRunde Runde { get; }

    // Liga und SchweizerSystem teilen sich mit Gruppenphase die "flache Teilnehmerliste ohne echte
    // Kopf-an-Kopf-Paarung"-Semantik (Issue #251-Folgefund, analog RundenEditorViewModel.IstLiga und
    // AuslosungService.ManuelleErgebnisseAbschliessenAsync.istGruppenmodus) — nur echte K.o.-/Sonder-
    // rundenarten benötigen wirkliche Heim/Auswärts-Paarungsslots.
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerfuegbareTeilnehmerGefiltert))]
    private ObservableCollection<TeilnehmerAnzeige> _verfuegbareTeilnehmer = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerfuegbareTeilnehmerGefiltert))]
    private string _teilnehmerFilter = "";

    [ObservableProperty] private TeilnehmerAnzeige? _ausgewaehlterVerfuegbarTeilnehmer;

    public IEnumerable<TeilnehmerAnzeige> VerfuegbareTeilnehmerGefiltert =>
        string.IsNullOrWhiteSpace(TeilnehmerFilter)
            ? VerfuegbareTeilnehmer
            : VerfuegbareTeilnehmer.Where(t => t.Name.Contains(TeilnehmerFilter, StringComparison.OrdinalIgnoreCase));

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

    public string TeamsStatusText => TeamsBenoetigt == 0
        ? $"{TeamsZugewiesen} Teams zugewiesen"
        : TeamsZugewiesen == TeamsBenoetigt
            ? $"✓  Alle {TeamsBenoetigt} Teams zugewiesen"
            : TeamsZugewiesen > TeamsBenoetigt
                ? $"⚠  {TeamsZugewiesen} Teams zugewiesen — {TeamsZugewiesen - TeamsBenoetigt} zu viel (Runde erwartet {TeamsBenoetigt})"
                : $"{TeamsZugewiesen} / {TeamsBenoetigt} Teams zugewiesen — noch {TeamsBenoetigt - TeamsZugewiesen} fehlen";

    /// <summary>Meldet dem Host (<see cref="PaarungsfestlegungViewModel"/>) eine Änderung, die dessen
    /// Runden-Statusliste betrifft (Abschließen/Zurücksetzen).</summary>
    public event Action? Geaendert;

    public bool HatUngespeicherteAenderungen => !IstAbgeschlossen && TeamsZugewiesen > 0;

    public ManuellePaarungWorkspaceViewModel(
        AuslosungService auslosungService,
        MannschaftService mannschaftService,
        SpielplanService spielplanService,
        UebergangsregelService uebergangsregelService,
        TurnierInstanz instanz,
        TurnierRunde runde,
        IReadOnlyList<TurnierRunde> instanzRunden)
    {
        _auslosungService = auslosungService;
        _mannschaftService = mannschaftService;
        _spielplanService = spielplanService;
        _uebergangsregelService = uebergangsregelService;
        _instanz = instanz;
        Runde = runde;
        _instanzRunden = instanzRunden;
    }

    public async Task LadenAsync()
    {
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
            IReadOnlyList<TeilnehmerAnzeige> alleTeilnehmer;
            bool hatVorgaenger;
            if (eingehendeRegeln.Count > 0)
            {
                // Issue #251-Folgefund: unterstützt mehrere unabhängige Quellrunden gleichzeitig
                // (Champions-League-Testfall: Achtelfinale erhält Platz 1-8 direkt aus der Liga-Phase UND
                // die Sieger der Playoff-Runde).
                alleTeilnehmer = TeilnehmerPoolHilfsmittel.PlatzhalterAusRegelnGenerieren(eingehendeRegeln);
                hatVorgaenger = true;
            }
            else
            {
                var vorherigeRunde = TeilnehmerPoolHilfsmittel.VorgaengerrundeErmitteln(_instanzRunden, Runde);
                if (vorherigeRunde is null)
                {
                    var teams = await _mannschaftService.AlleNachTurnierfilterAsync(
                        _instanz.VerbandId, _instanz.LandId, _instanz.MannschaftsartFilter);
                    alleTeilnehmer = teams.Select(TeilnehmerAnzeige.AusMannschaft).ToList();
                    hatVorgaenger = false;
                }
                else
                {
                    alleTeilnehmer = TeilnehmerPoolHilfsmittel.PlatzhalterOhneRegelnGenerieren(Runde, vorherigeRunde);
                    hatVorgaenger = true;
                }
            }

            // Issue #251-Folgefund: bei einer Vorgängerrunde ist die tatsächlich aufgelöste Teilnehmerliste
            // maßgeblich für die Slot-Anzahl, nicht Runde.EingangsMannschaftsanzahl — das Feld wird erst beim
            // erneuten Öffnen der Runde im Runden-Editor automatisch mit der Übergangsregel-Summe abgeglichen
            // (RundenEditorViewModel.EingangAusRegelnAbleiten) und kann bis dahin veraltet sein. Ohne
            // Vorgängerrunde (erste Runde, echte Teams) bleibt das Feld die einzig sinnvolle Quelle.
            int teamAnzahl = hatVorgaenger ? alleTeilnehmer.Count : Runde.EingangsMannschaftsanzahl;
            TeamsBenoetigt = teamAnzahl;
            SlotsAufbauen(alleTeilnehmer, teamAnzahl);
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
        finally { IstLadend = false; }
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
                        GruppenName = $"Gruppe {GruppenBuchstabe(gNr)}",
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
                // Liga/SchweizerSystem (Issue #251-Folgefund): flache Teilnehmerliste ohne Gruppen-
                // struktur — eine einzelne "Gruppe" repräsentiert hier die gesamte Teilnehmerliste
                // der Runde, analog zu RundenEditorViewModel.IstLiga (Liga und SchweizerSystem teilen
                // sich dort dieselbe Darstellung: reine Tabellenrang-Platzierung ohne Gruppenindex).
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
            VerfuegbareTeilnehmer = [];
        }
        else
        {
            VerfuegbareTeilnehmer = new ObservableCollection<TeilnehmerAnzeige>(alleTeilnehmer.OrderBy(t => t.Name));
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

    private static string GruppenBuchstabe(int nr) => nr <= 26 ? ((char)('A' + nr - 1)).ToString() : $"G{nr}";

    // ── Öffentliche Methoden: Slot-Zuweisung (Drag&Drop/Doppelklick-Ziel im View-Code-behind) ──────

    public string? TeamInKoSlotPlatzieren(TeilnehmerAnzeige team, ManuellerKoSlot slot)
    {
        ManuellesFehler = null;
        if (HatKeinGleichesLandVerletzung(team, slot.PaarungsNr, istHeim: slot.IstHeim, out var fehler))
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
        if (HatKeinGleichesLandVerletzungGruppe(team, slot.GruppenNr, out var fehler))
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
        if (!VerfuegbareTeilnehmer.Any(t => t.Name == team.Name && t.MannschaftId == team.MannschaftId))
            VerfuegbareTeilnehmer.Add(team);
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
    }

    private bool HatKeinGleichesLandVerletzung(TeilnehmerAnzeige neuTeam, int paarungsNr, bool istHeim, out string? fehler)
    {
        fehler = null;
        if (_auslosung?.Regeln.Any(r => r.IstAktiv && r.Regelart == AuslosungsRegelArt.KeinGleichesLand) != true)
            return false;
        if (!neuTeam.LandId.HasValue) return false;
        var paarung = KoPaarungen.FirstOrDefault(p => p.PaarungsNr == paarungsNr);
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
        if (_auslosung?.Regeln.Any(r => r.IstAktiv && r.Regelart == AuslosungsRegelArt.KeinGleichesLand) != true)
            return false;
        if (!neuTeam.LandId.HasValue) return false;
        var gruppe = Gruppen.FirstOrDefault(g => g.GruppenNr == gruppenNr);
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

            _auslosung ??= await _auslosungService.ErstellenAsync(new AuslosungErstellenEingabe(
                _instanz.Id, Runde.Id, Runde.Name, Auslosungsart.Manuell, IstAutomatisch: false));

            await _auslosungService.ManuelleErgebnisseAbschliessenAsync(_auslosung.Id, paarungen);
            await SpielplanAutomatischGenerierenAsync(_auslosung.Id);
            Geaendert?.Invoke();
            await LadenAsync();
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
            await _auslosungService.ZuruecksetzenAsync(_auslosung.Id);
            Geaendert?.Invoke();
            await LadenAsync();
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    private bool KannAbschliessen() => IstBearbeitbar;
    private bool KannZuruecksetzen() => IstAbgeschlossen;

    /// <summary>Generiert den Spielplan automatisch, sobald die Paarung abgeschlossen ist (Issue #19,
    /// analog AuslosungViewModel). Fragt bei bereits vorhandenen Spielen nach, ob sie ersetzt werden
    /// sollen (z.B. aus einer früheren, inzwischen zurückgesetzten Paarung derselben Runde).</summary>
    private async Task SpielplanAutomatischGenerierenAsync(int auslosungId)
    {
        var detail = await _auslosungService.FindenNachIdAsync(auslosungId);
        if (detail is null || detail.Status != AuslosungsStatus.Abgeschlossen) return;

        var rundeId = detail.TurnierRundeId;
        var vorhandeneSpiele = await _spielplanService.SpielplanAbrufenAsync(rundeId);
        if (vorhandeneSpiele.Count > 0)
        {
            var antwort = MessageBox.Show(
                $"Die Runde hat bereits {vorhandeneSpiele.Count} Spiel(e) (z.B. aus einer früheren Paarung). " +
                "Soll der bestehende Spielplan gelöscht und aus dieser Paarung neu generiert werden?",
                "Spielplan neu generieren",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (antwort != MessageBoxResult.Yes) return;

            await _spielplanService.LoeschenAsync(rundeId);
        }

        await _spielplanService.GenerierenAusAuslosungAsync(rundeId, [.. detail.Ergebnisse]);
    }
}
