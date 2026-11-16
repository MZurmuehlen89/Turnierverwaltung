using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using static Fussballturnierverwaltung.Tests.Simulation.Ablauffolge;
using static Fussballturnierverwaltung.Tests.Simulation.SimulationsEngineTestdaten;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>
/// Ausführliche Simulation Minute für Minute (#361): Nachspielzeit, Tore nacheinander, Taktikwechsel, Karten,
/// Golden/Silver Goal. Die Zufallsfolge baut <see cref="Ablauffolge"/> auf; Heimteam λ = 1,95, Gastteam λ = 0,975,
/// ein Trefferwurf von 0 entscheidet für das Heimteam, ein Wert knapp unter 1 für das Gastteam.
/// </summary>
public class SpielablaufTests
{
    private const string KOPFBALL = "Tor per Kopfball (oben links)";

    private static SimulationsErgebnis Simulieren(VorgegebenerZufall zufall, params VerfahrenArt[] verfahren) =>
        SimulationsEngine.Simulieren(HeimTeam(), GastTeam(), VerfahrenKette(verfahren), ausfuehrlich: true, zufall);

    private static (int? HeimVerl, int? GastVerl, int? HeimElf, int? GastElf, EntscheidungArt Art,
                    IReadOnlyList<SimuliertesEreignis> Ereignisse, bool? LosGewinnerHeim)
        Anwenden(VorgegebenerZufall zufall, params VerfahrenArt[] verfahren) =>
        SimulationsEngine.EntscheidungsverfahrenAnwenden(
            HeimTeam(), GastTeam(), VerfahrenKette(verfahren), ausfuehrlich: true, zufall);

    private static IEnumerable<(int, int?, EreignisArt, bool, string)> Kurz(IEnumerable<SimuliertesEreignis> ereignisse) =>
        ereignisse.Select(e => (e.Minute, e.Nachspielminute, e.Art, e.IstHeim, e.Beschreibung));

    // ── Reguläre Spielzeit und Nachspielzeit (K5, K7) ─────────────────────

    [Fact]
    public void OhneTorUndKarte_NullZuNullOhneTaktikwechsel_LaengsteNachspielzeit()
    {
        var zufall = new Ablauffolge().Zufall();

        var e = Simulieren(zufall);

        (e.HeimToreRegulaer, e.AuswaertsToreRegulaer).Should().Be((0, 0));
        e.Ereignisse.Should().BeEmpty("ein Spiel, in dem nie ein Team zurückliegt, hat keinen Taktikwechsel");
        (e.HalbzeitHeimTore, e.HalbzeitAuswaertsTore).Should().Be(Stand(0, 0));
        zufall.Verbraucht.Should().Be((45 + 3 + 45 + 6) * 5 + 2, "fünf Würfe je Minute plus zwei Nachspielzeiten");
    }

    [Theory]
    [InlineData(JA, 1, 2)]
    [InlineData(NEIN, 3, 6)]
    public void Nachspielzeit_ErsteHalbzeitEinsBisDreiZweiteZweiBisSechs(double wert, int erste, int zweite)
    {
        var zufall = new Ablauffolge().Ruhig(45).Nachspielzeit(wert).Ruhig(erste).Ruhig(45).Nachspielzeit(wert).Zufall();

        Simulieren(zufall);

        zufall.Verbraucht.Should().Be((45 + erste + 45 + zweite) * 5 + 2);
    }

    [Theory]
    [InlineData(JA, 0)]
    [InlineData(NEIN, 2)]
    public void Nachspielzeit_VerlaengerungJeHaelfteNullBisZwei(double wert, int minuten)
    {
        var zufall = new Ablauffolge().Ruhig(15).Nachspielzeit(wert).Ruhig(minuten).Ruhig(15).Nachspielzeit(wert).Zufall();

        Anwenden(zufall, VerfahrenArt.VerlaengerungKlassisch);

        zufall.Verbraucht.Should().Be((30 + 2 * minuten) * 5 + 2);
    }

