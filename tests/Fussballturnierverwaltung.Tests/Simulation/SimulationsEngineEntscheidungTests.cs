using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using static Fussballturnierverwaltung.Tests.Simulation.SimulationsEngineTestdaten;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>
/// Entscheidungsverfahren bei Gleichstand (#615): Verlängerung, Golden Goal, Silver Goal, Elfmeterschießen, Los.
/// Die Zufallsfolge ist vorgegeben (<see cref="VorgegebenerZufall"/>), damit jeder Zweig gezielt erreicht wird.
/// Poisson-Ziehung (Knuth): Werte werden multipliziert, bis das Produkt e^-λ unterschreitet; die Anzahl der Werte
/// minus eins ist die Torzahl. Ein erster Wert knapp über bzw. unter e^-λ legt damit die Torerwartung fest.
/// </summary>
public class SimulationsEngineEntscheidungTests
{
    private const double TRIFFT = 0.0;
    private const double VERSCHIESST = 0.99;

    private static (int? HeimVerl, int? GastVerl, int? HeimElf, int? GastElf, EntscheidungArt Art,
                    IReadOnlyList<SimuliertesEreignis> Ereignisse, bool? LosGewinnerHeim)
        Anwenden(VorgegebenerZufall zufall, bool ausfuehrlich, params VerfahrenArt[] arten) =>
        Anwenden(zufall, ausfuehrlich, HeimTeam(), GastTeam(), arten);

    private static (int? HeimVerl, int? GastVerl, int? HeimElf, int? GastElf, EntscheidungArt Art,
                    IReadOnlyList<SimuliertesEreignis> Ereignisse, bool? LosGewinnerHeim)
        Anwenden(VorgegebenerZufall zufall, bool ausfuehrlich, Mannschaft heim, Mannschaft gast, params VerfahrenArt[] arten) =>
        SimulationsEngine.EntscheidungsverfahrenAnwenden(
            heim, gast, VerfahrenKette(arten), ausfuehrlich, zufall);

    // ── Reihenfolge der Verfahren ─────────────────────────────────────────

    [Fact]
    public void Verfahren_LaufenNachReihenfolgeNichtNachListenposition()
    {
        // Losentscheid hat Rang 1, steht aber hinten in der Liste — er entscheidet, das Elfmeterschießen läuft nicht.
        EntscheidungsVerfahren[] verfahren =
        [
            Verfahren(VerfahrenArt.Elfmeterschiessen, rang: 2),
            Verfahren(VerfahrenArt.Losentscheid, rang: 1)
        ];

        var e = SimulationsEngine.EntscheidungsverfahrenAnwenden(
            HeimTeam(), GastTeam(), verfahren, false, new VorgegebenerZufall([0.0]));

        e.Art.Should().Be(EntscheidungArt.Losentscheid);
        e.HeimElf.Should().BeNull();
    }

    // ── Los und Münzwurf ──────────────────────────────────────────────────

    [Theory]
    [InlineData(VerfahrenArt.Losentscheid, EntscheidungArt.Losentscheid, 0.0, true)]
    [InlineData(VerfahrenArt.Losentscheid, EntscheidungArt.Losentscheid, 0.6, false)]
    [InlineData(VerfahrenArt.Muenzwurf, EntscheidungArt.Muenzwurf, 0.0, true)]
    [InlineData(VerfahrenArt.Muenzwurf, EntscheidungArt.Muenzwurf, 0.6, false)]
    public void LosOderMuenze_ErsteSeiteGewinntHeim(
        VerfahrenArt verfahren, EntscheidungArt erwartet, double wurf, bool heimGewinnt)
    {
        var e = Anwenden(new VorgegebenerZufall([wurf]), true, verfahren);

        e.Art.Should().Be(erwartet);
        e.LosGewinnerHeim.Should().Be(heimGewinnt);
        e.Ereignisse.Should().BeEmpty();
        e.HeimVerl.Should().BeNull();
        e.HeimElf.Should().BeNull();
    }

