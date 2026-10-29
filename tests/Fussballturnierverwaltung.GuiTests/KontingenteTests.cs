using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Landes-Quoten als Kontingente im Turnierkopf (#572): Spielklasse und Startplatz aus der Ligapyramide des
/// Landes (der Seed verknüpft Deutschland mit seiner Pyramide), Platzbereich in der Spalte „ergibt“, freie Plätze
/// gegen den Eingang der ersten Runde. Jeder Test legt sein Turnier aus der Champions-League-Vorlage (Ligaphase mit
/// 36 Teams, Vereine) unter eindeutigem Namen an; der Seed bleibt unverändert.</summary>
public sealed class KontingenteTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string VORLAGE = "UEFA Champions League";
    private const string VARIANTE = "36 Teams · ab 2024/25";
    private const string GANZES_LAND = "— ganzes Land —";
    private const string BUNDESLIGA = "1 · Bundesliga";

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
        Abwarten(() => quoten.Spielklasse(0) == GANZES_LAND && quoten.Ergibt(0) == "—", "ganzes Land vorgeschlagen");
        quoten.SpielklasseWaehlen(0, BUNDESLIGA);
        quoten.AnzahlSetzen(0, 4);
        Abwarten(() => quoten.Ergibt(0) == "Bundesliga, Plätze 1–4", "erstes Kontingent");

        quoten.LandHinzufuegen();
        quoten.LandWaehlen(1, "Deutschland");
        Abwarten(() => quoten.Spielklasse(1) == BUNDESLIGA && quoten.AbPlatz(1) == 5, "Bundesliga ab Platz 5 vorgeschlagen");
        quoten.AnzahlSetzen(1, 2);
        Abwarten(() => quoten.Ergibt(1) == "Bundesliga, Plätze 5–6", "zweites Kontingent");

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

        Abwarten(() => quoten.Zeilen == 2, "beide Kontingente geladen");
        (quoten.Ergibt(0), quoten.Ergibt(1)).Should().Be(("Bundesliga, Plätze 1–4", "Bundesliga, Plätze 5–6"));
    }

    /// <summary>Ein Land ohne Ligapyramide hat nur „ganzes Land“ und steht danach in keiner weiteren Zeile zur Wahl.</summary>
    [Fact]
    public void LandOhnePyramide_KeineSpielklasse_UndKeineZweiteZeile()
    {
        var (turniere, _) = TurnierAnlegen();
        var quoten = turniere.Quoten();
        quoten.Aufklappen();

        quoten.LandHinzufuegen();
        quoten.LandWaehlen(0, "England");
        Abwarten(() => !quoten.SpielklasseWaehlbar(0) && quoten.Spielklasse(0) == GANZES_LAND, "Spielklasse gesperrt");

        quoten.LandHinzufuegen();
        quoten.LandAngeboten(1, "England").Should().BeFalse("England hat schon eine Quote für das ganze Land");
        quoten.LandAngeboten(1, "Deutschland").Should().BeTrue();

        turniere.Kopf().Abbrechen();
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private (TurnierlisteSeite Turniere, string Name) TurnierAnlegen()
    {
        string name = $"GuiTest {Guid.NewGuid().ToString("N")[..8]}";
        var turniere = app.Hauptfenster.TurniereOeffnen();
        turniere.AusVorlageErstellen(VORLAGE, VARIANTE, "2027", name);
        turniere.Kopf().Auswaehlen(name);
        return (turniere, name);
    }

    private static void Abwarten(Func<bool> bedingung, string beschreibung) =>
        Wait.Until(bedingung, Wait.DefaultTimeout, beschreibung);
}
