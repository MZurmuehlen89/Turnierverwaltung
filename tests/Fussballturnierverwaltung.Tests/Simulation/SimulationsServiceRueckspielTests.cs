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

    private static SimulationsErgebnis RueckspielMitRegel(int seed) =>
        SimulationsDienste.Service(SimulationsDienste.NeuerContext(), new Zufallsquelle()).RueckspielSimulieren(
            SimulationsEngineTestdaten.GastTeam(), SimulationsEngineTestdaten.HeimTeam(),
            _hinspielEinsZuEins, _verlaengerungDannElfmeter, new Random(seed), auswaertstorRegelAktiv: true);

    /// <summary>K1–K3 (#539): bei Hinspiel 1:1 (Rückspiel-Heim traf dort auswärts einmal) und aktiver Regel entscheidet
    /// nach ausgeglichenem Gesamtstand allein der Auswärtstore-Vergleich der regulären Spielzeit – Rückspiel-Heim
    /// gewinnt bei 0 eigenen Rückspiel-Auswärtstoren des Gasts … und umgekehrt; bei Gleichstand läuft das Verfahren.
    /// Über mehrere Seeds, jede der drei Lagen muss vorkommen (sonst prüfte der Test nichts).</summary>
    [Fact]
    public void AuswaertstorRegelAktiv_EntscheidetNachAusgeglichenemGesamtstandVorDemVerfahren()
    {
        var lagen = new HashSet<string>();
        for (int seed = 0; seed < 60; seed++)
        {
            var e = RueckspielMitRegel(seed);
            bool gesamtGleich = e.HeimToreRegulaer == e.AuswaertsToreRegulaer; // Hinspiel 1:1 → Summe gleich ⇔ Rückspiel torgleich
            // Auswärtstore: Rückspiel-Heim (Hinspiel-Auswärts) hat im Hinspiel 1, Rückspiel-Gast (Hinspiel-Heim) im Rückspiel e.Auswaerts.
            if (!gesamtGleich)
            {
                e.EntscheidungArt.Should().Be(EntscheidungArt.RegulaereSpielzeit, $"Seed {seed}");
                lagen.Add("gesamt");
            }
            else if (e.AuswaertsToreRegulaer != 1)
            {
                e.EntscheidungArt.Should().Be(EntscheidungArt.Auswaertstorregel, $"Seed {seed}");
                e.HeimToreVerlaengerung.Should().BeNull($"Seed {seed}");
                e.HeimElfmetertore.Should().BeNull($"Seed {seed}");
                lagen.Add("auswaertstore");
            }
            else
            {
                e.EntscheidungArt.Should().NotBe(EntscheidungArt.Auswaertstorregel, $"Seed {seed}");
                e.HeimToreVerlaengerung.Should().NotBeNull($"Seed {seed}: gleiche Auswärtstore → Verfahren");
                lagen.Add("verfahren");
            }
        }
        lagen.Should().BeEquivalentTo("gesamt", "auswaertstore", "verfahren");
    }

    [Fact]
    public void AuswaertstorRegelInaktiv_NieAuswaertstorregel()
    {
        for (int seed = 0; seed < 60; seed++)
            Rueckspiel(seed, _verlaengerungDannElfmeter).EntscheidungArt.Should().NotBe(EntscheidungArt.Auswaertstorregel, $"Seed {seed}");
    }

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