    // ── Verlängerung (30 Minuten, λ · 30/90) ─────────────────────────────
    // Heim λ = 0,65 → e^-λ = 0,522; Gast λ = 0,325 → e^-λ = 0,723.

    [Fact]
    public void Verlaengerung_HeimtorEntscheidet()
    {
        // Gastwert 0,71 knapp unter e^-λ: mit der Heimstärke des Gasts (λ = 0,65) fiele ein Tor.
        var zufall = new VorgegebenerZufall([0.53, 0.0, /* Gast */ 0.71]);

        var e = Anwenden(zufall, false, VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Elfmeterschiessen);

        e.Art.Should().Be(EntscheidungArt.Verlaengerung);
        (e.HeimVerl, e.GastVerl).Should().Be(Stand(1, 0));
        e.HeimElf.Should().BeNull("ein Sieger in der Verlängerung beendet die Kette");
        e.Ereignisse.Should().BeEmpty();
    }

    [Fact]
    public void Verlaengerung_GasttorEntscheidet()
    {
        var zufall = new VorgegebenerZufall([0.51, /* Gast */ 0.73, 0.0]);

        var e = Anwenden(zufall, false, VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Elfmeterschiessen);

        e.Art.Should().Be(EntscheidungArt.Verlaengerung);
        (e.HeimVerl, e.GastVerl).Should().Be(Stand(0, 1));
        e.HeimElf.Should().BeNull();
    }

    [Fact]
    public void Verlaengerung_Torgleich_ElfmeterschiessenFolgtUndVerlaengerungstoreBleiben()
    {
        double[] elfmeter = [.. Enumerable.Repeat(TRIFFT, 10), TRIFFT, VERSCHIESST];
        var zufall = new VorgegebenerZufall([0.53, 0.0, /* Gast */ 0.73, 0.0, .. elfmeter]);

        var e = Anwenden(zufall, false, VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Elfmeterschiessen);

        e.Art.Should().Be(EntscheidungArt.Elfmeterschiessen);
        (e.HeimVerl, e.GastVerl).Should().Be(Stand(1, 1));
        (e.HeimElf, e.GastElf).Should().Be(Stand(6, 5));
    }

    // ── Golden Goal der Schnellsimulation (je Minute 91–120 Torchance λ/90) ──
    // Heim 1,95/90 = 0,02167; Gast 0,975/90 = 0,01083. Ausführlich (Minute für Minute, #361): SpielablaufTests.

    [Fact]
    public void GoldenGoal_HeimTrifft_SpielEndetSofort()
    {
        // Minute 91: beide knapp über ihrer Torchance; Minute 92: Heim knapp darunter. Weitere Werte würden treffen.
        var zufall = new VorgegebenerZufall([0.0217, 0.0109, 0.0216, 0.5, TRIFFT, TRIFFT, TRIFFT, TRIFFT]);

        var e = Anwenden(zufall, false, VerfahrenArt.GoldenGoal);

        e.Art.Should().Be(EntscheidungArt.GoldenGoal);
        (e.HeimVerl, e.GastVerl).Should().Be(Stand(1, 0));
        zufall.Verbraucht.Should().Be(4, "nach dem ersten Tor wird nicht weitergespielt");
    }

