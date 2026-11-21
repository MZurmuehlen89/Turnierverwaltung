using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Enums;
using static Fussballturnierverwaltung.Tests.Simulation.SimulationsEngineTestdaten;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>#691: das Wiederholungsspiel als eigener Schritt. Anhalten und Austragen rechnet bitgleich zum Einzelzug
/// (derselbe <see cref="Random"/> in derselben Reihenfolge), ohne Wiederholungsspiel in der Kette ändert sich nichts.</summary>
public class WiederholungsspielSchrittTests
{
    private const int SEEDS = 300;

    public static TheoryData<VerfahrenArt[], bool> KettenMitWiederholungsspiel => new()
    {
        { [VerfahrenArt.Wiederholungsspiel, VerfahrenArt.Losentscheid], true },
        { [VerfahrenArt.Wiederholungsspiel, VerfahrenArt.Losentscheid], false },
        { [VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Wiederholungsspiel, VerfahrenArt.Losentscheid], true },
        { [VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Wiederholungsspiel, VerfahrenArt.Losentscheid], false },
        { [VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Wiederholungsspiel, VerfahrenArt.Elfmeterschiessen], true },
        { [VerfahrenArt.Wiederholungsspiel, VerfahrenArt.Elfmeterschiessen], true },
    };

    // K1: gleicher Seed, gleiche Kette → angehalten und danach ausgetragen = Einzelzug, samt Ereignissen.
    [Theory]
    [MemberData(nameof(KettenMitWiederholungsspiel))]
    public void AngehaltenUndAusgetragen_IstBitgleichZumEinzelzug(VerfahrenArt[] kette, bool ausfuehrlich)
    {
        int angehalten = 0;
        for (int seed = 0; seed < SEEDS; seed++)
        {
            var einzelzug = SimulationsEngine.Simulieren(HeimTeam(), GastTeam(), VerfahrenKette(kette), ausfuehrlich, new Random(seed));

            var schritt = SimulationsEngine.SimulierenBisWiederholung(HeimTeam(), GastTeam(), VerfahrenKette(kette), ausfuehrlich, new Random(seed));
            schritt.Ergebnis.Wiederholung.Should().BeNull("angehalten steht das Ergebnis ohne Wiederholungsspiel da (Seed {0})", seed);
            if (schritt.Ausstehend is not { } ausstehend)
            {
                schritt.Ergebnis.Should().BeEquivalentTo(einzelzug, $"Seed {seed}: kein Wiederholungsspiel");
                continue;
            }

            angehalten++;
            ausstehend.Austragen().Should().BeEquivalentTo(einzelzug, $"Seed {seed}");
        }
        angehalten.Should().BeGreaterThan(10, "die Seeds müssen das Wiederholungsspiel auch erreichen");
    }

    // K2: ohne Wiederholungsspiel in der Kette (oder bei entschiedenem Spiel) gibt es nichts Ausstehendes.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OhneWiederholungsspielInDerKette_NichtsAusstehend_ErgebnisGleichDemEinzelzug(bool ausfuehrlich)
    {
        var kette = VerfahrenKette(VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Elfmeterschiessen);
        for (int seed = 0; seed < SEEDS; seed++)
        {
            var einzelzug = SimulationsEngine.Simulieren(HeimTeam(), GastTeam(), kette, ausfuehrlich, new Random(seed));
            var schritt = SimulationsEngine.SimulierenBisWiederholung(HeimTeam(), GastTeam(), kette, ausfuehrlich, new Random(seed));

            schritt.Ausstehend.Should().BeNull($"Seed {seed}");
            schritt.Ergebnis.Should().BeEquivalentTo(einzelzug, $"Seed {seed}");
        }
    }

    [Fact]
    public void EntschiedenesSpiel_NichtsAusstehend_AuchMitWiederholungsspielInDerKette()
    {
        var kette = VerfahrenKette(VerfahrenArt.Wiederholungsspiel, VerfahrenArt.Losentscheid);
        var entschieden = Enumerable.Range(0, SEEDS)
            .Select(seed => SimulationsEngine.SimulierenBisWiederholung(HeimTeam(), GastTeam(), kette, true, new Random(seed)))
            .Where(s => s.Ergebnis.HeimToreRegulaer != s.Ergebnis.AuswaertsToreRegulaer)
            .ToList();

        entschieden.Should().NotBeEmpty();
        entschieden.Should().OnlyContain(s => s.Ausstehend == null);
    }

    // Verlängerung vor dem Wiederholungsspiel gehört schon zum angehaltenen Ergebnis.
    [Fact]
    public void Angehalten_VerlaengerungDavorSteht_WiederholungsspielNoch()
    {
        var kette = VerfahrenKette(VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Wiederholungsspiel, VerfahrenArt.Losentscheid);
        var schritt = Enumerable.Range(0, SEEDS)
            .Select(seed => SimulationsEngine.SimulierenBisWiederholung(HeimTeam(), GastTeam(), kette, true, new Random(seed)))
            .First(s => s.Ausstehend is not null);

        schritt.Ergebnis.HeimToreVerlaengerung.Should().NotBeNull();
        schritt.Ergebnis.EntscheidungArt.Should().Be(EntscheidungArt.Verlaengerung);
        schritt.Ergebnis.Ereignisse.Should().NotContain(e => e.ImWiederholungsspiel);
    }

    // K3
    [Fact]
    public void ZweiterAufrufVonAustragen_Wirft()
    {
        var kette = VerfahrenKette(VerfahrenArt.Wiederholungsspiel, VerfahrenArt.Losentscheid);
        var ausstehend = Enumerable.Range(0, SEEDS)
            .Select(seed => SimulationsEngine.SimulierenBisWiederholung(HeimTeam(), GastTeam(), kette, true, new Random(seed)).Ausstehend)
            .First(a => a is not null)!;

        ausstehend.Austragen();

        var zweiter = () => ausstehend.Austragen();
        zweiter.Should().Throw<InvalidOperationException>();
    }

    // Rückspiel: dieselbe Zuordnung über EntscheidungsverfahrenBisWiederholung.
    [Fact]
    public void Rueckspielkette_AngehaltenUndAusgetragen_IstBitgleichZumEinzelzug()
    {
        var kette = VerfahrenKette(VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Wiederholungsspiel, VerfahrenArt.Losentscheid);
        var regulaer = new SimulationsErgebnis(1, 1, null, null, null, null, EntscheidungArt.RegulaereSpielzeit, [], 0, 0);
        for (int seed = 0; seed < 50; seed++)
        {
            var einzelzug = regulaer.MitEntscheidung(
                SimulationsEngine.EntscheidungsverfahrenAnwenden(HeimTeam(), GastTeam(), kette, true, new Random(seed)));
            var schritt = SimulationsEngine.EntscheidungsverfahrenBisWiederholung(regulaer, HeimTeam(), GastTeam(), kette, new Random(seed));

            (schritt.Ausstehend?.Austragen() ?? schritt.Ergebnis).Should().BeEquivalentTo(einzelzug, $"Seed {seed}");
        }
    }
}
