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
}