    [Fact]
    public void Nachspielzeit_TorInDerErstenHalbzeitTraegtMinute45UndZaehltZumHalbzeitstand()
    {
        var zufall = new Ablauffolge().Ruhig(45).Nachspielzeit(NEIN).Ruhig(2).MinuteMitTor(heim: true).Zufall();

        var e = Simulieren(zufall);

        Kurz(e.Ereignisse.Where(x => x.Art == EreignisArt.Tor))
            .Should().Equal((45, 3, EreignisArt.Tor, true, KOPFBALL));
        (e.HalbzeitHeimTore, e.HalbzeitAuswaertsTore).Should().Be(Stand(1, 0));
        (e.HeimToreRegulaer, e.AuswaertsToreRegulaer).Should().Be((1, 0));
    }

    [Fact]
    public void TorInMinute46_ZaehltNichtZumHalbzeitstand()
    {
        var zufall = new Ablauffolge().Ruhig(45).Nachspielzeit(JA).Ruhig(1).MinuteMitTor(heim: false).Zufall();

        var e = Simulieren(zufall);

        Kurz(e.Ereignisse.Where(x => x.Art == EreignisArt.Tor))
            .Should().Equal((46, null, EreignisArt.Tor, false, KOPFBALL));
        (e.HalbzeitHeimTore, e.HalbzeitAuswaertsTore).Should().Be(Stand(0, 0));
    }

    [Theory]
    [InlineData(0.0, 0.149, 0.9999, "Tor per Kopfball nach Konter (unten rechts)")]
    [InlineData(0.9999, 0.151, 0.0, "Tor per Direktabnahme (oben links)")]
    public void Torbeschreibung_StilKonterUndZone(double stil, double konter, double zone, string erwartet)
    {
        var zufall = new Ablauffolge().Hinzu(JA, JA, stil, konter, zone).Zufall();

        Simulieren(zufall).Ereignisse.Should().Contain(x => x.Art == EreignisArt.Tor)
            .Which.Beschreibung.Should().Be(erwartet);
    }

    // ── Taktikwechsel und Tore nacheinander (K3, K4, K5, K8) ──────────────

    [Fact]
    public void Rueckstand_TaktikwechselJeStufe()
    {
        // 1:0 in Minute 1, danach fällt nichts mehr.
        var e = Simulieren(new Ablauffolge().MinuteMitTor(heim: true).Zufall());

        Kurz(e.Ereignisse.Where(x => x.Art == EreignisArt.Taktikwechsel)).Should().Equal(
            (60, null, EreignisArt.Taktikwechsel, false, "Stellt auf Offensive um"),
            (70, null, EreignisArt.Taktikwechsel, true, "Zieht sich zurück und verteidigt die Führung"),
            (80, null, EreignisArt.Taktikwechsel, false, "Wirft alles nach vorn"),
            (88, null, EreignisArt.Taktikwechsel, false, "Torwart geht mit nach vorn"));
    }

    [Fact]
    public void ZweiToreInEinerMinute_DasZweiteRechnetMitDemNeuenStand()
    {
        // Minute 86: Heimtor, beide Teams stellen um, Ausgleich, beide kehren zur gewohnten Ordnung zurück.
        var zufall = new Ablauffolge()
            .Ruhig(45).Nachspielzeit(JA).Ruhig(1).Ruhig(40)
            .Tor(heim: true).Tor(heim: false).KeinTor().Karten()
            .Zufall();

        var e = Simulieren(zufall);

        Kurz(e.Ereignisse).Should().Equal(
            (86, null, EreignisArt.Tor, true, KOPFBALL),
            (86, null, EreignisArt.Taktikwechsel, true, "Zieht sich zurück und verteidigt die Führung"),
            (86, null, EreignisArt.Taktikwechsel, false, "Wirft alles nach vorn"),
            (86, null, EreignisArt.Tor, false, KOPFBALL),
            (86, null, EreignisArt.Taktikwechsel, true, "Kehrt zur gewohnten Ordnung zurück"),
            (86, null, EreignisArt.Taktikwechsel, false, "Kehrt zur gewohnten Ordnung zurück"));
        (e.HeimToreRegulaer, e.AuswaertsToreRegulaer).Should().Be((1, 1));
    }

