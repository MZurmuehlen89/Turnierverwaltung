using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;
using static Fussballturnierverwaltung.Tests.Auslosungen.AuslosungTestdaten;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>Prüft die Losverfahren direkt und mit festem Startwert (#437) — ohne Dienst und Attrappen. Aussagen,
/// die für jede Ziehung gelten müssen, laufen über <see cref="STARTWERTE"/> verschiedene Startwerte.</summary>
public class LosverfahrenTests
{
    private const int STARTWERTE = 40;

    private static readonly IReadOnlyDictionary<int, string> _ohneGruppen = new Dictionary<int, string>();

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    private static RegelKontext Regeln(
        bool keinGleichesLand = false, bool keineGleicheGruppe = false, bool heimrechtNachLos = false,
        bool heimrechtNachSetzliste = false, bool freilosMoeglich = false, bool mitRueckspiel = false) =>
        new(keinGleichesLand, keineGleicheGruppe, heimrechtNachLos, heimrechtNachSetzliste, freilosMoeglich,
            Konfoederationsverteilung: false, mitRueckspiel);

    private static List<AuslosungsErgebnis> Ausloesen(
        Auslosung auslosung, RegelKontext regeln, int startwert, IReadOnlyDictionary<int, string>? gruppeJeMannschaft = null) =>
        Losverfahren.Ausloesen(auslosung, regeln, gruppeJeMannschaft ?? _ohneGruppen, new Random(startwert));

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
                Topf(1, [.. Enumerable.Range(1, 4).Select(id => Eintrag(Team(id), setzPosition: id))]),
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
    public void NachToepfen_PaartTopf1MitTopf2UndTopf3MitTopf4_UeberzaehligerTopfBleibtAussenVor()
    {
        var auslosung = NeueAuslosung(Rundenart.KoRunde, Auslosungsart.NachToepfen,
            [.. Enumerable.Range(1, 5).Select(topf => TopfMitTeams(topf, ersteId: 2 * topf - 1, anzahl: 2))]);

        AlleZiehungen(auslosung, Regeln()).Should().AllSatisfy(ziehung =>
        {
            ziehung.Select(e => e.PaarungsNummer).Should().Equal(1, 2, 3, 4);
            HeimIds(ziehung).Should().Equal([1, 2, 5, 6], "ohne Heimrecht-Regel steht der ungerade Topf in Topf-Reihenfolge daheim");
            AuswaertsIds(ziehung.Take(2)).Should().BeEquivalentTo([3, 4]);
            AuswaertsIds(ziehung.Skip(2)).Should().BeEquivalentTo([7, 8]);
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
                auslosung, Regeln(keinGleichesLand: true), _ohneGruppen, new Random(startwert), qualifikanten))
            .ToList();

        ziehungen.Should().AllSatisfy(ziehung =>
        {
            ziehung.Should().AllSatisfy(e => e.HeimMannschaftId.Should().BeNull());
            ziehung.Should().NotContain(e =>
                new[] { e.HeimPlatzhalterName, e.AuswaertsPlatzhalterName }.Order().SequenceEqual(new[] { "Platz 1 (Liga)", "Platz 2 (Liga)" }));
        });
    }

    [Fact]
    public void Zufall_KeinGleichesLand_OffenerPlatzhalter_HatKeinLand()
    {
        // Ohne aufgelöste Mannschaft gibt es kein Land, gegen das die Regel prüfen könnte — wie vor #483.
        var auslosung = NeueAuslosung(Rundenart.KoRunde, Auslosungsart.Zufall,
            [Topf(1, [.. Enumerable.Range(1, 2).Select(platz => new LostopfEintrag { Id = platz, PlatzhalterName = $"Platz {platz} (Liga)" })])]);

        var ausloesen = () => Losverfahren.Ausloesen(
            auslosung, Regeln(keinGleichesLand: true), _ohneGruppen, new Random(1), Qualifikanten.Keine);

        ausloesen.Should().NotThrow();
    }

    // ── Nach Setzliste ────────────────────────────────────────────────────────

    [Fact]
    public void NachSetzliste_GesetzteInReihenfolgeDerSetzposition_GegenUngesetzte()
    {
        var auslosung = NeueAuslosung(Rundenart.KoRunde, Auslosungsart.NachSetzliste,
            [
                Topf(1, Eintrag(Team(11), setzPosition: 3), Eintrag(Team(12), setzPosition: 1), Eintrag(Team(4))),
                Topf(2, Eintrag(Team(5)), Eintrag(Team(13), setzPosition: 2), Eintrag(Team(6)))
            ]);

        AlleZiehungen(auslosung, Regeln()).Should().AllSatisfy(ziehung =>
        {
            HeimIds(ziehung).Should().Equal(12, 13, 11);
            AuswaertsIds(ziehung).Should().BeEquivalentTo([4, 5, 6]);
        });
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
