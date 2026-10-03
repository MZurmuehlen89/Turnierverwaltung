using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Auslosungen;
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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Eine Runde in der Rundenliste des "Vorbelegung"-Tabs (Issue #297) mit ihrem aktuellen
/// Paarungsfestlegungs-Status. <see cref="IstEditierbarHier"/> ist false, wenn die Runde bereits eine echte
/// Auslosung (Zufall/NachSetzliste/…) hat — diese wird weiterhin in Turnierdurchführung → Auslosung verwaltet
/// (#288), hier nur als Hinweis angezeigt.</summary>
public partial class RundenVorbelegungsEintrag : ObservableObject
{
    public required TurnierRunde Runde { get; init; }
    [ObservableProperty] private bool _istEditierbarHier;
    [ObservableProperty] private string _statusText = "";
}

// ── Anzeigetypen für die Slot-Grids — 1:1 aus Wpf/ViewModels/ManuellePaarungWorkspaceViewModel.cs portiert ──

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
        TurnierInstanz instanz,
        TurnierRunde runde,
        IReadOnlyList<TurnierRunde> instanzRunden)
    {
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
                alleTeilnehmer = TeilnehmerPoolHilfsmittel.PlatzhalterAusRegelnGenerieren(
                    eingehendeRegeln, Kombinationstabelle.AusJson(Runde.KombinationstabelleJson));
                hatVorgaenger = true;
            }
            else
            {
                var vorherigeRunde = TeilnehmerPoolHilfsmittel.VorgaengerrundeErmitteln(_instanzRunden, Runde);
                if (vorherigeRunde is null)
                {
                    // Issue #297: "echte Auslosung mit Mannschaften nur bei Status != Vorlage" — eine
                    // Vorlage hat noch keine echten Teilnehmer, generische Platzhalter statt einer fachlich
                    // unpassenden echten Mannschaftsliste.
                    if (_instanz.Status == TurnierStatus.Vorlage)
                    {
                        alleTeilnehmer = Enumerable.Range(1, Runde.EingangsMannschaftsanzahl)
                            .Select(i => TeilnehmerAnzeige.AusPlatzhalter($"Team {i}")).ToList();
                    }
                    else
                    {
                        var teams = await _mannschaftService.AlleNachTurnierfilterAsync(
                            _instanz.VerbandId, _instanz.LandId, _instanz.MannschaftsartFilter);
                        alleTeilnehmer = teams.Select(TeilnehmerAnzeige.AusMannschaft).ToList();
                    }
                    hatVorgaenger = false;
                }
                else
                {
                    alleTeilnehmer = TeilnehmerPoolHilfsmittel.PlatzhalterOhneRegelnGenerieren(Runde, vorherigeRunde);
                    hatVorgaenger = true;
                }
            }

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

    private static string GruppenBuchstabe(int nr) => nr <= 26 ? ((char)('A' + nr - 1)).ToString() : $"G{nr}";

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

            if (!await ErgebnisVerlustBestaetigtAsync()) return;

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
            await TurnierBaumAktualisierenAsync();
            Geaendert?.Invoke();
            await LadenAsync();
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
    }

    private bool KannAbschliessen() => IstBearbeitbar && BelegungVollstaendig;
    private bool KannZuruecksetzen() => IstAbgeschlossen;

    /// <summary>Das Abschließen ersetzt den Spielplan der Runde ohne Rückfrage — wer die Paarungen abschließt,
    /// will den Spielplan dazu (gleiche Regel wie <c>AuslosungViewModel</c>). Nachgefragt wird nur, wenn dabei
    /// bereits eingetragene Ergebnisse verloren gingen, und dann VOR dem Abschließen: ein "Nein" danach
    /// hinterließe abgeschlossene Paarungen mit einem Spielplan, der nicht zu ihnen passt.</summary>
    private async Task<bool> ErgebnisVerlustBestaetigtAsync()
    {
        var vorhandeneSpiele = await _spielplanService.SpielplanAbrufenAsync(Runde.Id);
        int mitErgebnis = vorhandeneSpiele.Count(s => s.Ergebnis is not null);
        if (mitErgebnis == 0) return true;

        return await _dialogService.BestaetigenAsync(
            "Paarung abschließen",
            $"Die Runde hat bereits {mitErgebnis} Spiel(e) mit eingetragenem Ergebnis. Das Abschließen ersetzt den " +
            "Spielplan der Runde, diese Ergebnisse gehen dabei verloren. Trotzdem abschließen?");
    }

    /// <summary>Generiert den Spielplan automatisch, sobald die Paarung abgeschlossen ist (Issue #19,
    /// analog WPF-Original). Vorhandene Spiele der Runde werden ohne Rückfrage ersetzt; nach eingetragenen
    /// Ergebnissen fragt <see cref="ErgebnisVerlustBestaetigtAsync"/> schon vor dem Abschließen.</summary>
    private async Task SpielplanAutomatischGenerierenAsync(int auslosungId)
    {
        var detail = await _auslosungService.FindenNachIdAsync(auslosungId);
        if (detail is null || detail.Status != AuslosungsStatus.Abgeschlossen) return;

        var rundeId = detail.TurnierRundeId;
        var vorhandeneSpiele = await _spielplanService.SpielplanAbrufenAsync(rundeId);
        if (vorhandeneSpiele.Count > 0)
        {
            // Issue #320-Folgefund: kaskadenlos löschen — LoeschenAsync würde selbst sofort wieder die
            // Kaskade auslösen, die bei einer Platzhalter-K.o.-Runde mit weiterhin abgeschlossener
            // Auslosung erneut komplett neu generiert, bevor der folgende GenerierenAusAuslosungAsync-
            // Aufruf noch einmal generiert — macht aus 4 erwarteten Spielen 8 Phantom-Spiele.
            await _spielplanService.RundenSpielplanOhneKaskadeLoeschenAsync(rundeId);
        }

        await _spielplanService.GenerierenAusAuslosungAsync(rundeId, [.. detail.Ergebnisse]);

        // Folgerunden können bereits aufgelöste Platzhalter enthalten, die auf den alten (jetzt ersetzten)
        // Spielplan verwiesen haben (#306) — ohne die Kaskade blieben sie fälschlich stehen.
        await _spielplanService.KaskadeAusloesenAsync(detail.TurnierInstanzId);
        await TurnierBaumAktualisierenAsync();
    }

    /// <summary>Aktualisiert den Turnierbaum nach jeder Spielplan-Änderung automatisch (Issue #320-
    /// Folgefund: der Nutzer soll den Baum nie manuell neu generieren müssen). Für Turniere ganz ohne
    /// K.o.-Runden wirft <see cref="TurnierBaumService.GenerierenAsync"/>, was hier bewusst geschluckt wird
    /// (analog SpielplanViewModel.TurnierBaumAktualisierenAsync).</summary>
    private async Task TurnierBaumAktualisierenAsync()
    {
        try { await _baumService.GenerierenAsync(_instanz.Id); }
        catch { /* TurnierBaum-Rebuild ist optional — nie Fehler an den Benutzer weitergeben */ }
    }
}

