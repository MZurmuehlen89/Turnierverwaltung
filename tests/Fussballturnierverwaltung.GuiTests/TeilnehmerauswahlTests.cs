using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Turnier mit Landesquote für die Teilnehmerauswahl (#573): aus einer Vorlage unter eindeutigem Namen
/// angelegt, die Quote im Turnierkopf gesetzt und gespeichert, dann in der Turnierdurchführung geöffnet.</summary>
internal static class QuotenTurnier
{
    public static (TurnierdurchfuehrungSeite Turnier, string Name) Anlegen(
        TurnierverwaltungApp app, string vorlage, string variante, Action<QuotenBereich> quotenSetzen)
    {
        string name = $"GuiTest {Guid.NewGuid().ToString("N")[..8]}";
        var turniere = app.Hauptfenster.TurniereOeffnen();
        turniere.AusVorlageErstellen(vorlage, variante, "2027", name);
        var kopf = turniere.Kopf();
        kopf.Auswaehlen(name);
        var quoten = turniere.Quoten();
        quoten.Aufklappen();
        quotenSetzen(quoten);
        kopf.Speichern();

        var turnier = app.Hauptfenster.TurnierdurchfuehrungOeffnen();
        turnier.GewaehltesTurnier.Should().Be(name, "die Turnierdurchführung zeigt das eben bearbeitete Turnier");
        return (turnier, name);
    }

    /// <summary>Die Vereine eines Landes laut Datenbank.</summary>
    public static async Task<HashSet<string>> VereineAsync(TestDatenbank datenbank, string land) =>
        [.. await datenbank.SpalteLesenAsync(
            """
            SELECT m.Name FROM Mannschaften m JOIN Laender l ON l.Id = m.LandId
            WHERE l.Name = @land AND m.Mannschaftsart = 0
            """,
            ("@land", land))];
}

/// <summary>„Töpfe automatisch befüllen“ nach Kontingent (#573): Die 2. Bundesliga hat Vereine aus ganz Deutschland
/// im Pool; ohne Kontingent nähme die Automatik nach der Ligapyramide die Bundesligisten. Mit dem Kontingent
/// „Deutschland · 2. Bundesliga · 18“, das die Vorlage seit #574 aus dem Seed mitbringt, kommen genau die Plätze der
/// zweiten Spielklasse in die Töpfe – die Soll-Liste liest der Test aus der Pyramide, auf die Deutschland verweist.</summary>
public sealed class KontingentBefuellenTests(TurnierverwaltungApp app, TestDatenbank datenbank)
    : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string SAISON = "Saison";

    [Fact]
    public async Task Kontingent_BefuelltGenauDieVereineDerSpielklasse()
    {
        string name = app.Hauptfenster.TurnierAusVorlageErstellen("2. Bundesliga", "18 Teams");
        var turniere = app.Hauptfenster.TurniereOeffnen();
        turniere.Kopf().Auswaehlen(name);
        var quoten = turniere.Quoten();
        quoten.Aufklappen();
        KontingentTurnier.Abwarten(() => quoten.Zeilen == 1 && quoten.Zeile(0).Ergibt == "2. Bundesliga, Plätze 1–18");
        quoten.Zeile(0).Should().Be(new QuotenZeile("Deutschland", "2 · 2. Bundesliga", true, 1, "2. Bundesliga, Plätze 1–18"),
            "die Vorlage bringt das Kontingent aus dem Seed mit");
        var turnier = app.Hauptfenster.TurnierdurchfuehrungOeffnen(name);

        var auslosung = turnier.Auslosung();
        auslosung.Anlegen(SAISON);
        auslosung.RanglisteWaehlen(MiniTurnier.RANGLISTE);
        auslosung.ToepfeAutomatischBefuellen();

        var soll = await ZweiteSpielklasseAsync();
        var ist = await InDenToepfenAsync(name);
        soll.Should().HaveCount(18, "die Pyramide hat 18 Plätze in der 2. Bundesliga");
        ist.Should().BeEquivalentTo(soll);
        auslosung.PoolMaengel.Should().BeEmpty("der Pool erfüllt das Kontingent");
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    /// <summary>Die Vereine der zweiten Spielklasse in der Rangliste, auf die Deutschland verweist: die Plätze hinter
    /// dem letzten Abschnitt der ersten Spielklasse bis zum letzten der zweiten.</summary>
    private Task<IReadOnlyList<string>> ZweiteSpielklasseAsync() =>
        datenbank.SpalteLesenAsync(
            """
            SELECT m.Name FROM Laender l
            JOIN RanglistenEintraege e ON e.RanglisteId = l.RanglisteId
            JOIN Mannschaften m ON m.Id = e.MannschaftId
            WHERE l.Name = 'Deutschland'
              AND e.Rang > (SELECT MAX(a.BisPlatz) FROM RanglistenAbschnitte a WHERE a.RanglisteId = l.RanglisteId AND a.Spielklasse = 1)
              AND e.Rang <= (SELECT MAX(a.BisPlatz) FROM RanglistenAbschnitte a WHERE a.RanglisteId = l.RanglisteId AND a.Spielklasse = 2)
            """);

    private Task<IReadOnlyList<string>> InDenToepfenAsync(string turnier) =>
        datenbank.SpalteLesenAsync(
            """
            SELECT m.Name FROM LostopfEintraege le
            JOIN Lostoepfe l ON l.Id = le.LostopfId
            JOIN Auslosungen a ON a.Id = l.AuslosungId
            JOIN TurnierInstanzen t ON t.Id = a.TurnierInstanzId
            JOIN Mannschaften m ON m.Id = le.MannschaftId
            WHERE t.Name = @turnier
            """,
            ("@turnier", turnier));
}

/// <summary>Quote anklicken (#573, seit #576 im Teilnehmerpool, seit #593 als Zeile der Quotenliste): Ein Klick auf die Landesquote filtert die
/// Listen des Pools auf das Land, ein zweiter hebt den Filter wieder auf. Eigene Klasse und damit eigene App-Sitzung, weil das Anlegen mit ungespeicherten Quoten
/// abbrechen kann (wie <see cref="KontingentOhnePyramideTests"/>).</summary>
public sealed class QuoteFilterTests(TurnierverwaltungApp app, TestDatenbank datenbank)
    : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string LAND = "Deutschland";
    private const int GELESEN = 15;

    [Fact]
    public async Task QuoteAnklicken_FiltertDenPoolAufDasLand_ZweiterKlickHebtAuf()
    {
        var (turnier, _) = QuotenTurnier.Anlegen(app, KontingentTurnier.VORLAGE, KontingentTurnier.VARIANTE, quoten =>
        {
            quoten.LandHinzufuegen();
            quoten.LandWaehlen(0, LAND);
            quoten.AnzahlSetzen(0, 4);
        });
        var deutsche = await QuotenTurnier.VereineAsync(datenbank, LAND);
        var auslosung = turnier.Auslosung();
        auslosung.Anlegen("Ligaphase");
        auslosung.TeilnehmerpoolOeffnen();

        auslosung.QuoteAnklicken(LAND);
        KontingentTurnier.Abwarten(() => auslosung.LandFilter == LAND);
        auslosung.LandFilter.Should().Be(LAND);
        var gefiltert = auslosung.WeitereAnfang(GELESEN);
        gefiltert.Should().NotBeEmpty().And.OnlyContain(v => deutsche.Contains(v), "der Filter lässt nur Vereine aus {0} durch", LAND);

        auslosung.QuoteAnklicken(LAND);
        KontingentTurnier.Abwarten(() => auslosung.LandFilter == "Alle Länder");
        auslosung.LandFilter.Should().Be("Alle Länder", "der zweite Klick hebt den Filter auf");
        auslosung.WeitereAnfang(GELESEN).Should().Contain(v => !deutsche.Contains(v), "ohne Filter stehen Vereine aller Länder da");
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();
}
