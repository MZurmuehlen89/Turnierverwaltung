using FluentAssertions;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Turniere;

/// <summary>#624 K1: Auswahl und Eignung eines Regelwerks je Rundenart (reine Funktionen).</summary>
public class KoRegelwerkTests
{
    private static Regelwerk MitVerfahren() => new()
    {
        Name = "Mit", Entscheidungsverfahren = [new EntscheidungsVerfahren { Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen }]
    };

    private static Regelwerk OhneVerfahren() => new() { Name = "Ohne" };

    [Theory]
    [InlineData(Rundenart.KoRunde)]
    [InlineData(Rundenart.Finale)]
    [InlineData(Rundenart.SpielUmPlatzDrei)]
    public void Auswahl_Ko_NurRegelwerkeMitVerfahren(Rundenart art)
    {
        var mit = MitVerfahren();
        KoRegelwerk.Auswahl(art, [mit, OhneVerfahren()]).Should().Equal(mit);
    }

    [Theory]
    [InlineData(Rundenart.Liga)]
    [InlineData(Rundenart.Gruppenphase)]
    [InlineData(Rundenart.SchweizerSystem)]
    public void Auswahl_NichtKo_AlleRegelwerke(Rundenart art) =>
        KoRegelwerk.Auswahl(art, [MitVerfahren(), OhneVerfahren()]).Should().HaveCount(2);

    [Fact]
    public void KeinRegelwerk_NurErlaubt_WennStandardVerfahrenHat()
    {
        KoRegelwerk.KeinRegelwerkErlaubt(Rundenart.KoRunde, MitVerfahren()).Should().BeTrue();
        KoRegelwerk.KeinRegelwerkErlaubt(Rundenart.KoRunde, OhneVerfahren()).Should().BeFalse();
        KoRegelwerk.KeinRegelwerkErlaubt(Rundenart.KoRunde, null).Should().BeFalse();
        KoRegelwerk.KeinRegelwerkErlaubt(Rundenart.Liga, null).Should().BeTrue();
    }

    [Fact]
    public void RundeGeeignet_NutztEigenesRegelwerk_SonstDenStandard()
    {
        KoRegelwerk.RundeGeeignet(Rundenart.Finale, MitVerfahren(), OhneVerfahren()).Should().BeTrue();
        KoRegelwerk.RundeGeeignet(Rundenart.Finale, OhneVerfahren(), MitVerfahren()).Should().BeFalse("das eigene Regelwerk gilt, nicht der Standard");
        KoRegelwerk.RundeGeeignet(Rundenart.Finale, null, MitVerfahren()).Should().BeTrue();
        KoRegelwerk.RundeGeeignet(Rundenart.Finale, null, null).Should().BeFalse();
        KoRegelwerk.Fehler("Finale", Rundenart.Liga, OhneVerfahren(), null).Should().BeNull();
    }
}