/// <summary>"Vorbelegung"-Tab von <see cref="TurnierlisteViewModel"/> (Issue #297, ersetzt den entfallenen
/// "Paarungen"-Tab/<c>VorlagenPaarungenViewModel</c>) — dritter/vierter Tab neben Turnierkopf/Runden/
/// Übergänge. Für Runden mit von vornherein feststehenden Paarungen (<c>PaarungFestVorgegeben</c>,
/// <see cref="Auslosungsart.Manuell"/>) wird die Paarungsfestlegung hier vorgenommen (Details siehe
/// <see cref="VorbelegungWorkspace"/>). Runden mit einer echten Auslosung bleiben Turnierdurchführung → Auslosung
/// (#288) vorbehalten und werden hier nur read-only mit Hinweis angezeigt. Arbeitet — anders als der
/// entfallene "Paarungen"-Tab — vollständig unabhängig von <see cref="RundenViewModel"/> (lädt Runden/
/// Auslosungen selbst, analog WPF-Original <c>PaarungsfestlegungViewModel</c>).</summary>
public partial class VorbelegungViewModel : ViewModelBase, IRibbonAware
{
    private readonly TurnierInstanzService _instanzService;
    private readonly AuslosungService _auslosungService;
    private readonly MannschaftService _mannschaftService;
    private readonly SpielplanService _spielplanService;
    private readonly TurnierBaumService _baumService;
    private readonly UebergangsregelService _uebergangsregelService;
    private readonly IDialogService _dialogService;

    private TurnierInstanz? _instanz;
    private IReadOnlyList<TurnierRunde> _instanzRunden = [];

