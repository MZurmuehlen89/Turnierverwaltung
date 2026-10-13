using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Helpers;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Die Platzbelegung einer Runde in der Vorbelegung: das Raster aus Gruppen oder Paarungen, die noch
/// verfügbaren Teilnehmer und das Platzieren und Entfernen von Hand. Arbeitet ohne Datenbank — Laden und Speichern
/// macht der <see cref="VorbelegungWorkspace"/>, der sie zusammensetzt.</summary>
public class Platzbelegung : ObservableObject
{
    private readonly Rundenart _rundenart;
    private readonly int? _gruppenAnzahl;

    // Bei jedem Zug gefragt statt beim Aufbau übergeben: die Regeln hängen an der Auslosung der Runde, und die legt
    // der Workspace erst beim Abschließen an.
    private readonly Func<AuslosungsRegelArt, bool> _regelAktiv;

    private readonly ObservableCollection<TeilnehmerAnzeige> _verfuegbareTeilnehmer = [];

    /// <summary>Ein zurückgelegtes Team kehrt an seine ursprüngliche Position zurück statt ans Listenende
    /// (Platzhalter stehen bewusst nicht alphabetisch).</summary>
    private Ausgangsreihenfolge<(int?, string)> _ausgangsreihenfolge = new([]);

    public Platzbelegung(Rundenart rundenart, int? gruppenAnzahl, Func<AuslosungsRegelArt, bool> regelAktiv)
    {
        _rundenart = rundenart;
        _gruppenAnzahl = gruppenAnzahl;
        _regelAktiv = regelAktiv;
        Filter = new TeilnehmerFilterViewModel(_verfuegbareTeilnehmer);
        Filter.Geaendert += GefilterteTeilnehmerAbgleichen;
        _verfuegbareTeilnehmer.CollectionChanged += (_, _) => GefilterteTeilnehmerAbgleichen();
    }

    public bool IstGruppenModus => VorbelegungsRaster.HatGruppen(_rundenart);
    public bool IstKoModus => !IstGruppenModus;

    // ── Verfügbare Teilnehmer ────────────────────────────────────────────────

    /// <summary>Namensfilter über der Verfügbar-Liste. Die Auswahlfilter nach Verband und Land, die derselbe Filter
    /// in der Auslosung anbietet, bleiben hier ohne Optionen und damit wirkungslos.</summary>
    public TeilnehmerFilterViewModel Filter { get; }

    // Stabile Instanz, die nur abgeglichen wird (SammlungsAbgleich, analog Auslosung #388): eine bei jedem Zugriff
    // neu berechnete Where-Aufzählung meldete bei aktivem Namensfilter weder Platzieren noch Zurücklegen an die
    // ListView (#392).
    public ObservableCollection<TeilnehmerAnzeige> VerfuegbareTeilnehmerGefiltert { get; } = [];

    /// <summary>Der noch nicht platzierte Teilnehmer dieses Namens — unabhängig vom Namensfilter.</summary>
    public TeilnehmerAnzeige? VerfuegbarerTeilnehmer(string name) =>
        _verfuegbareTeilnehmer.FirstOrDefault(t => t.Name == name);

    private static (int?, string) TeilnehmerSchluessel(TeilnehmerAnzeige t) =>
        (t.MannschaftId, t.MannschaftId.HasValue ? "" : t.Name);

    private void GefilterteTeilnehmerAbgleichen() =>
        SammlungsAbgleich.Abgleichen(
            VerfuegbareTeilnehmerGefiltert,
            _verfuegbareTeilnehmer.Where(Filter.Passt).ToList(),
            TeilnehmerSchluessel);

    private void VerfuegbareTeilnehmerSetzen(IReadOnlyList<TeilnehmerAnzeige> teilnehmer)
    {
        _ausgangsreihenfolge = new(teilnehmer.Select(TeilnehmerSchluessel));
        SammlungsAbgleich.Abgleichen(_verfuegbareTeilnehmer, teilnehmer, TeilnehmerSchluessel);
    }

    private void ZurueckZuVerfuegbar(TeilnehmerAnzeige team)
    {
        var schluessel = TeilnehmerSchluessel(team);
        var vorhandene = _verfuegbareTeilnehmer.Select(TeilnehmerSchluessel).ToList();
        if (vorhandene.Contains(schluessel)) return;

        _verfuegbareTeilnehmer.Insert(_ausgangsreihenfolge.EinfuegeIndex(vorhandene, schluessel), team);
    }