    [Theory]
    [InlineData(true, "Tor ins leere Tor")]
    [InlineData(false, KOPFBALL)]
    public void Brechstange_EinTeilDerGegentoreGehtInsLeereTor(bool insLeereTor, string erwartet)
    {
        // 1:0 in Minute 1; ab der 88. Minute steht der Torhüter des Gasts mit vorn, in Minute 89 trifft das Heimteam.
        var folge = new Ablauffolge()
            .MinuteMitTor(heim: true).Ruhig(44).Nachspielzeit(JA).Ruhig(1).Ruhig(43)
            .Hinzu(JA, JA, insLeereTor ? JA : NEIN);
        if (!insLeereTor)
            folge.Hinzu(TorstilOhneKonter);

        var e = Simulieren(folge.Zufall());

        e.Ereignisse.Last(x => x.Art == EreignisArt.Tor)
            .Should().Be(new SimuliertesEreignis(89, EreignisArt.Tor, true, erwartet));
    }

    // ── Karten (K1, K6) ───────────────────────────────────────────────────

    [Fact]
    public void Karten_JeMinuteGelbUndRotJeTeam()
    {
        var e = Simulieren(new Ablauffolge().KeinTor().Karten(gelbHeim: true, rotGast: true).Zufall());

        Kurz(e.Ereignisse).Should().Equal(
            (1, null, EreignisArt.GelbeKarte, true, "Gelbe Karte"),
            (1, null, EreignisArt.RoteKarte, false, "Rote Karte"));
    }

    [Theory]
    // Wer trifft, folgt dem Anteil an der Torerwartung: 11 gegen 11 hat das Heimteam 1,95 / 2,925 = 0,667, nach
    // seiner Roten Karte 1,365 / (1,365 + 1,17) = 0,538. Ein Wurf von 0,6 trennt beide Fälle.
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void RoteKarte_WirktAbDerFolgeminute(bool karteInMinute1, bool heimTrifftInMinute2)
    {
        var e = Simulieren(new Ablauffolge()
            .KeinTor().Karten(rotHeim: karteInMinute1)
            .Hinzu(JA, 0.6).Hinzu(TorstilOhneKonter).KeinTor().Karten()
            .Zufall());

        e.Ereignisse.Single(x => x.Art == EreignisArt.Tor).Should().Be(
            new SimuliertesEreignis(2, EreignisArt.Tor, heimTrifftInMinute2, KOPFBALL));
    }

    [Fact]
    public void RoteKarte_TorDerselbenMinuteFaelltNochInGleichzahl()
    {
        // Die Tore einer Minute fallen vor ihren Karten: trotz Roter Karte in Minute 1 trifft das Heimteam dort mit 0,6.
        var e = Simulieren(new Ablauffolge()
            .Hinzu(JA, 0.6).Hinzu(TorstilOhneKonter).KeinTor().Karten(rotHeim: true)
            .Zufall());

        Kurz(e.Ereignisse.Where(x => x.Art is EreignisArt.Tor or EreignisArt.RoteKarte)).Should().Equal(
            (1, null, EreignisArt.Tor, true, KOPFBALL),
            (1, null, EreignisArt.RoteKarte, true, "Rote Karte"));
    }

    [Fact]
    public void Karten_HoechstensVierPlatzverweiseJeTeam()
    {
        var folge = new Ablauffolge();
        for (int minute = 0; minute < 6; minute++)
            folge.KeinTor().Karten(rotHeim: true, rotGast: true);

        var e = Simulieren(folge.Zufall());

        e.Ereignisse.Count(x => x.Art == EreignisArt.RoteKarte && x.IstHeim).Should().Be(4);
        e.Ereignisse.Count(x => x.Art == EreignisArt.RoteKarte && !x.IstHeim).Should().Be(4);
    }

    // ── Golden Goal und Silver Goal (K8) ──────────────────────────────────

