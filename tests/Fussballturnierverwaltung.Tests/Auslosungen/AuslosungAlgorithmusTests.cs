using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Application.Zufall;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>
/// Prüft das tatsächliche Auslosungs-Ergebnis für alle relevanten
/// Auslosungsart × Rundenart-Kombinationen:
/// - Gruppenphase → Gruppen-Zuordnungen (kein Auswaerts-Feld gesetzt)
/// - Alle anderen Rundenarten → Paarungen (Heim + Auswaerts gesetzt)
/// </summary>
public class AuslosungAlgorithmusTests
{
    private readonly FakeAuslosungRepository _repo;
    private readonly FakePlatzhalterKaskadeService _kaskade;
    private readonly AuslosungService _service;

    public AuslosungAlgorithmusTests()
    {
        _repo    = new FakeAuslosungRepository();
        _kaskade = new FakePlatzhalterKaskadeService();
        _service = new AuslosungService(
            _repo, new FakeVerbandsQuoteRepository(), new FakeLandesQuoteRepository(), new FakeTurnierRundeRepository(), new AuslosungsValidator(),
            _kaskade, new FakeRanglisteRepository(), new Zufallsquelle(), NullLogger<AuslosungService>.Instance);
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    // Jedes Team bekommt LandId = eigene Id → keine unbeabsichtigten GleichesLand-Treffer
    private static Mannschaft Team(int id) => new() { Id = id, Name = $"Team {id}", LandId = id };
    private static Mannschaft TeamMitLand(int id, int landId) => new() { Id = id, Name = $"Team {id}", LandId = landId };

    private static LostopfEintrag Eintrag(Mannschaft m, int? setzPos = null) =>
        new() { Id = m.Id, MannschaftId = m.Id, Mannschaft = m, SetzPosition = setzPos };

    private static LostopfEintrag PlatzhalterEintrag(int id, string name) =>
        new() { Id = id, PlatzhalterName = name };

    private static Lostopf Topf(int id, int reihenfolge, params LostopfEintrag[] eintraege) =>
        new() { Id = id, Reihenfolge = reihenfolge, Name = $"Topf {reihenfolge}",
                Eintraege = [.. eintraege] };

    private static AuslosungsRegel Regel(AuslosungsRegelArt art) =>
        new() { Regelart = art, Beschreibung = art.ToString(), IstAktiv = true };

    private Auslosung AuslosungMit(
        Rundenart rundenart, Auslosungsart auslosungsart, int? gruppenAnzahl = null,
        IEnumerable<Lostopf>? lostoepfe = null, IEnumerable<AuslosungsRegel>? regeln = null)
    {
        var a = new Auslosung
        {
            Id            = 1,
            TurnierRunde  = new TurnierRunde
            {
                Id = 1, Name = rundenart.ToString(), Rundenart = rundenart,
                GruppenAnzahl = gruppenAnzahl, RegelwerkId = 1
            },
            Auslosungsart = auslosungsart,
            Status        = AuslosungsStatus.Vorbereitung
        };
        foreach (var t in lostoepfe ?? []) a.Lostoepfe.Add(t);
        foreach (var r in regeln ?? [])    a.Regeln.Add(r);
        _repo.Speichern(a);
        return a;
    }

    private Task<IReadOnlyList<AuslosungsErgebnis>> Ausfuehren() =>
        _service.AusfuehrenAsync(_repo.Aktuelle!.Id);

    // Kurzform: ist das Ergebnis eine Gruppen-Zuordnung (kein Auswaerts-Feld)?
    private static bool IstGruppenZuordnung(AuslosungsErgebnis e) =>
        e.AuswaertsMannschaftId == null && e.AuswaertsPlatzhalterName == null && !e.IstFreilos;

    private static IEnumerable<int> AlleTeamIds(IReadOnlyList<AuslosungsErgebnis> ergebnisse) =>
        ergebnisse.SelectMany(e =>
            new[] { e.HeimMannschaftId, e.AuswaertsMannschaftId }
                .Where(id => id.HasValue)
                .Select(id => id!.Value));

    // ══════════════════════════════════════════════════════════════════════════
    // GRUPPENPHASE — alle Auslosungsarten
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Gruppenphase_Zufall_ErzeugtGruppenZuordnungen_KeineEinzelspiele()
    {
        var teams = Enumerable.Range(1, 8).Select(Team).ToList();
        var topf  = Topf(1, 1, teams.Select(t => Eintrag(t)).ToArray());
        AuslosungMit(Rundenart.Gruppenphase, Auslosungsart.Zufall, gruppenAnzahl: 2, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(8, "8 Teams → 8 Gruppen-Einträge");
        ergebnisse.Should().AllSatisfy(e => IstGruppenZuordnung(e).Should().BeTrue(),
            "Gruppenphase erzeugt Zuordnungen, keine Paarungen");
    }

    [Fact]
    public async Task Gruppenphase_Zufall_VerteiltTeamsAuf2Gruppen_Je4ProGruppe()
    {
        var teams = Enumerable.Range(1, 8).Select(Team).ToList();
        var topf  = Topf(1, 1, teams.Select(t => Eintrag(t)).ToArray());
        AuslosungMit(Rundenart.Gruppenphase, Auslosungsart.Zufall, gruppenAnzahl: 2, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();
        var gruppen    = ergebnisse.GroupBy(e => e.PaarungsNummer).ToList();

        gruppen.Should().HaveCount(2, "2 Gruppen");
        gruppen.Should().AllSatisfy(g => g.Should().HaveCount(4), "je 4 Teams pro Gruppe");
    }

    [Fact]
    public async Task Gruppenphase_Zufall_JedesTeam_NurEinmalZugewiesen()
    {
        var teams = Enumerable.Range(1, 6).Select(Team).ToList();
        var topf  = Topf(1, 1, teams.Select(t => Eintrag(t)).ToArray());
        AuslosungMit(Rundenart.Gruppenphase, Auslosungsart.Zufall, gruppenAnzahl: 3, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();
        var zugewieseneIds = ergebnisse.Select(e => e.HeimMannschaftId!.Value).ToList();

        zugewieseneIds.Should().OnlyHaveUniqueItems("kein Team darf in zwei Gruppen sein");
        zugewieseneIds.Should().BeEquivalentTo(Enumerable.Range(1, 6));
    }

    [Fact]
    public async Task Gruppenphase_NachToepfen_JeEinTeamProTopfProGruppe()
    {
        // 2 Töpfe à 4 Teams, 4 Gruppen → jede Gruppe enthält genau 1 Team aus jedem Topf
        var topf1Teams = Enumerable.Range(1, 4).Select(Team).ToList();
        var topf2Teams = Enumerable.Range(5, 4).Select(Team).ToList();
        var topf1      = Topf(1, 1, topf1Teams.Select(t => Eintrag(t)).ToArray());
        var topf2      = Topf(2, 2, topf2Teams.Select(t => Eintrag(t)).ToArray());
        AuslosungMit(Rundenart.Gruppenphase, Auslosungsart.NachToepfen,
                     gruppenAnzahl: 4, lostoepfe: [topf1, topf2]);

        var ergebnisse = await Ausfuehren();
        var gruppen    = ergebnisse.GroupBy(e => e.PaarungsNummer).ToList();

        gruppen.Should().HaveCount(4);
        gruppen.Should().AllSatisfy(g => g.Should().HaveCount(2),
            "2 Töpfe × 1 Eintrag pro Topf = 2 Teams pro Gruppe");

        var topf1Ids = topf1Teams.Select(t => t.Id).ToHashSet();
        var topf2Ids = topf2Teams.Select(t => t.Id).ToHashSet();
        gruppen.Should().AllSatisfy(g =>
        {
            var ids = g.Select(e => e.HeimMannschaftId!.Value).ToList();
            ids.Count(id => topf1Ids.Contains(id)).Should().Be(1, "1 Team aus Topf 1 pro Gruppe");
            ids.Count(id => topf2Ids.Contains(id)).Should().Be(1, "1 Team aus Topf 2 pro Gruppe");
        });
    }

    [Fact]
    public async Task Gruppenphase_NachSetzliste_GesetztesTeamJeEinmalProGruppe()
    {
        // 4 gesetzte + 4 ungesetzte → 4 Gruppen à 2, jede mit genau 1 Gesetztem
        var gesetzte   = Enumerable.Range(1, 4).Select(i => Eintrag(Team(i), setzPos: i)).ToArray();
        var ungesetzte = Enumerable.Range(5, 4).Select(i => Eintrag(Team(i))).ToArray();
        var topf       = Topf(1, 1, [.. gesetzte, .. ungesetzte]);
        AuslosungMit(Rundenart.Gruppenphase, Auslosungsart.NachSetzliste,
                     gruppenAnzahl: 4, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();
        var gruppen    = ergebnisse.GroupBy(e => e.PaarungsNummer).ToList();

        gruppen.Should().HaveCount(4);
        var gesetzteIds = new HashSet<int> { 1, 2, 3, 4 };
        gruppen.Should().AllSatisfy(g =>
            g.Count(e => gesetzteIds.Contains(e.HeimMannschaftId!.Value))
             .Should().Be(1, "jede Gruppe enthält genau einen Gesetzten"));
    }

    [Fact]
    public async Task Gruppenphase_NachSetzliste_UngesetzteToepfeJeEinmalProGruppe()
    {
        // Regressionstest (Issue #5-Diskussion): ungesetzte Töpfe wurden bisher topf-unabhängig
        // zusammengeworfen (GruppenBefuellen über alle ungesetzten Töpfe hinweg), wodurch eine
        // Gruppe z.B. 2 Teams aus Topf 2 bekommen konnte, während eine andere Gruppe noch leer
        // ausging. Jeder ungesetzte Topf muss weiterhin exakt 1 Team pro Gruppe beisteuern.
        var gesetzte = Enumerable.Range(1, 4).Select(i => Eintrag(Team(i), setzPos: i)).ToArray();
        var topf1    = Topf(1, 1, gesetzte);
        var topf2    = Topf(2, 2, Enumerable.Range(5, 4).Select(i => Eintrag(Team(i))).ToArray());
        var topf3    = Topf(3, 3, Enumerable.Range(9, 4).Select(i => Eintrag(Team(i))).ToArray());
        AuslosungMit(Rundenart.Gruppenphase, Auslosungsart.NachSetzliste,
                     gruppenAnzahl: 4, lostoepfe: [topf1, topf2, topf3]);

        var ergebnisse = await Ausfuehren();
        var gruppen    = ergebnisse.GroupBy(e => e.PaarungsNummer).ToList();

        gruppen.Should().HaveCount(4);
        var topf2Ids = new HashSet<int> { 5, 6, 7, 8 };
        var topf3Ids = new HashSet<int> { 9, 10, 11, 12 };
        gruppen.Should().AllSatisfy(g =>
        {
            g.Should().HaveCount(3, "1 Gesetzter + je 1 Team aus Topf 2 und Topf 3");
            g.Count(e => topf2Ids.Contains(e.HeimMannschaftId!.Value)).Should().Be(1,
                "jede Gruppe enthält genau ein Team aus Topf 2");
            g.Count(e => topf3Ids.Contains(e.HeimMannschaftId!.Value)).Should().Be(1,
                "jede Gruppe enthält genau ein Team aus Topf 3");
        });
    }

    [Fact]
    public async Task Gruppenphase_Benutzerdefiniert_VerhaltWieZufall()
    {
        // Benutzerdefiniert fällt auf Zufall-Algorithmus zurück
        var teams = Enumerable.Range(1, 4).Select(Team).ToList();
        var topf  = Topf(1, 1, teams.Select(t => Eintrag(t)).ToArray());
        AuslosungMit(Rundenart.Gruppenphase, Auslosungsart.Benutzerdefiniert,
                     gruppenAnzahl: 2, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(4);
        ergebnisse.Should().AllSatisfy(e => IstGruppenZuordnung(e).Should().BeTrue());
    }

    [Fact]
    public async Task Gruppenphase_KeinGleichesLand_TeamsAusGleichesLandNichtInSelberGruppe()
    {
        // 4 Teams: je 2 aus Land 10, je 2 aus Land 20 → Constraint: getrennte Gruppen
        var teams = new[]
        {
            TeamMitLand(1, 10), TeamMitLand(2, 10),
            TeamMitLand(3, 20), TeamMitLand(4, 20)
        };
        var topf = Topf(1, 1, teams.Select(t => Eintrag(t)).ToArray());
        AuslosungMit(Rundenart.Gruppenphase, Auslosungsart.Zufall, gruppenAnzahl: 2, lostoepfe: [topf],
                     regeln: [Regel(AuslosungsRegelArt.KeinGleichesLand)]);

        var ergebnisse = await Ausfuehren();
        var gruppen    = ergebnisse.GroupBy(e => e.PaarungsNummer).ToList();

        gruppen.Should().HaveCount(2);
        gruppen.Should().AllSatisfy(g =>
        {
            // Jede Gruppe sollte Teams aus beiden Ländern enthalten (= getrennt)
            var landIds = g
                .Select(e => teams.First(t => t.Id == e.HeimMannschaftId!.Value).LandId)
                .Distinct()
                .ToList();
            landIds.Should().HaveCount(2, "jede Gruppe hat Teams aus verschiedenen Ländern");
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // KO-RUNDE — alle Auslosungsarten
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task KoRunde_Zufall_ErzeugtPaarungen_NichtGruppenZuordnungen()
    {
        var teams = Enumerable.Range(1, 8).Select(Team).ToList();
        var topf  = Topf(1, 1, teams.Select(t => Eintrag(t)).ToArray());
        AuslosungMit(Rundenart.KoRunde, Auslosungsart.Zufall, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(4, "8 Teams → 4 Paarungen");
        ergebnisse.Should().AllSatisfy(e =>
        {
            e.HeimMannschaftId.Should().NotBeNull("Heim-Team muss gesetzt sein");
            e.AuswaertsMannschaftId.Should().NotBeNull("Auswärts-Team muss gesetzt sein");
        });
    }

    [Fact]
    public async Task KoRunde_Zufall_JedesTeam_GenauEinmal()
    {
        var teams = Enumerable.Range(1, 8).Select(Team).ToList();
        var topf  = Topf(1, 1, teams.Select(t => Eintrag(t)).ToArray());
        AuslosungMit(Rundenart.KoRunde, Auslosungsart.Zufall, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();

        AlleTeamIds(ergebnisse).Should().OnlyHaveUniqueItems();
        AlleTeamIds(ergebnisse).Should().BeEquivalentTo(Enumerable.Range(1, 8));
    }

    [Fact]
    public async Task KoRunde_NachToepfen_PaartJeweilsTeamsAusVerschiedenenToepfen()
    {
        var topf1 = Topf(1, 1, Enumerable.Range(1, 4).Select(i => Eintrag(Team(i))).ToArray());
        var topf2 = Topf(2, 2, Enumerable.Range(5, 4).Select(i => Eintrag(Team(i))).ToArray());
        AuslosungMit(Rundenart.KoRunde, Auslosungsart.NachToepfen, lostoepfe: [topf1, topf2]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(4);
        var topf1Ids = new HashSet<int> { 1, 2, 3, 4 };
        var topf2Ids = new HashSet<int> { 5, 6, 7, 8 };
        ergebnisse.Should().AllSatisfy(e =>
        {
            bool heimT1ausT1_aausT2 = topf1Ids.Contains(e.HeimMannschaftId!.Value)
                                   && topf2Ids.Contains(e.AuswaertsMannschaftId!.Value);
            bool heimT1ausT2_aausT1 = topf2Ids.Contains(e.HeimMannschaftId.Value)
                                   && topf1Ids.Contains(e.AuswaertsMannschaftId!.Value);
            (heimT1ausT1_aausT2 || heimT1ausT2_aausT1)
                .Should().BeTrue("jede Paarung verbindet je ein Team aus Topf 1 und Topf 2");
        });
    }

    // DFB-Pokal-Prinzip (#495): Topf 1 gegen Topf 2, bis der kleinere leer ist, der Überhang untereinander.
    [Fact]
    public async Task KoRunde_NachToepfen_UngleicheToepfe_UeberhangWirdUntereinanderGelost()
    {
        var topf1 = Topf(1, 1, Enumerable.Range(1, 6).Select(i => Eintrag(Team(i))).ToArray());
        var topf2 = Topf(2, 2, Enumerable.Range(7, 2).Select(i => Eintrag(Team(i))).ToArray());
        AuslosungMit(Rundenart.KoRunde, Auslosungsart.NachToepfen, lostoepfe: [topf1, topf2]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(4);
        AlleTeamIds(ergebnisse).Should().BeEquivalentTo(Enumerable.Range(1, 8));
        bool AusTopf2(int? id) => id >= 7;
        ergebnisse.Count(e => AusTopf2(e.HeimMannschaftId) != AusTopf2(e.AuswaertsMannschaftId))
            .Should().Be(2, "beide Topf-2-Teams spielen gegen Topf 1");
        ergebnisse.Count(e => AusTopf2(e.HeimMannschaftId) && AusTopf2(e.AuswaertsMannschaftId)).Should().Be(0);
        ergebnisse.Take(2).Should().AllSatisfy(e =>
            (AusTopf2(e.HeimMannschaftId) || AusTopf2(e.AuswaertsMannschaftId)).Should().BeTrue(
                "die topfübergreifenden Paarungen stehen vor dem Überhang"));
    }

    [Fact]
    public async Task KoRunde_NachToepfen_UngeradeAnzahlMitFreilos_FreilosAusDemUeberhang()
    {
        var topf1 = Topf(1, 1, Enumerable.Range(1, 5).Select(i => Eintrag(Team(i))).ToArray());
        var topf2 = Topf(2, 2, Enumerable.Range(6, 2).Select(i => Eintrag(Team(i))).ToArray());
        AuslosungMit(Rundenart.KoRunde, Auslosungsart.NachToepfen, lostoepfe: [topf1, topf2],
            regeln: [Regel(AuslosungsRegelArt.FreilosMoeglich)]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(4);
        ergebnisse.Should().ContainSingle(e => e.IstFreilos)
            .Which.HeimMannschaftId.Should().BeLessThanOrEqualTo(5, "nur der größere Topf hat einen Überhang");
        AlleTeamIds(ergebnisse).Should().BeEquivalentTo(Enumerable.Range(1, 7));
    }

    [Fact]
    public async Task KoRunde_NachToepfen_UngeradeAnzahlOhneFreilos_WirftMitHinweis()
    {
        var topf1 = Topf(1, 1, Enumerable.Range(1, 5).Select(i => Eintrag(Team(i))).ToArray());
        var topf2 = Topf(2, 2, Enumerable.Range(6, 2).Select(i => Eintrag(Team(i))).ToArray());
        AuslosungMit(Rundenart.KoRunde, Auslosungsart.NachToepfen, lostoepfe: [topf1, topf2]);

        var aktion = Ausfuehren;

        await aktion.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Freilos möglich*");
    }

    // Kein gleiches Land in beiden Phasen gemeinsam (#495): Bleiben 1+2 (beide Land 1) im Überhang, ginge er nicht
    // auf — die Suche muss Topf 2 so verteilen, dass je ein Team aus Land 1 und Land 2 übrig bleibt.
    [Fact]
    public async Task KoRunde_NachToepfen_KeinGleichesLand_UeberhangBleibtLoesbar()
    {
        for (int i = 0; i < 30; i++)
        {
            var topf1 = Topf(1, 1, Eintrag(TeamMitLand(1, 1)), Eintrag(TeamMitLand(2, 1)),
                                   Eintrag(TeamMitLand(3, 2)), Eintrag(TeamMitLand(4, 2)));
            var topf2 = Topf(2, 2, Eintrag(TeamMitLand(5, 3)), Eintrag(TeamMitLand(6, 3)));
            AuslosungMit(Rundenart.KoRunde, Auslosungsart.NachToepfen, lostoepfe: [topf1, topf2],
                regeln: [Regel(AuslosungsRegelArt.KeinGleichesLand)]);

            var ergebnisse = await Ausfuehren();

            ergebnisse.Should().HaveCount(3);
            ergebnisse.Should().NotContain(e =>
                (e.HeimMannschaftId <= 2) == (e.AuswaertsMannschaftId <= 2) && e.AuswaertsMannschaftId <= 4);
        }
    }

    [Fact]
    public async Task KoRunde_NachSetzliste_GesetzteVsUngesetzte()
    {
        var gesetzte   = Enumerable.Range(1, 4).Select(i => Eintrag(Team(i), setzPos: i)).ToArray();
        var ungesetzte = Enumerable.Range(5, 4).Select(i => Eintrag(Team(i))).ToArray();
        var topf       = Topf(1, 1, [.. gesetzte, .. ungesetzte]);
        AuslosungMit(Rundenart.KoRunde, Auslosungsart.NachSetzliste, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(4);
        var gesetzteIds   = new HashSet<int> { 1, 2, 3, 4 };
        var ungesetzteIds = new HashSet<int> { 5, 6, 7, 8 };
        ergebnisse.Should().AllSatisfy(e =>
        {
            gesetzteIds.Contains(e.HeimMannschaftId!.Value).Should().BeTrue("Heim = Gesetzter");
            ungesetzteIds.Contains(e.AuswaertsMannschaftId!.Value).Should().BeTrue("Auswaerts = Ungesetzter");
        });
    }

    // Mehr Ungesetzte als Gesetzte (#495): die übrigen Ungesetzten werden untereinander gelost statt herauszufallen.
    [Fact]
    public async Task KoRunde_NachSetzliste_UngleicheSeiten_UeberhangWirdUntereinanderGelost()
    {
        var gesetzte   = Enumerable.Range(1, 2).Select(i => Eintrag(Team(i), setzPos: i)).ToArray();
        var ungesetzte = Enumerable.Range(3, 6).Select(i => Eintrag(Team(i))).ToArray();
        AuslosungMit(Rundenart.KoRunde, Auslosungsart.NachSetzliste, lostoepfe: [Topf(1, 1, [.. gesetzte, .. ungesetzte])]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(4);
        AlleTeamIds(ergebnisse).Should().BeEquivalentTo(Enumerable.Range(1, 8));
        ergebnisse.Take(2).Select(e => e.HeimMannschaftId).Should().Equal([1, 2], "die Gesetzten in Setzreihenfolge vorn");
        ergebnisse.Skip(2).Should().AllSatisfy(e =>
            new[] { e.HeimMannschaftId, e.AuswaertsMannschaftId }.Should().AllSatisfy(id => id.Should().BeGreaterThan(2)));
    }

    [Fact]
    public async Task KoRunde_GruppensiegerGegenGruppenzweite_ErzeugtPaarungen()
    {
        var topf1 = Topf(1, 1, Enumerable.Range(1, 4).Select(i => Eintrag(Team(i))).ToArray());
        var topf2 = Topf(2, 2, Enumerable.Range(5, 4).Select(i => Eintrag(Team(i))).ToArray());
        AuslosungMit(Rundenart.KoRunde, Auslosungsart.GruppensiegerGegenGruppenzweite,
                     lostoepfe: [topf1, topf2]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(4);
        ergebnisse.Should().AllSatisfy(e =>
        {
            e.HeimMannschaftId.Should().NotBeNull();
            e.AuswaertsMannschaftId.Should().NotBeNull();
        });
        AlleTeamIds(ergebnisse).Should().BeEquivalentTo(Enumerable.Range(1, 8));
    }

    [Fact]
    public async Task KoRunde_Benutzerdefiniert_ErzeugtPaarungen()
    {
        var teams = Enumerable.Range(1, 4).Select(Team).ToList();
        var topf  = Topf(1, 1, teams.Select(t => Eintrag(t)).ToArray());
        AuslosungMit(Rundenart.KoRunde, Auslosungsart.Benutzerdefiniert, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(2);
        ergebnisse.Should().AllSatisfy(e =>
        {
            e.HeimMannschaftId.Should().NotBeNull();
            e.AuswaertsMannschaftId.Should().NotBeNull();
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // FINALE & SPIEL UM PLATZ 3
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Finale_Zufall_ZweiTeams_EinePaarung()
    {
        var topf = Topf(1, 1, Eintrag(Team(1)), Eintrag(Team(2)));
        AuslosungMit(Rundenart.Finale, Auslosungsart.Zufall, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(1);
        ergebnisse[0].HeimMannschaftId.Should().NotBeNull();
        ergebnisse[0].AuswaertsMannschaftId.Should().NotBeNull();
    }

    [Fact]
    public async Task SpielUmPlatzDrei_Zufall_ZweiTeams_EinePaarung()
    {
        var topf = Topf(1, 1, Eintrag(Team(1)), Eintrag(Team(2)));
        AuslosungMit(Rundenart.SpielUmPlatzDrei, Auslosungsart.Zufall, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(1);
    }

    [Fact]
    public async Task Finale_GruppensiegerGegenGruppenzweite_EinePaarung()
    {
        var topf1 = Topf(1, 1, Eintrag(Team(1)));
        var topf2 = Topf(2, 2, Eintrag(Team(2)));
        AuslosungMit(Rundenart.Finale, Auslosungsart.GruppensiegerGegenGruppenzweite,
                     lostoepfe: [topf1, topf2]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(1);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // LIGA
    // ══════════════════════════════════════════════════════════════════════════

    // Liga wird wie eine Gruppenphase mit genau einer Gruppe ausgelost (Bug #18, Commit a88fe19):
    // die Auslosung liefert Gruppen-Zuordnungen, keine Heim/Auswaerts-Paarungen. Die eigentlichen
    // Spielpaarungen entstehen erst bei der Spielplan-Generierung (RoundRobinGenerator).

    [Fact]
    public async Task Liga_Zufall_ErzeugtGruppenZuordnung_AlleTeamsInEinerGruppe()
    {
        var teams = Enumerable.Range(1, 6).Select(Team).ToList();
        var topf  = Topf(1, 1, teams.Select(t => Eintrag(t)).ToArray());
        AuslosungMit(Rundenart.Liga, Auslosungsart.Zufall, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(6, "6 Teams → 6 Gruppen-Einträge in der einen Liga-Gruppe");
        ergebnisse.Should().AllSatisfy(e => IstGruppenZuordnung(e).Should().BeTrue());
        ergebnisse.GroupBy(e => e.PaarungsNummer).Should().HaveCount(1);
        AlleTeamIds(ergebnisse).Should().BeEquivalentTo(Enumerable.Range(1, 6));
    }

    [Fact]
    public async Task Liga_TopfZuordnung_ErzeugtGruppenZuordnung_AlleTeamsInEinerGruppe()
    {
        var topf1 = Topf(1, 1, Eintrag(Team(1)), Eintrag(Team(2)));
        var topf2 = Topf(2, 2, Eintrag(Team(3)), Eintrag(Team(4)));
        AuslosungMit(Rundenart.Liga, Auslosungsart.TopfZuordnung, lostoepfe: [topf1, topf2]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(4);
        ergebnisse.Should().AllSatisfy(e => IstGruppenZuordnung(e).Should().BeTrue());
        ergebnisse.GroupBy(e => e.PaarungsNummer).Should().HaveCount(1);
        AlleTeamIds(ergebnisse).Should().BeEquivalentTo(Enumerable.Range(1, 4));
    }

    [Fact]
    public async Task Liga_NachToepfen_UebernimmtAlleTeamsAusAllenToepfen()
    {
        // Regressionstest: GruppenphaseNachToepfenAusloesen nahm bei gruppenAnzahl=1 (Liga)
        // bisher nur ein Team pro Topf und verwarf den Rest stillschweigend.
        var topf1 = Topf(1, 1, Enumerable.Range(1, 4).Select(i => Eintrag(Team(i))).ToArray());
        var topf2 = Topf(2, 2, Enumerable.Range(5, 4).Select(i => Eintrag(Team(i))).ToArray());
        AuslosungMit(Rundenart.Liga, Auslosungsart.NachToepfen, lostoepfe: [topf1, topf2]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(8, "alle Teams aus beiden Töpfen müssen in der einen Liga-Gruppe landen");
        ergebnisse.Should().AllSatisfy(e => IstGruppenZuordnung(e).Should().BeTrue());
        ergebnisse.GroupBy(e => e.PaarungsNummer).Should().HaveCount(1);
        AlleTeamIds(ergebnisse).Should().BeEquivalentTo(Enumerable.Range(1, 8));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // SCHWEIZER SYSTEM & SONDERRUNDE
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SchweizerSystem_Zufall_ErzeugtPaarungen()
    {
        var teams = Enumerable.Range(1, 8).Select(Team).ToList();
        var topf  = Topf(1, 1, teams.Select(t => Eintrag(t)).ToArray());
        AuslosungMit(Rundenart.SchweizerSystem, Auslosungsart.Zufall, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(4);
        ergebnisse.Should().AllSatisfy(e =>
        {
            e.HeimMannschaftId.Should().NotBeNull();
            e.AuswaertsMannschaftId.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task SchweizerSystem_NachSetzliste_ErzeugtPaarungen()
    {
        var gesetzte   = Enumerable.Range(1, 4).Select(i => Eintrag(Team(i), setzPos: i)).ToArray();
        var ungesetzte = Enumerable.Range(5, 4).Select(i => Eintrag(Team(i))).ToArray();
        var topf       = Topf(1, 1, [.. gesetzte, .. ungesetzte]);
        AuslosungMit(Rundenart.SchweizerSystem, Auslosungsart.NachSetzliste, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(4);
    }

    [Fact]
    public async Task Sonderrunde_Zufall_ErzeugtPaarungen()
    {
        var teams = Enumerable.Range(1, 4).Select(Team).ToList();
        var topf  = Topf(1, 1, teams.Select(t => Eintrag(t)).ToArray());
        AuslosungMit(Rundenart.Sonderrunde, Auslosungsart.Zufall, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(2);
        ergebnisse.Should().AllSatisfy(e =>
        {
            e.HeimMannschaftId.Should().NotBeNull();
            e.AuswaertsMannschaftId.Should().NotBeNull();
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // SPEZIALFÄLLE
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task KoRunde_Zufall_FreilosMoeglich_UngeradeAnzahl_ErzeugtFreilos()
    {
        var teams = Enumerable.Range(1, 7).Select(Team).ToList();
        var topf  = Topf(1, 1, teams.Select(t => Eintrag(t)).ToArray());
        AuslosungMit(Rundenart.KoRunde, Auslosungsart.Zufall, lostoepfe: [topf],
                     regeln: [Regel(AuslosungsRegelArt.FreilosMoeglich)]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(4, "3 Paarungen + 1 Freilos");
        ergebnisse.Count(e => e.IstFreilos).Should().Be(1);
        ergebnisse.Count(e => !e.IstFreilos).Should().Be(3);
    }

    [Fact]
    public async Task KoRunde_Zufall_GeradeAnzahl_KeinFreilos_AuchMitRegel()
    {
        var teams = Enumerable.Range(1, 8).Select(Team).ToList();
        var topf  = Topf(1, 1, teams.Select(t => Eintrag(t)).ToArray());
        AuslosungMit(Rundenart.KoRunde, Auslosungsart.Zufall, lostoepfe: [topf],
                     regeln: [Regel(AuslosungsRegelArt.FreilosMoeglich)]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(4);
        ergebnisse.Should().AllSatisfy(e => e.IstFreilos.Should().BeFalse());
    }

    [Fact]
    public async Task KoRunde_Zufall_Platzhalter_WerdenWieTeamsGepaart()
    {
        var eintraege = Enumerable.Range(1, 4)
            .Select(i => PlatzhalterEintrag(i, $"Sieger Spiel {i}"))
            .ToArray();
        var topf = Topf(1, 1, eintraege);
        AuslosungMit(Rundenart.KoRunde, Auslosungsart.Zufall, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(2);
        ergebnisse.Should().AllSatisfy(e =>
        {
            e.HeimPlatzhalterName.Should().NotBeNullOrEmpty();
            e.AuswaertsPlatzhalterName.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public async Task Gruppenphase_Zufall_Platzhalter_WerdenGruppenZugewiesen()
    {
        var eintraege = Enumerable.Range(1, 4)
            .Select(i => PlatzhalterEintrag(i, $"1. Gruppe {(char)('A' + i - 1)}"))
            .ToArray();
        var topf = Topf(1, 1, eintraege);
        AuslosungMit(Rundenart.Gruppenphase, Auslosungsart.Zufall, gruppenAnzahl: 2, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(4);
        ergebnisse.Should().AllSatisfy(e =>
        {
            e.HeimPlatzhalterName.Should().NotBeNullOrEmpty();
            IstGruppenZuordnung(e).Should().BeTrue();
        });
        ergebnisse.GroupBy(e => e.PaarungsNummer).Should().HaveCount(2);
    }

    [Fact]
    public async Task KoRunde_KeinGleichesLand_PaartTeamsAusVerschiedenenLaendern()
    {
        // 4 Teams: je 2 aus Land 10 und 20 → Constraint verhindert Paare aus gleichem Land
        var teams = new[]
        {
            TeamMitLand(1, 10), TeamMitLand(2, 10),
            TeamMitLand(3, 20), TeamMitLand(4, 20)
        };
        var topf = Topf(1, 1, teams.Select(t => Eintrag(t)).ToArray());
        AuslosungMit(Rundenart.KoRunde, Auslosungsart.Zufall, lostoepfe: [topf],
                     regeln: [Regel(AuslosungsRegelArt.KeinGleichesLand)]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(2);
        ergebnisse.Should().AllSatisfy(e =>
        {
            var heimLand   = teams.First(t => t.Id == e.HeimMannschaftId!.Value).LandId;
            var auswaertsLand = teams.First(t => t.Id == e.AuswaertsMannschaftId!.Value).LandId;
            heimLand.Should().NotBe(auswaertsLand, "gleiche Länder dürfen nicht gepaart werden");
        });
    }

    [Fact]
    public async Task KoRunde_Zufall_HeimrechtNachLos_JedesPaarungHatHeimUndAuswaerts()
    {
        var teams = Enumerable.Range(1, 4).Select(Team).ToList();
        var topf  = Topf(1, 1, teams.Select(t => Eintrag(t)).ToArray());
        AuslosungMit(Rundenart.KoRunde, Auslosungsart.Zufall, lostoepfe: [topf],
                     regeln: [Regel(AuslosungsRegelArt.HeimrechtNachLos)]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(2);
        ergebnisse.Should().AllSatisfy(e =>
        {
            e.HeimMannschaftId.Should().NotBeNull();
            e.AuswaertsMannschaftId.Should().NotBeNull();
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // LIGA — fehlende Auslosungsarten
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Liga_NachSetzliste_UebernimmtGesetzteUndUngesetzteTeams()
    {
        // Regressionstest: GruppenphaseNachSetzlisteAusloesen nahm bei gruppenAnzahl=1 (Liga)
        // bisher per Take(gruppenAnzahl) nur ein gesetztes Team und verwarf die übrigen.
        var gesetzte   = Enumerable.Range(1, 3).Select(i => Eintrag(Team(i), setzPos: i)).ToArray();
        var ungesetzte = Enumerable.Range(4, 3).Select(i => Eintrag(Team(i))).ToArray();
        var topf       = Topf(1, 1, [.. gesetzte, .. ungesetzte]);
        AuslosungMit(Rundenart.Liga, Auslosungsart.NachSetzliste, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(6, "alle gesetzten und ungesetzten Teams müssen in der einen Liga-Gruppe landen");
        ergebnisse.Should().AllSatisfy(e => IstGruppenZuordnung(e).Should().BeTrue());
        ergebnisse.GroupBy(e => e.PaarungsNummer).Should().HaveCount(1);
        AlleTeamIds(ergebnisse).Should().BeEquivalentTo(Enumerable.Range(1, 6));
    }

    [Fact]
    public async Task Liga_Benutzerdefiniert_ErzeugtGruppenZuordnung_AlleTeamsInEinerGruppe()
    {
        var teams = Enumerable.Range(1, 6).Select(Team).ToList();
        var topf  = Topf(1, 1, teams.Select(t => Eintrag(t)).ToArray());
        AuslosungMit(Rundenart.Liga, Auslosungsart.Benutzerdefiniert, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(6);
        ergebnisse.Should().AllSatisfy(e => IstGruppenZuordnung(e).Should().BeTrue());
        ergebnisse.GroupBy(e => e.PaarungsNummer).Should().HaveCount(1);
        AlleTeamIds(ergebnisse).Should().BeEquivalentTo(Enumerable.Range(1, 6));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TOPFLIGA — nur Topf-Zuordnung (Issue #347; andere Arten sperrt AuslosungsKompatibilitaet)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TopfLiga_TopfZuordnung_ErzeugtTopfUebergreifendePaarungen()
    {
        // Topf-Zuordnung braucht mindestens 3 Teams je Topf (Issue #347): 2 Töpfe à 3 → jedes Team 4 Spiele
        var topf1 = Topf(1, 1, Eintrag(Team(1)), Eintrag(Team(2)), Eintrag(Team(3)));
        var topf2 = Topf(2, 2, Eintrag(Team(4)), Eintrag(Team(5)), Eintrag(Team(6)));
        AuslosungMit(Rundenart.TopfLiga, Auslosungsart.TopfZuordnung, lostoepfe: [topf1, topf2]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(12);
        ergebnisse.Select(e => (Math.Min(e.HeimMannschaftId!.Value, e.AuswaertsMannschaftId!.Value),
                                Math.Max(e.HeimMannschaftId.Value, e.AuswaertsMannschaftId.Value)))
            .Should().OnlyHaveUniqueItems("kein Gegner zweimal");
    }

    // ── UCL-Ligaphase 4 × 9 (Issue #347) ─────────────────────────────────────

    private static List<Lostopf> UclToepfe(Func<int, Mannschaft> team) =>
        [.. Enumerable.Range(0, 4).Select(t =>
            Topf(t + 1, t + 1, Enumerable.Range(t * 9 + 1, 9).Select(i => Eintrag(team(i))).ToArray()))];

    private static int TopfVon(int teamId) => (teamId - 1) / 9;

    [Fact]
    public async Task TopfLiga_TopfZuordnung_UclLigaphase_JedesTeamAchtGegnerZweiJeTopfVierHeim()
    {
        AuslosungMit(Rundenart.TopfLiga, Auslosungsart.TopfZuordnung, lostoepfe: UclToepfe(Team));

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(144);
        foreach (int team in Enumerable.Range(1, 36))
        {
            var heim  = ergebnisse.Where(e => e.HeimMannschaftId == team).Select(e => e.AuswaertsMannschaftId!.Value).ToList();
            var gast  = ergebnisse.Where(e => e.AuswaertsMannschaftId == team).Select(e => e.HeimMannschaftId!.Value).ToList();
            var alle  = heim.Concat(gast).ToList();

            heim.Should().HaveCount(4, $"Team {team}: 4 Heimspiele");
            gast.Should().HaveCount(4, $"Team {team}: 4 Auswärtsspiele");
            alle.Should().OnlyHaveUniqueItems($"Team {team}: 8 verschiedene Gegner").And.NotContain(team);
            alle.GroupBy(TopfVon).Should().HaveCount(4).And.OnlyContain(g => g.Count() == 2, $"Team {team}: 2 Gegner je Topf");
            heim.GroupBy(TopfVon).Should().HaveCount(4, $"Team {team}: je Topf ein Heimgegner");
        }
    }

    [Fact]
    public async Task TopfLiga_TopfZuordnung_KeinGleichesLand_HoechstensZweiGegnerJeLand()
    {
        // 16 der 36 Teams aus 4 Ländern (je 4, über alle Töpfe verteilt), der Rest mit eigenem Land —
        // ohne Obergrenze bekäme ein Team leicht 3 Gegner aus demselben Land.
        static int LandVon(int id) => id % 9 < 4 ? 100 + id % 9 : id;
        for (int durchlauf = 0; durchlauf < 10; durchlauf++)
        {
            AuslosungMit(Rundenart.TopfLiga, Auslosungsart.TopfZuordnung,
                lostoepfe: UclToepfe(i => TeamMitLand(i, LandVon(i))),
                regeln: [Regel(AuslosungsRegelArt.KeinGleichesLand)]);
            var ergebnisse = await Ausfuehren();

            foreach (int team in Enumerable.Range(1, 36))
            {
                var gegnerLaender = ergebnisse
                    .Where(e => e.HeimMannschaftId == team || e.AuswaertsMannschaftId == team)
                    .Select(e => LandVon(e.HeimMannschaftId == team ? e.AuswaertsMannschaftId!.Value : e.HeimMannschaftId!.Value))
                    .ToList();
                gegnerLaender.Should().NotContain(LandVon(team), $"Team {team}: kein Gegner aus dem eigenen Land");
                gegnerLaender.GroupBy(l => l).Should().OnlyContain(g => g.Count() <= 2,
                    $"Team {team}: höchstens 2 Gegner aus demselben Land");
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // SCHWEIZER SYSTEM — fehlende Auslosungsarten
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SchweizerSystem_NachToepfen_PaartTeamsAusVerschiedenenToepfen()
    {
        var topf1 = Topf(1, 1, Enumerable.Range(1, 4).Select(i => Eintrag(Team(i))).ToArray());
        var topf2 = Topf(2, 2, Enumerable.Range(5, 4).Select(i => Eintrag(Team(i))).ToArray());
        AuslosungMit(Rundenart.SchweizerSystem, Auslosungsart.NachToepfen, lostoepfe: [topf1, topf2]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(4);
        ergebnisse.Should().AllSatisfy(e =>
        {
            e.HeimMannschaftId.Should().NotBeNull();
            e.AuswaertsMannschaftId.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task SchweizerSystem_TopfZuordnung_ErzeugtPaarungen()
    {
        // Topf-Zuordnung braucht mindestens 3 Teams je Topf (Issue #347): 2 Töpfe à 3 → jedes Team 4 Spiele
        var topf1 = Topf(1, 1, Eintrag(Team(1)), Eintrag(Team(2)), Eintrag(Team(3)));
        var topf2 = Topf(2, 2, Eintrag(Team(4)), Eintrag(Team(5)), Eintrag(Team(6)));
        AuslosungMit(Rundenart.SchweizerSystem, Auslosungsart.TopfZuordnung, lostoepfe: [topf1, topf2]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(12);
        ergebnisse.Select(e => (Math.Min(e.HeimMannschaftId!.Value, e.AuswaertsMannschaftId!.Value),
                                Math.Max(e.HeimMannschaftId.Value, e.AuswaertsMannschaftId.Value)))
            .Should().OnlyHaveUniqueItems("kein Gegner zweimal");
    }

    [Fact]
    public async Task SchweizerSystem_Benutzerdefiniert_ErzeugtPaarungen()
    {
        var teams = Enumerable.Range(1, 8).Select(Team).ToList();
        var topf  = Topf(1, 1, teams.Select(t => Eintrag(t)).ToArray());
        AuslosungMit(Rundenart.SchweizerSystem, Auslosungsart.Benutzerdefiniert, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(4);
        ergebnisse.Should().AllSatisfy(e =>
        {
            e.HeimMannschaftId.Should().NotBeNull();
            e.AuswaertsMannschaftId.Should().NotBeNull();
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // SONDERRUNDE — fehlende Auslosungsarten
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Sonderrunde_NachSetzliste_GesetzteAlsHeim()
    {
        var gesetzte   = new[] { Eintrag(Team(1), setzPos: 1), Eintrag(Team(2), setzPos: 2) };
        var ungesetzte = new[] { Eintrag(Team(3)), Eintrag(Team(4)) };
        var topf       = Topf(1, 1, [.. gesetzte, .. ungesetzte]);
        AuslosungMit(Rundenart.Sonderrunde, Auslosungsart.NachSetzliste, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(2);
        var gesetzteIds = new HashSet<int> { 1, 2 };
        ergebnisse.Should().AllSatisfy(e =>
            gesetzteIds.Contains(e.HeimMannschaftId!.Value).Should().BeTrue("Heim = Gesetzter"));
    }

    [Fact]
    public async Task Sonderrunde_NachToepfen_ErzeugtPaarungen()
    {
        var topf1 = Topf(1, 1, Eintrag(Team(1)), Eintrag(Team(2)));
        var topf2 = Topf(2, 2, Eintrag(Team(3)), Eintrag(Team(4)));
        AuslosungMit(Rundenart.Sonderrunde, Auslosungsart.NachToepfen, lostoepfe: [topf1, topf2]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(2);
        var topf1Ids = new HashSet<int> { 1, 2 };
        var topf2Ids = new HashSet<int> { 3, 4 };
        ergebnisse.Should().AllSatisfy(e =>
        {
            bool kreuzPaarung = (topf1Ids.Contains(e.HeimMannschaftId!.Value) && topf2Ids.Contains(e.AuswaertsMannschaftId!.Value))
                             || (topf2Ids.Contains(e.HeimMannschaftId.Value) && topf1Ids.Contains(e.AuswaertsMannschaftId!.Value));
            kreuzPaarung.Should().BeTrue();
        });
    }

    [Fact]
    public async Task Sonderrunde_TopfZuordnung_ErzeugtPaarungen()
    {
        // Topf-Zuordnung braucht mindestens 3 Teams je Topf (Issue #347): 2 Töpfe à 3 → jedes Team 4 Spiele
        var topf1 = Topf(1, 1, Eintrag(Team(1)), Eintrag(Team(2)), Eintrag(Team(3)));
        var topf2 = Topf(2, 2, Eintrag(Team(4)), Eintrag(Team(5)), Eintrag(Team(6)));
        AuslosungMit(Rundenart.Sonderrunde, Auslosungsart.TopfZuordnung, lostoepfe: [topf1, topf2]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(12);
        ergebnisse.Select(e => (Math.Min(e.HeimMannschaftId!.Value, e.AuswaertsMannschaftId!.Value),
                                Math.Max(e.HeimMannschaftId.Value, e.AuswaertsMannschaftId.Value)))
            .Should().OnlyHaveUniqueItems("kein Gegner zweimal");
    }

    [Fact]
    public async Task Sonderrunde_Benutzerdefiniert_ErzeugtPaarungen()
    {
        var teams = Enumerable.Range(1, 4).Select(Team).ToList();
        var topf  = Topf(1, 1, teams.Select(t => Eintrag(t)).ToArray());
        AuslosungMit(Rundenart.Sonderrunde, Auslosungsart.Benutzerdefiniert, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(2);
        ergebnisse.Should().AllSatisfy(e =>
        {
            e.HeimMannschaftId.Should().NotBeNull();
            e.AuswaertsMannschaftId.Should().NotBeNull();
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // FINALE — fehlende Auslosungsarten
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Finale_NachSetzliste_EinePaarungGesetzterVsUngesetzter()
    {
        var topf = Topf(1, 1, Eintrag(Team(1), setzPos: 1), Eintrag(Team(2)));
        AuslosungMit(Rundenart.Finale, Auslosungsart.NachSetzliste, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(1);
        ergebnisse[0].HeimMannschaftId.Should().Be(1, "Gesetzter steht als Heim");
        ergebnisse[0].AuswaertsMannschaftId.Should().Be(2);
    }

    [Fact]
    public async Task Finale_NachToepfen_EinePaarungTopfUebergreifend()
    {
        var topf1 = Topf(1, 1, Eintrag(Team(1)));
        var topf2 = Topf(2, 2, Eintrag(Team(2)));
        AuslosungMit(Rundenart.Finale, Auslosungsart.NachToepfen, lostoepfe: [topf1, topf2]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(1);
        ergebnisse[0].HeimMannschaftId.Should().NotBeNull();
        ergebnisse[0].AuswaertsMannschaftId.Should().NotBeNull();
    }

    [Fact]
    public async Task Finale_Benutzerdefiniert_EinePaarung()
    {
        var topf = Topf(1, 1, Eintrag(Team(1)), Eintrag(Team(2)));
        AuslosungMit(Rundenart.Finale, Auslosungsart.Benutzerdefiniert, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(1);
        ergebnisse[0].HeimMannschaftId.Should().NotBeNull();
        ergebnisse[0].AuswaertsMannschaftId.Should().NotBeNull();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // SPIEL UM PLATZ 3 — fehlende Auslosungsarten
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SpielUmPlatzDrei_NachSetzliste_EinePaarung()
    {
        var topf = Topf(1, 1, Eintrag(Team(1), setzPos: 1), Eintrag(Team(2)));
        AuslosungMit(Rundenart.SpielUmPlatzDrei, Auslosungsart.NachSetzliste, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(1);
        ergebnisse[0].HeimMannschaftId.Should().NotBeNull();
        ergebnisse[0].AuswaertsMannschaftId.Should().NotBeNull();
    }

    [Fact]
    public async Task SpielUmPlatzDrei_NachToepfen_EinePaarung()
    {
        var topf1 = Topf(1, 1, Eintrag(Team(1)));
        var topf2 = Topf(2, 2, Eintrag(Team(2)));
        AuslosungMit(Rundenart.SpielUmPlatzDrei, Auslosungsart.NachToepfen, lostoepfe: [topf1, topf2]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(1);
        ergebnisse[0].HeimMannschaftId.Should().NotBeNull();
        ergebnisse[0].AuswaertsMannschaftId.Should().NotBeNull();
    }

    [Fact]
    public async Task SpielUmPlatzDrei_GruppensiegerGegenGruppenzweite_EinePaarung()
    {
        var topf1 = Topf(1, 1, Eintrag(Team(1)));
        var topf2 = Topf(2, 2, Eintrag(Team(2)));
        AuslosungMit(Rundenart.SpielUmPlatzDrei, Auslosungsart.GruppensiegerGegenGruppenzweite,
                     lostoepfe: [topf1, topf2]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(1);
        ergebnisse[0].HeimMannschaftId.Should().NotBeNull();
        ergebnisse[0].AuswaertsMannschaftId.Should().NotBeNull();
    }

    [Fact]
    public async Task SpielUmPlatzDrei_Benutzerdefiniert_EinePaarung()
    {
        var topf = Topf(1, 1, Eintrag(Team(1)), Eintrag(Team(2)));
        AuslosungMit(Rundenart.SpielUmPlatzDrei, Auslosungsart.Benutzerdefiniert, lostoepfe: [topf]);

        var ergebnisse = await Ausfuehren();

        ergebnisse.Should().HaveCount(1);
        ergebnisse[0].HeimMannschaftId.Should().NotBeNull();
        ergebnisse[0].AuswaertsMannschaftId.Should().NotBeNull();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // MANUELLE AUSLOSUNG — ManuelleErgebnisseAbschliessenAsync
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ManuelleAuslosung_Gruppenphase_ErzeugtGruppenZuordnungen_KeinFreilos()
    {
        // Gruppenphase-Einträge haben nie ein Auswärtsteam — das ist die Norm, kein Freilos
        // (Regressionstest für den Bug, der jede Gruppen-Zeile fälschlich als Freilos markierte
        // und dadurch die Paarungs- statt der Gruppen-Ansicht auslöste, siehe #134-Diskussion).
        AuslosungMit(Rundenart.Gruppenphase, Auslosungsart.Manuell, gruppenAnzahl: 2);

        var paarungen = new List<ManuellePaarungEingabe>
        {
            new(Team(1).Id, null, null, null, PaarungsNummer: 1),
            new(Team(2).Id, null, null, null, PaarungsNummer: 1),
            new(Team(3).Id, null, null, null, PaarungsNummer: 2),
            new(Team(4).Id, null, null, null, PaarungsNummer: 2),
        };

        await _service.ManuelleErgebnisseAbschliessenAsync(_repo.Aktuelle!.Id, paarungen);

        _repo.GespeicherteErgebnisse.Should().NotBeNull();
        _repo.GespeicherteErgebnisse.Should().AllSatisfy(e =>
        {
            e.IstFreilos.Should().BeFalse();
            e.AuswaertsMannschaftId.Should().BeNull();
        });
    }

    [Fact]
    public async Task ManuelleAuslosung_KoRunde_OhneGegner_IstWeiterhinFreilos()
    {
        // Paarungsmodus: ein Slot ohne Auswärtsteam ist weiterhin ein echtes Freilos.
        AuslosungMit(Rundenart.KoRunde, Auslosungsart.Manuell);

        var paarungen = new List<ManuellePaarungEingabe>
        {
            new(Team(1).Id, null, null, null, PaarungsNummer: 1),
        };

        await _service.ManuelleErgebnisseAbschliessenAsync(_repo.Aktuelle!.Id, paarungen);

        _repo.GespeicherteErgebnisse.Should().ContainSingle(e => e.IstFreilos);
    }

    [Theory]
    [InlineData(Rundenart.Liga)]
    [InlineData(Rundenart.SchweizerSystem)]
    public async Task ManuelleAuslosung_LigaUndSchweizerSystem_ErzeugtTeilnehmerliste_KeinFreilos(Rundenart rundenart)
    {
        // Liga/SchweizerSystem teilen sich mit Gruppenphase die "flache Teilnehmerliste ohne echte
        // Paarung"-Semantik (Issue #251-Folgefund) — ein Team pro Zeile ist auch hier kein Freilos.
        AuslosungMit(rundenart, Auslosungsart.Manuell);

        var paarungen = new List<ManuellePaarungEingabe>
        {
            new(Team(1).Id, null, null, null, PaarungsNummer: 1),
            new(Team(2).Id, null, null, null, PaarungsNummer: 1),
            new(Team(3).Id, null, null, null, PaarungsNummer: 1),
        };

        await _service.ManuelleErgebnisseAbschliessenAsync(_repo.Aktuelle!.Id, paarungen);

        _repo.GespeicherteErgebnisse.Should().AllSatisfy(e =>
        {
            e.IstFreilos.Should().BeFalse();
            e.AuswaertsMannschaftId.Should().BeNull();
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ISSUE #306 — Platzhalter-Kaskade wird bei jeder Auslosungs-Zustandsänderung ausgelöst
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AusfuehrenAsync_ErsetztSpielplanDerRundeInEinemZug()
    {
        var teams = Enumerable.Range(1, 2).Select(Team).ToList();
        var topf  = Topf(1, 1, teams.Select(t => Eintrag(t)).ToArray());
        var auslosung = AuslosungMit(Rundenart.KoRunde, Auslosungsart.Zufall, lostoepfe: [topf]);
        auslosung.TurnierInstanzId = 42;

        await Ausfuehren();

        // Issue #414: das Ersetzen löst die Kaskade selbst aus (#306) — ein eigener Kaskadenlauf davor würde
        // eine Platzhalter-Runde schon einmal generieren.
        _kaskade.ErsetzteRundenIds.Should().Equal(auslosung.TurnierRundeId);
        _kaskade.AufrufeAnzahl.Should().Be(0);
    }

    [Fact]
    public async Task ZuruecksetzenAsync_LoestPlatzhalterKaskadeFuerTurnierInstanzAus()
    {
        var teams = Enumerable.Range(1, 2).Select(Team).ToList();
        var topf  = Topf(1, 1, teams.Select(t => Eintrag(t)).ToArray());
        var auslosung = AuslosungMit(Rundenart.KoRunde, Auslosungsart.Zufall, lostoepfe: [topf]);
        auslosung.TurnierInstanzId = 42;
        await Ausfuehren();

        await _service.ZuruecksetzenAsync(auslosung.Id);

        _kaskade.AufrufeAnzahl.Should().Be(1, "AusfuehrenAsync ersetzt den Spielplan samt Kaskade in einem Zug");
    }

    [Fact]
    public async Task ManuelleErgebnisseAbschliessenAsync_ErsetztSpielplanDerRundeInEinemZug()
    {
        var auslosung = AuslosungMit(Rundenart.KoRunde, Auslosungsart.Manuell);
        auslosung.TurnierInstanzId = 42;
        var paarungen = new List<ManuellePaarungEingabe> { new(Team(1).Id, null, Team(2).Id, null) };

        await _service.ManuelleErgebnisseAbschliessenAsync(auslosung.Id, paarungen);

        // Issue #411: das Ersetzen löst die Kaskade selbst aus — ein eigener Kaskadenlauf davor würde eine
        // Platzhalter-Runde schon einmal generieren.
        _kaskade.ErsetzteRundenIds.Should().Equal(auslosung.TurnierRundeId);
        _kaskade.AufrufeAnzahl.Should().Be(0);
    }
}