    // ── Raster, Zähler und Vollständigkeit ───────────────────────────────────

    private IReadOnlyList<ManuelleGruppe> _gruppen = [];
    public IReadOnlyList<ManuelleGruppe> Gruppen
    {
        get => _gruppen;
        private set => SetProperty(ref _gruppen, value);
    }

    private IReadOnlyList<ManuelleKoPaarungAnzeige> _koPaarungen = [];
    public IReadOnlyList<ManuelleKoPaarungAnzeige> KoPaarungen
    {
        get => _koPaarungen;
        private set => SetProperty(ref _koPaarungen, value);
    }

    /// <summary>Alle Plätze in Rasterreihenfolge — je Runde ist nur eine der beiden Listen gefüllt.</summary>
    private IEnumerable<VorbelegungsPlatz> AllePlaetze =>
        Gruppen.SelectMany(g => g.Slots)
            .Concat(KoPaarungen.SelectMany(p => new[] { p.HeimSlot, p.AuswaertsSlot }));

    private int _teamsZugewiesen;
    public int TeamsZugewiesen
    {
        get => _teamsZugewiesen;
        private set
        {
            if (SetProperty(ref _teamsZugewiesen, value)) OnPropertyChanged(nameof(TeamsStatusText));
        }
    }

    private int _teamsBenoetigt;
    public int TeamsBenoetigt
    {
        get => _teamsBenoetigt;
        private set
        {
            if (SetProperty(ref _teamsBenoetigt, value)) OnPropertyChanged(nameof(TeamsStatusText));
        }
    }

    /// <summary>Gibt "Paarung abschließen" erst frei, wenn nichts mehr fehlt — statt eine lückenhafte Belegung
    /// erst beim Abschließen abzulehnen (Gruppen) bzw. Paarungen ohne Heimteam stillschweigend wegzulassen
    /// (K.o.).</summary>
    public bool BelegungVollstaendig => IstGruppenModus
        ? VorbelegungsRaster.GruppenVollstaendig(AllePlaetze.Select(p => !p.IstLeer).ToList())
        : VorbelegungsRaster.PaarungenVollstaendig(
            KoPaarungen.Select(p => (!p.HeimSlot.IstLeer, !p.AuswaertsSlot.IstLeer)).ToList(), TeamsBenoetigt);

    public string TeamsStatusText => TeamsBenoetigt == 0
        ? $"{TeamsZugewiesen} Teams zugewiesen"
        : TeamsZugewiesen == TeamsBenoetigt
            ? $"✓  Alle {TeamsBenoetigt} Teams zugewiesen"
            : TeamsZugewiesen > TeamsBenoetigt
                ? $"⚠  {TeamsZugewiesen} Teams zugewiesen — {TeamsZugewiesen - TeamsBenoetigt} zu viel (Runde erwartet {TeamsBenoetigt})"
                : $"{TeamsZugewiesen} / {TeamsBenoetigt} Teams zugewiesen — noch {TeamsBenoetigt - TeamsZugewiesen} fehlen";

    private void BelegungGeaendert()
    {
        TeamsZugewiesen = AllePlaetze.Count(p => !p.IstLeer);

        // Bewusst bei jedem Aufruf statt über die Zähler-Property: zwei Züge können dieselbe Anzahl ergeben
        // und trotzdem die Vollständigkeit ändern (z.B. Team vom Auswärts- auf den Heimplatz).
        OnPropertyChanged(nameof(BelegungVollstaendig));
    }

    // ── Aufbau ───────────────────────────────────────────────────────────────

    /// <summary>Leeres Raster, alle Teilnehmer sind verfügbar.</summary>
    public void LeerAufbauen(IReadOnlyList<TeilnehmerAnzeige> teilnehmer, int teamsBenoetigt)
    {
        RasterAnlegen(teamsBenoetigt);
        VerfuegbareTeilnehmerSetzen(InAnzeigeReihenfolge(teilnehmer));
        BelegungGeaendert();
    }

    /// <summary>Raster mit der Belegung einer abgeschlossenen Auslosung, verfügbar ist niemand mehr.</summary>
    public void AbgeschlossenAufbauen(IEnumerable<AuslosungsErgebnis> ergebnisse, int teamsBenoetigt)
    {
        RasterAnlegen(teamsBenoetigt);
        if (IstGruppenModus) GruppenAusErgebnissenBesetzen(ergebnisse);
        else PaarungenAusErgebnissenBesetzen(ergebnisse);
        VerfuegbareTeilnehmerSetzen([]);
        BelegungGeaendert();
    }

