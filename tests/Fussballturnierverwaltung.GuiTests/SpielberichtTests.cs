using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;
using static Fussballturnierverwaltung.GuiTests.MiniTurnier;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Spielbericht aus dem Spielplan (#302, #534): „Ausführlich simulieren“ simuliert ein Gruppenspiel im Reiter
/// „Simulation“, „Spielverlauf ansehen“ zeigt später dasselbe Protokoll, ohne neu zu simulieren — und das eines schnell
/// simulierten Spiels, dessen Ereignisse die Simulation immer mitspeichert (#171). Geprüft werden Invarianten: Der
/// Stand im Spielkopf ist der des Spielplans, die Tore der Zeitleiste ergeben ihn, der Zeitstrahl zeigt je Seite
/// dieselben Ereignisse wie die Zeitleiste.</summary>
public sealed class SpielberichtTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    [Fact]
    public void AusfuehrlichUndSpielverlauf_ZeigenDasErgebnisDesSpielplans()
    {
        var (turnier, auslosung) = GruppenauslosungVorbereiten(app.Hauptfenster);
        auslosung.Durchfuehren();
        var spielplan = turnier.Spielplan();
        spielplan.RundeWaehlen(GRUPPENPHASE);
        var spiele = spielplan.Spiele();
        var (erstes, zweites) = (spiele[0], spiele[1]);

        spielplan.SpielWaehlen(erstes.Heim, erstes.Auswaerts);
        var ausfuehrlich = turnier.AusfuehrlichSimulieren().BerichtAbwarten(erstes.Heim, erstes.Auswaerts);
        BerichtStimmig(ausfuehrlich, ErgebnisImSpielplan(turnier, erstes));

        // Zuerst das zweite Spiel ansehen: Der Reiter zeigt danach nicht mehr den Bericht des ersten, das erneute
        // Ansehen des ersten muss ihn also wirklich laden (sonst bestünde der Vergleich schon mit dem alten Bericht).
        spielplan = turnier.Spielplan();
        spielplan.RundeSimulieren(SPIELE_GRUPPENPHASE - 1);
        spielplan.SpielWaehlen(zweites.Heim, zweites.Auswaerts);
        var schnell = turnier.SpielverlaufAnsehen().BerichtAbwarten(zweites.Heim, zweites.Auswaerts);
        BerichtStimmig(schnell, ErgebnisImSpielplan(turnier, zweites));

        turnier.Spielplan().SpielWaehlen(erstes.Heim, erstes.Auswaerts);
        var angesehen = turnier.SpielverlaufAnsehen().BerichtAbwarten(erstes.Heim, erstes.Auswaerts);
        angesehen.Should().BeEquivalentTo(ausfuehrlich, "„Spielverlauf ansehen“ zeigt das gespeicherte Protokoll");
        ErgebnisImSpielplan(turnier, erstes).Should().Be(ausfuehrlich.Kopf.Tore, "Ansehen simuliert nicht neu");
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private static (int Heim, int Auswaerts)? ErgebnisImSpielplan(TurnierdurchfuehrungSeite turnier, SpielZeile spiel)
    {
        var spielplan = turnier.Spielplan();
        spielplan.RundeWaehlen(GRUPPENPHASE);
        return Spielstand.Tore(spielplan.Spiele().Single(s => s.Heim == spiel.Heim && s.Auswaerts == spiel.Auswaerts).Ergebnis);
    }

    private static void BerichtStimmig(Spielbericht bericht, (int Heim, int Auswaerts)? imSpielplan)
    {
        bericht.Kopf.Tore.Should().NotBeNull().And.Be(imSpielplan, "der Spielkopf zeigt das Ergebnis des Spielplans");
        bericht.ToreAusEreignissen.Should().Be(bericht.Kopf.Tore!.Value, "die Tore der Zeitleiste ergeben den Stand");
        bericht.HeimMarker.Should().BeEquivalentTo(bericht.HeimOhneElfmeterschiessen,
            "der Zeitstrahl zeigt die Ereignisse der Heimmannschaft");
        bericht.AuswaertsMarker.Should().BeEquivalentTo(bericht.AuswaertsOhneElfmeterschiessen,
            "der Zeitstrahl zeigt die Ereignisse der Auswärtsmannschaft");
    }
}