    [Fact]
    public void GoldenGoal_TorschuetzeNachAnteilAnDerTorerwartung_NichtPerLos()
    {
        // Regression #361: der alte Ablauf ließ hier beide treffen und lieferte per Los (0,6) das Gastteam.
        var zufall = new VorgegebenerZufall([JA, JA, 0.6]);

        var e = Anwenden(zufall, VerfahrenArt.GoldenGoal);

        e.Art.Should().Be(EntscheidungArt.GoldenGoal);
        (e.HeimVerl, e.GastVerl).Should().Be(Stand(1, 0));
        e.Ereignisse.Should().ContainSingle()
            .Which.Should().Be(new SimuliertesEreignis(91, EreignisArt.GoldenGoal, true, "Golden Goal"));
        zufall.Verbraucht.Should().Be(2, "nach dem ersten Tor wird nicht weitergespielt");
    }

    [Fact]
    public void GoldenGoal_GastTrifftInDerNachspielzeitDerErstenHaelfte()
    {
        var zufall = new Ablauffolge().Ruhig(15).Nachspielzeit(NEIN).Ruhig(1).GoldenGoal(heim: false).Zufall(danach: null);

        var e = Anwenden(zufall, VerfahrenArt.GoldenGoal);

        (e.HeimVerl, e.GastVerl).Should().Be(Stand(0, 1));
        e.Ereignisse.Should().ContainSingle()
            .Which.Should().Be(new SimuliertesEreignis(105, EreignisArt.GoldenGoal, false, "Golden Goal", 2));
    }

    [Fact]
    public void GoldenGoal_OhneTor_FolgeverfahrenEntscheidet()
    {
        var zufall = new Ablauffolge().Zufall();

        var e = Anwenden(zufall, VerfahrenArt.GoldenGoal, VerfahrenArt.Losentscheid);

        e.Art.Should().Be(EntscheidungArt.Losentscheid);
        (e.HeimVerl, e.GastVerl).Should().Be(Stand(0, 0));
        e.Ereignisse.Should().BeEmpty();
        zufall.Verbraucht.Should().Be((30 + 2 + 2) * 5 + 2 + 1, "volle Verlängerung, dann der Losentscheid");
    }

    [Fact]
    public void GoldenGoal_NieZweiTore_StaerkeresTeamGewinntDeutlichOefter()
    {
        var stark = Team(1, off: 85, mid: 85, def: 85);
        var schwach = Team(2, off: 60, mid: 60, def: 60);
        var rng = new Random(361);
        int entschieden = 0, starkGewinnt = 0;

        for (int i = 0; i < 3000; i++)
        {
            var e = SimulationsEngine.EntscheidungsverfahrenAnwenden(
                stark, schwach, VerfahrenKette(VerfahrenArt.GoldenGoal), ausfuehrlich: true, rng, Spielortvorteil.Neutral);
            ((e.HeimVerl ?? 0) + (e.GastVerl ?? 0)).Should().BeLessThanOrEqualTo(1);
            e.Ereignisse.Count(x => x.Art == EreignisArt.GoldenGoal).Should().BeLessThanOrEqualTo(1);
            if (e.HeimVerl + e.GastVerl == 1) entschieden++;
            if (e.HeimVerl == 1) starkGewinnt++;
        }

        ((double)starkGewinnt / entschieden).Should().BeGreaterThan(0.7);
    }

    [Fact]
    public void SilverGoal_FuehrungNachErsterHaelfte_SpielEndet()
    {
        var zufall = new Ablauffolge().MinuteMitTor(heim: true).Zufall();

        var e = Anwenden(zufall, VerfahrenArt.SilverGoal, VerfahrenArt.Elfmeterschiessen);

        e.Art.Should().Be(EntscheidungArt.SilverGoal);
        (e.HeimVerl, e.GastVerl).Should().Be(Stand(1, 0));
        e.HeimElf.Should().BeNull();
        e.Ereignisse.Should().AllSatisfy(x => x.Minute.Should().BeLessThanOrEqualTo(105));
    }

    [Fact]
    public void SilverGoal_ZweiteHaelfteEntscheidet_IstVerlaengerungMitSummeBeiderHaelften()
    {
        var zufall = new Ablauffolge()
            .MinuteMitTor(heim: true).Ruhig(13).MinuteMitTor(heim: false).Nachspielzeit(JA)
            .MinuteMitTor(heim: false)
            .Zufall();

        var e = Anwenden(zufall, VerfahrenArt.SilverGoal, VerfahrenArt.Elfmeterschiessen);

        e.Art.Should().Be(EntscheidungArt.Verlaengerung);
        (e.HeimVerl, e.GastVerl).Should().Be(Stand(1, 2));
        e.Ereignisse.Where(x => x.Art == EreignisArt.Tor).Select(x => (x.Minute, x.IstHeim))
            .Should().Equal((91, true), (105, false), (106, false));
    }

