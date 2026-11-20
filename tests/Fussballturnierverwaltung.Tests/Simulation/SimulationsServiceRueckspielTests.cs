using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Zufall;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Rückspiel der freien Hin-/Rückspiel-Simulation samt Entscheidungsverfahren (#509 Schnitt C, K3): bei festem
/// Seed exakt dasselbe Ergebnis wie vor dem Verschieben in den Service. Die Erwartungen stammen vom unverändert
/// verschobenen Code (Hinspiel 1:1, Rückspiel-Heim = <c>GastTeam</c>, Rückspiel-Gast = <c>HeimTeam</c>).</summary>
public class SimulationsServiceRueckspielTests
{
    private static readonly SimulationsErgebnis _hinspielEinsZuEins =
        new(1, 1, null, null, null, null, EntscheidungArt.RegulaereSpielzeit, []);

    private static readonly EntscheidungsVerfahren[] _verlaengerungDannElfmeter =
        SimulationsEngineTestdaten.VerfahrenKette(VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Elfmeterschiessen);

    private static SimulationsErgebnis Rueckspiel(int seed, IReadOnlyList<EntscheidungsVerfahren> verfahren)
    {
        var service = SimulationsDienste.Service(SimulationsDienste.NeuerContext(), new Zufallsquelle());
        return service.RueckspielSimulieren(
            SimulationsEngineTestdaten.GastTeam(), SimulationsEngineTestdaten.HeimTeam(),
            _hinspielEinsZuEins, verfahren, new Random(seed));
    }

    private static (int, int, int?, int?, int?, int?, EntscheidungArt, int) Kennzahlen(SimulationsErgebnis e) =>
        (e.HeimToreRegulaer, e.AuswaertsToreRegulaer, e.HeimToreVerlaengerung, e.AuswaertsToreVerlaengerung,
         e.HeimElfmetertore, e.AuswaertsElfmetertore, e.EntscheidungArt, e.Ereignisse.Count);

    [Fact]
    public void GesamtstandNach90MinutenEntschieden_KeinVerfahren()
    {
        Kennzahlen(Rueckspiel(5, _verlaengerungDannElfmeter))
            .Should().Be((2, 0, null, null, null, null, EntscheidungArt.RegulaereSpielzeit, 7));
    }

    [Fact]
    public void GesamtstandAusgeglichen_RueckspielGehtInDieVerlaengerung()
    {
        Kennzahlen(Rueckspiel(1, _verlaengerungDannElfmeter))
            .Should().Be((1, 1, 1, 0, null, null, EntscheidungArt.Verlaengerung, 16));
    }

    [Fact]
    public void GesamtstandAusgeglichen_NachVerlaengerungKeinSieger_Elfmeterschiessen()
    {
        Kennzahlen(Rueckspiel(4, _verlaengerungDannElfmeter))
            .Should().Be((0, 0, 0, 0, 5, 6, EntscheidungArt.Elfmeterschiessen, 21));
    }

    [Fact]
    public void GesamtstandAusgeglichen_OhneVerfahren_BleibtBeiDerRegulaerenSpielzeit()
    {
        var ergebnis = Rueckspiel(1, []);

        (ergebnis.HeimToreRegulaer, ergebnis.AuswaertsToreRegulaer).Should().Be((1, 1));
        ergebnis.EntscheidungArt.Should().Be(EntscheidungArt.RegulaereSpielzeit);
        ergebnis.HeimToreVerlaengerung.Should().BeNull();
        ergebnis.HeimElfmetertore.Should().BeNull();
    }
}
