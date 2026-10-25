using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;

namespace Fussballturnierverwaltung.Tests.Spielplan;

public class SchweizerVerlaufTests
{
    private static Spiel Spiel(int spieltag, int? heim, int? gast) =>
        new() { Spieltag = spieltag, HeimMannschaftId = heim, AuswaertsMannschaftId = gast };

    [Fact]
    public void OhneSpiele_ErsterSpieltag_OhneVerlauf()
    {
        var verlauf = SchweizerVerlauf.AusSpielen([], [1, 2, 3, 4]);

        verlauf.NaechsterSpieltag.Should().Be(1);
        verlauf.BereitsGespielt.Should().BeEmpty();
        verlauf.FreilosHistorie.Should().BeEmpty();
        verlauf.HeimBilanz.Should().BeEmpty();
    }

    [Fact]
    public void ZweiSpieltage_ZaehltPaarungenUnabhaengigVomHeimrecht_UndHeimspieleJeTeam()
    {
        Spiel[] spiele = [Spiel(1, 1, 2), Spiel(1, 3, 4), Spiel(2, 4, 1), Spiel(2, 2, 3)];

        var verlauf = SchweizerVerlauf.AusSpielen(spiele, [1, 2, 3, 4]);

        verlauf.NaechsterSpieltag.Should().Be(3);
        verlauf.BereitsGespielt.Should().Equal((1, 2), (3, 4), (1, 4), (2, 3));
        verlauf.FreilosHistorie.Should().BeEmpty();
        verlauf.HeimBilanz.Should().Equal(new Dictionary<int, int> { [1] = 1, [3] = 1, [4] = 1, [2] = 1 });
    }

    [Fact]
    public void TeamMitWenigerSpielenAlsSpieltagen_HatteEinFreilos()
    {
        // Fünf Teams: an Spieltag 1 setzt Team 5 aus, an Spieltag 2 Team 1
        Spiel[] spiele = [Spiel(1, 1, 2), Spiel(1, 3, 4), Spiel(2, 5, 2), Spiel(2, 4, 3)];

        var verlauf = SchweizerVerlauf.AusSpielen(spiele, [1, 2, 3, 4, 5]);

        verlauf.FreilosHistorie.Should().BeEquivalentTo([1, 5]);
        verlauf.BereitsGespielt.Should().Equal([(1, 2), (3, 4), (2, 5)], "4-3 ist dieselbe Paarung wie 3-4");
    }

    [Fact]
    public void SpieleMitPlatzhalter_ZaehlenNicht_AberDerSpieltag()
    {
        Spiel[] spiele = [Spiel(1, 1, 2), Spiel(2, null, 1)];

        var verlauf = SchweizerVerlauf.AusSpielen(spiele, [1, 2]);

        verlauf.NaechsterSpieltag.Should().Be(3);
        verlauf.BereitsGespielt.Should().Equal((1, 2));
        verlauf.HeimBilanz.Should().Equal(new Dictionary<int, int> { [1] = 1 });
        verlauf.FreilosHistorie.Should().BeEquivalentTo([1, 2], "beide haben an zwei Spieltagen nur einmal gespielt");
    }
}
