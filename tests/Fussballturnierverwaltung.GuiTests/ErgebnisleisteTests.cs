using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Ergebnis-Leiste unter der Spielliste (#391): Stände in der Reihenfolge des Spielverlaufs, Silver-Goal-Haken,
/// Golden-Goal-Prüfung und Losentscheid von Hand. Je Regelwerk ein von Hand aufgebautes Turnier: Halbfinale (4 Teams,
/// zufällig ausgelost) → Finale. Die Ergebnisse werden von Hand eingetragen – eine Simulation träfe die Fälle nur
/// zufällig.</summary>
public sealed class ErgebnisleisteTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string HALBFINALE = "Halbfinale";
    private const string FINALE = "Finale";

    private const string SILVER_GOAL = "UEFA EM 2004 (Silver Goal)";
    private const string GOLDEN_GOAL = "UEFA EM 1996 (Golden Goal)";
    private const string VERLAENGERUNG_LOS = "FIFA WM 1958–1970 (Verlängerung + Los)";

    // AK1, AK2, AK6, AK7a (AK7b: ErgebnisleistePlatzhalterTests)
    [Fact]
    public void SilverGoal_HakenEntscheidetZwischenSilverGoalUndVerlaengerung()
    {
        var (spielplan, halbfinale) = TurnierAufbauen(SILVER_GOAL);
        var leiste = spielplan.Leiste();

        leiste.ZeigtAuswahlHinweis.Should().BeTrue("ohne gewähltes Spiel bleibt die Leiste mit Hinweis stehen (AK6)");

        var (silverGoal, verlaengerung) = (halbfinale[0], halbfinale[1]);
        spielplan.SpielWaehlen(silverGoal.Heim, silverGoal.Auswaerts);
        leiste.ToreSetzen(Ergebnisleiste.TORE, (2, 1));
        leiste.Zeigt(Ergebnisleiste.VERLAENGERUNG).Should().BeFalse("ohne Remis bleibt nur die 90-Minuten-Zeile (AK1)");
        leiste.ToreSetzen(Ergebnisleiste.TORE, (1, 1));
        leiste.WarteAufZeile(Ergebnisleiste.VERLAENGERUNG, sichtbar: true);
        leiste.ZeigtSilverGoalHaken.Should().BeFalse("vor einem Sieg in der Verlängerung gibt es keinen Haken (AK2)");
        leiste.ToreSetzen(Ergebnisleiste.VERLAENGERUNG, (2, 1));
        Wait.Until(() => leiste.ZeigtSilverGoalHaken, Wait.DefaultTimeout, "Silver-Goal-Haken");
        leiste.SilverGoalHaken(true);
        leiste.Speichern();

        spielplan.SpielWaehlen(verlaengerung.Heim, verlaengerung.Auswaerts);
        leiste.ToreSetzen(Ergebnisleiste.TORE, (0, 0));
        leiste.ToreSetzen(Ergebnisleiste.VERLAENGERUNG, (1, 0));
        Wait.Until(() => leiste.ZeigtSilverGoalHaken, Wait.DefaultTimeout, "Silver-Goal-Haken");
        leiste.Speichern();

        var spiele = spielplan.Spiele();
        Zeile(spiele, silverGoal).Zusatz.Should().Contain("S.G.", "mit Haken gilt Silver Goal (AK2)");
        Zeile(spiele, verlaengerung).Zusatz.Should().Contain("n.V.", "ohne Haken gilt n.V. nach 120 Minuten (AK2)")
            .And.NotContain("S.G.");

        spielplan.SpielWaehlen(silverGoal.Heim, silverGoal.Auswaerts);
        leiste.SilverGoalAngehakt.Should().BeTrue("ein gespeichertes S.G. öffnet mit gesetztem Haken (AK7a)");
    }

    // AK3, AK1 (Elfmeterschießen)
    [Fact]
    public void GoldenGoal_HoechstensEinTorInDerVerlaengerung()
    {
        var (spielplan, halbfinale) = TurnierAufbauen(GOLDEN_GOAL);
        var leiste = spielplan.Leiste();
        var (goldenGoal, elfmeter) = (halbfinale[0], halbfinale[1]);

        spielplan.SpielWaehlen(goldenGoal.Heim, goldenGoal.Auswaerts);
        leiste.ToreSetzen(Ergebnisleiste.TORE, (1, 1));
        leiste.ToreSetzen(Ergebnisleiste.VERLAENGERUNG, (3, 1));
        Wait.Until(() => leiste.Fehler.Contains("Golden Goal"), Wait.DefaultTimeout, "Fehler zum Golden Goal");
        leiste.SpeichernMoeglich.Should().BeFalse("mit zwei Toren in der Verlängerung ist Speichern gesperrt (AK3)");
        leiste.ToreSetzen(Ergebnisleiste.VERLAENGERUNG, (2, 1));
        Wait.Until(() => leiste.Fehler.Length == 0, Wait.DefaultTimeout, "Fehler verschwunden");
        leiste.ZeigtSilverGoalHaken.Should().BeFalse("unter Golden Goal gibt es keinen Haken");
        leiste.Speichern();

        spielplan.SpielWaehlen(elfmeter.Heim, elfmeter.Auswaerts);
        leiste.ToreSetzen(Ergebnisleiste.TORE, (1, 1));
        leiste.ToreSetzen(Ergebnisleiste.VERLAENGERUNG, (1, 1));
        leiste.WarteAufZeile(Ergebnisleiste.ELFMETER, sichtbar: true);
        leiste.ToreSetzen(Ergebnisleiste.ELFMETER, (4, 3));
        leiste.Speichern();

        var spiele = spielplan.Spiele();
        Zeile(spiele, goldenGoal).Zusatz.Should().Contain("G.G.");
        Zeile(spiele, elfmeter).Zusatz.Should().Contain("i.E. 4:3");
    }

    // AK8, AK9
    [Fact]
    public void Losentscheid_NachTorloserVerlaengerung()
    {
        var (spielplan, halbfinale) = TurnierAufbauen(VERLAENGERUNG_LOS);
        var leiste = spielplan.Leiste();
        var (los, regulaer) = (halbfinale[0], halbfinale[1]);

        spielplan.SpielWaehlen(los.Heim, los.Auswaerts);
        leiste.ToreSetzen(Ergebnisleiste.TORE, (0, 0));
        leiste.ToreSetzen(Ergebnisleiste.VERLAENGERUNG, (0, 0));
        Wait.Until(() => leiste.ZeigtLos, Wait.DefaultTimeout, "Zeile „Los fiel auf“");
        leiste.Zeigt(Ergebnisleiste.ELFMETER).Should().BeFalse("das Regelwerk kennt kein Elfmeterschießen");
        leiste.SpeichernMoeglich.Should().BeFalse("ohne Losgewinner ist Speichern gesperrt (AK8)");
        leiste.LosFielAuf(heim: false);
        leiste.Speichern();

        spielplan.ErgebnisEintragen(regulaer.Heim, regulaer.Auswaerts, (2, 0));

        Zeile(spielplan.Spiele(), los).Zusatz.Should().Contain("n.V., Los");
        spielplan.RundeWaehlen(FINALE);
        var finalspiel = spielplan.Spiele().Should().ContainSingle().Subject;
        new[] { finalspiel.Heim, finalspiel.Auswaerts }.Should().BeEquivalentTo([los.Auswaerts, regulaer.Heim],
            "ins Finale kommt, auf wen das Los fiel – nicht stillschweigend das Auswärtsteam (AK9)");
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private static SpielZeile Zeile(IReadOnlyList<SpielZeile> spiele, SpielZeile gesucht) =>
        spiele.Single(s => s.Heim == gesucht.Heim && s.Auswaerts == gesucht.Auswaerts);

    /// <summary>Turnier mit dem Standard-Regelwerk <paramref name="regelwerk"/>, Halbfinale ausgelost; liefert den
    /// Spielplan mit gewähltem Halbfinale und dessen beide Spiele.</summary>
    private (SpielplanReiter Spielplan, IReadOnlyList<SpielZeile> Halbfinale) TurnierAufbauen(string regelwerk)
    {
        string name = HandTurnier.EindeutigerName();
        var turniere = app.Hauptfenster.TurniereOeffnen();
        turniere.Anlegen(name, "2027", 4, regelwerk);

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
        return (turnier.Spielplan(), halbfinale);
    }
}
