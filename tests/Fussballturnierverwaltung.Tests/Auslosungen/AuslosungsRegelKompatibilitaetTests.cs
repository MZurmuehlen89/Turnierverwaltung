using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>Matrix Auslosungsregel × Auslosungsart × Rundenart (Issue #331).</summary>
public class AuslosungsRegelKompatibilitaetTests
{
    [Theory]
    [InlineData(AuslosungsRegelArt.KeinGleichesLand,       Auslosungsart.Zufall,        Rundenart.KoRunde)]
    [InlineData(AuslosungsRegelArt.KeinGleichesLand,       Auslosungsart.NachSetzliste, Rundenart.KoRunde)]
    [InlineData(AuslosungsRegelArt.KeinGleichesLand,       Auslosungsart.TopfZuordnung, Rundenart.TopfLiga)]
    [InlineData(AuslosungsRegelArt.KeinGleichesLand,       Auslosungsart.Manuell,       Rundenart.Gruppenphase)]
    [InlineData(AuslosungsRegelArt.KeineGleicheGruppe,     Auslosungsart.NachToepfen,   Rundenart.KoRunde)]
    [InlineData(AuslosungsRegelArt.KeineGleicheGruppe,     Auslosungsart.Manuell,       Rundenart.KoRunde)]
    [InlineData(AuslosungsRegelArt.HeimrechtNachLos,       Auslosungsart.NachToepfen,   Rundenart.KoRunde)]
    [InlineData(AuslosungsRegelArt.HeimrechtNachSetzliste, Auslosungsart.NachSetzliste, Rundenart.KoRunde)]
    [InlineData(AuslosungsRegelArt.FreilosMoeglich,        Auslosungsart.Zufall,        Rundenart.KoRunde)]
    public void PassendeKombination_IstNichtGesperrt(AuslosungsRegelArt regel, Auslosungsart art, Rundenart rundenart) =>
        AuslosungsRegelKompatibilitaet.SperrGrund(regel, art, rundenart, folgtAufGruppenphase: true).Should().BeNull();

    [Theory]
    [InlineData(AuslosungsRegelArt.KeinGleichesLand,       Auslosungsart.Zufall,        Rundenart.Liga)]
    [InlineData(AuslosungsRegelArt.KeinGleichesLand,       Auslosungsart.Zufall,        Rundenart.SchweizerSystem)]
    [InlineData(AuslosungsRegelArt.HeimrechtNachLos,       Auslosungsart.Manuell,       Rundenart.KoRunde)]
    [InlineData(AuslosungsRegelArt.HeimrechtNachLos,       Auslosungsart.Zufall,        Rundenart.Gruppenphase)]
    [InlineData(AuslosungsRegelArt.HeimrechtNachSetzliste, Auslosungsart.TopfZuordnung, Rundenart.TopfLiga)]
    [InlineData(AuslosungsRegelArt.FreilosMoeglich,        Auslosungsart.NachToepfen,   Rundenart.KoRunde)]
    [InlineData(AuslosungsRegelArt.FreilosMoeglich,        Auslosungsart.NachSetzliste, Rundenart.KoRunde)]
    [InlineData(AuslosungsRegelArt.FreilosMoeglich,        Auslosungsart.Zufall,        Rundenart.Gruppenphase)]
    public void UnpassendeKombination_IstGesperrtMitBegruendung(AuslosungsRegelArt regel, Auslosungsart art, Rundenart rundenart) =>
        AuslosungsRegelKompatibilitaet.SperrGrund(regel, art, rundenart, folgtAufGruppenphase: true).Should().NotBeNullOrWhiteSpace();

    [Fact]
    public void KeineGleicheGruppe_OhneVorangegangeneGruppenphase_IstGesperrt() =>
        AuslosungsRegelKompatibilitaet.SperrGrund(
                AuslosungsRegelArt.KeineGleicheGruppe, Auslosungsart.Zufall, Rundenart.KoRunde, folgtAufGruppenphase: false)
            .Should().Contain("keine Gruppenphase");

    [Fact]
    public void KeineGleicheGruppe_BeiGruppensiegerGegenZweite_WirktImmer()
    {
        AuslosungsRegelKompatibilitaet.ImmerAktivGrund(
            AuslosungsRegelArt.KeineGleicheGruppe, Auslosungsart.GruppensiegerGegenGruppenzweite).Should().NotBeNull();
        AuslosungsRegelKompatibilitaet.Wirkt(
                AuslosungsRegelArt.KeineGleicheGruppe, istAktiv: false, Auslosungsart.GruppensiegerGegenGruppenzweite,
                Rundenart.KoRunde, folgtAufGruppenphase: true)
            .Should().BeTrue();
    }

    [Fact]
    public void GesperrteRegel_WirktNicht_AuchWennAktiv() =>
        AuslosungsRegelKompatibilitaet.Wirkt(
                AuslosungsRegelArt.FreilosMoeglich, istAktiv: true, Auslosungsart.NachToepfen,
                Rundenart.KoRunde, folgtAufGruppenphase: true)
            .Should().BeFalse();

    [Fact]
    public void HeimrechtRegeln_SchliessenSichGegenseitigAus()
    {
        AuslosungsRegelKompatibilitaet.SchliessenSichAus(
            AuslosungsRegelArt.HeimrechtNachLos, AuslosungsRegelArt.HeimrechtNachSetzliste).Should().BeTrue();
        AuslosungsRegelKompatibilitaet.SchliessenSichAus(
            AuslosungsRegelArt.HeimrechtNachLos, AuslosungsRegelArt.KeinGleichesLand).Should().BeFalse();
    }

    [Fact]
    public void AngeboteneRegeln_EnthaltenKeineEntfallenenRegeln() =>
        AuslosungsRegelKompatibilitaet.AngeboteneRegeln.Should().HaveCount(5)
            .And.OnlyContain(r => AuslosungsRegelKompatibilitaet.SperrGrund(r, Auslosungsart.Zufall, Rundenart.KoRunde, true)
                                  != "Diese Regel wird nicht mehr unterstützt.");

    [Theory]
    [InlineData("1. Gruppe A", "A", 1)]
    [InlineData("2. Gruppe D", "D", 2)]
    [InlineData("Sieger Gruppe B", "B", 1)]
    public void GruppenHerkunft_LiestGruppeUndPlatzAusPlatzhalter(string name, string gruppe, int platz) =>
        GruppenHerkunft.AusPlatzhalter(name).Should().Be((gruppe, platz));

    [Theory]
    [InlineData("3. Gruppe A/D/E/F")]
    [InlineData("Sieger Viertelfinale 1")]
    [InlineData("Team 4")]
    [InlineData(null)]
    public void GruppenHerkunft_OhneEindeutigeGruppe_IstNull(string? name) =>
        GruppenHerkunft.AusPlatzhalter(name).Should().BeNull();
}