    [Fact]
    public void Verlaengerung_EreignisseFolgenDenenDerSpielzeit()
    {
        // 0:0 nach 90 Minuten, dann trifft das Heimteam in Minute 91 per Golden Goal.
        var zufall = new Ablauffolge()
            .Ruhig(45).Nachspielzeit(JA).Ruhig(1).Ruhig(45).Nachspielzeit(JA).Ruhig(2)
            .GoldenGoal(heim: true)
            .Zufall(danach: null);

        var e = Simulieren(zufall, VerfahrenArt.GoldenGoal);

        e.EntscheidungArt.Should().Be(EntscheidungArt.GoldenGoal);
        e.Ereignisse.Should().ContainSingle()
            .Which.Should().Be(new SimuliertesEreignis(91, EreignisArt.GoldenGoal, true, "Golden Goal"));
        (e.HalbzeitHeimTore, e.HalbzeitAuswaertsTore).Should().Be(Stand(0, 0));
    }

    // ── Monte Carlo (K1, K2, K4, K6) ──────────────────────────────────────

    [Fact]
    public void RoteKarte_TeamTrifftDanachDeutlichSeltener()
    {
        double ohneKarte = 0, nachKarte = 0;
        int spieleOhne = 0, minutenNach = 0;
        var rng = new Random(361);

        for (int i = 0; i < 20000; i++)
        {
            var e = SimulationsEngine.Simulieren(Team(1), Team(2), [], ausfuehrlich: true, rng);
            var rot = e.Ereignisse.FirstOrDefault(x => x.Art == EreignisArt.RoteKarte && x.IstHeim);
            if (rot is null)
            {
                spieleOhne++;
                ohneKarte += e.HeimToreRegulaer;
            }
            else if (rot.Minute <= 30)
            {
                minutenNach += 90 - rot.Minute;
                nachKarte += e.Ereignisse.Count(x => x.Art == EreignisArt.Tor && x.IstHeim && x.Minute > rot.Minute);
            }
        }

        double rateOhne = ohneKarte / (spieleOhne * 90.0);
        double rateNach = nachKarte / minutenNach;
        rateNach.Should().BeLessThan(rateOhne * 0.85);
    }

    [Fact]
    public void Verlaengerung_UebernimmtPlatzverweiseDesVorausgehendenSpiels()
    {
        var rng = new Random(361);
        int heimOhne = 0, gastOhne = 0, heimMit = 0, gastMit = 0;
        var verlaengerung = VerfahrenKette(VerfahrenArt.VerlaengerungKlassisch);

        for (int i = 0; i < 4000; i++)
        {
            var ohne = SimulationsEngine.EntscheidungsverfahrenAnwenden(Team(1), Team(2), verlaengerung, true, rng);
            var mit = SimulationsEngine.EntscheidungsverfahrenAnwenden(
                Team(1), Team(2), verlaengerung, true, rng, platzverweise: new Platzverweise(1, 0));
            heimOhne += ohne.HeimVerl!.Value; gastOhne += ohne.GastVerl!.Value;
            heimMit  += mit.HeimVerl!.Value;  gastMit  += mit.GastVerl!.Value;
        }

        heimMit.Should().BeLessThan((int)(heimOhne * 0.85));
        gastMit.Should().BeGreaterThan((int)(gastOhne * 1.1));
    }

