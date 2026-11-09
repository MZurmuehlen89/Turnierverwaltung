using FluentAssertions;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Turniere.Eingaben;

public class TurnierRundeEingabeTests
{
    private const string KONFIGURATION = "{\"anzahlSpieltage\":5}";

    private static TurnierRundeEingabe Eingabe(Rundenart rundenart, int? ranglistenQuelleId = null) => new(
        "Runde", rundenart, EingangsMannschaftsanzahl: 16, AusgangsMannschaftsanzahl: 8, RegelwerkId: null,
        KonfigurationJson: KONFIGURATION, GruppenAnzahl: 4,
        StandardRanglisteId: ranglistenQuelleId, TitelverteidigerInTopf1: true, GegnerJeTopf: 1);

    [Fact]
    public void OhneUnwirksameAngaben_SchweizerSystem_BehaeltDieSpieltagKonfiguration() =>
        Eingabe(Rundenart.SchweizerSystem).OhneUnwirksameAngaben().KonfigurationJson.Should().Be(KONFIGURATION);

    [Theory]
    [InlineData(Rundenart.Liga)]
    [InlineData(Rundenart.Gruppenphase)]
    [InlineData(Rundenart.KoRunde)]
    public void OhneUnwirksameAngaben_AndereRundenart_VerwirftDieSpieltagKonfiguration(Rundenart rundenart) =>
        Eingabe(rundenart).OhneUnwirksameAngaben().KonfigurationJson.Should().BeNull();

    [Fact]
    public void OhneUnwirksameAngaben_Gruppenphase_BehaeltDieGruppenanzahl() =>
        Eingabe(Rundenart.Gruppenphase).OhneUnwirksameAngaben().GruppenAnzahl.Should().Be(4);

    [Theory]
    [InlineData(Rundenart.Liga)]
    [InlineData(Rundenart.SchweizerSystem)]
    [InlineData(Rundenart.KoRunde)]
    public void OhneUnwirksameAngaben_AndereRundenart_VerwirftDieGruppenanzahl(Rundenart rundenart) =>
        Eingabe(rundenart).OhneUnwirksameAngaben().GruppenAnzahl.Should().BeNull();

    [Fact]
    public void OhneUnwirksameAngaben_MitStandardRangliste_BehaeltTitelverteidigerInTopf1() =>
        Eingabe(Rundenart.Liga, ranglistenQuelleId: 7).OhneUnwirksameAngaben().TitelverteidigerInTopf1.Should().BeTrue();

    [Fact]
    public void OhneUnwirksameAngaben_OhneStandardRangliste_VerwirftTitelverteidigerInTopf1() =>
        Eingabe(Rundenart.Liga).OhneUnwirksameAngaben().TitelverteidigerInTopf1.Should().BeFalse();

    [Fact]
    public void OhneUnwirksameAngaben_TopfLiga_BehaeltGegnerJeTopf() =>
        Eingabe(Rundenart.TopfLiga).OhneUnwirksameAngaben().GegnerJeTopf.Should().Be(1);

    [Theory]
    [InlineData(Rundenart.Liga)]
    [InlineData(Rundenart.Gruppenphase)]
    [InlineData(Rundenart.KoRunde)]
    public void OhneUnwirksameAngaben_AndereRundenart_SetztGegnerJeTopfZurueck(Rundenart rundenart) =>
        Eingabe(rundenart).OhneUnwirksameAngaben().GegnerJeTopf.Should().Be(2);

    [Fact]
    public void OhneUnwirksameAngaben_SchweizerSystem_VerwirftPaarungFestVorgegeben() =>
        (Eingabe(Rundenart.SchweizerSystem) with { PaarungFestVorgegeben = true }).OhneUnwirksameAngaben()
            .PaarungFestVorgegeben.Should().BeFalse();

    [Theory]
    [InlineData(Rundenart.KoRunde)]
    [InlineData(Rundenart.Gruppenphase)]
    public void OhneUnwirksameAngaben_AndereRundenart_BehaeltPaarungFestVorgegeben(Rundenart rundenart) =>
        (Eingabe(rundenart) with { PaarungFestVorgegeben = true }).OhneUnwirksameAngaben()
            .PaarungFestVorgegeben.Should().BeTrue();

    private static readonly TabellenBereichEingabe[] _bereiche = [new(17, 18, TabellenBereichArt.Abstieg, "Abstieg")];

    [Theory]
    [InlineData(Rundenart.Liga)]
    [InlineData(Rundenart.Gruppenphase)]
    [InlineData(Rundenart.TopfLiga)]
    [InlineData(Rundenart.SchweizerSystem)]
    public void OhneUnwirksameAngaben_RundeMitTabelle_BehaeltDieTabellenbereiche(Rundenart rundenart) =>
        (Eingabe(rundenart) with { TabellenBereiche = _bereiche }).OhneUnwirksameAngaben().TabellenBereiche.Should().Equal(_bereiche);

    [Theory]
    [InlineData(Rundenart.KoRunde)]
    [InlineData(Rundenart.Finale)]
    public void OhneUnwirksameAngaben_RundeOhneTabelle_VerwirftDieTabellenbereiche(Rundenart rundenart) =>
        (Eingabe(rundenart) with { TabellenBereiche = _bereiche }).OhneUnwirksameAngaben().TabellenBereiche.Should().BeNull();

    [Fact]
    public void OhneUnwirksameAngaben_LaesstAlleAnderenAngabenUnveraendert()
    {
        var eingabe = Eingabe(Rundenart.Gruppenphase, ranglistenQuelleId: 7) with { KonfigurationJson = null, GegnerJeTopf = 2 };

        eingabe.OhneUnwirksameAngaben().Should().Be(eingabe);
    }
}
