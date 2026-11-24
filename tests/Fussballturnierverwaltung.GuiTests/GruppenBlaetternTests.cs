using FluentAssertions;
using static Fussballturnierverwaltung.GuiTests.MiniTurnier;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Gruppen tragen überall Buchstaben und lassen sich im gefilterten Spielplan durchblättern (#544): Kopf der
/// Gruppentabellen und Gruppenleiste zeigen „Gruppe A“, ‹ ist bei der ersten, › bei der letzten Gruppe gesperrt.</summary>
public sealed class GruppenBlaetternTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    [Fact]
    public void Spielplan_ZeigtBuchstabenUndBlaettertZwischenGruppen_AnDenEndenGesperrt()
    {
        var (turnier, auslosung) = GruppenauslosungVorbereiten(app.Hauptfenster);
        auslosung.Durchfuehren();
        var spielplan = turnier.Spielplan();
        spielplan.RundeWaehlen(GRUPPENPHASE);

        spielplan.GruppenTabellen().Keys.Should().Contain("Gruppe A").And.Contain("Gruppe B");

        spielplan.GruppeFiltern("Gruppe A");
        spielplan.VorigeGruppeMoeglich.Should().BeFalse("vor der ersten Gruppe hört das Blättern auf");
        spielplan.NaechsteGruppeMoeglich.Should().BeTrue();

        spielplan.NaechsteGruppe();
        spielplan.GewaehlteGruppe.Should().Be("Gruppe B");
        spielplan.NaechsteGruppeMoeglich.Should().BeFalse("hinter der letzten Gruppe hört das Blättern auf");
        spielplan.VorigeGruppeMoeglich.Should().BeTrue();

        spielplan.VorigeGruppe();
        spielplan.GewaehlteGruppe.Should().Be("Gruppe A");
    }

    public void Dispose()
    {
        app.BeiFehlschlagBelegeSichern();
        app.Hauptfenster.Aufraeumen();
    }
}