    private void RasterAnlegen(int teamsBenoetigt)
    {
        TeamsBenoetigt = teamsBenoetigt;
        Gruppen = IstGruppenModus
            ? VorbelegungsRaster.Gruppen(_rundenart, _gruppenAnzahl, teamsBenoetigt).Select(LeereGruppe).ToList()
            : [];
        KoPaarungen = IstGruppenModus
            ? []
            : Enumerable.Range(1, VorbelegungsRaster.PaarungsAnzahl(teamsBenoetigt)).Select(LeerePaarung).ToList();
    }

    private static ManuelleGruppe LeereGruppe(RasterGruppe gruppe) => new(
        gruppe.GruppenNr,
        gruppe.Name,
        Enumerable.Range(1, gruppe.PlatzAnzahl).Select(_ => new VorbelegungsPlatz()).ToList());

    private static ManuelleKoPaarungAnzeige LeerePaarung(int paarungsNr) =>
        new(paarungsNr, new VorbelegungsPlatz(), new VorbelegungsPlatz());

    // Platzhalter (z.B. "Platz 9 (Ligaphase)" … "Platz 24 (Ligaphase)") kommen aus den Generatoren
    // in TeilnehmerPoolHilfsmittel bereits in der richtigen (numerischen) Reihenfolge — ein
    // Alphabet-Sort nach Name reißt sie auseinander ("Platz 10" vor "Platz 9", Fund aus #297-Test).
    // Nur echte Mannschaftslisten (Erste-Runde-ohne-Vorgänger-Fall) profitieren vom Alphabet-Sort.
    private static List<TeilnehmerAnzeige> InAnzeigeReihenfolge(IReadOnlyList<TeilnehmerAnzeige> teilnehmer) =>
        teilnehmer.All(t => t.IstPlatzhalter)
            ? teilnehmer.ToList()
            : teilnehmer.OrderBy(t => t.Name).ToList();

    private void GruppenAusErgebnissenBesetzen(IEnumerable<AuslosungsErgebnis> ergebnisse)
    {
        foreach (var e in ergebnisse)
        {
            var platz = Gruppen.FirstOrDefault(g => g.GruppenNr == e.PaarungsNummer)?.Slots.FirstOrDefault(s => s.IstLeer);
            platz?.Besetzen(TeamAnzeige(e.HeimMannschaftId, e.HeimMannschaft, e.HeimPlatzhalterName));
        }
    }

    private void PaarungenAusErgebnissenBesetzen(IEnumerable<AuslosungsErgebnis> ergebnisse)
    {
        foreach (var e in ergebnisse)
        {
            var paarung = KoPaarungen.FirstOrDefault(p => p.PaarungsNr == e.PaarungsNummer);
            if (paarung is null) continue;
            paarung.HeimSlot.Besetzen(TeamAnzeige(e.HeimMannschaftId, e.HeimMannschaft, e.HeimPlatzhalterName));
            if (!e.IstFreilos && (e.AuswaertsMannschaftId.HasValue || e.AuswaertsPlatzhalterName is not null))
                paarung.AuswaertsSlot.Besetzen(
                    TeamAnzeige(e.AuswaertsMannschaftId, e.AuswaertsMannschaft, e.AuswaertsPlatzhalterName));
        }
    }

    private static TeilnehmerAnzeige TeamAnzeige(int? mannschaftId, Mannschaft? mannschaft, string? platzhalterName) =>
        mannschaftId.HasValue
            ? new TeilnehmerAnzeige(mannschaftId, mannschaft?.Name ?? "?", mannschaft?.Land?.Kurzname,
                LandId: mannschaft?.LandId, Wappen: MannschaftsIcon.Fuer(mannschaft))
            : TeilnehmerAnzeige.AusPlatzhalter(platzhalterName ?? "?");

    // ── Platzieren und Entfernen (Ziehen, Doppeltipp und Rechtsklick im Code-Behind) ──────────────

    private string? _regelmeldung;

    /// <summary>Warum der letzte Zug abgelehnt wurde — <c>null</c>, solange keine Regel verletzt ist.</summary>
    public string? Regelmeldung
    {
        get => _regelmeldung;
        private set => SetProperty(ref _regelmeldung, value);
    }

