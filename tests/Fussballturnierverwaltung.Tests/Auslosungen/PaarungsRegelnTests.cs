using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

public class PaarungsRegelnTests
{
    private static readonly RegelHerkunft _deutschlandGruppeA = new(LandId: 10, Gruppe: "A");
    private static readonly RegelHerkunft _deutschlandGruppeB = new(LandId: 10, Gruppe: "B");
    private static readonly RegelHerkunft _spanienGruppeA = new(LandId: 11, Gruppe: "A");
    private static readonly RegelHerkunft _spanienGruppeB = new(LandId: 11, Gruppe: "B");
    private static readonly RegelHerkunft _unbekannt = new(LandId: null, Gruppe: null);

    private static AuslosungsRegelArt? BeideRegeln(RegelHerkunft a, RegelHerkunft b) =>
        PaarungsRegeln.VerletzteRegel(keinGleichesLand: true, keineGleicheGruppe: true, a, b);

    [Fact]
    public void VerletzteRegel_GleichesLand_MeldetKeinGleichesLand() =>
        BeideRegeln(_deutschlandGruppeA, _deutschlandGruppeB).Should().Be(AuslosungsRegelArt.KeinGleichesLand);

    [Fact]
    public void VerletzteRegel_GleicheGruppe_MeldetKeineGleicheGruppe() =>
        BeideRegeln(_deutschlandGruppeA, _spanienGruppeA).Should().Be(AuslosungsRegelArt.KeineGleicheGruppe);

    [Fact]
    public void VerletzteRegel_AnderesLandUndAndereGruppe_MeldetNichts() =>
        BeideRegeln(_deutschlandGruppeA, _spanienGruppeB).Should().BeNull();

    [Fact]
    public void VerletzteRegel_GleichesLandUndGleicheGruppe_MeldetDasLandZuerst() =>
        BeideRegeln(_deutschlandGruppeA, _deutschlandGruppeA).Should().Be(AuslosungsRegelArt.KeinGleichesLand);

    [Fact]
    public void VerletzteRegel_RegelnNichtAktiv_MeldetNichts() =>
        PaarungsRegeln.VerletzteRegel(
                keinGleichesLand: false, keineGleicheGruppe: false, _deutschlandGruppeA, _deutschlandGruppeA)
            .Should().BeNull();

    [Fact]
    public void VerletzteRegel_NurLandregelAktiv_UebergehtGleicheGruppe() =>
        PaarungsRegeln.VerletzteRegel(
                keinGleichesLand: true, keineGleicheGruppe: false, _deutschlandGruppeA, _spanienGruppeA)
            .Should().BeNull();

    [Fact]
    public void VerletzteRegel_NurGruppenregelAktiv_UebergehtGleichesLand() =>
        PaarungsRegeln.VerletzteRegel(
                keinGleichesLand: false, keineGleicheGruppe: true, _deutschlandGruppeA, _deutschlandGruppeB)
            .Should().BeNull();

    [Fact]
    public void VerletzteRegel_OhneLandUndGruppe_MeldetNichts() =>
        BeideRegeln(_unbekannt, _unbekannt).Should().BeNull();

    [Fact]
    public void VerletzteRegel_HerkunftAusPlatzhalternamen_ErkenntGleicheGruppe()
    {
        var ersterA = new RegelHerkunft(null, GruppenHerkunft.AusPlatzhalter("1. Gruppe A")?.Gruppe);
        var zweiterA = new RegelHerkunft(null, GruppenHerkunft.AusPlatzhalter("2. Gruppe A")?.Gruppe);
        var zweiterB = new RegelHerkunft(null, GruppenHerkunft.AusPlatzhalter("2. Gruppe B")?.Gruppe);

        BeideRegeln(ersterA, zweiterA).Should().Be(AuslosungsRegelArt.KeineGleicheGruppe);
        BeideRegeln(ersterA, zweiterB).Should().BeNull();
    }

    // ── Kombinationsplätze (#489) ────────────────────────────────────────────

    [Fact]
    public void VerletzteRegel_KombinationsplatzMitDerGruppeDesGegners_MeldetKeineGleicheGruppe()
    {
        var ersterA = RegelHerkunft.AusPlatzhalter(null, "1. Gruppe A");
        var dritterADEF = RegelHerkunft.AusPlatzhalter(null, "3. Gruppe A/D/E/F");

        BeideRegeln(ersterA, dritterADEF).Should().Be(AuslosungsRegelArt.KeineGleicheGruppe);
        BeideRegeln(dritterADEF, ersterA).Should().Be(AuslosungsRegelArt.KeineGleicheGruppe);
        RegelHerkunft.NurMoeglich(ersterA, dritterADEF).Should().BeTrue();
    }

    [Fact]
    public void VerletzteRegel_KombinationsplatzOhneDieGruppeDesGegners_MeldetNichts() =>
        // EURO 2024, Achtelfinale: 1B gegen 3ADEF
        BeideRegeln(RegelHerkunft.AusPlatzhalter(null, "1. Gruppe B"), RegelHerkunft.AusPlatzhalter(null, "3. Gruppe A/D/E/F"))
            .Should().BeNull();

    [Fact]
    public void VerletzteRegel_ZweiKombinationsplaetze_MeldetNichts() =>
        // Zwei verschiedene Dritte kommen nie aus derselben Gruppe.
        BeideRegeln(RegelHerkunft.AusPlatzhalter(null, "3. Gruppe A/B/C"), RegelHerkunft.AusPlatzhalter(null, "3. Gruppe A/D/E"))
            .Should().BeNull();

    [Fact]
    public void AusPlatzhalter_FesteGruppe_HatKeineKandidaten()
    {
        var ersterA = RegelHerkunft.AusPlatzhalter(null, "1. Gruppe A");

        ersterA.Gruppe.Should().Be("A");
        ersterA.KandidatenGruppen.Should().BeNull();
        RegelHerkunft.NurMoeglich(ersterA, RegelHerkunft.AusPlatzhalter(null, "2. Gruppe A")).Should().BeFalse();
    }
}
