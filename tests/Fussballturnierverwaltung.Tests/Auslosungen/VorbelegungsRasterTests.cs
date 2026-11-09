using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

public class VorbelegungsRasterTests
{
    // ── Gruppen oder Paarungen ────────────────────────────────────────────────

    [Theory]
    [InlineData(Rundenart.Gruppenphase, true)]
    [InlineData(Rundenart.Liga, true)]
    [InlineData(Rundenart.SchweizerSystem, false)] // paart wie eine K.-o.-Runde (#627)
    [InlineData(Rundenart.KoRunde, false)]
    [InlineData(Rundenart.TopfLiga, false)]
    public void HatGruppen_GiltFuerGruppenphaseUndLiga(Rundenart rundenart, bool erwartet) =>
        VorbelegungsRaster.HatGruppen(rundenart).Should().Be(erwartet);

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

    [Fact]
    public void Gruppen_Liga_HatEineGruppeMitEinemPlatzJeTeilnehmer() =>
        VorbelegungsRaster.Gruppen(Rundenart.Liga, gruppenAnzahl: 4, teamAnzahl: 36)
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

    // ── Vollständigkeit ───────────────────────────────────────────────────────

    [Fact]
    public void GruppenVollstaendig_AllePlaetzeBesetzt_IstVollstaendig() =>
        VorbelegungsRaster.GruppenVollstaendig([true, true, true]).Should().BeTrue();

    [Fact]
    public void GruppenVollstaendig_EinPlatzFrei_IstUnvollstaendig() =>
        VorbelegungsRaster.GruppenVollstaendig([true, false, true]).Should().BeFalse();

    [Fact]
    public void GruppenVollstaendig_OhnePlaetze_IstUnvollstaendig() =>
        VorbelegungsRaster.GruppenVollstaendig([]).Should().BeFalse();

    [Fact]
    public void PaarungenVollstaendig_AlleErwartetenTeamsPlatziert_IstVollstaendig() =>
        VorbelegungsRaster.PaarungenVollstaendig([(true, true), (true, true)], teamsBenoetigt: 4).Should().BeTrue();

    [Fact]
    public void PaarungenVollstaendig_TeamFehlt_IstUnvollstaendig() =>
        VorbelegungsRaster.PaarungenVollstaendig([(true, true), (true, false)], teamsBenoetigt: 4).Should().BeFalse();

    [Fact]
    public void PaarungenVollstaendig_NichtsPlatziert_IstUnvollstaendig() =>
        VorbelegungsRaster.PaarungenVollstaendig([(false, false)], teamsBenoetigt: 0).Should().BeFalse();

    [Fact]
    public void PaarungenVollstaendig_FreilosAufDemHeimplatz_IstVollstaendig() =>
        VorbelegungsRaster.PaarungenVollstaendig([(true, true), (true, false)], teamsBenoetigt: 3).Should().BeTrue();

    [Fact]
    public void PaarungenVollstaendig_EinzelnesTeamAufDemAuswaertsplatz_IstUnvollstaendig() =>
        VorbelegungsRaster.PaarungenVollstaendig([(true, true), (false, true)], teamsBenoetigt: 3).Should().BeFalse();

    [Fact]
    public void PaarungenVollstaendig_OhneBekannteTeilnehmerzahl_ReichtEinePaarungMitHeimteam() =>
        VorbelegungsRaster.PaarungenVollstaendig([(true, false)], teamsBenoetigt: 0).Should().BeTrue();
}
