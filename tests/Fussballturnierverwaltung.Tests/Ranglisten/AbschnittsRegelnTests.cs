using FluentAssertions;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;

namespace Fussballturnierverwaltung.Tests.Ranglisten;

public class AbschnittsRegelnTests
{
    private static RanglistenAbschnittEingabe Abschnitt(string name, int klasse, int bis) => new(null, name, klasse, bis);

    [Fact]
    public void Fehler_GueltigePyramide_KeineFehler()
    {
        AbschnittsRegeln.Fehler([Abschnitt("Bundesliga", 1, 18), Abschnitt("2. Bundesliga", 2, 36)]).Should().BeEmpty();
    }

    [Fact]
    public void Fehler_NameFehlt_SpielklasseUndBisPlatzZuKlein()
    {
        var fehler = AbschnittsRegeln.Fehler([Abschnitt(" ", 0, 0)]);

        fehler.Should().HaveCount(3);
        fehler.Should().AllSatisfy(f => f.Text.Should().StartWith("Abschnitt 1:"));
        fehler.Select(f => (f.Feld, f.Zeile)).Should().Equal(
            (nameof(RanglistenAbschnittEingabe.Name), 0),
            (nameof(RanglistenAbschnittEingabe.Spielklasse), 0),
            (nameof(RanglistenAbschnittEingabe.BisPlatz), 0));
    }

    [Fact]
    public void Fehler_BisPlatzNichtAufsteigend()
    {
        var fehler = AbschnittsRegeln.Fehler([Abschnitt("Bundesliga", 1, 18), Abschnitt("2. Bundesliga", 2, 18)]);

        var meldung = fehler.Should().ContainSingle().Which;
        meldung.Text.Should().Contain("„2. Bundesliga“").And.Contain("(18)");
        (meldung.Feld, meldung.Zeile).Should().Be((nameof(RanglistenAbschnittEingabe.BisPlatz), 1));
    }

    [Fact]
    public void Fehler_GleicheSpielklasseMehrfach_IstErlaubt()
    {
        // Parallele Staffeln wie die Regionalligen.
        AbschnittsRegeln.Fehler([Abschnitt("Regionalliga Nord", 4, 18), Abschnitt("Regionalliga Süd", 4, 36)]).Should().BeEmpty();
    }

    [Fact]
    public void UeberhangHinweis_NurWennLetzterAbschnittUeberDieEintraegeReicht()
    {
        RanglistenAbschnittEingabe[] abschnitte = [Abschnitt("Bundesliga", 1, 18), Abschnitt("2. Bundesliga", 2, 36)];

        AbschnittsRegeln.UeberhangHinweis(abschnitte, 36).Should().BeNull();
        AbschnittsRegeln.UeberhangHinweis(abschnitte, 20).Should().Contain("Platz 36").And.Contain("20 Einträge");
        AbschnittsRegeln.UeberhangHinweis([], 0).Should().BeNull();
    }

    [Fact]
    public void Uebersicht_BerechnetVonPlatzAusDemVorigenAbschnitt()
    {
        var zeilen = AbschnittsRegeln.Uebersicht([Abschnitt("Bundesliga", 1, 18), Abschnitt("2. Bundesliga", 2, 36)]);

        zeilen.Select(z => (z.VonPlatz, z.BisPlatz)).Should().Equal((1, 18), (19, 36));
    }
}
