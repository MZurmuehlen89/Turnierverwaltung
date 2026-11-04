using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Ranglisten-Editor (#535): anlegen über die Suchbox, umsortieren und entfernen, in der Liste suchen,
/// Abschnitte der Ligapyramide mit Live-Prüfung, Pokalsieger und Löschen. Jeder Test arbeitet mit einer eigenen
/// Rangliste unter eindeutigem Namen – angelegt über die Oberfläche oder per SQL (<see cref="TestRanglisten"/>); die
/// Ranglisten des Seeds bleiben unberührt. Wo es geht, prüft der Test das Gespeicherte in der Datenbank statt nur in
/// der Anzeige.</summary>
public sealed class RanglistenPflegeTests(TurnierverwaltungApp app, TestDatenbank datenbank)
    : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private static readonly string[] _nationen = ["Spanien", "Frankreich", "England", "Portugal", "Belgien"];

    [Fact]
    public async Task Anlegen_UeberDieSuchbox_SpeichertDieReihenfolge()
    {
        var seite = app.Hauptfenster.RanglistenOeffnen();
        string name = EindeutigerName();

        seite.Anlegen(name, "Argentinien", "Brasilien", "Marokko");

        seite.Anzahl(name).Should().Be("3 Einträge");
        (await GespeicherteReihenfolgeAsync(name)).Should().Equal("Argentinien", "Brasilien", "Marokko");
        seite.Editor.Auswaehlen(name);
        seite.EintraegeAbwarten(new(1, "Argentinien"), new(2, "Brasilien"), new(3, "Marokko"))
            .Should().Equal(new RanglistenEintrag(1, "Argentinien"), new(2, "Brasilien"), new(3, "Marokko"));
    }

    [Fact]
    public void OhneName_WirdNichtGespeichert()
    {
        const string MELDUNG = "Name darf nicht leer sein";
        var seite = app.Hauptfenster.RanglistenOeffnen();
        seite.Editor.Neu();

        seite.Editor.SpeichernAbgelehnt(MELDUNG).Should().Contain(MELDUNG);
        seite.Editor.SpeichernFreigegeben.Should().BeTrue("die Eingabe ist noch nicht gespeichert");

        seite.Editor.Abbrechen();
    }

    /// <summary>↑/↓, „Auf Rang verschieben“ und „Entfernen“ wirken auf die markierten Einträge; die Ränge folgen
    /// immer der Position, auch in der Datenbank.</summary>
    [Fact]
    public async Task Umsortieren_UndEntfernen_HaeltDieRaengeFortlaufend()
    {
        string name = await TestRanglisten.NationenAnlegenAsync(datenbank, _nationen);
        var seite = app.Hauptfenster.RanglistenOeffnen();
        seite.Editor.Auswaehlen(name);

        seite.Markieren("Spanien");
        seite.Runter();
        seite.ReihenfolgeAbwarten("Frankreich", "Spanien", "England", "Portugal", "Belgien")
            .Should().Equal("Frankreich", "Spanien", "England", "Portugal", "Belgien");

        seite.Markieren("Portugal");
        seite.Hoch();
        seite.ReihenfolgeAbwarten("Frankreich", "Spanien", "Portugal", "England", "Belgien")
            .Should().Equal("Frankreich", "Spanien", "Portugal", "England", "Belgien");

        seite.Markieren("Belgien");
        seite.AufRangVerschieben(1);
        seite.ReihenfolgeAbwarten("Belgien", "Frankreich", "Spanien", "Portugal", "England")
            .Should().Equal("Belgien", "Frankreich", "Spanien", "Portugal", "England");

        seite.Markieren("Spanien", "England");
        seite.Entfernen();
        seite.EintraegeAbwarten(new(1, "Belgien"), new(2, "Frankreich"), new(3, "Portugal"))
            .Should().Equal(new RanglistenEintrag(1, "Belgien"), new(2, "Frankreich"), new(3, "Portugal"));

        seite.Editor.Speichern();
        (await GespeicherteReihenfolgeAsync(name)).Should().Equal("Belgien", "Frankreich", "Portugal");
    }

    /// <summary>Die Suche springt zum Treffer und markiert ihn; ein unbekannter Text meldet „kein Treffer“.</summary>
    [Fact]
    public async Task InListeSuchen_MarkiertDenTreffer()
    {
        string name = await TestRanglisten.NationenAnlegenAsync(datenbank, _nationen);
        var seite = app.Hauptfenster.RanglistenOeffnen();
        seite.Editor.Auswaehlen(name);

        seite.InListeSuchen("Portu");
        seite.MarkierteAbwarten("Portugal").Should().Equal("Portugal");

        seite.InListeSuchen("Atlantis");
        seite.KeinTrefferAbwarten().Should().BeTrue("keine Mannschaft der Liste heißt so");
    }

    /// <summary>Zwei Abschnitte teilen die Liste: Trennbänder an Platz 1 und 3, „von Platz“ aus dem Abschnitt davor.
    /// Ein Name fehlt bzw. „bis Platz“ fällt nicht – das sperrt das Speichern; ein Abschnitt über die Liste hinaus ist
    /// nur ein Hinweis.</summary>
    [Fact]
    public async Task Abschnitte_TeilenDieListe_UndUngueltigeSperrenDasSpeichern()
    {
        string name = await TestRanglisten.NationenAnlegenAsync(datenbank, _nationen);
        var seite = app.Hauptfenster.RanglistenOeffnen();
        seite.Editor.Auswaehlen(name);
        seite.LigapyramideAufklappen();

        seite.AbschnittHinzufuegen();
        seite.AbschnittsFehlerAbwarten("Abschnitt 1: Name fehlt.").Should().Equal("Abschnitt 1: Name fehlt.");
        seite.SpeichernFreigabeAbwarten(false).Should().BeFalse("ein Abschnitt hat keinen Namen");

        seite.AbschnittBenennen(0, "Oben");
        seite.AbschnittBisPlatzSetzen(0, 2);
        seite.AbschnittHinzufuegen();
        seite.AbschnittBenennen(1, "Unten");
        seite.AbschnitteAbwarten(new("Oben", 1, 2, 1), new("Unten", 2, 5, 3))
            .Should().Equal(new LigaAbschnitt("Oben", 1, 2, 1), new("Unten", 2, 5, 3));
        RanglistenEintrag[] geteilt =
        [
            new(1, "Spanien", "Oben · Spielklasse 1"), new(2, "Frankreich"),
            new(3, "England", "Unten · Spielklasse 2"), new(4, "Portugal"), new(5, "Belgien")
        ];
        seite.EintraegeAbwarten(geteilt).Should().Equal(geteilt);

        const string NICHT_AUFSTEIGEND = "Abschnitt „Unten“: „bis Platz“ muss größer sein als beim Abschnitt davor (2).";
        seite.AbschnittBisPlatzSetzen(1, 2);
        seite.AbschnittsFehlerAbwarten(NICHT_AUFSTEIGEND).Should().Equal(NICHT_AUFSTEIGEND);
        seite.SpeichernFreigabeAbwarten(false).Should().BeFalse("„bis Platz“ steigt nicht");

        const string UEBERHANG = "Der letzte Abschnitt reicht bis Platz 7, die Rangliste hat 5 Einträge.";
        seite.AbschnittBisPlatzSetzen(1, 7);
        seite.AbschnittsFehlerAbwarten().Should().BeEmpty();
        seite.HinweisAbwarten(UEBERHANG).Should().Be(UEBERHANG);
        seite.SpeichernFreigabeAbwarten(true).Should().BeTrue("der Überhang ist erlaubt");

        seite.Editor.Speichern();
        (await GespeicherteAbschnitteAsync(name)).Should().Equal("Oben 1 2", "Unten 2 7");
    }

    [Fact]
    public async Task Pokalsieger_SetzenUndEntfernen_WirdGespeichert()
    {
        const string POKALSIEGER = "VfB Stuttgart";
        string name = await TestRanglisten.NationenAnlegenAsync(datenbank, _nationen);
        var seite = app.Hauptfenster.RanglistenOeffnen();
        seite.Editor.Auswaehlen(name);
        seite.LigapyramideAufklappen();

        seite.Pokalsieger.Waehlen(POKALSIEGER);
        seite.SpeichernFreigabeAbwarten(true).Should().BeTrue("die Wahl ist eine Änderung");
        seite.Pokalsieger.Text.Should().Be(POKALSIEGER);
        seite.Editor.Speichern();
        (await GespeicherterPokalsiegerAsync(name)).Should().Be(POKALSIEGER);

        seite.LigapyramideAufklappen();
        seite.PokalsiegerEntfernen();
        seite.Editor.Speichern();
        (await GespeicherterPokalsiegerAsync(name)).Should().BeEmpty("der Pokalsieger ist entfernt");
    }

    [Fact]
    public async Task Loeschen_FragtNachUndEntferntErstNachBestaetigung()
    {
        string name = await TestRanglisten.NationenAnlegenAsync(datenbank, _nationen);
        var seite = app.Hauptfenster.RanglistenOeffnen();

        seite.Editor.Auswaehlen(name);
        seite.Editor.Loeschen(bestaetigen: false);
        seite.Anzahl(name).Should().Be("5 Einträge", "Abbrechen lässt die Rangliste stehen");

        seite.Editor.Auswaehlen(name);
        seite.Editor.Loeschen(bestaetigen: true);
        seite.Editor.NichtMehrGelistetAbwarten(name).Should().BeTrue("die Rangliste ist gelöscht");
        (await datenbank.WertLesenAsync<int>("SELECT COUNT(*) FROM Ranglisten WHERE Name = @name", ("@name", name)))
            .Should().Be(0);
    }

    public void Dispose()
    {
        app.BeiFehlschlagBelegeSichern();
        app.Hauptfenster.Aufraeumen();
    }

    private static string EindeutigerName() => $"GuiTest {Guid.NewGuid().ToString("N")[..8]}";

    private Task<IReadOnlyList<string>> GespeicherteReihenfolgeAsync(string rangliste) => datenbank.SpalteLesenAsync(
        """
        SELECT m.Name FROM RanglistenEintraege e
        JOIN Ranglisten r ON r.Id = e.RanglisteId JOIN Mannschaften m ON m.Id = e.MannschaftId
        WHERE r.Name = @name ORDER BY e.Rang
        """,
        ("@name", rangliste));

    private Task<IReadOnlyList<string>> GespeicherteAbschnitteAsync(string rangliste) => datenbank.SpalteLesenAsync(
        """
        SELECT CONCAT(a.Name, ' ', a.Spielklasse, ' ', a.BisPlatz) FROM RanglistenAbschnitte a
        JOIN Ranglisten r ON r.Id = a.RanglisteId WHERE r.Name = @name ORDER BY a.BisPlatz
        """,
        ("@name", rangliste));

    /// <summary>Name des gespeicherten Pokalsiegers; leer, wenn keiner gesetzt ist.</summary>
    private async Task<string> GespeicherterPokalsiegerAsync(string rangliste) =>
        (await datenbank.SpalteLesenAsync(
            """
            SELECT m.Name FROM Ranglisten r JOIN Mannschaften m ON m.Id = r.PokalsiegerId WHERE r.Name = @name
            """,
            ("@name", rangliste))).SingleOrDefault() ?? string.Empty;
}
