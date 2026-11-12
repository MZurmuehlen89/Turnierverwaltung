using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;
using GuiTest.Framework;
using static Fussballturnierverwaltung.GuiTests.MiniTurnier;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>#567 (Spec-Pilot #591): In der rundenweisen Ansicht liegt das Spiel um Platz 3 zwischen Halbfinale und
/// Finale (Vorbild kicker.de). Die Akzeptanzkriterien stehen im Issue; hier die, die nur die laufende App zeigt:
/// AK2 (Blättern) und die Höhen aus AK3/AK4. Die Linien selbst haben keinen UIA-Knoten — ihre Geometrie prüfen die
/// Unit-Tests (<c>RundenbaumLayoutTests</c>, <c>RundenFensterTests</c>, <c>VerbindungslinienTests</c>).</summary>
public sealed class TurnierbaumPlatzDreiTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    // Karten stehen per Canvas.Top auf ganzen Pixeln; ein Pixel Rundung je Seite.
    private const double TOLERANZ = 2.0;

    [Fact]
    public void Rundenweise_HalbfinaleMuendetInPlatzDrei_PlatzDreiStehtVorDemFinale()
    {
        var (turnier, auslosung) = GruppenauslosungVorbereiten(app.Hauptfenster);
        auslosung.Durchfuehren();
        turnier.RundeSimulieren(GRUPPENPHASE, SPIELE_GRUPPENPHASE);
        var halbfinale = turnier.Auslosung();
        halbfinale.Anlegen(HALBFINALE);
        halbfinale.ToepfeAutomatischBefuellen();
        halbfinale.Durchfuehren();

        var spielplan = turnier.Spielplan();
        spielplan.RundeWaehlen(HALBFINALE);
        var baum = spielplan.Turnierbaum();

        // AK2/AK3: Halbfinale | Spiel um Platz 3, die Platz-3-Karte mittig zwischen den Halbfinals.
        baum.WarteAufFenster(HALBFINALE, SPIEL_UM_PLATZ_DREI);
        var (halbfinals, platzDrei) = baum.KartenMittenNachLayout(links: 2, rechts: 1,
            (l, r) => Math.Abs(r[0] - (l[0] + l[1]) / 2) <= TOLERANZ);
        platzDrei.Single().Should().BeApproximately((halbfinals[0] + halbfinals[1]) / 2, TOLERANZ,
            "die Platz-3-Karte steht mittig zwischen den Halbfinals");

        // AK2/AK4: weiter zu Spiel um Platz 3 | Finale, beide auf derselben Höhe.
        baum.NaechsteRunde();
        baum.WarteAufFenster(SPIEL_UM_PLATZ_DREI, FINALE);
        var (links, finale) = baum.KartenMittenNachLayout(links: 1, rechts: 1,
            (l, r) => Math.Abs(r[0] - l[0]) <= TOLERANZ);
        finale.Single().Should().BeApproximately(links.Single(), TOLERANZ, "das Finale steht auf Höhe der Platz-3-Karte");
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();
}
