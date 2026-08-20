using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Simulation;

public class SimulationsEngineTests
{
    private static Mannschaft Team(int id, int off = 70, int mid = 70, int def = 70,
                                   int heim = 75, int ausw = 65) =>
        new() { Id = id, Name = $"Team {id}", Kurzname = $"T{id}",
                OffensivRating = off, MittelfeldRating = mid, DefensivRating = def,
                Heimstaerke = heim, Auswaertsstaerke = ausw };

    private static EntscheidungsVerfahren Verfahren(VerfahrenArt art, int rang = 1) =>
        new() { VerfahrenArt = art, Reihenfolge = rang };

    [Fact]
    public void GleicherSeed_LiefertGleichesErgebnis()
    {
        var heim = Team(1);
        var gast = Team(2);
        var r1 = SimulationsEngine.Simulieren(heim, gast, [], false, new Random(42));
        var r2 = SimulationsEngine.Simulieren(heim, gast, [], false, new Random(42));

        r1.Should().BeEquivalentTo(r2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(999)]
    public void ToranzahlenSindNichtNegativ(int seed)
    {
        var erg = SimulationsEngine.Simulieren(Team(1), Team(2), [], false, new Random(seed));

        erg.HeimToreRegulaer.Should().BeGreaterThanOrEqualTo(0);
        erg.AuswaertsToreRegulaer.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void OhneVerfahren_KeineVerlaengerungsOderElfmeterTore()
    {
        var erg = SimulationsEngine.Simulieren(Team(1), Team(2), [], false, new Random(42));

        erg.HeimToreVerlaengerung.Should().BeNull();
        erg.AuswaertsToreVerlaengerung.Should().BeNull();
        erg.HeimElfmetertore.Should().BeNull();
        erg.AuswaertsElfmetertore.Should().BeNull();
    }

    [Fact]
    public void SchnellSimulation_LiefertKeineEreignisse()
    {
        var erg = SimulationsEngine.Simulieren(Team(1), Team(2), [], false, new Random(42));

        erg.Ereignisse.Should().BeEmpty();
    }

    [Fact]
    public void AusfuehrlicheSimulation_EreignisseHabenGueltigeMinuten()
    {
        // Seed 5 liefert mindestens ein Tor, sodass Ereignisse entstehen
        var erg = SimulationsEngine.Simulieren(Team(1), Team(2), [], true, new Random(5));

        erg.Ereignisse.Should().NotBeEmpty();
        erg.Ereignisse.Should().AllSatisfy(e => e.Minute.Should().BeInRange(1, 90));
    }

    [Fact]
    public void AusfuehrlicheSimulation_TorEreignisseHabenZufaelligenTorstil()
    {
        // Issue #126: Tor-Ereignisse tragen zur Flavor-Anreicherung einen zufälligen Torstil in der
        // Beschreibung ("Tor per <Stil>") statt nur "Tor". Seed durchsuchen, bis mindestens ein Tor fällt.
        var torEreignisse = Enumerable.Range(0, 50)
            .SelectMany(seed => SimulationsEngine.Simulieren(Team(1), Team(2), [], true, new Random(seed)).Ereignisse)
            .Where(e => e.Art == EreignisArt.Tor)
            .ToList();

        torEreignisse.Should().NotBeEmpty();
        torEreignisse.Should().AllSatisfy(e => e.Beschreibung.Should().StartWith("Tor per "));
    }

    [Fact]
    public void AusfuehrlicheSimulation_EinigeTorEreignisseSindKontertore()
    {
        // Issue #127: mit ~15% Wahrscheinlichkeit trägt ein Tor-Ereignis den Zusatz "nach Konter" —
        // über genug simulierte Tore hinweg müssen sowohl Kontertore als auch normale Tore vorkommen.
        var torBeschreibungen = Enumerable.Range(0, 200)
            .SelectMany(seed => SimulationsEngine.Simulieren(Team(1), Team(2), [], true, new Random(seed)).Ereignisse)
            .Where(e => e.Art == EreignisArt.Tor)
            .Select(e => e.Beschreibung)
            .ToList();

        torBeschreibungen.Should().Contain(b => b.EndsWith("nach Konter"));
        torBeschreibungen.Should().Contain(b => !b.EndsWith("nach Konter"));
    }

    [Fact]
    public void MitElfmeterVerfahren_BeimUnentschiedenImmerSieger()
    {
        var verfahren = new[] { Verfahren(VerfahrenArt.Elfmeterschiessen) };

        for (int seed = 0; seed < 200; seed++)
        {
            var erg = SimulationsEngine.Simulieren(Team(1), Team(2), verfahren, false, new Random(seed));
            if (erg.HeimToreRegulaer == erg.AuswaertsToreRegulaer)
            {
                erg.EntscheidungArt.Should().Be(EntscheidungArt.Elfmeterschiessen);
                erg.HeimElfmetertore.Should().NotBeNull();
                erg.AuswaertsElfmetertore.Should().NotBeNull();
                erg.HeimElfmetertore.Should().NotBe(
                    erg.AuswaertsElfmetertore, "Elfmeter darf nicht mit Gleichstand enden");
            }
        }
    }

    [Fact]
    public void MitSilverGoalVerfahren_LiefertVerlaengerungstoreBeiEntscheidung()
    {
        // Regression #115: VerfahrenArt.SilverGoal fehlte im Engine-Switch und fiel in den
        // default-Zweig, wodurch HeimToreVerlaengerung/AuswaertsToreVerlaengerung trotz
        // konfigurierter Verlängerung immer null blieben.
        var verfahren = new[]
        {
            Verfahren(VerfahrenArt.SilverGoal, rang: 1),
            Verfahren(VerfahrenArt.Elfmeterschiessen, rang: 2)
        };

        for (int seed = 0; seed < 200; seed++)
        {
            var erg = SimulationsEngine.Simulieren(Team(1), Team(2), verfahren, false, new Random(seed));
            if (erg.HeimToreRegulaer != erg.AuswaertsToreRegulaer) continue;

            erg.EntscheidungArt.Should().BeOneOf(EntscheidungArt.SilverGoal, EntscheidungArt.Elfmeterschiessen);
            erg.HeimToreVerlaengerung.Should().NotBeNull();
            erg.AuswaertsToreVerlaengerung.Should().NotBeNull();

            if (erg.EntscheidungArt == EntscheidungArt.Elfmeterschiessen)
            {
                erg.HeimToreVerlaengerung.Should().Be(erg.AuswaertsToreVerlaengerung,
                    "Elfmeterschießen wird nur erreicht, wenn die Verlängerung unentschieden endet");
                erg.HeimElfmetertore.Should().NotBeNull();
                erg.AuswaertsElfmetertore.Should().NotBeNull();
            }
        }
    }

    [Fact]
    public void StarkesTeamGewinntHaeufiger()
    {
        var stark  = Team(1, off: 95, mid: 90, def: 90, heim: 95, ausw: 90);
        var schwach = Team(2, off: 40, mid: 40, def: 40, heim: 60, ausw: 55);

        int starkGewinnt = 0, schwachGewinnt = 0;
        for (int seed = 0; seed < 500; seed++)
        {
            var erg = SimulationsEngine.Simulieren(stark, schwach, [], false, new Random(seed));
            if (erg.HeimToreRegulaer      > erg.AuswaertsToreRegulaer) starkGewinnt++;
            else if (erg.AuswaertsToreRegulaer > erg.HeimToreRegulaer)  schwachGewinnt++;
        }

        starkGewinnt.Should().BeGreaterThan(schwachGewinnt * 3,
            "Ein stark überlegenes Team soll mehr als dreimal so oft gewinnen");
    }
}
