using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;

namespace Fussballturnierverwaltung.Tests.Spielplan;

public class UebergangsAbdeckungTests
{
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 2, 2)]
    [InlineData(3, 8, 6)]
    public void Plaetze_Platzierungsbereich_ZaehltBeideGrenzenMit(int von, int bis, int erwartet) =>
        UebergangsAbdeckung.Plaetze(von, bis, anzahlBeste: null).Should().Be(erwartet);

    [Fact]
    public void Plaetze_GruppenuebergreifendeRegel_ZaehltDieAnzahlStattDesBereichs() =>
        UebergangsAbdeckung.Plaetze(3, 3, anzahlBeste: 4).Should().Be(4);

    [Fact]
    public void Unstimmigkeit_OhneAusgehendeRegel_GibtKeinenHinweis() =>
        UebergangsAbdeckung.Unstimmigkeit(16, []).Should().BeNull();

    [Fact]
    public void Unstimmigkeit_SummePasst_GibtKeinenHinweis() =>
        UebergangsAbdeckung.Unstimmigkeit(16, [8, 8]).Should().BeNull();

    [Fact]
    public void Unstimmigkeit_ZuWenigPlaetze_NenntSummeUndAusgang() =>
        UebergangsAbdeckung.Unstimmigkeit(16, [8, 4]).Should().Be("Übergangsregeln decken nur 12 von 16 Plätzen ab.");

    [Fact]
    public void Unstimmigkeit_ZuVielePlaetze_NenntSummeUndAusgang() =>
        UebergangsAbdeckung.Unstimmigkeit(16, [16, 8])
            .Should().Be("Übergangsregeln decken 24 von 16 Plätzen ab — mehr als erwartet.");

    [Fact]
    public void Unstimmigkeit_RegelOhneZeilen_ZaehltAlsNullPlaetze() =>
        UebergangsAbdeckung.Unstimmigkeit(4, [0]).Should().Be("Übergangsregeln decken nur 0 von 4 Plätzen ab.");
}
