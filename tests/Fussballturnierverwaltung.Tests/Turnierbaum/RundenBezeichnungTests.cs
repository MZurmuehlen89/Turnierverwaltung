using FluentAssertions;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;

namespace Fussballturnierverwaltung.Tests.Turnierbaum;

/// <summary>Issue #438: Namen, Kürzel und Knoten-Labels der Runden im Turnierbaum.</summary>
public class RundenBezeichnungTests
{
    private static BracketRunde Runde(string? name, int anzahlSpiele, Spiel? spiel = null) => new(
        1,
        Enumerable.Range(1, anzahlSpiele).Select(i => new TurnierBaumKnoten { Id = i, Spiel = spiel }).ToList(),
        new Dictionary<int, Spiel>(),
        name);

    [Fact]
    public void Name_IstDerNameDerTurnierrunde()
    {
        RundenBezeichnung.Name(Runde("Playoff-Runde", 8)).Should().Be("Playoff-Runde");
    }

    [Theory]
    [InlineData(1, "Finale")]
    [InlineData(2, "Halbfinale")]
    [InlineData(4, "Viertelfinale")]
    [InlineData(8, "Achtelfinale")]
    [InlineData(16, "Letzte 32")]
    [InlineData(32, "Letzte 64")]
    [InlineData(3, "3 Spiele")]
    public void Name_OhneRundennameAusDerSpielanzahl(int anzahlSpiele, string erwartet)
    {
        RundenBezeichnung.Name(Runde(null, anzahlSpiele)).Should().Be(erwartet);
    }

    [Theory]
    [InlineData("Finale", "F")]
    [InlineData("Halbfinale", "HF")]
    [InlineData("Viertelfinale", "VF")]
    [InlineData(" achtelfinale ", "AF")]
    [InlineData("Sechzehntelfinale", "L32-")]
    [InlineData("Spiel um Platz 3", "P3")]
    [InlineData("Spiel um Platz drei", "P3")]
    [InlineData("Playoff-Runde", "PO")]
    [InlineData("Play-off", "PO")]
    [InlineData("Zwischenrunde", "Z")]
    [InlineData("Erste Hauptrunde", "EH")]
    [InlineData("Runde 2", "R2-")]
    public void Kuerzel_AusDemRundennamen(string name, string erwartet)
    {
        // Die Spielanzahl (8) spräche für "AF" — der Name geht vor.
        RundenBezeichnung.Kuerzel(Runde(name, 8)).Should().Be(erwartet);
    }

    [Theory]
    [InlineData(null, 1, "F")]
    [InlineData(null, 2, "HF")]
    [InlineData(null, 4, "VF")]
    [InlineData(null, 8, "AF")]
    [InlineData(null, 16, "L32-")]
    [InlineData(null, 32, "L64-")]
    [InlineData(null, 3, "R3-")]
    [InlineData("  ", 4, "VF")]
    [InlineData("-", 4, "VF")]
    public void Kuerzel_OhneVerwertbarenNamenAusDerSpielanzahl(string? name, int anzahlSpiele, string erwartet)
    {
        RundenBezeichnung.Kuerzel(Runde(name, anzahlSpiele)).Should().Be(erwartet);
    }

    [Theory]
    [InlineData("Playoff-Runde", 8, "PO")]
    [InlineData(null, 16, "L32-")]
    [InlineData(null, 1, "F")]
    public void Kuerzel_OhneTurnierbaumAusNameUndSpielanzahl(string? name, int anzahlSpiele, string erwartet)
    {
        RundenBezeichnung.Kuerzel(name, anzahlSpiele).Should().Be(erwartet);
    }

    [Fact]
    public void Kuerzel_SpielUmPlatzDreiOhneNamen_HeisstNichtWieDasFinale()
    {
        RundenBezeichnung.Kuerzel(null, 1, istSpielUmPlatzDrei: true).Should().Be("P3");
    }

    [Theory]
    [InlineData("Achtelfinale", 8, 2, "AF3")]
    [InlineData("Sechzehntelfinale", 16, 0, "L32-1")]
    [InlineData("Runde 2", 4, 3, "R2-4")]
    [InlineData("Finale", 1, 0, "F")]
    [InlineData("Runde 2", 1, 0, "R2")]
    public void KnotenLabel_KuerzelMitNummerUndBeiEinemSpielDasKuerzelAllein(
        string name, int anzahlSpiele, int position, string erwartet)
    {
        RundenBezeichnung.KnotenLabel(Runde(name, anzahlSpiele), position).Should().Be(erwartet);
    }

    [Fact]
    public void HatFestePaarungen_WennEineSpaetereRundePlatzhalterNennt()
    {
        var mitPlatzhalter = new Spiel { HeimPlatzhalterName = "Sieger AF1" };

        RundenBezeichnung.HatFestePaarungen([Runde("Achtelfinale", 2, new Spiel()), Runde("Finale", 1, mitPlatzhalter)])
            .Should().BeTrue();
    }

    [Fact]
    public void HatFestePaarungen_PlatzhalterNurInDerErstenRundeZaehlenNicht()
    {
        var mitPlatzhalter = new Spiel { AuswaertsPlatzhalterName = "Zweiter Gruppe A" };

        RundenBezeichnung.HatFestePaarungen([Runde("Achtelfinale", 2, mitPlatzhalter), Runde("Finale", 1, new Spiel())])
            .Should().BeFalse();
        RundenBezeichnung.HatFestePaarungen([Runde("Achtelfinale", 2), Runde("Finale", 1)])
            .Should().BeFalse("Knoten ohne Spiel sind keine festen Paarungen");
    }
}
