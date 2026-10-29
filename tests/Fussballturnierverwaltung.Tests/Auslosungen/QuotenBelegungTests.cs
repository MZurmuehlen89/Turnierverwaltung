using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

public class QuotenBelegungTests
{
    // ── Testdaten ─────────────────────────────────────────────────────────────

    private static readonly Verband _uefa = new() { Id = 1, Name = "UEFA" };
    private static readonly Verband _caf = new() { Id = 2, Name = "CAF" };
    private static readonly Land _deutschland = new() { Id = 10, Name = "Deutschland", VerbandId = 1 };
    private static readonly Land _spanien = new() { Id = 11, Name = "Spanien", VerbandId = 1 };
    private static readonly Land _ghana = new() { Id = 20, Name = "Ghana", VerbandId = 2 };

    private static Mannschaft Team(Land land) => new() { Name = $"Team {land.Name}", LandId = land.Id, Land = land };

    private static Lostopf Topf(params LostopfEintrag[] eintraege) => new() { Eintraege = eintraege.ToList() };

    private static LostopfEintrag Eintrag(Mannschaft mannschaft) => new() { Mannschaft = mannschaft };

    private static LostopfEintrag Platzhalter(string name) => new() { PlatzhalterName = name };

    private static VerbandsQuote Quote(Verband verband, int anzahl) =>
        new() { VerbandId = verband.Id, Verband = verband, Anzahl = anzahl };

    private static LandesQuote Quote(Land land, int anzahl) =>
        new() { LandId = land.Id, Land = land, Anzahl = anzahl };

    // ── Verbandsquoten ────────────────────────────────────────────────────────

    [Fact]
    public void JeVerband_ZaehltMannschaftenAllerToepfeUeberDenVerbandIhresLandes()
    {
        var toepfe = new[]
        {
            Topf(Eintrag(Team(_deutschland)), Eintrag(Team(_ghana))),
            Topf(Eintrag(Team(_spanien))),
        };

        var staende = QuotenBelegung.JeVerband([Quote(_uefa, 3), Quote(_caf, 1)], toepfe);

        staende.Should().Equal(new QuotenStand("UEFA", 2, 3, 1), new QuotenStand("CAF", 1, 1, 2));
    }

    [Fact]
    public void JeVerband_VerbandOhneMannschaftImTopf_StehtBeiNull()
    {
        var staende = QuotenBelegung.JeVerband([Quote(_caf, 5)], [Topf(Eintrag(Team(_deutschland)))]);

        staende.Should().Equal(new QuotenStand("CAF", 0, 5, 2));
    }

    [Fact]
    public void JeVerband_PlatzhalterUndMannschaftenOhneGeladenesLand_ZaehlenNicht()
    {
        var ohneLand = new Mannschaft { Name = "Ohne Land", LandId = _deutschland.Id, Land = null! };

        var staende = QuotenBelegung.JeVerband(
            [Quote(_uefa, 2)], [Topf(Platzhalter("Sieger Gruppe A"), Eintrag(ohneLand), Eintrag(Team(_spanien)))]);

        staende.Should().Equal(new QuotenStand("UEFA", 1, 2, 1));
    }

    [Fact]
    public void JeVerband_VerbandNichtMitgeladen_ErsatznameMitId()
    {
        var quote = new VerbandsQuote { VerbandId = 7, Verband = null!, Anzahl = 4 };

        QuotenBelegung.JeVerband([quote], []).Should().Equal(new QuotenStand("Verband 7", 0, 4, 7));
    }

    [Fact]
    public void JeVerband_OhneQuoten_LeereListe()
    {
        QuotenBelegung.JeVerband([], [Topf(Eintrag(Team(_deutschland)))]).Should().BeEmpty();
    }

    // ── Landesquoten ──────────────────────────────────────────────────────────

    [Fact]
    public void JeLand_ZaehltUeberDieLandIdDerMannschaft_AuchOhneGeladenesLand()
    {
        var ohneLand = new Mannschaft { Name = "Ohne Land", LandId = _deutschland.Id, Land = null! };
        var toepfe = new[]
        {
            Topf(Eintrag(Team(_deutschland)), Eintrag(ohneLand), Platzhalter("Sieger Spiel 1")),
            Topf(Eintrag(Team(_spanien))),
        };

        var staende = QuotenBelegung.JeLand([Quote(_deutschland, 4), Quote(_spanien, 1), Quote(_ghana, 2)], toepfe);

        staende.Should().Equal(
            new QuotenStand("Deutschland", 2, 4, 10), new QuotenStand("Spanien", 1, 1, 11), new QuotenStand("Ghana", 0, 2, 20));
    }

    [Fact]
    public void JeLand_UeberschritteneQuote_AktuellLiegtUeberDerAnzahl()
    {
        var toepfe = new[] { Topf(Eintrag(Team(_ghana)), Eintrag(Team(_ghana))) };

        QuotenBelegung.JeLand([Quote(_ghana, 1)], toepfe).Should().Equal(new QuotenStand("Ghana", 2, 1, 20));
    }

    [Fact]
    public void JeLand_LandNichtMitgeladen_ErsatznameMitId()
    {
        var quote = new LandesQuote { LandId = 33, Land = null!, Anzahl = 2 };

        QuotenBelegung.JeLand([quote], []).Should().Equal(new QuotenStand("Land 33", 0, 2, 33));
    }

    [Fact]
    public void JeLand_KontingenteEinesLandes_BildenEinenStand()
    {
        LandesQuote[] kontingente =
        [
            new() { LandId = _deutschland.Id, Land = _deutschland, Spielklasse = 1, AbPlatz = 1, Anzahl = 4 },
            new() { LandId = _deutschland.Id, Land = _deutschland, Spielklasse = 1, AbPlatz = 5, Anzahl = 2 },
        ];

        QuotenBelegung.JeLand(kontingente, [Topf(Eintrag(Team(_deutschland)))])
            .Should().Equal(new QuotenStand("Deutschland", 1, 6, 10));
    }

    // ── Freie Plätze (#572) ───────────────────────────────────────────────────

    [Fact]
    public void FreiePlaetze_EingangOhneQuotenUndTeamsUeberDerQuote()
    {
        // Eingang 8, Quoten 2 + 3 → 3 freie Plätze. Belegt: das dritte Ghana-Team über seiner Quote von 2 und das
        // Spanien-Team ohne Quote; das Deutschland-Team zählt auf seine Quote.
        var toepfe = new[]
        {
            Topf(Eintrag(Team(_ghana)), Eintrag(Team(_ghana)), Eintrag(Team(_ghana))),
            Topf(Eintrag(Team(_spanien)), Eintrag(Team(_deutschland)), Platzhalter("Sieger Spiel 1")),
        };
        var staende = QuotenBelegung.JeLand([Quote(_ghana, 2), Quote(_deutschland, 3)], toepfe);

        QuotenBelegung.FreiePlaetze(staende, toepfe, eingang: 8).Should().Be(new QuotenStand("Freie Plätze", 2, 3));
    }
}