    [Fact]
    public void Nachspielzeit_BeiKnappemRueckstandFallenMehrToreAlsBeiGleichstand()
    {
        var rng = new Random(361);
        int knapp = 0, toreKnapp = 0, gleich = 0, toreGleich = 0, insLeereTor = 0;

        for (int i = 0; i < 20000; i++)
        {
            var e = SimulationsEngine.Simulieren(Team(1), Team(2), [], ausfuehrlich: true, rng);
            var tore = e.Ereignisse.Where(x => x.Art == EreignisArt.Tor).ToList();
            static bool inNachspielzeit(SimuliertesEreignis x) => x.Minute == 90 && x.Nachspielminute is not null;
            int differenz = tore.Where(x => !inNachspielzeit(x)).Sum(x => x.IstHeim ? 1 : -1);
            int spaete = tore.Count(inNachspielzeit);
            insLeereTor += tore.Count(x => x.Beschreibung == "Tor ins leere Tor");

            if (Math.Abs(differenz) == 1) { knapp++; toreKnapp += spaete; }
            else if (differenz == 0) { gleich++; toreGleich += spaete; }
        }

        ((double)toreKnapp / knapp).Should().BeGreaterThan(1.15 * toreGleich / gleich);
        insLeereTor.Should().BePositive();
    }

    [Fact]
    public void Verlaengerung_MehrKartenUndLeichtWenigerToreJeMinuteAlsRegulaer()
    {
        // Spielminuten: regulär 90 + im Mittel 2 + 4 Nachspielzeit, Verlängerung 30 + 1 + 1.
        var rng = new Random(361);
        var verlaengerung = VerfahrenKette(VerfahrenArt.VerlaengerungKlassisch);
        int toreRegulaer = 0, kartenRegulaer = 0, toreVerl = 0, kartenVerl = 0;
        const int SPIELE = 6000;

        for (int i = 0; i < SPIELE; i++)
        {
            var r = SimulationsEngine.Simulieren(Team(1), Team(2), [], ausfuehrlich: true, rng);
            toreRegulaer += r.HeimToreRegulaer + r.AuswaertsToreRegulaer;
            kartenRegulaer += r.Ereignisse.Count(IstKarte);
            var v = SimulationsEngine.EntscheidungsverfahrenAnwenden(Team(1), Team(2), verlaengerung, true, rng);
            toreVerl += v.HeimVerl!.Value + v.GastVerl!.Value;
            kartenVerl += v.Ereignisse.Count(IstKarte);
        }

        double kartenJeMinRegulaer = kartenRegulaer / 96.0, kartenJeMinVerl = kartenVerl / 32.0;
        double toreJeMinRegulaer   = toreRegulaer / 96.0,   toreJeMinVerl   = toreVerl / 32.0;
        kartenJeMinVerl.Should().BeGreaterThan(kartenJeMinRegulaer * 1.3);
        toreJeMinVerl.Should().BeInRange(toreJeMinRegulaer * 0.6, toreJeMinRegulaer * 0.97);
    }

    [Fact]
    public void Karten_ImMittelZweiEinhalbGelbeUndGutEinZehntelRoteJeTeam()
    {
        var rng = new Random(361);
        const int SPIELE = 4000;
        var ereignisse = Enumerable.Range(0, SPIELE)
            .SelectMany(_ => SimulationsEngine.Simulieren(Team(1), Team(2), [], ausfuehrlich: true, rng).Ereignisse)
            .ToList();

        (ereignisse.Count(x => x.Art == EreignisArt.GelbeKarte) / (2.0 * SPIELE)).Should().BeInRange(2.3, 2.7);
        (ereignisse.Count(x => x.Art == EreignisArt.RoteKarte) / (2.0 * SPIELE)).Should().BeInRange(0.11, 0.19);
    }

    // ── Reproduzierbarkeit (K11) ──────────────────────────────────────────

    [Fact]
    public void GleicherSeed_GleichesErgebnisSamtAllerEreignisse()
    {
        var kette = VerfahrenKette(VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Elfmeterschiessen);
        Mannschaft heim = Team(1, off: 80), gast = Team(2, def: 80);

        for (int seed = 0; seed < 50; seed++)
        {
            var a = SimulationsEngine.Simulieren(heim, gast, kette, ausfuehrlich: true, new Random(seed));
            var b = SimulationsEngine.Simulieren(heim, gast, kette, ausfuehrlich: true, new Random(seed));

            b.Should().BeEquivalentTo(a, o => o.WithStrictOrdering());
        }
    }

    private static bool IstKarte(SimuliertesEreignis e) => e.Art is EreignisArt.GelbeKarte or EreignisArt.RoteKarte;
}
