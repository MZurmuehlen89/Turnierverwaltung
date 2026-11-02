using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>DFB-Pokal mit Profi- und Amateurtopf (#470), an der Vorlage mit den Seed-Vereinen und der Ligapyramide
/// Deutschland (Profigrenze: 2. Bundesliga). 1. Runde: die besten 32 der Pyramide gegen den Rest, der ganze Amateurtopf
/// spielt zu Hause – auch die Zweitligisten darin. 2. Runde: „Automatisch befüllen“ teilt nach Spielklasse in ungleich
/// große Töpfe, jeder Amateur trifft auf einen Profi, solange es Profis gibt, und hat dann Heimrecht. Topfinhalte und
/// Spielklassen kommen aus der Datenbank, die Paarungen aus dem Spielplan.</summary>
public sealed class DfbPokalTests(TurnierverwaltungApp app, TestDatenbank datenbank)
    : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string ERSTE_RUNDE = "1. Runde";
    private const string ZWEITE_RUNDE = "2. Runde";
    private const string PYRAMIDE = "Ligapyramide Deutschland 2025/26";
    private const int PROFIGRENZE = 2;

    [Fact]
    public async Task Pokal_AmateurtopfDaheim_ZweiteRundeNachSpielklasse()
    {
        string name = app.Hauptfenster.TurnierAusVorlageErstellen("DFB-Pokal", "64 Teams");
        var turnier = app.Hauptfenster.TurnierdurchfuehrungOeffnen(name);
        var klasse = await SpielklassenAsync();

        var auslosung = turnier.Auslosung();
        auslosung.Anlegen(ERSTE_RUNDE);
        auslosung.WarteBisAlleZugewiesen();
        var amateurtopf = await TopfAsync(name, ERSTE_RUNDE, 2);
        amateurtopf.Should().HaveCount(32);
        amateurtopf.Should().Contain(verein => klasse[verein] <= PROFIGRENZE,
            "36 Profis passen nicht in einen Topf mit 32 Plätzen – die schwächsten Zweitligisten landen im Amateurtopf");
        auslosung.Durchfuehren();
        var ersteRunde = turnier.RundeSimulieren(ERSTE_RUNDE, 32);
        ersteRunde.Select(s => s.Heim).Should().BeEquivalentTo(amateurtopf, "in der 1. Runde spielt der Amateurtopf zu Hause");

        auslosung = turnier.Auslosung();
        auslosung.Anlegen(ZWEITE_RUNDE);
        auslosung.WarteBisAlleZugewiesen();
        auslosung.Durchfuehren();
        var zweiteRunde = turnier.SpieleDerRunde(ZWEITE_RUNDE);

        var teams = zweiteRunde.SelectMany(s => new[] { s.Heim, s.Auswaerts }).ToList();
        int profis = teams.Count(t => klasse[t] <= PROFIGRENZE);
        int amateure = teams.Count - profis;
        (await TopfAsync(name, ZWEITE_RUNDE, 1)).Should().HaveCount(profis, "Topf 1 ist der Profitopf");
        (await TopfAsync(name, ZWEITE_RUNDE, 2)).Should().HaveCount(amateure, "Topf 2 ist der Amateurtopf");
        var gemischt = zweiteRunde.Where(s => klasse[s.Heim] <= PROFIGRENZE != klasse[s.Auswaerts] <= PROFIGRENZE).ToList();
        gemischt.Should().HaveCount(Math.Min(profis, amateure), "Amateure treffen auf Profis, solange beide Töpfe Teams haben");
        gemischt.Should().OnlyContain(s => klasse[s.Heim] > PROFIGRENZE, "der Amateur hat Heimrecht gegen den Profi");
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    /// <summary>Spielklasse je Verein aus den Abschnitten der Pyramide (Abschnitt = kleinstes „bis Platz“ ab dem Rang).</summary>
    private async Task<Dictionary<string, int>> SpielklassenAsync() =>
        (await datenbank.SpalteLesenAsync(
            """
            SELECT m.Name + '|' + CAST((SELECT TOP 1 a.Spielklasse FROM RanglistenAbschnitte a
                                        WHERE a.RanglisteId = e.RanglisteId AND a.BisPlatz >= e.Rang
                                        ORDER BY a.BisPlatz) AS nvarchar(10))
            FROM RanglistenEintraege e
            JOIN Ranglisten r ON r.Id = e.RanglisteId
            JOIN Mannschaften m ON m.Id = e.MannschaftId
            WHERE r.Name = @pyramide
            """,
            ("@pyramide", PYRAMIDE)))
        .Select(zeile => zeile.Split('|'))
        .ToDictionary(teile => teile[0], teile => int.Parse(teile[1], System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Einträge eines Lostopfs der Auslosung einer Runde: Vereinsname, sonst Platzhaltername.</summary>
    private Task<IReadOnlyList<string>> TopfAsync(string turnier, string runde, int topf) =>
        datenbank.SpalteLesenAsync(
            """
            SELECT COALESCE(m.Name, e.PlatzhalterName)
            FROM LostopfEintraege e
            JOIN Lostoepfe l ON l.Id = e.LostopfId
            JOIN Auslosungen a ON a.Id = l.AuslosungId
            JOIN TurnierRunden r ON r.Id = a.TurnierRundeId
            JOIN TurnierInstanzen t ON t.Id = r.TurnierInstanzId
            LEFT JOIN Mannschaften m ON m.Id = e.MannschaftId
            WHERE t.Name = @turnier AND r.Name = @runde AND l.Reihenfolge = @topf
            """,
            ("@turnier", turnier), ("@runde", runde), ("@topf", topf));
}
