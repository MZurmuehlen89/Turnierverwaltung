using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Enums;
using static Fussballturnierverwaltung.Tests.Simulation.SimulationsEngineTestdaten;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>#359 K2–K4, K6: die reine Rechnung der dynamischen Teamstärke (Elo-Formel, Wertung, Reihenfolge, Wirkung).</summary>
public class DynamischeTeamstaerkeTests
{
    private const double K = 20;

    private static WertungsSpiel Spiel(int nummer, int heim, int gast, int heimTore, int gastTore,
        Spielortvorteil ort = Spielortvorteil.Neutral, double heimStart = 1500, double gastStart = 1500) =>
        new(new Wertungsschluessel(1, nummer, nummer), heim, gast, heimStart, gastStart, heimTore, gastTore, ort);

    private static Wertungsschluessel Ende => new(99, 0, 0);

    // ── K2 Formel ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(70, 1410)]
    [InlineData(75, 1500)]
    [InlineData(80, 1590)]
    public void Startwert_FolgtDerGeeichtenAbbildung(int rating, double erwartet) =>
        DynamischeTeamstaerke.Startwert(Team(1, rating, rating, rating)).Should().BeApproximately(erwartet, 1e-9);

    [Theory]
    [InlineData(TurnierKategorie.Weltmeisterschaft, 60)]
    [InlineData(TurnierKategorie.Kontinentalmeisterschaft, 50)]
    [InlineData(TurnierKategorie.Vereinspokal, 30)]
    [InlineData(TurnierKategorie.Liga, 20)]
    [InlineData(TurnierKategorie.KleinesTurnier, 20)]
    [InlineData(TurnierKategorie.Benutzerdefiniert, 20)]
    public void KFaktor_NachWettbewerb(TurnierKategorie kategorie, double erwartet) =>
        DynamischeTeamstaerke.KFaktor(kategorie).Should().Be(erwartet);

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(1, 1.0)]
    [InlineData(-1, 1.0)]
    [InlineData(2, 1.5)]
    [InlineData(-2, 1.5)]
    [InlineData(3, 1.75)]
    [InlineData(5, 2.0)]
    public void Tordifferenzfaktor_NachEloratings(int differenz, double erwartet) =>
        DynamischeTeamstaerke.Tordifferenzfaktor(differenz).Should().Be(erwartet);

    [Fact]
    public void Erwartung_Elo400ErgibtZehnElftel()
    {
        DynamischeTeamstaerke.Erwartung(0).Should().Be(0.5);
        DynamischeTeamstaerke.Erwartung(400).Should().BeApproximately(10.0 / 11.0, 1e-12);
    }

    [Theory]
    [InlineData(1, 0, 10.0)]    // G 1, W 1, We 0,5
    [InlineData(2, 0, 15.0)]    // G 1,5
    [InlineData(3, 0, 17.5)]    // G 1,75
    [InlineData(1, 1, 0.0)]     // Remis zwischen Gleichstarken
    [InlineData(0, 4, -18.75)]  // G 1,875 · (0 − 0,5) · 20
    public void Update_GleichstarkeTeamsAufNeutralemPlatz(int heimTore, int gastTore, double erwartet)
    {
        var abw = DynamischeTeamstaerke.Abweichungen([Spiel(1, 1, 2, heimTore, gastTore)], K, Ende);

        abw.GetValueOrDefault(1).Should().BeApproximately(erwartet, 1e-9);
        abw.GetValueOrDefault(2).Should().BeApproximately(-erwartet, 1e-9);
    }

    [Fact]
    public void Heimbonus_GiltFuerDasTeamMitDemOrtsvorteil()
    {
        // Gleich stark, Remis: das Heimteam hätte mit Bonus (dr 60) gewinnen sollen – verliert Elo.
        double we = 1 / (1 + Math.Pow(10, -60 / 400.0));
        var heim = DynamischeTeamstaerke.Abweichungen([Spiel(1, 1, 2, 1, 1, Spielortvorteil.Heim)], K, Ende);
        heim[1].Should().BeApproximately(K * (0.5 - we), 1e-9);

        // Hat der nominelle Gast den Ortsvorteil (Gastgeber), verliert er die Elo.
        var gast = DynamischeTeamstaerke.Abweichungen([Spiel(1, 1, 2, 1, 1, Spielortvorteil.Auswaerts)], K, Ende);
        gast[2].Should().BeApproximately(K * (0.5 - we), 1e-9);

        // Neutraler Platz: kein Bonus.
        DynamischeTeamstaerke.Abweichungen([Spiel(1, 1, 2, 1, 1)], K, Ende)[1].Should().Be(0);
    }

    [Fact]
    public void Update_BerueckichtigtStartwerteUndBisherigeAbweichung()
    {
        // Team 1 ist um 400 Punkte stärker: ein knapper Sieg bringt kaum etwas, eine Niederlage kostet viel.
        var sieg = DynamischeTeamstaerke.Abweichungen([Spiel(1, 1, 2, 1, 0, heimStart: 1900, gastStart: 1500)], K, Ende);
        sieg[1].Should().BeApproximately(K * (1 - 10.0 / 11.0), 1e-9);

        // Zweites Spiel rechnet mit dem Stand nach dem ersten (Abweichung geht in die Differenz ein).
        var zwei = DynamischeTeamstaerke.Abweichungen(
            [Spiel(1, 1, 2, 3, 0), Spiel(2, 1, 3, 0, 0)], K, Ende);
        double nachErstem = 17.5;
        double we = DynamischeTeamstaerke.Erwartung(nachErstem);
        zwei[1].Should().BeApproximately(nachErstem + K * (0.5 - we), 1e-9);
    }

    // ── K3 Wertung ────────────────────────────────────────────────────────

    private static Austragung Termin(Spielortvorteil ort = Spielortvorteil.Heim, bool neutral = false) =>
        new(new Wertungsschluessel(2, 3, 4), 10, 20, 1500, 1600, ort, neutral);

    [Fact]
    public void Wertungen_OhneWiederholung_EinEintragMitDenToren()
    {
        var eintraege = DynamischeTeamstaerke.Wertungen(Termin(), 2, 1, null).ToList();

        eintraege.Should().ContainSingle();
        eintraege[0].Should().Be(new WertungsSpiel(new Wertungsschluessel(2, 3, 4), 10, 20, 1500, 1600, 2, 1, Spielortvorteil.Heim));
    }

    [Fact]
    public void Wertungen_TorgleichNachVerlaengerung_ZaehltAlsRemis_AuchWennElfmeterEntscheiden()
    {
        // Das Elfmeterschießen steckt nicht in den Toren: 1:1 nach Verlängerung bleibt ein Remis.
        var termin = new Austragung(new Wertungsschluessel(1, 1, 1), 10, 20, 1500, 1500, Spielortvorteil.Neutral, true);

        var abw = DynamischeTeamstaerke.Abweichungen(DynamischeTeamstaerke.Wertungen(termin, 1, 1, null), K, Ende);

        abw.GetValueOrDefault(10).Should().Be(0);
    }

    [Fact]
    public void Wertungen_Wiederholungsspiel_TauschtDasHeimrechtUndSeineTore()
    {
        var wdh = new Wiederholungsergebnis(0, 2, null, null);   // aus Sicht des ersten Heimteams: 0:2

        var eintraege = DynamischeTeamstaerke.Wertungen(Termin(), 1, 1, wdh).ToList();

        eintraege.Should().HaveCount(2);
        eintraege[1].Should().Be(new WertungsSpiel(
            new Wertungsschluessel(2, 3, 4, 1), 20, 10, 1600, 1500, 2, 0, Spielortvorteil.Heim));
        eintraege[1].Schluessel.CompareTo(eintraege[0].Schluessel).Should().BePositive();
    }

    [Theory]
    [InlineData(Spielortvorteil.Neutral, true)]
    [InlineData(Spielortvorteil.Auswaerts, true)]
    public void Wertungen_Wiederholungsspiel_AufNeutralemPlatzBleibenDieSeiten(Spielortvorteil ort, bool neutral)
    {
        var wdh = new Wiederholungsergebnis(0, 2, 1, 0);   // Verlängerung zählt: 1:2

        var eintraege = DynamischeTeamstaerke.Wertungen(Termin(ort, neutral), 1, 1, wdh).ToList();

        eintraege[1].Should().Be(new WertungsSpiel(
            new Wertungsschluessel(2, 3, 4, 1), 10, 20, 1500, 1600, 1, 2, ort));
    }

    // ── K4 Reihenfolge ────────────────────────────────────────────────────

    [Fact]
    public void Reihenfolge_ZaehltDerSchluessel_NichtDieListenposition()
    {
        var spiele = new[] { Spiel(1, 1, 2, 3, 0), Spiel(2, 2, 3, 0, 1), Spiel(3, 3, 1, 2, 2) };

        var geordnet = DynamischeTeamstaerke.Abweichungen(spiele, K, Ende);
        var umgekehrt = DynamischeTeamstaerke.Abweichungen(spiele.Reverse(), K, Ende);

        foreach (int id in new[] { 1, 2, 3 })
            umgekehrt[id].Should().BeApproximately(geordnet[id], 1e-9);
    }

    [Fact]
    public void SpaetereErgebnisse_WirkenNichtZurueck()
    {
        var spiele = new[] { Spiel(1, 1, 2, 3, 0), Spiel(2, 1, 3, 5, 0) };

        var vorSpiel2 = DynamischeTeamstaerke.Abweichungen(spiele, K, spiele[1].Schluessel);
        var nurErstes = DynamischeTeamstaerke.Abweichungen([spiele[0]], K, Ende);

        vorSpiel2[1].Should().Be(nurErstes[1]);
        vorSpiel2.Should().NotContainKey(3);
    }

    [Fact]
    public void EigenesSpiel_ZaehltNichtMit_AlsoRechnetEinNeuesSimulierenMitDemStandDavor()
    {
        var spiel = Spiel(1, 1, 2, 3, 0);

        DynamischeTeamstaerke.Abweichungen([spiel], K, spiel.Schluessel).Should().BeEmpty();
    }

    // ── K6 Wirkung ────────────────────────────────────────────────────────

    [Fact]
    public void Anwenden_OhneAbweichung_LaesstDieFormUnveraendert()
    {
        var form = new Turnierform(0.03, -0.05);

        DynamischeTeamstaerke.Anwenden(form, 0, 0).Should().Be(form);
    }

    [Fact]
    public void Anwenden_NachUeberraschungssieg_SteigtDieTorerwartungDesSiegersUndSinktDieDesVerlierers()
    {
        // Außenseiter (Start 1300) schlägt Favorit (1700) 2:0 → Abweichungen auf beiden Seiten.
        var abw = DynamischeTeamstaerke.Abweichungen([Spiel(1, 1, 2, 2, 0, heimStart: 1300, gastStart: 1700)], K, Ende);
        abw[1].Should().BePositive();
        abw[2].Should().BeNegative();

        // Im Folgespiel gegen einen Dritten (Abweichung 0): Sieger ×, Verlierer ÷.
        var form = DynamischeTeamstaerke.Anwenden(new Turnierform(0, 0), abw[1], 0);
        form.HeimFaktor.Should().BeGreaterThan(1.0);
        form.GastFaktor.Should().BeLessThan(1.0);

        var verlierer = DynamischeTeamstaerke.Anwenden(new Turnierform(0, 0), abw[2], 0);
        verlierer.HeimFaktor.Should().BeLessThan(1.0);
    }

    [Fact]
    public void Anwenden_AddiertDieAbweichungMitWirkungAufBeideSeiten()
    {
        double w = DynamischeTeamstaerke.WIRKUNG * DynamischeTeamstaerke.VOLLE_WIRKUNG;

        var form = DynamischeTeamstaerke.Anwenden(new Turnierform(0.1, -0.1), 40, -20);

        form.Heim.Should().BeApproximately(0.1 + w * 40, 1e-12);
        form.Gast.Should().BeApproximately(-0.1 - w * 20, 1e-12);
    }
}
