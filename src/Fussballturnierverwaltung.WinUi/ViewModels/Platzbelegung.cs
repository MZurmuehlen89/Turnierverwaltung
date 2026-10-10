using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Helpers;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Die Platzbelegung einer Runde in der Vorbelegung: das Raster aus Gruppen oder Paarungen, die noch
/// verfügbaren Teilnehmer und das Platzieren und Entfernen von Hand. Arbeitet ohne Datenbank — Laden und Speichern
/// macht der <see cref="VorbelegungWorkspace"/>, der sie zusammensetzt.</summary>
public partial class Platzbelegung : ObservableObject
{
    private readonly TurnierRunde _runde;
    private readonly Func<AuslosungsRegelArt, bool> _regelAktiv;

    public bool IstGruppenModus => _runde.Rundenart is Rundenart.Gruppenphase or Rundenart.Liga or Rundenart.SchweizerSystem;
    public bool IstKoModus => !IstGruppenModus;

    [ObservableProperty] private string? _manuellesFehler;

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

    public Platzbelegung(TurnierRunde runde, Func<AuslosungsRegelArt, bool> regelAktiv)
    {
        _runde = runde;
        _regelAktiv = regelAktiv;
        VerfuegbareTeilnehmer.CollectionChanged += (_, _) => GefilterteTeilnehmerAbgleichen();
    }

    /// <summary>Baut das Raster neu auf: mit der Belegung einer abgeschlossenen Auslosung oder leer mit allen
    /// Teilnehmern als verfügbar.</summary>
    public void SlotsAufbauen(IReadOnlyList<TeilnehmerAnzeige> alleTeilnehmer, int teamAnzahl, Auslosung? abgeschlosseneAuslosung)
    {
        TeamsBenoetigt = teamAnzahl;
        RasterAnlegen(teamAnzahl);
        if (abgeschlosseneAuslosung is not null)
            AbgeschlosseneBelegungZeigen(abgeschlosseneAuslosung);
        else
            VerfuegbareTeilnehmerZeigen(alleTeilnehmer);
        AktualisiereTeamsZugewiesen();
    }

    private void RasterAnlegen(int teamAnzahl)
    {
        if (IstGruppenModus)
        {
            Gruppen = new ObservableCollection<ManuelleGruppe>(
                VorbelegungsRaster.Gruppen(_runde.Rundenart, _runde.GruppenAnzahl, teamAnzahl).Select(LeereGruppe));
            KoPaarungen = [];
        }
        else
        {
            KoPaarungen = new ObservableCollection<ManuelleKoPaarungAnzeige>(
                Enumerable.Range(1, VorbelegungsRaster.PaarungsAnzahl(teamAnzahl)).Select(LeerePaarung));
            Gruppen = [];
        }
    }

    private static ManuelleGruppe LeereGruppe(RasterGruppe gruppe) => new()
    {
        GruppenNr   = gruppe.GruppenNr,
        GruppenName = gruppe.Name,
        Slots       = new ObservableCollection<ManuellerGruppenSlot>(
            Enumerable.Range(1, gruppe.PlatzAnzahl)
                .Select(platz => new ManuellerGruppenSlot { GruppenNr = gruppe.GruppenNr, SlotNr = platz }))
    };

    private static ManuelleKoPaarungAnzeige LeerePaarung(int paarungsNr) => new(
        paarungsNr,
        new ManuellerKoSlot { PaarungsNr = paarungsNr, IstHeim = true },
        new ManuellerKoSlot { PaarungsNr = paarungsNr, IstHeim = false });

    private void AbgeschlosseneBelegungZeigen(Auslosung auslosung)
    {
        SlotsAusErgebnissenBefuellen(auslosung.Ergebnisse);
        _ausgangsRang = [];
        SammlungsAbgleich.Abgleichen(VerfuegbareTeilnehmer, [], TeilnehmerSchluessel);
    }

    private void VerfuegbareTeilnehmerZeigen(IReadOnlyList<TeilnehmerAnzeige> alleTeilnehmer)
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
    }

    // Die Gruppen-Herkunft steckt im Platzhalternamen („1. Gruppe A“), echte Mannschaften gibt es hier nur in
    // der ersten Runde ohne Vorgänger.
    private static RegelHerkunft HerkunftVon(TeilnehmerAnzeige team) =>
        new(team.LandId, GruppenHerkunft.AusPlatzhalter(team.Name)?.Gruppe);

    private string? RegelVerletzung(TeilnehmerAnzeige neuTeam, TeilnehmerAnzeige anderes, string beziehung) =>
        PaarungsRegeln.VerletzteRegel(
            _regelAktiv(AuslosungsRegelArt.KeinGleichesLand), _regelAktiv(AuslosungsRegelArt.KeineGleicheGruppe),
            HerkunftVon(neuTeam), HerkunftVon(anderes)) switch
        {
            AuslosungsRegelArt.KeinGleichesLand =>
                $"Regel 'Kein gleiches Land': '{neuTeam.Name}' und '{anderes.Name}' " +
                $"sind aus demselben Land und dürfen nicht {beziehung}.",
            AuslosungsRegelArt.KeineGleicheGruppe =>
                $"Regel 'Keine gleiche Gruppe': '{neuTeam.Name}' und '{anderes.Name}' " +
                $"waren in derselben Gruppe und dürfen nicht {beziehung}.",
            _ => null
        };

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

    /// <summary>Die Belegung als Eingabe für den Dienst. Lücken gibt es hier nicht mehr — der Befehl ist erst bei
    /// <see cref="BelegungVollstaendig"/> freigegeben; eine K.-o.-Paarung ohne Heimteam ist dann ganz leer.</summary>
    public List<ManuellePaarungEingabe> PaarungsEingaben() => IstGruppenModus
        ? Gruppen.SelectMany(g => g.Slots)
            .Where(s => !s.IstLeer)
            .Select(s => ManuellePaarungEingabe.FuerGruppenplatz(s.GruppenNr, s.Team!.MannschaftId, s.Team.Name))
            .ToList()
        : KoPaarungen
            .Where(p => !p.HeimSlot.IstLeer)
            .Select(p => ManuellePaarungEingabe.FuerPaarung(
                p.PaarungsNr,
                (p.HeimSlot.Team!.MannschaftId, p.HeimSlot.Team.Name),
                p.AuswaertsSlot.Team is { } gast ? (gast.MannschaftId, gast.Name) : null))
            .ToList();
}
