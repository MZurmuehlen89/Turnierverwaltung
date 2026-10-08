using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

public class VorbelegungsRasterTests
{
    // ── Gruppen ───────────────────────────────────────────────────────────────

    [Fact]
    public void Gruppen_Gruppenphase_VerteiltTeilnehmerGleichmaessigUndBenenntNachBuchstaben()
    {
        var gruppen = VorbelegungsRaster.Gruppen(Rundenart.Gruppenphase, gruppenAnzahl: 4, teamAnzahl: 16);

        gruppen.Should().Equal(
            new RasterGruppe(1, "Gruppe A", 4),
            new RasterGruppe(2, "Gruppe B", 4),
            new RasterGruppe(3, "Gruppe C", 4),
            new RasterGruppe(4, "Gruppe D", 4));
    }

    [Fact]
    public void Gruppen_GruppenphaseMitRest_RundetDiePlaetzeJeGruppeAb() =>
        VorbelegungsRaster.Gruppen(Rundenart.Gruppenphase, gruppenAnzahl: 4, teamAnzahl: 18)
            .Should().OnlyContain(g => g.PlatzAnzahl == 4);

    [Fact]
    public void Gruppen_GruppenphaseOhneTeilnehmerzahl_HatVierPlaetzeJeGruppe() =>
        VorbelegungsRaster.Gruppen(Rundenart.Gruppenphase, gruppenAnzahl: 2, teamAnzahl: 0)
            .Should().Equal(new RasterGruppe(1, "Gruppe A", 4), new RasterGruppe(2, "Gruppe B", 4));

    [Fact]
    public void Gruppen_GruppenphaseOhneGruppenanzahl_HatEineGruppeMitAllenTeilnehmern() =>
        VorbelegungsRaster.Gruppen(Rundenart.Gruppenphase, gruppenAnzahl: null, teamAnzahl: 6)
            .Should().Equal(new RasterGruppe(1, "Gruppe A", 6));

    [Theory]
    [InlineData(Rundenart.Liga)]
    [InlineData(Rundenart.SchweizerSystem)]
    public void Gruppen_LigaUndSchweizerSystem_HabenEineGruppeMitEinemPlatzJeTeilnehmer(Rundenart rundenart) =>
        VorbelegungsRaster.Gruppen(rundenart, gruppenAnzahl: 4, teamAnzahl: 36)
            .Should().Equal(new RasterGruppe(1, "Teilnehmer", 36));

    [Fact]
    public void Gruppen_LigaOhneTeilnehmerzahl_HatEinenPlatz() =>
        VorbelegungsRaster.Gruppen(Rundenart.Liga, gruppenAnzahl: null, teamAnzahl: 0)
            .Should().Equal(new RasterGruppe(1, "Teilnehmer", 1));

    // ── Paarungen ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(16, 8)]
    [InlineData(2, 1)]
    [InlineData(7, 4)] // ungerade: die letzte Paarung hat ein Freilos
    [InlineData(0, 1)] // Teilnehmerzahl unbekannt
    public void PaarungsAnzahl_IstDieHaelfteAufgerundet(int teamAnzahl, int erwartet) =>
        VorbelegungsRaster.PaarungsAnzahl(teamAnzahl).Should().Be(erwartet);
}
