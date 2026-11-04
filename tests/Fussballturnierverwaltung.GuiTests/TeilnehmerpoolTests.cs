using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Teilnehmerpool (#576): Conference League mit der Landesquote „Deutschland · 4“ — der Pool ist vorbelegt und
/// erfüllt die Quote. Ein deutscher Verein per Doppelklick heraus verletzt sie, „Automatisch befüllen“ ist dann gesperrt
/// und der Reiter Lostöpfe weist darauf hin; ein Reiterwechsel fragt, ob der geänderte Pool verworfen werden soll. Ein
/// deutscher Verein hinein, und alles ist wieder möglich. Eigene
/// Klasse und damit eigene App-Sitzung, weil das Anlegen mit ungespeicherten Quoten abbrechen kann (wie
/// <see cref="QuoteFilterTests"/>).</summary>
public sealed class TeilnehmerpoolTests(TurnierverwaltungApp app)
    : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string LAND = "Deutschland";
    private const string GESAMT = "Gesamt";
    private const string VOLL = "36 / 36";

    [Fact]
    public void Pool_VorbelegtTauschenPerDoppelklick_SperreBisDieQuoteWiederStimmt()
    {
        var (turnier, _) = QuotenTurnier.Anlegen(app, KontingentTurnier.VORLAGE, KontingentTurnier.VARIANTE, quoten =>
        {
            quoten.LandHinzufuegen();
            quoten.LandWaehlen(0, LAND);
            quoten.AnzahlSetzen(0, 4);
        });
        var auslosung = turnier.Auslosung();
        auslosung.Anlegen("Ligaphase");
        auslosung.TeilnehmerpoolOeffnen();

        auslosung.Quote(GESAMT).Should().Be(VOLL, "der Pool ist aus den Ranglisten vorbelegt");
        auslosung.Quote(LAND).Should().Be("4 / 4");
        auslosung.PoolMaengel.Should().BeEmpty();

        auslosung.QuoteAnklicken(LAND);
        KontingentTurnier.Abwarten(() => auslosung.LandFilter == LAND);
        string heraus = auslosung.ErstesAusPoolNehmen();
        KontingentTurnier.Abwarten(() => auslosung.Quote(GESAMT) == "35 / 36");
        auslosung.Quote(GESAMT).Should().Be("35 / 36", "'{0}' hat den Pool verlassen", heraus);
        auslosung.PoolMaengel.Should().NotBeEmpty("dem Pool fehlt eine Mannschaft");
        auslosung.PoolMeldungen.Should().Be(1, "was dem Pool fehlt, steht nur einmal da (#593)");
        auslosung.ReiterwechselAbbrechen("Statistik");
        auslosung.Quote(GESAMT).Should().Be("35 / 36", "nach abgelehnter Rückfrage bleibt der geänderte Pool stehen");
        auslosung.AutomatischBefuellenMoeglich().Should().BeFalse("die Automatik wartet, bis der Pool seine Quoten erfüllt");
        auslosung.LostoepfeOeffnen();
        auslosung.PoolHinweis.Should().NotBeEmpty("der Reiter Lostöpfe weist auf den Pool hin");
        auslosung.PoolMeldungen.Should().Be(1, "auch im Reiter Lostöpfe nur eine Meldung");

        auslosung.TeilnehmerpoolOeffnen();
        string hinein = auslosung.ErstesInPoolAufnehmen();
        KontingentTurnier.Abwarten(() => auslosung.Quote(GESAMT) == VOLL);
        auslosung.Quote(GESAMT).Should().Be(VOLL, "'{0}' ist in den Pool gekommen", hinein);
        auslosung.Quote(LAND).Should().Be("4 / 4");
        auslosung.PoolMaengel.Should().BeEmpty();
        auslosung.AutomatischBefuellenMoeglich().Should().BeTrue("der Pool erfüllt seine Quoten wieder");
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();
}