    public void RegelmeldungLoeschen() => Regelmeldung = null;

    /// <summary>Setzt einen verfügbaren Teilnehmer auf den Platz — außer eine aktive Regel verbietet es, dann
    /// steht der Grund in <see cref="Regelmeldung"/>. Ein Team, das dort schon steht, wird ersetzt und ist
    /// wieder verfügbar.</summary>
    public void Platzieren(TeilnehmerAnzeige team, VorbelegungsPlatz platz)
    {
        Regelmeldung = null;
        if (!_verfuegbareTeilnehmer.Contains(team)) return; // bereits platziert — nie doppelt besetzen (#392)

        Regelmeldung = RegelVerletzungAuf(team, platz);
        if (Regelmeldung is not null) return;

        _verfuegbareTeilnehmer.Remove(team);
        // Erst zurücklegen, dann besetzen: Besetzen überschreibt, das bisherige Team wäre sonst weder platziert
        // noch verfügbar (#454).
        if (platz.Leeren() is { } verdraengt) ZurueckZuVerfuegbar(verdraengt);
        platz.Besetzen(team);
        BelegungGeaendert();
    }

    public void Entfernen(VorbelegungsPlatz platz)
    {
        if (platz.Leeren() is { } team) ZurueckZuVerfuegbar(team);
        Regelmeldung = null;
        BelegungGeaendert();
    }

    public VorbelegungsPlatz? NaechsterFreierPlatz() => AllePlaetze.FirstOrDefault(p => p.IstLeer);

    // ── Regelprüfung ─────────────────────────────────────────────────────────

    private string? RegelVerletzungAuf(TeilnehmerAnzeige team, VorbelegungsPlatz platz)
    {
        var (mitspieler, beziehung) = MitspielerVon(platz);
        return mitspieler
            .Where(m => m != platz && !m.IstLeer) // das Team auf dem Zielplatz wird ersetzt, nicht Mitspieler (#454)
            .Select(m => RegelVerletzung(team, m.Team!, beziehung))
            .FirstOrDefault(meldung => meldung is not null);
    }

    /// <summary>Die Plätze, gegen deren Teams ein Team auf <paramref name="platz"/> geprüft wird: die seiner Gruppe
    /// bzw. der andere Platz seiner Paarung.</summary>
    private (IEnumerable<VorbelegungsPlatz> Plaetze, string Beziehung) MitspielerVon(VorbelegungsPlatz platz)
    {
        if (Gruppen.FirstOrDefault(g => g.Slots.Contains(platz)) is { } gruppe)
            return (gruppe.Slots, "in dieselbe Gruppe");
        if (KoPaarungen.FirstOrDefault(p => p.HeimSlot == platz || p.AuswaertsSlot == platz) is { } paarung)
            return ([platz == paarung.HeimSlot ? paarung.AuswaertsSlot : paarung.HeimSlot], "gepaart werden");
        return ([], "");
    }

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

    // Die Gruppen-Herkunft steckt im Platzhalternamen („1. Gruppe A“), echte Mannschaften gibt es hier nur in
    // der ersten Runde ohne Vorgänger.
    private static RegelHerkunft HerkunftVon(TeilnehmerAnzeige team) =>
        new(team.LandId, GruppenHerkunft.AusPlatzhalter(team.Name)?.Gruppe);

    // ── Ergebnis ─────────────────────────────────────────────────────────────

    /// <summary>Die Belegung als Eingabe für den Dienst. Lücken gibt es bei <see cref="BelegungVollstaendig"/>
    /// nicht mehr; eine K.-o.-Paarung ohne Heimteam ist dann ganz leer.</summary>
    public List<ManuellePaarungEingabe> AlsPaarungsEingaben() => IstGruppenModus
        ? Gruppen
            .SelectMany(g => g.Slots
                .Where(s => !s.IstLeer)
                .Select(s => ManuellePaarungEingabe.FuerGruppenplatz(g.GruppenNr, s.Team!.MannschaftId, s.Team.Name)))
            .ToList()
        : KoPaarungen
            .Where(p => !p.HeimSlot.IstLeer)
            .Select(p => ManuellePaarungEingabe.FuerPaarung(
                p.PaarungsNr,
                (p.HeimSlot.Team!.MannschaftId, p.HeimSlot.Team.Name),
                p.AuswaertsSlot.Team is { } gast ? (gast.MannschaftId, gast.Name) : null))
            .ToList();
}
