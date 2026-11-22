using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Zufall;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Rückspiel der freien Hin-/Rückspiel-Simulation samt Entscheidungsverfahren (#509 Schnitt C, K3): bei festem
/// Seed exakt dasselbe Ergebnis wie vor dem Verschieben in den Service. Die Erwartungen stammen vom unverändert
/// verschobenen Code, seit #692 (eine Chance je Minute) und #694 (Heimvorteil, Spreizung, Zeitprofil) mit Seeds derselben Lagen neu erzeugt (Hinspiel 1:1, Rückspiel-Heim = <c>GastTeam</c>, Rückspiel-Gast = <c>HeimTeam</c>).</summary>
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

    /// <summary>K1–K3 (#539) bei aktiver Regel und Hinspiel 1:1, über mehrere Seeds; jede der drei Lagen muss vorkommen
    /// (sonst prüfte der Test nichts): (1) der Gesamtstand entscheidet – regulärer Ausgang, (2) Gesamtstand gleich und
    /// ungleiche Auswärtstore – Art „Auswärtstorregel“ ohne Verfahren, (3) auch die Auswärtstore gleich – das Verfahren
    /// läuft. Das Rückspiel-Heim traf im Hinspiel einmal auswärts, das Rückspiel-Auswärts im Rückspiel
    /// <c>AuswaertsToreRegulaer</c> mal; gleich viele heißt also genau 1.</summary>
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
        Kennzahlen(Rueckspiel(8, _verlaengerungDannElfmeter))
            .Should().Be((1, 0, null, null, null, null, EntscheidungArt.RegulaereSpielzeit, 6));
    }

    [Fact]
    public void GesamtstandAusgeglichen_RueckspielGehtInDieVerlaengerung()
    {
        Kennzahlen(Rueckspiel(4, _verlaengerungDannElfmeter))
            .Should().Be((1, 1, 1, 0, null, null, EntscheidungArt.Verlaengerung, 22));
    }

    [Fact]
    public void GesamtstandAusgeglichen_NachVerlaengerungKeinSieger_Elfmeterschiessen()
    {
        Kennzahlen(Rueckspiel(18, _verlaengerungDannElfmeter))
            .Should().Be((0, 0, 0, 0, 3, 4, EntscheidungArt.Elfmeterschiessen, 18));
    }

    [Fact]
    public void GesamtstandAusgeglichen_OhneVerfahren_BleibtBeiDerRegulaerenSpielzeit()
    {
        var ergebnis = Rueckspiel(3, []);

        (ergebnis.HeimToreRegulaer, ergebnis.AuswaertsToreRegulaer).Should().Be((1, 1));
        ergebnis.EntscheidungArt.Should().Be(EntscheidungArt.RegulaereSpielzeit);
        ergebnis.HeimToreVerlaengerung.Should().BeNull();
        ergebnis.HeimElfmetertore.Should().BeNull();
    }

    // ── Wiederholungsspiel als eigener Schritt (#691) ─────────────────────

    private static SimulationsService Service() =>
        SimulationsDienste.Service(SimulationsDienste.NeuerContext(), new Zufallsquelle());

    /// <summary>Hinspiel 1:1, Kette „Wiederholungsspiel, Los“: Endet das Rückspiel torgleich, steht das Wiederholungsspiel an;
    /// ausgetragen ist das Ergebnis bitgleich zum Einzelzug <c>RueckspielSimulieren</c>.</summary>
    [Fact]
    public void RueckspielBisWiederholung_GesamtstandGleich_WiederholungAusstehendUndAusgetragenBitgleich()
    {
        var kette = SimulationsEngineTestdaten.VerfahrenKette(VerfahrenArt.Wiederholungsspiel, VerfahrenArt.Losentscheid);
        int ausstehend = 0;
        for (int seed = 0; seed < 100; seed++)
        {
            var einzelzug = Rueckspiel(seed, kette);
            var schritt = Service().RueckspielBisWiederholungSimulieren(
                SimulationsEngineTestdaten.GastTeam(), SimulationsEngineTestdaten.HeimTeam(),
                _hinspielEinsZuEins, kette, new Random(seed));

            bool torgleich = schritt.Ergebnis.HeimToreRegulaer == schritt.Ergebnis.AuswaertsToreRegulaer;
            (schritt.Ausstehend is not null).Should().Be(torgleich, $"Seed {seed}: Wiederholungsspiel nur beim Gesamt-Remis");
            if (schritt.Ausstehend is not { } offen)
                continue;

            ausstehend++;
            schritt.Ergebnis.Wiederholung.Should().BeNull();
            offen.Austragen().Should().BeEquivalentTo(einzelzug, $"Seed {seed}");
        }
        ausstehend.Should().BeGreaterThan(5);
    }

    [Fact]
    public void RueckspielBisWiederholung_HinspielFuehrtDeutlich_NichtsAusstehend()
    {
        var kette = SimulationsEngineTestdaten.VerfahrenKette(VerfahrenArt.Wiederholungsspiel, VerfahrenArt.Losentscheid);
        var hinspiel = new SimulationsErgebnis(9, 0, null, null, null, null, EntscheidungArt.RegulaereSpielzeit, []);
        for (int seed = 0; seed < 30; seed++)
        {
            var schritt = Service().RueckspielBisWiederholungSimulieren(
                SimulationsEngineTestdaten.GastTeam(), SimulationsEngineTestdaten.HeimTeam(), hinspiel, kette, new Random(seed));

            schritt.Ausstehend.Should().BeNull($"Seed {seed}");
        }
    }
}
