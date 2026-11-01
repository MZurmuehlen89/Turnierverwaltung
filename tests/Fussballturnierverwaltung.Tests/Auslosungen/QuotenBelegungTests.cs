using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>Quotenstand eines Teilnehmerpools (#423, seit #576 der Pool statt der Lostöpfe).</summary>
public class QuotenBelegungTests
{
    // ── Testdaten ─────────────────────────────────────────────────────────────

    private const int UEFA = 1;
    private const int CAF = 2;
    private const int DEUTSCHLAND = 10;
    private const int SPANIEN = 11;
    private const int GHANA = 20;

    private static int _naechsteId = 1;

    private static AuswahlKandidat Team(int land, int? verband = null) =>
        new(_naechsteId++, $"Team {land}", land, verband ?? (land == GHANA ? CAF : UEFA));

    private static VerbandsKontingent Verband(int verbandId, string name, int anzahl) => new(verbandId, name, anzahl, null);

    private static LandesKontingent Land(int landId, string name, int anzahl, int? spielklasse = null, int? abPlatz = null) =>
        new(landId, name, anzahl, spielklasse, abPlatz, null);

    // ── Verbandsquoten ────────────────────────────────────────────────────────

    [Fact]
    public void JeVerband_ZaehltDieMannschaftenDesPoolsUeberIhrenVerband()
    {
        var staende = QuotenBelegung.JeVerband(
            [Verband(UEFA, "UEFA", 3), Verband(CAF, "CAF", 1)], [Team(DEUTSCHLAND), Team(GHANA), Team(SPANIEN)]);

        staende.Should().Equal(new QuotenStand("UEFA", 2, 3, UEFA), new QuotenStand("CAF", 1, 1, CAF));
    }

    [Fact]
    public void JeVerband_VerbandOhneMannschaftImPool_StehtBeiNull()
    {
        QuotenBelegung.JeVerband([Verband(CAF, "CAF", 5)], [Team(DEUTSCHLAND)])
            .Should().Equal(new QuotenStand("CAF", 0, 5, CAF));
    }

    [Fact]
    public void JeVerband_MannschaftOhneBekanntenVerband_ZaehltNicht()
    {
        var ohneVerband = new AuswahlKandidat(999, "Ohne Verband", DEUTSCHLAND, null);

        QuotenBelegung.JeVerband([Verband(UEFA, "UEFA", 2)], [ohneVerband, Team(SPANIEN)])
            .Should().Equal(new QuotenStand("UEFA", 1, 2, UEFA));
    }

    [Fact]
    public void JeVerband_OhneQuoten_LeereListe()
    {
        QuotenBelegung.JeVerband([], [Team(DEUTSCHLAND)]).Should().BeEmpty();
    }

    // ── Landesquoten ──────────────────────────────────────────────────────────

    [Fact]
    public void JeLand_ZaehltUeberDasLandDerMannschaft()
    {
        var staende = QuotenBelegung.JeLand(
            [Land(DEUTSCHLAND, "Deutschland", 4), Land(SPANIEN, "Spanien", 1), Land(GHANA, "Ghana", 2)],
            [Team(DEUTSCHLAND), Team(DEUTSCHLAND), Team(SPANIEN)]);

        staende.Should().Equal(
            new QuotenStand("Deutschland", 2, 4, DEUTSCHLAND), new QuotenStand("Spanien", 1, 1, SPANIEN),
            new QuotenStand("Ghana", 0, 2, GHANA));
    }

    [Fact]
    public void JeLand_KontingenteEinesLandes_BildenEinenStand()
    {
        QuotenBelegung.JeLand(
                [Land(DEUTSCHLAND, "Deutschland", 4, 1, 1), Land(DEUTSCHLAND, "Deutschland", 2, 1, 5)], [Team(DEUTSCHLAND)])
            .Should().Equal(new QuotenStand("Deutschland", 1, 6, DEUTSCHLAND));
    }

    // ── Stand einer Quote ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(2, 3, true, 0, 2)]
    [InlineData(3, 3, false, 0, 3)]
    [InlineData(5, 3, false, 2, 3)]
    public void Quote_VerletztSolangeNichtVoll_UeberzaehligeBelegenFreiePlaetze(
        int aktuell, int anzahl, bool verletzt, int ueberzaehlig, int besetzt)
    {
        var stand = new QuotenStand("Deutschland", aktuell, anzahl, DEUTSCHLAND);

        stand.Verletzt.Should().Be(verletzt);
        stand.Ueberzaehlig.Should().Be(ueberzaehlig);
        stand.Besetzt.Should().Be(besetzt);
    }

    [Theory]
    [InlineData(3, 3, false)]
    [InlineData(2, 3, false)]
    [InlineData(4, 3, true)]
    public void FreiePlaetze_VerletztNurBeiUeberbelegung(int aktuell, int anzahl, bool verletzt)
    {
        new QuotenStand("Freie Plätze", aktuell, anzahl).Verletzt.Should().Be(verletzt);
    }

    // ── Freie Plätze (#572) ───────────────────────────────────────────────────

    [Fact]
    public void FreiePlaetze_EingangOhneQuotenUndTeamsUeberDerQuote()
    {
        // Eingang 8, Quoten 2 + 3 → 3 freie Plätze. Belegt: das dritte Ghana-Team über seiner Quote von 2 und das
        // Spanien-Team ohne Quote; das Deutschland-Team zählt auf seine Quote.
        AuswahlKandidat[] pool = [Team(GHANA), Team(GHANA), Team(GHANA), Team(SPANIEN), Team(DEUTSCHLAND)];
        var staende = QuotenBelegung.JeLand([Land(GHANA, "Ghana", 2), Land(DEUTSCHLAND, "Deutschland", 3)], pool);

        QuotenBelegung.FreiePlaetze(staende, pool.Length, eingang: 8).Should().Be(new QuotenStand("Freie Plätze", 2, 3));
    }
}
