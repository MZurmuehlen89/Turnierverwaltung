using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Landes-Quoten als Kontingente im Turnierkopf (#572): Spielklasse und Startplatz aus der Ligapyramide des
/// Landes (der Seed verknüpft Deutschland mit seiner Pyramide), Platzbereich in der Spalte „ergibt“, freie Plätze
/// gegen den Eingang der ersten Runde. Das Turnier kommt aus <see cref="KontingentTurnier"/>; der Seed bleibt
/// unverändert.</summary>
public sealed class KontingenteTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string GANZES_LAND = "— ganzes Land —";
    private const string BUNDESLIGA = "1 · Bundesliga";
    private const string POKALSIEGER = "Pokalsieger";

    /// <summary>Zwei Kontingente Deutschlands wie bei CL und EL: Plätze 1–4, dann schlägt die zweite Zeile die
    /// Bundesliga ab Platz 5 vor und bietet „ganzes Land“ nicht mehr an. Nach dem Speichern und erneuten Öffnen
    /// stehen beide Kontingente unverändert da.</summary>
    [Fact]
    public void Kontingente_ZeigenPlatzbereichUndFreiePlaetze_UndBleibenGespeichert()
    {
        var (turniere, name) = TurnierAnlegen();
        var quoten = turniere.Quoten();
        quoten.Aufklappen();

        quoten.LandHinzufuegen();
        quoten.LandWaehlen(0, "Deutschland");
        var ganzesLand = new QuotenZeile("Deutschland", GANZES_LAND, true, 1, "—");
        Abwarten(() => quoten.Zeile(0) == ganzesLand);
        quoten.Zeile(0).Should().Be(ganzesLand, "eine einzige Zeile beginnt mit dem ganzen Land");

        quoten.SpielklasseWaehlen(0, BUNDESLIGA);
        quoten.AnzahlSetzen(0, 4);
        var erstes = new QuotenZeile("Deutschland", BUNDESLIGA, true, 1, "Bundesliga, Plätze 1–4");
        Abwarten(() => quoten.Zeile(0) == erstes);
        quoten.Zeile(0).Should().Be(erstes);

        quoten.LandHinzufuegen();
        quoten.LandWaehlen(1, "Deutschland");
        var vorschlag = new QuotenZeile("Deutschland", BUNDESLIGA, true, 5, "Bundesliga, Platz 5");
        Abwarten(() => quoten.Zeile(1) == vorschlag);
        quoten.Zeile(1).Should().Be(vorschlag, "die zweite Zeile schließt hinter dem ersten Kontingent an");

        quoten.AnzahlSetzen(1, 2);
        Abwarten(() => quoten.Zeile(1).Ergibt == "Bundesliga, Plätze 5–6");
        quoten.Zeile(1).Ergibt.Should().Be("Bundesliga, Plätze 5–6");

        quoten.SpielklassenAngebot(1).Should().NotContain(GANZES_LAND, "Deutschland hat schon ein Kontingent");
        quoten.SpielklassenAngebot(0).Should().NotContain(GANZES_LAND);
        quoten.FreiePlaetze.Should().Be("30 freie Plätze", "36 Plätze in der Ligaphase, 6 davon für Deutschland");

        var kopf = turniere.Kopf();
        kopf.Speichern();
        app.Hauptfenster.MannschaftenOeffnen();
        turniere = app.Hauptfenster.TurniereOeffnen();
        turniere.Kopf().Auswaehlen(name);
        quoten = turniere.Quoten();
        quoten.Aufklappen();

        Abwarten(() => quoten.Zeilen == 2);
        new[] { quoten.Zeile(0), quoten.Zeile(1) }.Should().Equal(erstes, vorschlag with { Ergibt = "Bundesliga, Plätze 5–6" });
    }

    /// <summary>Live-Prüfung der Quoten (#595): Ein Kontingent, das nicht in die Spielklasse passt, markiert „ab Platz“
    /// und sperrt Speichern. Bei zugeklapptem Bereich klappt der Klick auf den Fehler in der Meldungsleiste ihn auf und
    /// setzt den Fokus ins Feld. Danach wird die Zeile gültig gespeichert – die Klasse fährt ohne offene Änderung fort.</summary>
    [Fact]
    public void KontingentPasstNicht_MarkiertAbPlatz_UndDieLeisteSpringtInDenZugeklapptenBereich()
    {
        const string PASST_NICHT = "Deutschland: 20 Team(s) ab Platz 1 passen nicht in Spielklasse 1.";
        var (turniere, _) = TurnierAnlegen();
        var quoten = turniere.Quoten();
        quoten.Aufklappen();
        quoten.LandHinzufuegen();
        quoten.LandWaehlen(0, "Deutschland");
        quoten.SpielklasseWaehlen(0, BUNDESLIGA);
        Abwarten(() => quoten.Zeile(0).Spielklasse == BUNDESLIGA);

        quoten.AnzahlSetzen(0, 20);
        turniere.KopfFehlerAbwarten(PASST_NICHT).Should().Equal(PASST_NICHT);
        Feldmarkierung.FehlerAbwarten(quoten.AbPlatzFeld(0), PASST_NICHT).Should().Be(PASST_NICHT);
        var kopf = turniere.Kopf();
        kopf.SpeichernFreigegeben.Should().BeFalse("ein Kontingent passt nicht");

        quoten.Zuklappen();
        kopf.Meldungen.Anspringen(PASST_NICHT);
        Feldmarkierung.FokusAbwarten(quoten.AbPlatzFeld(0)).Should().BeTrue("der Klick klappt die Quoten auf und springt ins Feld");

        quoten.AnzahlSetzen(0, 4);
        Feldmarkierung.FehlerAbwarten(quoten.AbPlatzFeld(0), "").Should().BeEmpty();
        kopf.Speichern();
    }

    /// <summary>Pokalsieger (#583): Neben dem Bundesliga-Kontingent 1–4 bietet die zweite Zeile „Pokalsieger“ an. Gewählt
    /// schlägt sie „Nachrücken ab“ Platz 5 vor, sperrt die Anzahl und nennt in „ergibt“ den Pokalsieger der Pyramide (der
    /// Seed setzt den VfB Stuttgart). Eine dritte Zeile bietet „Pokalsieger“ nicht mehr an. Nach dem Speichern und erneuten
    /// Öffnen steht die Zeile unverändert da.</summary>
    [Fact]
    public void Pokalsieger_NebenKontingent_SchlaegtNachrueckenVor_UndBleibtGespeichert()
    {
        var (turniere, name) = TurnierAnlegen();
        var quoten = turniere.Quoten();
        quoten.Aufklappen();

        quoten.LandHinzufuegen();
        quoten.LandWaehlen(0, "Deutschland");
        quoten.SpielklasseWaehlen(0, BUNDESLIGA);
        quoten.AnzahlSetzen(0, 4);
        Abwarten(() => quoten.Zeile(0).Ergibt == "Bundesliga, Plätze 1–4");

        quoten.LandHinzufuegen();
        quoten.LandWaehlen(1, "Deutschland");
        Abwarten(() => quoten.Zeile(1).Spielklasse == BUNDESLIGA);
        quoten.SpielklassenAngebot(1).Should().Contain(POKALSIEGER);
        quoten.SpielklasseWaehlen(1, POKALSIEGER);
        var pokal = new QuotenZeile("Deutschland", POKALSIEGER, true, 5, "VfB Stuttgart, sonst Bundesliga ab Platz 5");
        Abwarten(() => quoten.Zeile(1) == pokal);
        quoten.Zeile(1).Should().Be(pokal, "nachgerückt wird hinter dem Bundesliga-Kontingent");
        quoten.AnzahlWaehlbar(1).Should().BeFalse("der Pokalsieger hat genau einen Platz");

        quoten.LandHinzufuegen();
        quoten.LandWaehlen(2, "Deutschland");
        Abwarten(() => quoten.Zeile(2).Spielklasse == BUNDESLIGA);
        quoten.SpielklassenAngebot(2).Should().NotContain(POKALSIEGER, "Deutschland hat schon ein Pokalsieger-Kontingent");

        var kopf = turniere.Kopf();
        kopf.Speichern();
        app.Hauptfenster.MannschaftenOeffnen();
        turniere = app.Hauptfenster.TurniereOeffnen();
        turniere.Kopf().Auswaehlen(name);
        quoten = turniere.Quoten();
        quoten.Aufklappen();

        Abwarten(() => quoten.Zeilen == 3);
        Enumerable.Range(0, 3).Select(quoten.Zeile).Should().Contain(pokal);
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private (TurnierlisteSeite Turniere, string Name) TurnierAnlegen() => KontingentTurnier.Anlegen(app);

    private static void Abwarten(Func<bool> erfuellt) => KontingentTurnier.Abwarten(erfuellt);
}

/// <summary>Land ohne Ligapyramide (#572). Eigene Klasse und damit eigene App-Sitzung: bricht ein Szenario mit
/// ungespeicherten Quoten ab, fragt die App beim nächsten „Aus Vorlage erstellen“ nach und der zweite Dialog brachte
/// sie zum Absturz (wie #531).</summary>
public sealed class KontingentOhnePyramideTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string GANZES_LAND = "— ganzes Land —";

    /// <summary>Ein Land ohne Ligapyramide hat nur „ganzes Land“ und steht danach in keiner weiteren Zeile zur Wahl.</summary>
    [Fact]
    public void LandOhnePyramide_KeineSpielklasse_UndKeineZweiteZeile()
    {
        var (turniere, _) = TurnierAnlegen();
        var quoten = turniere.Quoten();
        quoten.Aufklappen();

        quoten.LandHinzufuegen();
        quoten.LandWaehlen(0, "Griechenland");
        // Griechenland hat keine Pyramide im Seed (England bekam seine mit #575).
        var griechenland = new QuotenZeile("Griechenland", GANZES_LAND, false, 1, "—");
        Abwarten(() => quoten.Zeile(0) == griechenland);
        quoten.Zeile(0).Should().Be(griechenland, "ohne Pyramide gibt es keine Spielklasse");

        quoten.LandHinzufuegen();
        quoten.LandAngeboten(1, "Griechenland").Should().BeFalse("Griechenland hat schon eine Quote für das ganze Land");
        quoten.LandAngeboten(1, "Deutschland").Should().BeTrue();
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private (TurnierlisteSeite Turniere, string Name) TurnierAnlegen() => KontingentTurnier.Anlegen(app);

    private static void Abwarten(Func<bool> erfuellt) => KontingentTurnier.Abwarten(erfuellt);
}
