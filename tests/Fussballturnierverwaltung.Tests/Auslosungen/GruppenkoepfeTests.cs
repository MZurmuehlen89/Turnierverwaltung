using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using static Fussballturnierverwaltung.Tests.Auslosungen.AuslosungTestdaten;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>Gastgeber und Titelverteidiger als feste Gruppenköpfe (Issue #487).</summary>
public class GruppenkoepfeTests
{
    private const int STARTWERTE = 40;

    private static TurnierRunde Gruppenphase(int[] gastgeberGruppen, int? titelverteidigerGruppe = null, bool tvInTopf1 = false) =>
        new()
        {
            Rundenart = Rundenart.Gruppenphase, GruppenAnzahl = 3, GastgeberGruppen = [.. gastgeberGruppen],
            TitelverteidigerGruppe = titelverteidigerGruppe, TitelverteidigerInTopf1 = tvInTopf1
        };

    // ── Bestimmen und Topf 1 ──────────────────────────────────────────────────

    [Fact]
    public void Bestimmen_GastgeberInReihenfolgeDerListe_UeberzaehligeOhneGruppe()
    {
        var koepfe = Gruppenkoepfe.Bestimmen(Gruppenphase([1, 2, 4]), [10, 11, 12, 13], titelverteidigerId: null);

        koepfe.Should().BeEquivalentTo(new Dictionary<int, int> { [10] = 0, [11] = 1, [12] = 3 });
    }

    [Fact]
    public void Bestimmen_TitelverteidigerIstGastgeber_GastgeberGruppeZaehlt()
    {
        var koepfe = Gruppenkoepfe.Bestimmen(Gruppenphase([1], titelverteidigerGruppe: 3), [10], titelverteidigerId: 10);

        koepfe.Should().BeEquivalentTo(new Dictionary<int, int> { [10] = 0 });
    }

    [Fact]
    public void Bestimmen_KeineGruppenphase_KeineKoepfe()
    {
        var runde = Gruppenphase([1], 2);
        runde.Rundenart = Rundenart.KoRunde;

        Gruppenkoepfe.Bestimmen(runde, [10], 20).Should().BeEmpty();
    }

    [Fact]
    public void VorabInTopf1_GruppenkoepfeKommenInTopf1() =>
        Gruppenkoepfe.VorabInTopf1(Gruppenphase([1], 2), [10], 20).Should().Equal(10, 20);

    [Fact]
    public void VorabInTopf1_TitelverteidigerOhneGruppe_NurMitHaken()
    {
        Gruppenkoepfe.VorabInTopf1(Gruppenphase([1]), [10], 20).Should().Equal(10);
        Gruppenkoepfe.VorabInTopf1(Gruppenphase([1], tvInTopf1: true), [10], 20).Should().Equal(10, 20);
    }

    // ── Einlegen ──────────────────────────────────────────────────────────────

    private static readonly Dictionary<int, int> _gastgeber10 = new() { [10] = 1 };

    [Fact]
    public void Einlegen_FehlenderKopfKommtInDenErstenTopf()
    {
        var toepfe = new[] { TopfMitTeams(2, ersteId: 4, anzahl: 2), TopfMitTeams(1, ersteId: 1, anzahl: 2) };

        var (geaendert, ohnePlatz) = Gruppenkoepfe.Einlegen(_gastgeber10, toepfe, hoechstens: 6);

        geaendert.Should().BeTrue();
        ohnePlatz.Should().BeEmpty();
        toepfe[1].Eintraege.Select(e => e.MannschaftId).Should().Equal(1, 2, 10);
    }

    [Fact]
    public void Einlegen_KopfLiegtSchonInEinemTopf_AendertNichts()
    {
        var toepfe = new[] { TopfMitTeams(1, ersteId: 1, anzahl: 2), TopfMitTeams(2, ersteId: 9, anzahl: 2) };

        Gruppenkoepfe.Einlegen(_gastgeber10, toepfe, hoechstens: 6).Geaendert.Should().BeFalse();
        toepfe[0].Eintraege.Should().HaveCount(2);
    }

    [Fact]
    public void Einlegen_ToepfeVoll_KopfOhnePlatz()
    {
        var toepfe = new[] { TopfMitTeams(1, ersteId: 1, anzahl: 3) };

        var (geaendert, ohnePlatz) = Gruppenkoepfe.Einlegen(_gastgeber10, toepfe, hoechstens: 3);

        geaendert.Should().BeFalse();
        ohnePlatz.Should().Equal(10);
        toepfe[0].Eintraege.Should().HaveCount(3);
    }

    // ── Text ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("A, B, D", new[] { 1, 2, 4 })]
    [InlineData("abd", new[] { 1, 2, 4 })]
    [InlineData(" ", new int[0])]
    public void AusText_LiestBuchstaben(string text, int[] erwartet) =>
        Gruppenkoepfe.AusText(text, gruppenAnzahl: 12, out _).Should().Equal(erwartet);

    [Theory]
    [InlineData("A, 1", "*keine Gruppe*")]
    [InlineData("A, A", "*doppelt*")]
    [InlineData("E", "*4 Gruppen nicht*")]
    public void AusText_Fehler(string text, string fehler)
    {
        Gruppenkoepfe.AusText(text, gruppenAnzahl: 4, out var meldung).Should().BeNull();
        meldung.Should().Match(fehler);
    }

    [Fact]
    public void AlsText_Buchstaben() => Gruppenkoepfe.AlsText([1, 2, 4]).Should().Be("A, B, D");

    [Fact]
    public void Widerspruch_GleicheGruppeFuerGastgeberUndTitelverteidiger() =>
        Gruppenkoepfe.Widerspruch([1], 1, 4).Should().Match("*Gruppe A*zugleich*");

    // ── Auslosen ──────────────────────────────────────────────────────────────

    // Drei Gruppen, Topf 1 = 1–3, Topf 2 = 4–6, Topf 3 = 7–9; Gastgeber ist Team 3, Titelverteidiger Team 5 (Topf 2).
    private static readonly Dictionary<int, int> _gastgeberBTitelverteidigerC = new() { [3] = 1, [5] = 2 };

    private static readonly RegelKontext _ohneRegeln = new(false, false, false, false, false, false, false);

    private static List<List<AuslosungsErgebnis>> AlleZiehungen(Auslosungsart art, Dictionary<int, int> koepfe)
    {
        var auslosung = NeueAuslosung(Rundenart.Gruppenphase, art,
            [TopfMitTeams(1, ersteId: 1, anzahl: 3), TopfMitTeams(2, ersteId: 4, anzahl: 3), TopfMitTeams(3, ersteId: 7, anzahl: 3)],
            gruppenAnzahl: 3);
        auslosung.Gruppenposition = Gruppenposition.AlleGelost;
        return [.. Enumerable.Range(1, STARTWERTE).Select(s =>
            Losverfahren.Ausloesen(auslosung, new LosVorbereitung(_ohneRegeln, new Dictionary<int, string>(), koepfe), new Random(s)))];
    }

    private static List<int> Gruppe(List<AuslosungsErgebnis> ziehung, int nummer) =>
        [.. ziehung.Where(e => e.PaarungsNummer == nummer).Select(e => e.HeimMannschaftId!.Value)];

    [Theory]
    [InlineData(Auslosungsart.NachToepfen)]
    [InlineData(Auslosungsart.NachSetzliste)]
    public void Ausloesen_KoepfeFuehrenIhreGruppeAn_JeGruppeEinTeamJeTopf(Auslosungsart art)
    {
        AlleZiehungen(art, _gastgeberBTitelverteidigerC).Should().AllSatisfy(ziehung =>
        {
            Gruppe(ziehung, 2).First().Should().Be(3, "der Gastgeber führt Gruppe B an");
            Gruppe(ziehung, 3).First().Should().Be(5, "der Titelverteidiger führt Gruppe C an");
            foreach (int nr in new[] { 1, 2, 3 })
                Gruppe(ziehung, nr).Select(id => (id - 1) / 3).Should().BeEquivalentTo([0, 1, 2]);
        });
    }

    [Fact]
    public void Ausloesen_Zufall_KoepfeFuehrenIhreGruppeAn() =>
        AlleZiehungen(Auslosungsart.Zufall, _gastgeberBTitelverteidigerC).Should().AllSatisfy(ziehung =>
        {
            Gruppe(ziehung, 2).First().Should().Be(3);
            Gruppe(ziehung, 3).First().Should().Be(5);
            ziehung.GroupBy(e => e.PaarungsNummer).Should().OnlyContain(g => g.Count() == 3);
        });

    // Gastgeber (Team 3) auf B, die übrigen Gesetzten aus Topf 1 füllen in Topf-Reihenfolge die freien Gruppen A und C.
    [Fact]
    public void Ausloesen_NachSetzliste_GesetzteFuellenDieFreienGruppen()
    {
        var auslosung = NeueAuslosung(Rundenart.Gruppenphase, Auslosungsart.NachSetzliste,
            [Topf(1, Eintrag(Team(1)), Eintrag(Team(2)), Eintrag(Team(3))),
             TopfMitTeams(2, ersteId: 4, anzahl: 3), TopfMitTeams(3, ersteId: 7, anzahl: 3)],
            gruppenAnzahl: 3);

        foreach (int s in Enumerable.Range(1, STARTWERTE))
        {
            var ziehung = Losverfahren.Ausloesen(
                auslosung, new LosVorbereitung(_ohneRegeln, new Dictionary<int, string>(), new Dictionary<int, int> { [3] = 1 }),
                new Random(s));
            Gruppe(ziehung, 1).First().Should().Be(1);
            Gruppe(ziehung, 2).First().Should().Be(3);
            Gruppe(ziehung, 3).First().Should().Be(2);
        }
    }
}
