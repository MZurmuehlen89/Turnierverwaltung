using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using static Fussballturnierverwaltung.Tests.Auslosungen.AuslosungTestdaten;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>Prüft die Losverfahren direkt und mit festem Startwert (#437) — ohne Dienst und Attrappen. Aussagen,
/// die für jede Ziehung gelten müssen, laufen über <see cref="STARTWERTE"/> verschiedene Startwerte.</summary>
public class LosverfahrenTests
{
    private const int STARTWERTE = 40;

    private static readonly IReadOnlyDictionary<int, string> _ohneGruppen = new Dictionary<int, string>();
    private static readonly IReadOnlyDictionary<int, int> _ohneKoepfe = new Dictionary<int, int>();

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    private static RegelKontext Regeln(
        bool keinGleichesLand = false, bool keineGleicheGruppe = false, bool heimrechtNachLos = false,
        bool heimrechtNachSetzliste = false, bool freilosMoeglich = false, bool mitRueckspiel = false,
        Spielklassen? heimrechtFuerUnterklassige = null) =>
        new(keinGleichesLand, keineGleicheGruppe, heimrechtNachLos, heimrechtNachSetzliste, freilosMoeglich,
            Konfoederationsverteilung: false, mitRueckspiel, heimrechtFuerUnterklassige);

    private static List<AuslosungsErgebnis> Ausloesen(
        Auslosung auslosung, RegelKontext regeln, int startwert, IReadOnlyDictionary<int, string>? gruppeJeMannschaft = null) =>
        Losverfahren.Ausloesen(auslosung, Vorbereitung(regeln, gruppeJeMannschaft), new Random(startwert));

    private static LosVorbereitung Vorbereitung(RegelKontext regeln, IReadOnlyDictionary<int, string>? gruppeJeMannschaft = null) =>
        new(regeln, gruppeJeMannschaft ?? _ohneGruppen, _ohneKoepfe);

    /// <summary>Alle Ziehungen der Startwerte 1 … <see cref="STARTWERTE"/>.</summary>
    private static List<List<AuslosungsErgebnis>> AlleZiehungen(
        Auslosung auslosung, RegelKontext regeln, IReadOnlyDictionary<int, string>? gruppeJeMannschaft = null) =>
        [.. Enumerable.Range(1, STARTWERTE).Select(startwert => Ausloesen(auslosung, regeln, startwert, gruppeJeMannschaft))];

    /// <summary>Lesbare Kurzform einer Ziehung, z. B. „1:3-7 | 2:5-(frei)“ — Heim und Auswärts als Mannschafts-Id
    /// oder Platzhaltername.</summary>
    private static string Kurzform(IEnumerable<AuslosungsErgebnis> ergebnisse) =>
        string.Join(" | ", ergebnisse.Select(e =>
            $"{e.PaarungsNummer}:{e.HeimMannschaftId?.ToString() ?? e.HeimPlatzhalterName}-" +
            (e.IstFreilos ? "(frei)" : e.AuswaertsMannschaftId?.ToString() ?? e.AuswaertsPlatzhalterName)));

    private static IEnumerable<int> HeimIds(IEnumerable<AuslosungsErgebnis> ergebnisse) =>
        ergebnisse.Select(e => e.HeimMannschaftId!.Value);

    private static IEnumerable<int> AuswaertsIds(IEnumerable<AuslosungsErgebnis> ergebnisse) =>
        ergebnisse.Select(e => e.AuswaertsMannschaftId!.Value);

    // ── Zufall wird übergeben: gleicher Startwert, gleiche Ziehung ────────────

    // Zwei Töpfe mit je vier Mannschaften, die des ersten Topfs sind gesetzt — passt für jede Auslosungsart.
    private static Auslosung AchtTeams(Rundenart rundenart, Auslosungsart auslosungsart, int? gruppenAnzahl) =>
        NeueAuslosung(
            rundenart, auslosungsart,
            [
                Topf(1, [.. Enumerable.Range(1, 4).Select(id => Eintrag(Team(id)))]),
                TopfMitTeams(2, ersteId: 5, anzahl: 4)
            ],
            gruppenAnzahl);

    [Theory]
    [InlineData(Rundenart.KoRunde, Auslosungsart.Zufall, null)]
    [InlineData(Rundenart.KoRunde, Auslosungsart.NachToepfen, null)]
    [InlineData(Rundenart.KoRunde, Auslosungsart.NachSetzliste, null)]
    [InlineData(Rundenart.Gruppenphase, Auslosungsart.Zufall, 2)]
    [InlineData(Rundenart.Gruppenphase, Auslosungsart.NachToepfen, 4)]
    [InlineData(Rundenart.Gruppenphase, Auslosungsart.NachSetzliste, 4)]
    public void GleicherStartwert_LiefertDieselbeZiehung_AndereStartwerteLiefernAndere(
        Rundenart rundenart, Auslosungsart auslosungsart, int? gruppenAnzahl)
    {
        var regeln = Regeln(heimrechtNachLos: true);

        var ersterLauf  = AlleZiehungen(AchtTeams(rundenart, auslosungsart, gruppenAnzahl), regeln).Select(Kurzform).ToList();
        var zweiterLauf = AlleZiehungen(AchtTeams(rundenart, auslosungsart, gruppenAnzahl), regeln).Select(Kurzform).ToList();

        zweiterLauf.Should().Equal(ersterLauf, "der Zufall kommt allein aus dem übergebenen Generator");
        ersterLauf.Distinct().Should().HaveCountGreaterThan(1, "verschiedene Startwerte müssen verschieden auslosen");
    }

    // ── Zufällige Paarungen ───────────────────────────────────────────────────

    [Fact]
    public void Zufall_JedeMannschaftGenauEinmal_PaarungenAbEinsDurchnummeriert()
    {
        var auslosung = NeueAuslosung(Rundenart.KoRunde, Auslosungsart.Zufall, [TopfMitTeams(1, ersteId: 1, anzahl: 8)]);

        AlleZiehungen(auslosung, Regeln()).Should().AllSatisfy(ziehung =>
        {
            ziehung.Select(e => e.PaarungsNummer).Should().Equal(1, 2, 3, 4);
            HeimIds(ziehung).Concat(AuswaertsIds(ziehung)).Should().BeEquivalentTo(Enumerable.Range(1, 8));
        });
    }

    [Fact]
    public void Zufall_KeinGleichesLand_FindetDieEinzigMoeglicheAufteilung()
    {
        // Drei Mannschaften aus Land 1 und drei aus anderen Ländern: jede Paarung braucht genau eine aus Land 1.
        // Blindes Paaren in gemischter Reihenfolge fährt sich hier meist fest — die Suche muss Paare zurücknehmen.
        var landJeTeam = new Dictionary<int, int> { [1] = 1, [2] = 1, [3] = 1, [4] = 2, [5] = 2, [6] = 3 };
        var auslosung  = NeueAuslosung(Rundenart.KoRunde, Auslosungsart.Zufall,
            [Topf(1, [.. landJeTeam.Select(t => Eintrag(Team(t.Key, landId: t.Value)))])]);

        AlleZiehungen(auslosung, Regeln(keinGleichesLand: true)).Should().AllSatisfy(ziehung =>
        {
            ziehung.Should().HaveCount(3);
            ziehung.Should().AllSatisfy(e =>
                new[] { landJeTeam[e.HeimMannschaftId!.Value], landJeTeam[e.AuswaertsMannschaftId!.Value] }
                    .Count(land => land == 1).Should().Be(1));
        });
    }

    [Fact]
    public void Zufall_KeinGleichesLand_NichtErfuellbar_FehlerNenntDieEinschraenkung()
    {
        var auslosung = NeueAuslosung(Rundenart.KoRunde, Auslosungsart.Zufall,
            [Topf(1, [.. Enumerable.Range(1, 4).Select(id => Eintrag(Team(id, landId: 1)))])]);

        var ausloesen = () => Ausloesen(auslosung, Regeln(keinGleichesLand: true), startwert: 1);

        ausloesen.Should().Throw<InvalidOperationException>()
            .WithMessage("Keine gültige Auslosung gefunden*'Kein gleiches Land'*");
    }

    [Fact]
    public void Zufall_UngeradeAnzahlMitFreilos_FreilosIstLetztesErgebnis_UndWirdGelost()
    {
        var auslosung = NeueAuslosung(Rundenart.KoRunde, Auslosungsart.Zufall, [TopfMitTeams(1, ersteId: 1, anzahl: 5)]);

        var ziehungen = AlleZiehungen(auslosung, Regeln(freilosMoeglich: true));

        ziehungen.Should().AllSatisfy(ziehung =>
        {
            ziehung.Select(e => (e.PaarungsNummer, e.IstFreilos)).Should().Equal((1, false), (2, false), (3, true));
            ziehung[2].AuswaertsMannschaftId.Should().BeNull();
            HeimIds(ziehung).Concat(AuswaertsIds(ziehung.Take(2))).Should().BeEquivalentTo(Enumerable.Range(1, 5));
        });
        ziehungen.Select(z => z[2].HeimMannschaftId).Distinct().Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void Zufall_UngeradeAnzahlOhneFreilos_EineMannschaftBleibtOhnePaarung()
    {
        var auslosung = NeueAuslosung(Rundenart.KoRunde, Auslosungsart.Zufall, [TopfMitTeams(1, ersteId: 1, anzahl: 5)]);

        AlleZiehungen(auslosung, Regeln()).Should().AllSatisfy(ziehung =>
        {
            ziehung.Should().HaveCount(2);
            ziehung.Should().NotContain(e => e.IstFreilos);
        });
    }

    // ── Nach Töpfen ───────────────────────────────────────────────────────────

    [Fact]
    // Ein überzähliger letzter Topf wird untereinander gelost wie der Überhang ungleich großer Töpfe (#495) — bis
    // dahin blieb er außen vor. Die Oberfläche lässt eine ungerade Topfanzahl ohnehin nicht zu (AuslosungsValidator).
    public void NachToepfen_PaartTopf1MitTopf2UndTopf3MitTopf4_UeberzaehligerTopfUntereinander()
    {
        var auslosung = NeueAuslosung(Rundenart.KoRunde, Auslosungsart.NachToepfen,
            [.. Enumerable.Range(1, 5).Select(topf => TopfMitTeams(topf, ersteId: 2 * topf - 1, anzahl: 2))]);

        AlleZiehungen(auslosung, Regeln()).Should().AllSatisfy(ziehung =>
        {
            ziehung.Select(e => e.PaarungsNummer).Should().Equal(1, 2, 3, 4, 5);
            HeimIds(ziehung.Take(4)).Should().Equal([1, 2, 5, 6], "ohne Heimrecht-Regel steht der vordere Topf in Topf-Reihenfolge daheim");
            AuswaertsIds(ziehung.Take(2)).Should().BeEquivalentTo([3, 4]);
            AuswaertsIds(ziehung.Skip(2).Take(2)).Should().BeEquivalentTo([7, 8]);
            HeimIds(ziehung.Skip(4)).Concat(AuswaertsIds(ziehung.Skip(4))).Should().BeEquivalentTo([9, 10]);
        });
    }

    [Fact]
    public void NachToepfen_KeineGleicheGruppe_MitMannschaften_ErzwingtUeberKreuz()
    {
        // Gruppensieger 1 (A) und 2 (B) gegen Gruppenzweite 3 (A) und 4 (B): nur über Kreuz erlaubt.
        var auslosung = NeueAuslosung(Rundenart.KoRunde, Auslosungsart.GruppensiegerGegenGruppenzweite,
            [TopfMitTeams(1, ersteId: 1, anzahl: 2), TopfMitTeams(2, ersteId: 3, anzahl: 2)]);
        var gruppeJeMannschaft = new Dictionary<int, string> { [1] = "A", [2] = "B", [3] = "A", [4] = "B" };

        AlleZiehungen(auslosung, Regeln(keineGleicheGruppe: true), gruppeJeMannschaft)
            .Select(Kurzform).Distinct().Should().Equal("1:1-4 | 2:2-3");
    }

    [Fact]
    public void NachToepfen_KeineGleicheGruppe_MitPlatzhaltern_LiestDieGruppeAusDemNamen()
    {
        var auslosung = NeueAuslosung(Rundenart.KoRunde, Auslosungsart.GruppensiegerGegenGruppenzweite,
            [
                Topf(1, new LostopfEintrag { Id = 1, PlatzhalterName = "1. Gruppe A" }, new LostopfEintrag { Id = 2, PlatzhalterName = "Sieger Gruppe B" }),
                Topf(2, new LostopfEintrag { Id = 3, PlatzhalterName = "2. Gruppe A" }, new LostopfEintrag { Id = 4, PlatzhalterName = "2. Gruppe B" })
            ]);

        AlleZiehungen(auslosung, Regeln(keineGleicheGruppe: true))
            .Select(Kurzform).Distinct().Should().Equal("1:1. Gruppe A-2. Gruppe B | 2:Sieger Gruppe B-2. Gruppe A");
    }

    [Fact]
    public void Zufall_KeinGleichesLand_MitPlatzhaltern_PrueftDasLandIhrerQualifikanten()
    {
        // #483: Platz 1 und 2 der Liga sind beide aus Land 1 — als Platzhalter dürfen sie nicht gepaart werden, und
        // im Ergebnis bleiben die Platzhalter stehen (Kaskade und Spielplan hängen an ihnen).
        var landJePlatz = new Dictionary<int, int> { [1] = 1, [2] = 1, [3] = 2, [4] = 3 };
        var auslosung = NeueAuslosung(Rundenart.KoRunde, Auslosungsart.Zufall,
            [Topf(1, [.. landJePlatz.Keys.Select(platz => new LostopfEintrag { Id = platz, PlatzhalterName = $"Platz {platz} (Liga)" })])]);
        var qualifikanten = new Qualifikanten(
            landJePlatz.ToDictionary(p => $"Platz {p.Key} (Liga)", p => Team(p.Key, landId: p.Value)), AnzahlOffen: 0);

        var ziehungen = Enumerable.Range(1, STARTWERTE)
            .Select(startwert => Losverfahren.Ausloesen(
                auslosung, Vorbereitung(Regeln(keinGleichesLand: true)), new Random(startwert), qualifikanten))
            .ToList();

        ziehungen.Should().AllSatisfy(ziehung =>
        {
            ziehung.Should().AllSatisfy(e => e.HeimMannschaftId.Should().BeNull());
            ziehung.Should().NotContain(e =>
                new[] { e.HeimPlatzhalterName, e.AuswaertsPlatzhalterName }.Order().SequenceEqual(new[] { "Platz 1 (Liga)", "Platz 2 (Liga)" }));
        });
    }

    [Fact]
    public void NachToepfen_KeineGleicheGruppe_BesteDritte_PrueftDieGruppeIhrerQualifikanten()
    {
        // #489: „1. bester 3. Platz“ trägt keine Gruppe im Namen — sie kommt vom Qualifikanten. Bester Dritter 1 kommt
        // aus Gruppe A, bester Dritter 2 aus Gruppe B, also bleibt nur je die Paarung mit dem Sieger der anderen Gruppe.
        var auslosung = NeueAuslosung(Rundenart.KoRunde, Auslosungsart.NachToepfen,
        [
            Topf(1, new LostopfEintrag { Id = 1, PlatzhalterName = "1. Gruppe A" }, new LostopfEintrag { Id = 2, PlatzhalterName = "1. Gruppe B" }),
            Topf(2, new LostopfEintrag { Id = 3, PlatzhalterName = "1. bester 3. Platz" }, new LostopfEintrag { Id = 4, PlatzhalterName = "2. bester 3. Platz" }),
        ]);
        var qualifikanten = new Qualifikanten(
            new Dictionary<string, Mannschaft> { ["1. bester 3. Platz"] = Team(30), ["2. bester 3. Platz"] = Team(31) }, AnzahlOffen: 0);
        var gruppeJeMannschaft = new Dictionary<int, string> { [30] = "A", [31] = "B" };

        var ziehungen = Enumerable.Range(1, STARTWERTE)
            .Select(startwert => Losverfahren.Ausloesen(
                auslosung, Vorbereitung(Regeln(keineGleicheGruppe: true), gruppeJeMannschaft), new Random(startwert), qualifikanten))
            .ToList();

        ziehungen.Select(z => string.Join(" | ", z.OrderBy(e => e.HeimPlatzhalterName)
                .Select(e => $"{e.HeimPlatzhalterName}-{e.AuswaertsPlatzhalterName}")))
            .Distinct().Should().Equal("1. Gruppe A-2. bester 3. Platz | 1. Gruppe B-1. bester 3. Platz");
    }

    [Fact]
    public void Zufall_KeinGleichesLand_OffenerPlatzhalter_HatKeinLand()
    {
        // Ohne aufgelöste Mannschaft gibt es kein Land, gegen das die Regel prüfen könnte — wie vor #483.
        var auslosung = NeueAuslosung(Rundenart.KoRunde, Auslosungsart.Zufall,
            [Topf(1, [.. Enumerable.Range(1, 2).Select(platz => new LostopfEintrag { Id = platz, PlatzhalterName = $"Platz {platz} (Liga)" })])]);

        var ausloesen = () => Losverfahren.Ausloesen(
            auslosung, Vorbereitung(Regeln(keinGleichesLand: true)), new Random(1), Qualifikanten.Keine);

        ausloesen.Should().NotThrow();
    }

    // ── Nach Setzliste ────────────────────────────────────────────────────────

    // Topf 1 ist die Setzliste (#563): die Gesetzten in Topf-Reihenfolge, jeder gegen ein Team aus irgendeinem der
    // übrigen Töpfe. „Nach Töpfen“ lost hier dagegen Topf 1 gegen Topf 2 und den Rest untereinander — dort könnten
    // zwei Gesetzte aufeinandertreffen.
    [Fact]
    public void NachSetzliste_Topf1GesetztInTopfReihenfolge_GegenDieUebrigenToepfe()
    {
        var auslosung = NeueAuslosung(Rundenart.KoRunde, Auslosungsart.NachSetzliste,
            [
                Topf(1, Eintrag(Team(11)), Eintrag(Team(12)), Eintrag(Team(13))),
                Topf(2, Eintrag(Team(4))),
                Topf(3, Eintrag(Team(5)), Eintrag(Team(6)))
            ]);

        AlleZiehungen(auslosung, Regeln()).Should().AllSatisfy(ziehung =>
        {
            HeimIds(ziehung).Should().Equal(11, 12, 13);
            AuswaertsIds(ziehung).Should().BeEquivalentTo([4, 5, 6]);
        });
    }

    [Fact]
    public void NachToepfen_GleicheToepfe_GesetzteKoennenAufeinandertreffen()
    {
        var auslosung = NeueAuslosung(Rundenart.KoRunde, Auslosungsart.NachToepfen,
            [
                Topf(1, Eintrag(Team(11)), Eintrag(Team(12)), Eintrag(Team(13))),
                Topf(2, Eintrag(Team(4))),
                Topf(3, Eintrag(Team(5)), Eintrag(Team(6)))
            ]);

        AlleZiehungen(auslosung, Regeln()).Should().Contain(ziehung =>
            ziehung.Any(e => e.HeimMannschaftId > 10 && e.AuswaertsMannschaftId > 10),
            "zum Vergleich: ohne Setzliste ist Topf 1 nur gegen Topf 2 geschützt");
    }

    // ── Heimrecht ─────────────────────────────────────────────────────────────

    private static Auslosung ZweiToepfeJeDrei() =>
        NeueAuslosung(Rundenart.KoRunde, Auslosungsart.NachToepfen,
            [TopfMitTeams(1, ersteId: 1, anzahl: 3), TopfMitTeams(2, ersteId: 4, anzahl: 3)]);

    [Fact]
    public void HeimrechtNachLos_BeideRichtungenKommenVor()
    {
        var ziehungen = AlleZiehungen(ZweiToepfeJeDrei(), Regeln(heimrechtNachLos: true));

        var heimIds = ziehungen.SelectMany(HeimIds).ToList();
        heimIds.Should().Contain(id => id <= 3, "Topf 1 steht mal daheim");
        heimIds.Should().Contain(id => id >= 4, "Topf 2 steht mal daheim");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HeimrechtNachSetzliste_BessererTopfDaheim_AuchWennZusaetzlichGelostWuerde(bool zusaetzlichNachLos)
    {
        var regeln = Regeln(heimrechtNachSetzliste: true, heimrechtNachLos: zusaetzlichNachLos);

        AlleZiehungen(ZweiToepfeJeDrei(), regeln).Should().AllSatisfy(ziehung =>
            HeimIds(ziehung).Should().BeEquivalentTo([1, 2, 3]));
    }

    [Fact]
    public void HeimrechtNachSetzliste_MitRueckspiel_BessererTopfImHinspielAuswaerts()
    {
        var regeln = Regeln(heimrechtNachSetzliste: true, mitRueckspiel: true);

        AlleZiehungen(ZweiToepfeJeDrei(), regeln).Should().AllSatisfy(ziehung =>
            AuswaertsIds(ziehung).Should().BeEquivalentTo([1, 2, 3]));
    }

    // ── Heimrecht für Unterklassige (#393) ───────────────────────────────────

    // Topf 1 (Ids 1–3) Klasse 1, Topf 2 (Ids 4–6) Klasse 3; Profigrenze 2 wie im DFB-Pokal.
    private static readonly Spielklassen _profisGegenAmateure = new(
        new Dictionary<int, int> { [1] = 1, [2] = 1, [3] = 1, [4] = 3, [5] = 3, [6] = 3 }, Profigrenze: 2);

    [Fact]
    public void HeimrechtFuerUnterklassige_AmateurSpieltZuHause()
    {
        var regeln = Regeln(heimrechtFuerUnterklassige: _profisGegenAmateure);

        AlleZiehungen(ZweiToepfeJeDrei(), regeln).Should().AllSatisfy(ziehung =>
            HeimIds(ziehung).Should().BeEquivalentTo([4, 5, 6]));
    }

    [Fact]
    public void HeimrechtFuerUnterklassige_MitRueckspiel_AmateurImHinspielAuswaerts()
    {
        var regeln = Regeln(heimrechtFuerUnterklassige: _profisGegenAmateure, mitRueckspiel: true);

        AlleZiehungen(ZweiToepfeJeDrei(), regeln).Should().AllSatisfy(ziehung =>
            AuswaertsIds(ziehung).Should().BeEquivalentTo([4, 5, 6]));
    }

    [Fact]
    public void HeimrechtFuerUnterklassige_OhneSpielklassen_EntscheidetDasLos()
    {
        var heimIds = AlleZiehungen(ZweiToepfeJeDrei(), Regeln(heimrechtFuerUnterklassige: Spielklassen.Keine))
            .SelectMany(HeimIds).ToList();

        heimIds.Should().Contain(id => id <= 3).And.Contain(id => id >= 4);
    }

    [Fact]
    public void HeimrechtFuerUnterklassige_Platzhalter_NimmtDieKlasseDerMannschaftDahinter()
    {
        // Ein Topf mit zwei Platzhaltern: hinter „Sieger A“ steht ein Profi (Id 1), hinter „Sieger B“ ein Amateur (Id 4).
        var auslosung = NeueAuslosung(Rundenart.KoRunde, Auslosungsart.Zufall,
            [Topf(1, new LostopfEintrag { Id = 1, PlatzhalterName = "Sieger A" }, new LostopfEintrag { Id = 2, PlatzhalterName = "Sieger B" })]);
        var qualifikanten = new Qualifikanten(
            new Dictionary<string, Mannschaft> { ["Sieger A"] = Team(1), ["Sieger B"] = Team(4) }, AnzahlOffen: 0);

        Enumerable.Range(1, STARTWERTE)
            .Select(startwert => Losverfahren.Ausloesen(
                auslosung, Vorbereitung(Regeln(heimrechtFuerUnterklassige: _profisGegenAmateure)), new Random(startwert),
                qualifikanten))
            .Should().AllSatisfy(ziehung => ziehung.Single().HeimPlatzhalterName.Should().Be("Sieger B"));
    }

    // ── Gruppenphase und Liga ─────────────────────────────────────────────────

    [Fact]
    public void Gruppenphase_NachToepfen_JeGruppeEineMannschaftJeTopf_OhneGegner()
    {
        var auslosung = NeueAuslosung(Rundenart.Gruppenphase, Auslosungsart.NachToepfen,
            [TopfMitTeams(1, ersteId: 1, anzahl: 3), TopfMitTeams(2, ersteId: 4, anzahl: 3)], gruppenAnzahl: 3);

        AlleZiehungen(auslosung, Regeln()).Should().AllSatisfy(ziehung =>
        {
            ziehung.Should().OnlyContain(e => e.AuswaertsMannschaftId == null && !e.IstFreilos);
            ziehung.GroupBy(e => e.PaarungsNummer).Select(g => g.Key).Should().BeEquivalentTo([1, 2, 3]);
            ziehung.GroupBy(e => e.PaarungsNummer).Should().AllSatisfy(gruppe =>
            {
                HeimIds(gruppe).Count(id => id <= 3).Should().Be(1, "eine Mannschaft aus Topf 1");
                HeimIds(gruppe).Count(id => id >= 4).Should().Be(1, "eine Mannschaft aus Topf 2");
            });
        });
    }

    // ── Position in der Gruppe (#475) ─────────────────────────────────────────

    // Drei Gruppen, drei Töpfe: Topf 1 = 1–3, Topf 2 = 4–6, Topf 3 = 7–9.
    private static Auslosung DreiToepfeDreiGruppen(Gruppenposition position, Auslosungsart art = Auslosungsart.NachToepfen)
    {
        var auslosung = NeueAuslosung(Rundenart.Gruppenphase, art,
            [TopfMitTeams(1, ersteId: 1, anzahl: 3), TopfMitTeams(2, ersteId: 4, anzahl: 3), TopfMitTeams(3, ersteId: 7, anzahl: 3)],
            gruppenAnzahl: 3);
        auslosung.Gruppenposition = position;
        return auslosung;
    }

    /// <summary>Der Topf (1–3) je Position, Gruppe für Gruppe — z. B. „1,2,3|1,3,2|…“.</summary>
    private static List<List<int>> TopfJePosition(List<AuslosungsErgebnis> ziehung) =>
        [.. ziehung.GroupBy(e => e.PaarungsNummer).Select(g => HeimIds(g).Select(id => (id - 1) / 3 + 1).ToList())];

    [Fact]
    public void Gruppenposition_NachTopf_PositionEntsprichtDemTopf()
    {
        AlleZiehungen(DreiToepfeDreiGruppen(Gruppenposition.NachTopf), Regeln()).Should().AllSatisfy(ziehung =>
            TopfJePosition(ziehung).Should().AllSatisfy(gruppe => gruppe.Should().Equal(1, 2, 3)));
    }

    [Fact]
    public void Gruppenposition_Topf1Fest_Topf1AufPosition1_UebrigeGelost()
    {
        var ziehungen = AlleZiehungen(DreiToepfeDreiGruppen(Gruppenposition.Topf1Fest), Regeln());

        ziehungen.SelectMany(TopfJePosition).Should().AllSatisfy(gruppe => gruppe[0].Should().Be(1));
        ziehungen.SelectMany(TopfJePosition).Select(g => g[1]).Distinct().Should().BeEquivalentTo([2, 3],
            "Position 2 ist nicht an Topf 2 gebunden");
    }

    [Fact]
    public void Gruppenposition_AlleGelost_AuchTopf1NichtFestAufPosition1()
    {
        var ziehungen = AlleZiehungen(DreiToepfeDreiGruppen(Gruppenposition.AlleGelost), Regeln());

        ziehungen.SelectMany(TopfJePosition).Select(g => g[0]).Distinct().Should().BeEquivalentTo([1, 2, 3]);
        ziehungen.Should().AllSatisfy(ziehung =>
            TopfJePosition(ziehung).Should().AllSatisfy(gruppe => gruppe.Should().BeEquivalentTo([1, 2, 3],
                "die Gruppenzuordnung bleibt eine Mannschaft je Topf")));
    }

    // Gesetzte bleiben bei jeder Variante auf Position 1 ihrer Gruppe.
    [Fact]
    public void Gruppenposition_AlleGelost_NachSetzliste_GesetzteBleibenAufPosition1()
    {
        var auslosung = NeueAuslosung(Rundenart.Gruppenphase, Auslosungsart.NachSetzliste,
            [Topf(1, Eintrag(Team(1)), Eintrag(Team(2)), Eintrag(Team(3))),
             TopfMitTeams(2, ersteId: 4, anzahl: 3), TopfMitTeams(3, ersteId: 7, anzahl: 3)],
            gruppenAnzahl: 3);
        auslosung.Gruppenposition = Gruppenposition.AlleGelost;

        var ziehungen = AlleZiehungen(auslosung, Regeln());

        ziehungen.Should().AllSatisfy(ziehung =>
            ziehung.GroupBy(e => e.PaarungsNummer).Select(g => HeimIds(g).First())
                .Should().Equal([1, 2, 3], "die Gesetzten führen ihre Gruppe an"));
        ziehungen.SelectMany(z => z.GroupBy(e => e.PaarungsNummer)).Select(g => HeimIds(g).ElementAt(1))
            .Should().Contain(id => id >= 7, "die übrigen Positionen sind gelost");
    }

    // Topf 1 ist die Setzliste (#563): seine Teams führen der Reihe nach Gruppe A, B, C an — die Gruppe der
    // Gesetzten wird nicht gelost, die übrigen Töpfe geben je Gruppe ein Team (#488).
    [Theory]
    [InlineData(Gruppenposition.NachTopf)]
    [InlineData(Gruppenposition.Topf1Fest)]
    [InlineData(Gruppenposition.AlleGelost)]
    public void NachSetzliste_Topf1InReihenfolgeAufDieGruppen_JeGruppeEinTeamJeTopf(Gruppenposition position)
    {
        var auslosung = NeueAuslosung(Rundenart.Gruppenphase, Auslosungsart.NachSetzliste,
            [Topf(1, Eintrag(Team(2)), Eintrag(Team(1)), Eintrag(Team(3))),
             TopfMitTeams(2, ersteId: 4, anzahl: 3), TopfMitTeams(3, ersteId: 7, anzahl: 3)],
            gruppenAnzahl: 3);
        auslosung.Gruppenposition = position;

        AlleZiehungen(auslosung, Regeln()).Should().AllSatisfy(ziehung =>
        {
            ziehung.GroupBy(e => e.PaarungsNummer).OrderBy(g => g.Key).Select(g => HeimIds(g).First())
                .Should().Equal([1, 2, 3], "Gruppe A, B, C in Setzreihenfolge");
            TopfJePosition(ziehung).Should().AllSatisfy(gruppe => gruppe.Should().BeEquivalentTo([1, 2, 3]));
        });
    }

    // Ohne Töpfe gibt es keine Bindung an einen Topf — die Position wird unabhängig von der Auswahl gelost.
    [Fact]
    public void Gruppenposition_Zufall_ImmerGelost()
    {
        var ziehungen = AlleZiehungen(DreiToepfeDreiGruppen(Gruppenposition.NachTopf, Auslosungsart.Zufall), Regeln());

        ziehungen.SelectMany(TopfJePosition).Select(g => string.Join(",", g)).Distinct()
            .Should().HaveCountGreaterThan(1);
    }

    [Theory]
    [InlineData(Gruppenposition.Topf1Fest)]
    [InlineData(Gruppenposition.AlleGelost)]
    public void Gruppenposition_GleicherStartwert_GleicheReihenfolge(Gruppenposition position)
    {
        var ersterLauf  = AlleZiehungen(DreiToepfeDreiGruppen(position), Regeln()).Select(Kurzform).ToList();
        var zweiterLauf = AlleZiehungen(DreiToepfeDreiGruppen(position), Regeln()).Select(Kurzform).ToList();

        zweiterLauf.Should().Equal(ersterLauf);
        ersterLauf.Distinct().Should().HaveCountGreaterThan(1);
    }

    [Theory]
    [InlineData(Auslosungsart.Zufall)]
    [InlineData(Auslosungsart.NachToepfen)]
    [InlineData(Auslosungsart.NachSetzliste)]
    public void Liga_AlleMannschaftenGenauEinmalInEinerGruppe(Auslosungsart auslosungsart)
    {
        var auslosung = AchtTeams(Rundenart.Liga, auslosungsart, gruppenAnzahl: null);

        AlleZiehungen(auslosung, Regeln()).Should().AllSatisfy(ziehung =>
        {
            ziehung.Should().OnlyContain(e => e.PaarungsNummer == 1 && e.AuswaertsMannschaftId == null && !e.IstFreilos);
            HeimIds(ziehung).Should().BeEquivalentTo(Enumerable.Range(1, 8));
        });
    }

    // Die Reihenfolge ist die Schlüsselzahl, aus der der Spielplan entsteht — sie muss gelost sein (#474).
    [Theory]
    [InlineData(Auslosungsart.Zufall)]
    [InlineData(Auslosungsart.NachToepfen)]
    [InlineData(Auslosungsart.NachSetzliste)]
    public void Liga_ReihenfolgeWirdGelost_GleicherStartwertGleicheReihenfolge(Auslosungsart auslosungsart)
    {
        var ersterLauf  = AlleZiehungen(AchtTeams(Rundenart.Liga, auslosungsart, null), Regeln()).Select(Kurzform).ToList();
        var zweiterLauf = AlleZiehungen(AchtTeams(Rundenart.Liga, auslosungsart, null), Regeln()).Select(Kurzform).ToList();

        zweiterLauf.Should().Equal(ersterLauf, "der Zufall kommt allein aus dem übergebenen Generator");
        ersterLauf.Distinct().Should().HaveCountGreaterThan(1, "verschiedene Startwerte müssen verschieden auslosen");
    }

    [Fact]
    public void Gruppenphase_OhneGruppenanzahl_Fehler()
    {
        var auslosung = NeueAuslosung(Rundenart.Gruppenphase, Auslosungsart.Zufall, [TopfMitTeams(1, ersteId: 1, anzahl: 4)]);

        var ausloesen = () => Ausloesen(auslosung, Regeln(), startwert: 1);

        ausloesen.Should().Throw<InvalidOperationException>().WithMessage("*keine Gruppenanzahl*");
    }
}
