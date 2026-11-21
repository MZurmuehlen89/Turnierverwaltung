using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Enums;
using static Fussballturnierverwaltung.Tests.Simulation.SimulationsEngineTestdaten;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Die Turnierform in der Engine (#679, K7, K8): Sie wirkt auf die Torerwartung des ganzen Spiels, wandert mit dem
/// Team, wenn das Heimrecht im Wiederholungsspiel wechselt, und berührt das Elfmeterschießen nicht.</summary>
public class TurnierformEngineTests
{
    // Heimteam deutlich stärker in diesem Turnier: e^(0,6 − σ²) ≈ 1,8 auf sein λ, ≈ 0,55 auf das des Gastes.
    private static readonly Turnierform _heimStark = new(0.3, -0.3);

    private const int SPIELE = 4000;

    private static double MittelHeimToreRegulaer(Turnierform? form, bool ausfuehrlich)
    {
        var rng = new Random(679);
        return Enumerable.Range(0, SPIELE).Average(_ =>
            SimulationsEngine.Simulieren(Team(1), Team(2), [], ausfuehrlich, rng, turnierform: form).HeimToreRegulaer);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegulaereSpielzeit_StarkeFormErhoehtDieToreDesHeimteams(bool ausfuehrlich)
    {
        double ohne = MittelHeimToreRegulaer(null, ausfuehrlich);
        double mit = MittelHeimToreRegulaer(_heimStark, ausfuehrlich);

        (mit / ohne).Should().BeInRange(1.5, 2.0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegulaereSpielzeit_NeutraleFormWirktNurUeberDenMittelwertAusgleich(bool ausfuehrlich)
    {
        double ohne = MittelHeimToreRegulaer(null, ausfuehrlich);
        double mit = MittelHeimToreRegulaer(new Turnierform(0.05, 0.05), ausfuehrlich);

        (mit / ohne).Should().BeInRange(0.95, 1.05);
    }

    // ── K7 Ganzes Spiel: Verlängerung ─────────────────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Verlaengerung_StarkeFormErhoehtDieToreDesHeimteams(bool ausfuehrlich)
    {
        double Mittel(Turnierform? form)
        {
            var rng = new Random(680);
            return Enumerable.Range(0, SPIELE).Average(_ =>
                SimulationsEngine.EntscheidungsverfahrenAnwenden(
                    Team(1), Team(2), VerfahrenKette(VerfahrenArt.VerlaengerungKlassisch), ausfuehrlich, rng,
                    turnierform: form).HeimVerl ?? 0);
        }

        (Mittel(_heimStark) / Mittel(null)).Should().BeInRange(1.4, 2.2);
    }

    // ── K7 Wiederholungsspiel: die Form wandert mit dem Team ──────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Wiederholungsspiel_FormWandertMitDemTeamBeiGetauschtemHeimrecht(bool ausfuehrlich)
    {
        // Gleiche Teams, Heimrecht wechselt: das erste Heimteam ist im Wiederholungsspiel Gast (Ortsfaktor 0,65 gegenüber
        // 0,75) und steht dort ohne Form schlechter; mit seiner starken Form, die mitwandert, besser.
        var rng = new Random(681);
        int heimToreWdh = 0, gastToreWdh = 0;
        for (int i = 0; i < SPIELE * 3; i++)
        {
            var e = SimulationsEngine.Simulieren(
                Team(1), Team(2), VerfahrenKette(VerfahrenArt.Wiederholungsspiel), ausfuehrlich, rng,
                turnierform: _heimStark);
            if (e.Wiederholung is not { } w) continue;
            heimToreWdh += w.HeimTore;
            gastToreWdh += w.AuswaertsTore;
        }

        // Ohne Mitwandern stünde die Form beim neuen Heimteam: Verhältnis erstes Heim : erster Gast ≈ 0,15. So ≈ 1,2.
        ((double)heimToreWdh / gastToreWdh).Should().BeGreaterThan(0.8);
    }

    // ── K8 Elfmeterschießen unberührt ─────────────────────────────────────

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    public void Elfmeterschiessen_RechnetMitUndOhneFormGleich(int seed, bool ausfuehrlich)
    {
        string Lauf(Turnierform? form)
        {
            var rng = new Random(seed);
            var e = SimulationsEngine.EntscheidungsverfahrenAnwenden(
                Team(1, off: 80), Team(2, off: 60), VerfahrenKette(VerfahrenArt.Elfmeterschiessen), ausfuehrlich, rng,
                turnierform: form);
            return $"{e.HeimElf}:{e.GastElf}/{e.Ereignisse.Count}/{rng.Next()}";
        }

        Lauf(_heimStark).Should().Be(Lauf(null));
    }
}