    [ObservableProperty] private bool _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatKeineRunden))]
    [NotifyPropertyChangedFor(nameof(ZeigtRundenBereich))]
    private bool _keinTurnierGewaehlt = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatKeineRunden))]
    [NotifyPropertyChangedFor(nameof(HatKeineFestVorgegebenenRunden))]
    [NotifyPropertyChangedFor(nameof(ZeigtRundenBereich))]
    private ObservableCollection<RundenVorbelegungsEintrag> _runden = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatKeineRunden))]
    [NotifyPropertyChangedFor(nameof(HatKeineFestVorgegebenenRunden))]
    [NotifyPropertyChangedFor(nameof(ZeigtRundenBereich))]
    private bool _hatUeberhauptRunden;

    public bool HatKeineRunden => !KeinTurnierGewaehlt && !HatUeberhauptRunden;
    public bool HatKeineFestVorgegebenenRunden => !KeinTurnierGewaehlt && HatUeberhauptRunden && Runden.Count == 0;

    /// <summary>Turnier gewählt, hat Runden, und mindestens eine davon ist als Vorbelegung-Kandidat
    /// sichtbar — der eigentliche Runden-/Workspace-Bereich ist dann an der Reihe (statt eines der drei
    /// Platzhalter-Hinweise oben).</summary>
    public bool ZeigtRundenBereich => !KeinTurnierGewaehlt && !HatKeineRunden && !HatKeineFestVorgegebenenRunden;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RundeNichtEditierbarHier))]
    [NotifyPropertyChangedFor(nameof(ZeigeWorkspace))]
    private RundenVorbelegungsEintrag? _ausgewaehlteRunde;

    /// <summary>Runde ist ausgewählt, hat aber bereits eine echte (nicht-manuelle) Auslosung — die
    /// Paarungsfestlegung bleibt dann Turnierdurchführung → Auslosung (#288) vorbehalten.</summary>
    public bool RundeNichtEditierbarHier => AusgewaehlteRunde is not null && !AusgewaehlteRunde.IstEditierbarHier;

    public bool ZeigeWorkspace => AusgewaehlteRunde is not null && AusgewaehlteRunde.IstEditierbarHier;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private VorbelegungWorkspace? _workspace;

    /// <summary>Unterdrückt die Verwerfen-Rückfrage bzw. den Wechsel-Hook, während die Rundenauswahl vom Code
    /// selbst gesetzt wird (Instanzwechsel, Rücksprung nach "Nein").</summary>
    private bool _rundenAuswahlOhneRueckfrage;
    private bool _rundenRuecksprungAktiv;

    public VorbelegungViewModel(
        TurnierInstanzService instanzService,
        AuslosungService auslosungService,
        MannschaftService mannschaftService,
        SpielplanService spielplanService,
        TurnierBaumService baumService,
        UebergangsregelService uebergangsregelService,
        IDialogService dialogService)
    {
        _instanzService = instanzService;
        _auslosungService = auslosungService;
        _mannschaftService = mannschaftService;
        _spielplanService = spielplanService;
        _baumService = baumService;
        _uebergangsregelService = uebergangsregelService;
        _dialogService = dialogService;
    }

    /// <summary>Vom Composite gesetzt, wenn sich die geteilte Instanzauswahl ändert — unabhängig von
    /// <see cref="RundenViewModel"/>, lädt Runden/Auslosungen selbst (analog WPF-Original
    /// <c>PaarungsfestlegungViewModel.AktivierenAsync</c>).</summary>
    public async Task InstanzSetzenAsync(TurnierInstanz? instanz)
    {
        KeinTurnierGewaehlt = instanz is null;
        _rundenAuswahlOhneRueckfrage = true;
        try { AusgewaehlteRunde = null; }
        finally { _rundenAuswahlOhneRueckfrage = false; }

        if (instanz is null)
        {
            _instanz = null;
            _instanzRunden = [];
            Runden = [];
            HatUeberhauptRunden = false;
            return;
        }

        IstLadend = true;
        Fehlermeldung = null;
        try
        {
            _instanz = await _instanzService.FindenNachIdMitRundenAsync(instanz.Id);
            if (_instanz is null)
            {
                Runden = [];
                HatUeberhauptRunden = false;
                return;
            }

            _instanzRunden = _instanz.Runden.OrderBy(r => r.Reihenfolge).ToList();
            HatUeberhauptRunden = _instanzRunden.Count > 0;
            var auslosungen = await _auslosungService.AlleNachInstanzAbrufenAsync(_instanz.Id);

            // Issue #320: GroupBy statt ToDictionary — Neuanlage einer zweiten Auslosung für dieselbe Runde
            // ist jetzt in AuslosungService.ErstellenAsync unterbunden, aber vor diesem Fix bereits entstandene
            // Alt-Daten sollen die Ansicht nicht mit einer ArgumentException zum Absturz bringen (die Manuell-
            // Auslosung, sofern vorhanden, gewinnt — das ist die für diesen Tab relevante).
            var auslosungNachRundeId = auslosungen
                .GroupBy(a => a.TurnierRundeId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.Auslosungsart == Auslosungsart.Manuell).First());

            var sichtbareRunden = _instanzRunden.Where(r =>
                r.PaarungFestVorgegeben ||
                auslosungNachRundeId.GetValueOrDefault(r.Id)?.Auslosungsart == Auslosungsart.Manuell);

            Runden = new ObservableCollection<RundenVorbelegungsEintrag>(
                sichtbareRunden.Select(r => EintragErstellen(r, auslosungNachRundeId.GetValueOrDefault(r.Id))));
        }
        catch (Exception ex) { Fehlermeldung = ex.Message; }
        finally { IstLadend = false; }
    }

    private static RundenVorbelegungsEintrag EintragErstellen(TurnierRunde runde, Auslosung? auslosung) => new()
    {
        Runde = runde,
        IstEditierbarHier = auslosung is null || auslosung.Auslosungsart == Auslosungsart.Manuell,
        StatusText = auslosung switch
        {
            null => "Keine feste Paarung",
            { Auslosungsart: not Auslosungsart.Manuell } => "Auslosung in Turnierdurchführung",
            { Status: AuslosungsStatus.Abgeschlossen } => "Abgeschlossen",
            _ => "In Bearbeitung"
        }
    };

    /// <summary>Rückfrage, bevor eine begonnene, noch nicht abgeschlossene Vorbelegung verloren geht (Runden-,
    /// Turnier- oder Menüwechsel) — gespeichert wird ausschließlich über "Paarung abschließen", ein Wechsel
    /// verwirft die platzierten Teams. Ohne begonnene Vorbelegung synchron <c>true</c>.</summary>
    public async Task<bool> VerwerfenBestaetigtAsync()
    {
        if (Workspace is not { HatUngespeicherteAenderungen: true } workspace) return true;

        return await _dialogService.BestaetigenAsync(
            "Vorbelegung verwerfen",
            $"Für '{workspace.Runde.Name}' sind bereits {workspace.TeamsZugewiesen} Team(s) platziert, die Paarung " +
            "ist aber noch nicht abgeschlossen. Die Vorbelegung geht dabei verloren. Wirklich fortfahren?");
    }

    partial void OnAusgewaehlteRundeChanged(RundenVorbelegungsEintrag? oldValue, RundenVorbelegungsEintrag? newValue)
    {
        if (_rundenRuecksprungAktiv) return;
        _ = RundenWechselAsync(oldValue, newValue);
    }

    private async Task RundenWechselAsync(RundenVorbelegungsEintrag? oldValue, RundenVorbelegungsEintrag? newValue)
    {
        // Läuft ohne begonnene Vorbelegung synchron durch (abgeschlossener Task) — wie vor der Rückfrage.
        if (!_rundenAuswahlOhneRueckfrage && !await VerwerfenBestaetigtAsync())
        {
            _rundenRuecksprungAktiv = true;
            try { AusgewaehlteRunde = oldValue; }
            finally { _rundenRuecksprungAktiv = false; }
            return;
        }

        if (Workspace is not null)
            Workspace.Geaendert -= OnWorkspaceGeaendert;

        if (newValue is null || !newValue.IstEditierbarHier || _instanz is null)
        {
            Workspace = null;
            return;
        }

        var workspace = new VorbelegungWorkspace(
            _auslosungService, _mannschaftService, _spielplanService, _baumService, _uebergangsregelService, _dialogService,
            _instanz, newValue.Runde, _instanzRunden);
        workspace.Geaendert += OnWorkspaceGeaendert;
        Workspace = workspace;
        _ = workspace.LadenAsync();
    }

    /// <summary>Nach Abschließen/Zurücksetzen im Workspace: nur den Status der aktuell ausgewählten
    /// Runde in der Liste aktualisieren — die übrigen Runden können davon nicht betroffen sein.</summary>
    private void OnWorkspaceGeaendert()
    {
        if (AusgewaehlteRunde is null || Workspace is null) return;
        AusgewaehlteRunde.IstEditierbarHier = true;
        AusgewaehlteRunde.StatusText = Workspace.IstAbgeschlossen ? "Abgeschlossen" : "In Bearbeitung";
    }

    // ── IRibbonAware — eigene Datensatz-lose Aktionen (Precedent: WPF SimulationsViewModel, Issue #173) ──
    public string RibbonTabTitel => "Vorbelegung";
    public ICommand? NeuBefehl => null;
    public ICommand? SpeichernBefehl => null;
    public ICommand? AbbrechenBefehl => null;
    public ICommand? LoeschenBefehl => null;
    public bool HatStandardAktionen => false;
    public bool HatLoeschenAktion => false;

    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen => Workspace is null
        ? []
        :
        [
            new RibbonGruppe
            {
                Header = "Paarung",
                Aktionen =
                [
                    new RibbonAktion { Header = "Paarung abschließen", Befehl = Workspace.AbschliessenCommand, IconGlyph = "" },
                    new RibbonAktion { Header = "Zurücksetzen", Befehl = Workspace.ZuruecksetzenCommand, IconGlyph = "" },
                ],
            },
        ];
}
