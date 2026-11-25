using FluentAssertions;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Tabelle;

/// <summary>Issue #355 K2: Vortrag aus den Punkten der Quellrunde und dem Modus der Zielrunde.</summary>
public class PunktevortragTests
{
    [Theory]
    [InlineData(Punktevortrag.Keine,               31, 0, 0)]
    [InlineData(Punktevortrag.Voll,                31, 31, 31)]
    [InlineData(Punktevortrag.HalbiertAbgerundet,  31, 15, 15.5)]
    [InlineData(Punktevortrag.HalbiertAufgerundet, 31, 16, 15.5)]
    [InlineData(Punktevortrag.HalbiertAbgerundet,  30, 15, 15)]
    [InlineData(Punktevortrag.HalbiertAufgerundet, 30, 15, 15)]
    [InlineData(Punktevortrag.HalbiertAbgerundet,  0, 0, 0)]
    [InlineData(Punktevortrag.HalbiertAufgerundet, 1, 1, 0.5)]
    public void Berechnen_LiefertPunkteUndUngerundetenWert(Punktevortrag modus, int quellPunkte, int punkte, double ungerundet)
    {
        var vortrag = PunktevortragBerechnung.Berechnen(quellPunkte, modus);

        vortrag.Punkte.Should().Be(punkte);
        vortrag.Ungerundet.Should().Be((decimal)ungerundet);
    }

    [Fact]
    public void Berechnen_UngeradeHalbierung_IstGerundetUndMerktDieRichtung()
    {
        var ab = PunktevortragBerechnung.Berechnen(31, Punktevortrag.HalbiertAbgerundet);
        var auf = PunktevortragBerechnung.Berechnen(31, Punktevortrag.HalbiertAufgerundet);

        ab.IstGerundet.Should().BeTrue();
        ab.IstAbgerundet.Should().BeTrue();
        ab.IstAufgerundet.Should().BeFalse();
        auf.IstGerundet.Should().BeTrue();
        auf.IstAufgerundet.Should().BeTrue();
        auf.IstAbgerundet.Should().BeFalse();
    }

    [Theory]
    [InlineData(Punktevortrag.Keine, 31)]
    [InlineData(Punktevortrag.Voll, 31)]
    [InlineData(Punktevortrag.HalbiertAbgerundet, 30)]
    [InlineData(Punktevortrag.HalbiertAufgerundet, 30)]
    public void Berechnen_GeradeOderUngehalbiert_IstNichtGerundet(Punktevortrag modus, int quellPunkte) =>
        PunktevortragBerechnung.Berechnen(quellPunkte, modus).IstGerundet.Should().BeFalse();
}
