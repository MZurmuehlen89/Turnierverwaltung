using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Wiederholungsspiel von Hand eingegeben (#547, K11): Regelwerk „Verlängerung + Wiederholungsspiel + Los“.
/// Ein von Hand aufgebautes Turnier – Halbfinale (4 Teams, zufällig ausgelost) → Finale. Spiel 1 wird nach torgleicher
/// Verlängerung im Wiederholungsspiel entschieden; Spiel 2 bleibt auch im Wiederholungsspiel und seiner Verlängerung
/// torgleich, dann entscheidet das Los. Die Ergebnisse sind eingetragen statt simuliert – eine Simulation träfe die
/// Fälle nur zufällig.</summary>
public sealed class WiederholungsspielTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string HALBFINALE = "Halbfinale";
    private const string FINALE = "Finale";
    private const string REGELWERK = "Historisch: Verlängerung + Wiederholungsspiel + Los";

    [Fact]
    public void Wiederholungsspiel_EntscheidetNachTorgleicherVerlaengerung_LosErstDanach()
    {
        var (turnier, spielplan, halbfinale) = TurnierAufbauen();
        var leiste = spielplan.Leiste();
        var (entschieden, gelost) = (halbfinale[0], halbfinale[1]);

        // Spiel 1: 1:1, 1:1 n.V. → das Wiederholungsspiel wird fällig, das Los noch nicht.
        spielplan.SpielWaehlen(entschieden.Heim, entschieden.Auswaerts);
        leiste.Zeigt(Ergebnisleiste.WIEDERHOLUNG).Should().BeFalse("vor einem Remis gibt es kein Wiederholungsspiel");
        leiste.ToreSetzen(Ergebnisleiste.TORE, (1, 1));
        leiste.ToreSetzen(Ergebnisleiste.VERLAENGERUNG, (1, 1));
        leiste.WarteAufZeile(Ergebnisleiste.WIEDERHOLUNG, sichtbar: true);
        leiste.ZeigtLos.Should().BeFalse("das Los folgt erst, wenn auch das Wiederholungsspiel torgleich endet");
        leiste.SpeichernMoeglich.Should().BeFalse("ohne die Tore des Wiederholungsspiels ist Speichern gesperrt");
        leiste.Zeigt(Ergebnisleiste.WIEDERHOLUNG_VERLAENGERUNG).Should().BeFalse("das Wiederholungsspiel ist noch nicht torgleich");
        leiste.ToreSetzen(Ergebnisleiste.WIEDERHOLUNG, (0, 2));
        leiste.Speichern();

        // Spiel 2: 0:0, 0:0 n.V., Wiederholungsspiel 1:1 → eigene Verlängerung, 1:1 → Los.
        spielplan.SpielWaehlen(gelost.Heim, gelost.Auswaerts);
        leiste.ToreSetzen(Ergebnisleiste.TORE, (0, 0));
        leiste.ToreSetzen(Ergebnisleiste.VERLAENGERUNG, (0, 0));
        leiste.WarteAufZeile(Ergebnisleiste.WIEDERHOLUNG, sichtbar: true);
        leiste.ToreSetzen(Ergebnisleiste.WIEDERHOLUNG, (1, 1));
        leiste.WarteAufZeile(Ergebnisleiste.WIEDERHOLUNG_VERLAENGERUNG, sichtbar: true);
        leiste.ZeigtLos.Should().BeFalse("das Wiederholungsspiel hat noch seine Verlängerung");
        leiste.ToreSetzen(Ergebnisleiste.WIEDERHOLUNG_VERLAENGERUNG, (1, 1));
        Wait.Until(() => leiste.ZeigtLos, Wait.DefaultTimeout, "Zeile „Los fiel auf“ nach torgleichem Wiederholungsspiel");
        leiste.LosFielAuf(heim: true);
        leiste.Speichern();

        // #247: Spielverlauf ansehen – das Wiederholungsspiel steht als eigene Karte neben dem Spiel, mit dem tatsächlichen
        // Heimteam links (die Runde ist nicht neutral, das Heimrecht tauscht) und dem Ergebnis aus dessen Sicht.
        spielplan.SpielWaehlen(entschieden.Heim, entschieden.Auswaerts);
        var karten = turnier.SpielverlaufAnsehen().WiederholungsspielAbwarten(entschieden.Heim, entschieden.Auswaerts);
        karten.Spiel.Score.Should().Be("1:1", "das erste Spiel endete 1:1 und nach Verlängerung (kumulativ eingetragen) weiter 1:1");
        karten.Spiel.Zwischenstaende.Should().Contain("Wdh. 0:2");
        karten.Kontext.Should().Be("Wiederholungsspiel");
        karten.Wiederholungsspiel.Should().Match<SpielKopfAnzeige>(k =>
            k.Heim == entschieden.Auswaerts && k.Auswaerts == entschieden.Heim && k.Score == "2:0" && k.Entscheidung == "Heimrecht getauscht");
        turnier.Spielplan(); // zurück in den Reiter Spielplan

        var spiele = spielplan.Spiele();
        Zeile(spiele, entschieden).Zusatz.Should().Contain("Wdh. 0:2");
        Zeile(spiele, gelost).Zusatz.Should().Contain("Wdh. 1:1 n.V.").And.Contain("Los");
        spielplan.RundeWaehlen(FINALE);
        var finalspiel = spielplan.Spiele().Should().ContainSingle().Subject;
        new[] { finalspiel.Heim, finalspiel.Auswaerts }.Should().BeEquivalentTo([entschieden.Auswaerts, gelost.Heim],
            "ins Finale kommen der Sieger des Wiederholungsspiels und, auf wen das Los fiel");
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private static SpielZeile Zeile(IReadOnlyList<SpielZeile> spiele, SpielZeile gesucht) =>
        spiele.Single(s => s.Heim == gesucht.Heim && s.Auswaerts == gesucht.Auswaerts);

    private (TurnierdurchfuehrungSeite Turnier, SpielplanReiter Spielplan, IReadOnlyList<SpielZeile> Halbfinale) TurnierAufbauen()
    {
        string name = HandTurnier.EindeutigerName();
        var turniere = app.Hauptfenster.TurniereOeffnen();
        turniere.Anlegen(name, "2027", 4, REGELWERK);

        var runden = turniere.Runden();
        runden.Anlegen(HALBFINALE, "K.o.-Runde", 4, 2);
        runden.Anlegen(FINALE, "Finale", 2, 1);
        runden.Auswaehlen(HALBFINALE);
        runden.AusgehendeRegelAnlegen(FINALE);
        runden.Speichern();

        var turnier = app.Hauptfenster.TurnierdurchfuehrungOeffnen();
        turnier.GewaehltesTurnier.Should().Be(name);
        var auslosung = turnier.Auslosung();
        auslosung.Anlegen(HALBFINALE, "Zufällig");
        auslosung.RanglisteWaehlen(MiniTurnier.RANGLISTE);
        auslosung.ToepfeAutomatischBefuellen();
        auslosung.Durchfuehren();

        var halbfinale = turnier.SpieleDerRunde(HALBFINALE);
        halbfinale.Should().HaveCount(2);
        return (turnier, turnier.Spielplan(), halbfinale);
    }
}