    [Fact]
    public void GoldenGoal_GastTrifft()
    {
        var zufall = new VorgegebenerZufall([0.5, 0.0108]);

        var e = Anwenden(zufall, false, VerfahrenArt.GoldenGoal);

        (e.HeimVerl, e.GastVerl).Should().Be(Stand(0, 1));
    }

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(0.6, false)]
    public void GoldenGoal_BeideTreffenInDerselbenMinute_LosEntscheidet(double los, bool heimTrifft)
    {
        // #629: bis dahin zählte immer das Heimtor. Nur noch in der Schnellsimulation – ausführlich würfelt der
        // Ablauf seit #361 nach dem Anteil an der Torerwartung.
        var e = Anwenden(new VorgegebenerZufall([TRIFFT, TRIFFT, los]), false, VerfahrenArt.GoldenGoal);

        (e.HeimVerl, e.GastVerl).Should().Be(heimTrifft ? Stand(1, 0) : Stand(0, 1));
    }

    [Fact]
    public void GoldenGoal_OhneTorIn30Minuten_FolgeverfahrenEntscheidet()
    {
        double[] dreissigMinutenOhneTor = [.. Enumerable.Repeat(0.5, 60)];
        var zufall = new VorgegebenerZufall([.. dreissigMinutenOhneTor, /* Los */ 0.0]);

        var e = Anwenden(zufall, false, VerfahrenArt.GoldenGoal, VerfahrenArt.Losentscheid);

        e.Art.Should().Be(EntscheidungArt.Losentscheid);
        (e.HeimVerl, e.GastVerl).Should().Be(Stand(0, 0));
        e.Ereignisse.Should().BeEmpty();
        zufall.Verbraucht.Should().Be(61, "je Minute 91–120 zwei Torchancen, dann der Losentscheid");
    }

    [Fact]
    public void GoldenGoal_SchnellSimulation_OhneEreignis()
    {
        var e = Anwenden(new VorgegebenerZufall([TRIFFT, 0.5]), false, VerfahrenArt.GoldenGoal);

        (e.HeimVerl, e.GastVerl).Should().Be(Stand(1, 0));
        e.Ereignisse.Should().BeEmpty();
    }

    // ── Silver Goal (zwei Hälften à 15 Minuten, λ · 15/90) ────────────────
    // Heim λ = 0,325 → e^-λ = 0,723; Gast λ = 0,1625 → e^-λ = 0,850.

    [Fact]
    public void SilverGoal_FuehrungNachErsterHaelfte_SpielEndet()
    {
        var zufall = new VorgegebenerZufall([0.73, 0.0, /* Gast */ 0.0]);

        var e = Anwenden(zufall, false, VerfahrenArt.SilverGoal, VerfahrenArt.Elfmeterschiessen);

        e.Art.Should().Be(EntscheidungArt.SilverGoal);
        (e.HeimVerl, e.GastVerl).Should().Be(Stand(1, 0));
        zufall.Verbraucht.Should().Be(3, "die zweite Hälfte wird nicht gespielt");
    }

    [Fact]
    public void SilverGoal_GastFuehrtNachErsterHaelfte()
    {
        var zufall = new VorgegebenerZufall([0.71, /* Gast */ 0.86, 0.0]);

        var e = Anwenden(zufall, false, VerfahrenArt.SilverGoal);

        e.Art.Should().Be(EntscheidungArt.SilverGoal);
        (e.HeimVerl, e.GastVerl).Should().Be(Stand(0, 1));
    }

    [Fact]
    public void SilverGoal_ZweiteHaelfteEntscheidet_IstVerlaengerungMitSummeBeiderHaelften()
    {
        // 1:1 nach der ersten, 0:1 in der zweiten Hälfte → 1:2 nach Verlängerung.
        var zufall = new VorgegebenerZufall([0.73, 0.0, 0.86, 0.0, /* zweite Hälfte */ 0.71, 0.86, 0.0]);

        var e = Anwenden(zufall, false, VerfahrenArt.SilverGoal, VerfahrenArt.Elfmeterschiessen);

        e.Art.Should().Be(EntscheidungArt.Verlaengerung);
        (e.HeimVerl, e.GastVerl).Should().Be(Stand(1, 2));
        e.HeimElf.Should().BeNull();
    }

    [Fact]
    public void SilverGoal_ZweiteHaelfteHeimtor()
    {
        var zufall = new VorgegebenerZufall([0.0, 0.0, /* zweite Hälfte */ 0.73, 0.0, 0.84]);

        var e = Anwenden(zufall, false, VerfahrenArt.SilverGoal);

        e.Art.Should().Be(EntscheidungArt.Verlaengerung);
        (e.HeimVerl, e.GastVerl).Should().Be(Stand(1, 0));
    }

    [Fact]
    public void SilverGoal_BeideHaelftenTorgleich_ElfmeterschiessenFolgt()
    {
        double[] elfmeter = [.. Enumerable.Repeat(TRIFFT, 10), VERSCHIESST, TRIFFT];
        var zufall = new VorgegebenerZufall([0.73, 0.0, 0.86, 0.0, /* zweite Hälfte */ 0.0, 0.0, .. elfmeter]);

        var e = Anwenden(zufall, false, VerfahrenArt.SilverGoal, VerfahrenArt.Elfmeterschiessen);

        e.Art.Should().Be(EntscheidungArt.Elfmeterschiessen);
        (e.HeimVerl, e.GastVerl).Should().Be(Stand(1, 1));
        (e.HeimElf, e.GastElf).Should().Be(Stand(5, 6));
    }

    // ── Elfmeterschießen ──────────────────────────────────────────────────

    [Theory]
    [InlineData(80, 0.749, true)]   // 73 % + (80 − 70) · 0,2 % = 75 %
    [InlineData(80, 0.751, false)]
    [InlineData(60, 0.709, true)]   // 73 % − 10 · 0,2 % = 71 %
    [InlineData(60, 0.711, false)]
    // Deckel 50–92 %: bei gültigen Ratings (1–100, MannschaftValidator) nicht erreichbar, schützt nur vor
    // ungültigen Daten – daher Ratings außerhalb des Bereichs.
    [InlineData(200, 0.919, true)]
    [InlineData(200, 0.921, false)]
    [InlineData(-200, 0.499, true)]
    [InlineData(-200, 0.501, false)]
    public void Elfmeter_HeimSchiesstMitEigenemRating(int offensiv, double wurf, bool trifft)
    {
        // Erster Heimschuss mit dem Wurf, danach treffen alle bis zum Fehlschuss des Gasts im Sudden Death.
        double[] folge = [wurf, .. Enumerable.Repeat(TRIFFT, 10), VERSCHIESST];

        var e = Anwenden(new VorgegebenerZufall(folge), true, Team(1, off: offensiv), Team(2, off: 70),
            VerfahrenArt.Elfmeterschiessen);

        e.Ereignisse[0].Art.Should().Be(trifft ? EreignisArt.ElfmeterTor : EreignisArt.ElfmeterVerschossen);
    }

    [Theory]
    [InlineData(80, 0.749, true)]
    [InlineData(80, 0.751, false)]
    [InlineData(60, 0.709, true)]
    [InlineData(60, 0.711, false)]
    public void Elfmeter_GastSchiesstMitEigenemRating(int offensiv, double wurf, bool trifft)
    {
        var heim = Team(1, off: 70);
        var gast = Team(2, off: offensiv);
        double[] folge = [TRIFFT, wurf, .. Enumerable.Repeat(TRIFFT, 8), TRIFFT, VERSCHIESST];

        var e = Anwenden(new VorgegebenerZufall(folge), true, heim, gast, VerfahrenArt.Elfmeterschiessen);

        e.Ereignisse[1].Art.Should().Be(trifft ? EreignisArt.ElfmeterTor : EreignisArt.ElfmeterVerschossen);
    }

    [Theory]
    // Heim 3:0 nach drei Runden – der Gast kann mit zwei Schüssen nicht mehr ausgleichen.
    [InlineData(new[] { TRIFFT, VERSCHIESST, TRIFFT, VERSCHIESST, TRIFFT, VERSCHIESST }, 3, 0)]
    // Heim 4:1 nach dem vierten Heimschuss – entschieden, bevor der Gast zum vierten Mal antritt.
    [InlineData(new[] { TRIFFT, TRIFFT, TRIFFT, VERSCHIESST, TRIFFT, VERSCHIESST, TRIFFT }, 4, 1)]
    // Gast 3:0 nach drei Runden.
    [InlineData(new[] { VERSCHIESST, TRIFFT, VERSCHIESST, TRIFFT, VERSCHIESST, TRIFFT }, 0, 3)]
    // Gast 3:1 – der vierte Heim-Fehlschuss entscheidet, der Gast muss nicht mehr antreten.
    [InlineData(new[] { TRIFFT, TRIFFT, VERSCHIESST, TRIFFT, VERSCHIESST, TRIFFT, VERSCHIESST }, 1, 3)]
    // 5:4 nach dem fünften Heimschuss – der Gast kann mit dem letzten Schuss noch ausgleichen und muss antreten.
    [InlineData(new[] { TRIFFT, TRIFFT, TRIFFT, TRIFFT, TRIFFT, TRIFFT, TRIFFT, TRIFFT, TRIFFT, VERSCHIESST }, 5, 4)]
    // 5:3 nach dem fünften Heimschuss – der Gast kann mit einem Schuss nicht mehr ausgleichen.
    [InlineData(new[] { TRIFFT, TRIFFT, TRIFFT, TRIFFT, TRIFFT, TRIFFT, TRIFFT, VERSCHIESST, TRIFFT }, 5, 3)]
    public void Elfmeter_EndetSobaldRechnerischEntschieden(double[] schuesse, int heimTore, int gastTore)
    {
        var zufall = new VorgegebenerZufall(schuesse);

        var e = Anwenden(zufall, true, VerfahrenArt.Elfmeterschiessen);

        (e.HeimElf, e.GastElf).Should().Be(Stand(heimTore, gastTore));
        e.Ereignisse.Should().HaveCount(schuesse.Length, "kein Schuss nach der Entscheidung");
        zufall.Verbraucht.Should().Be(schuesse.Length);
    }

    [Fact]
    public void Elfmeter_NachFuenfRundenGleichstand_SuddenDeathBisZurEntscheidung()
    {
        double[] schuesse = [.. Enumerable.Repeat(TRIFFT, 12), TRIFFT, VERSCHIESST];

        var e = Anwenden(new VorgegebenerZufall(schuesse), true, VerfahrenArt.Elfmeterschiessen);

        e.Art.Should().Be(EntscheidungArt.Elfmeterschiessen);
        (e.HeimElf, e.GastElf).Should().Be(Stand(7, 6));
        e.LosGewinnerHeim.Should().BeNull();
    }

    [Fact]
    public void Elfmeter_GastGewinntImSuddenDeath()
    {
        double[] schuesse = [.. Enumerable.Repeat(TRIFFT, 10), VERSCHIESST, TRIFFT];

        var e = Anwenden(new VorgegebenerZufall(schuesse), false, VerfahrenArt.Elfmeterschiessen);

        (e.HeimElf, e.GastElf).Should().Be(Stand(5, 6));
        e.HeimVerl.Should().BeNull();
        e.Ereignisse.Should().BeEmpty();
    }

    [Fact]
    public void Elfmeter_Ausfuehrlich_JederSchussEinEreignisAbMinute121()
    {
        double[] schuesse = [TRIFFT, VERSCHIESST, TRIFFT, VERSCHIESST, TRIFFT, VERSCHIESST];

        var e = Anwenden(new VorgegebenerZufall(schuesse), true, VerfahrenArt.Elfmeterschiessen);

        e.Ereignisse.Should().Equal(
            new SimuliertesEreignis(121, EreignisArt.ElfmeterTor, true, "Elfmeter verwandelt"),
            new SimuliertesEreignis(122, EreignisArt.ElfmeterVerschossen, false, "Elfmeter verschossen"),
            new SimuliertesEreignis(123, EreignisArt.ElfmeterTor, true, "Elfmeter verwandelt"),
            new SimuliertesEreignis(124, EreignisArt.ElfmeterVerschossen, false, "Elfmeter verschossen"),
            new SimuliertesEreignis(125, EreignisArt.ElfmeterTor, true, "Elfmeter verwandelt"),
            new SimuliertesEreignis(126, EreignisArt.ElfmeterVerschossen, false, "Elfmeter verschossen"));
    }
}
