using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>K.-o. mit Hin- und Rückspiel und Auswärtstorregel (#532), mit von Hand eingetragenen Ergebnissen – eine
/// Simulation träfe die beiden Grenzfälle nur zufällig. Ein von Hand aufgebautes Turnier: Halbfinale (4 Teams, Hin-
/// und Rückspiel, Auswärtstorregel) → Finale. Paarung 1 steht insgesamt gleich, ein Team hat mehr Auswärtstore; in
/// Paarung 2 sind auch die Auswärtstore gleich, nach torloser Verlängerung entscheidet das Elfmeterschießen.</summary>
public sealed class AuswaertstorUndElfmeterTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string HALBFINALE = "Halbfinale";
    private const string FINALE = "Finale";

    [Fact]
    public void HinUndRueckspiel_AuswaertstoreUndElfmeterschiessenEntscheiden()
    {
        var turnier = TurnierAufbauen();
        var auslosung = turnier.Auslosung();
        auslosung.Anlegen(HALBFINALE, "Zufällig");
        auslosung.RanglisteWaehlen(MiniTurnier.RANGLISTE);
        auslosung.ToepfeAutomatischBefuellen();
        auslosung.Durchfuehren();

        var paare = Spielstand.HinUndRueckspiele(turnier.SpieleDerRunde(HALBFINALE));
        paare.Should().HaveCount(2, "zwei Paarungen mit Hin- und Rückspiel");
        var (auswaertstore, elfmeter) = (paare[0], paare[1]);

        var spielplan = turnier.Spielplan();
        // Gesamt 2:2, auswärts erzielt: Hinspiel-Gast 1, Rückspiel-Gast 0 → der Gast des Hinspiels kommt weiter.
        spielplan.ErgebnisEintragen(auswaertstore.Hin.Heim, auswaertstore.Hin.Auswaerts, (2, 1));
        spielplan.ErgebnisEintragen(auswaertstore.Rueck.Heim, auswaertstore.Rueck.Auswaerts, (1, 0));
        // Gesamt 1:1, auswärts je 0 → Verlängerung ohne Tor, Elfmeterschießen 3:4 aus Sicht des Rückspiel-Gastgebers.
        spielplan.ErgebnisEintragen(elfmeter.Hin.Heim, elfmeter.Hin.Auswaerts, (1, 0));
        spielplan.ErgebnisEintragen(elfmeter.Rueck.Heim, elfmeter.Rueck.Auswaerts, (1, 0),
            nachVerlaengerung: (1, 0), elfmeter: (3, 4));

        var halbfinale = Spielstand.HinUndRueckspiele(turnier.SpieleDerRunde(HALBFINALE));
        string ueberAuswaertstore = auswaertstore.Hin.Auswaerts;
        string imElfmeterschiessen = elfmeter.Hin.Heim;
        halbfinale.Select(p => Spielstand.Weiterkommender(p.Hin, p.Rueck, auswaertstorregel: true))
            .Should().BeEquivalentTo([ueberAuswaertstore, imElfmeterschiessen], "so sind die Ergebnisse eingetragen");
        halbfinale.Single(p => p.Rueck.Heim == elfmeter.Rueck.Heim).Rueck.Zusatz.Should().Contain("i.E. 3:4");

        var finale = turnier.SpieleDerRunde(FINALE).Should().ContainSingle().Subject;
        new[] { finale.Heim, finale.Auswaerts }.Should().BeEquivalentTo([ueberAuswaertstore, imElfmeterschiessen],
            "ins Finale kommen der Sieger nach Auswärtstoren und der Sieger im Elfmeterschießen");
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private TurnierdurchfuehrungSeite TurnierAufbauen()
    {
        string name = HandTurnier.EindeutigerName();
        var turniere = app.Hauptfenster.TurniereOeffnen();
        turniere.Anlegen(name, "2027", 4, HandTurnier.REGELWERK);

        var runden = turniere.Runden();
        runden.Anlegen(HALBFINALE, "K.o.-Runde", 4, 2);
        runden.MitRueckspiel(auswaertstorregel: true);
        runden.Anlegen(FINALE, "Finale", 2, 1);
        runden.Auswaehlen(HALBFINALE);
        runden.AusgehendeRegelAnlegen(FINALE);
        runden.Speichern();

        var turnier = app.Hauptfenster.TurnierdurchfuehrungOeffnen();
        turnier.GewaehltesTurnier.Should().Be(name);
        return turnier;
    }
}
