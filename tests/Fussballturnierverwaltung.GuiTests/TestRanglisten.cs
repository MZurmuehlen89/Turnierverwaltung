using FluentAssertions;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Ranglisten, die ein Test an der Oberfläche vorbei anlegt, wenn er Teams und Reihenfolge selbst festlegen
/// will statt sich auf die Ranglisten des Seeds (FIFA-Weltrangliste, Klub-Koeffizient, Ligapyramiden) zu stützen.</summary>
internal static class TestRanglisten
{
    // Werte von Domain.Enums.Mannschaftsart; das Testprojekt referenziert die Domain nicht
    private const int VEREIN = 0;
    private const int NATIONALMANNSCHAFT = 1;

    /// <summary>Legt eine Rangliste mit <paramref name="nationen"/> in dieser Reihenfolge unter eindeutigem Namen an
    /// und liefert ihren Namen.</summary>
    public static Task<string> NationenAnlegenAsync(TestDatenbank datenbank, IReadOnlyList<string> nationen) =>
        AnlegenAsync(datenbank, "Nationen", nationen, NATIONALMANNSCHAFT);

    /// <summary>Legt eine Rangliste aus Vereinen an: je Land die ersten <c>Anzahl</c> Vereine des Seeds, die Länder
    /// reihum (1. aus Land 1, 1. aus Land 2 …, dann die 2. aus jedem Land). Liefert den Namen und die Vereine in
    /// Ranglistenreihenfolge mit ihrem Land.</summary>
    public static async Task<(string Name, IReadOnlyList<(string Verein, string Land)> Eintraege)> VereineAnlegenAsync(
        TestDatenbank datenbank, IReadOnlyList<(string Land, int Anzahl)> laender)
    {
        var jeLand = new List<IReadOnlyList<string>>();
        foreach (var (land, anzahl) in laender)
        {
            var vereine = await datenbank.SpalteLesenAsync(
                """
                SELECT TOP (@anzahl) m.Name FROM Mannschaften m JOIN Laender l ON l.Id = m.LandId
                WHERE l.Name = @land AND m.Mannschaftsart = @art ORDER BY m.Id
                """,
                ("@anzahl", anzahl), ("@land", land), ("@art", VEREIN));
            vereine.Should().HaveCount(anzahl, $"der Seed hat mindestens {anzahl} Vereine aus {land}");
            jeLand.Add(vereine);
        }

        var eintraege = Reihum(jeLand, laender);
        string name = await AnlegenAsync(datenbank, "Vereine", [.. eintraege.Select(e => e.Verein)], VEREIN);
        return (name, eintraege);
    }

    private static List<(string Verein, string Land)> Reihum(
        List<IReadOnlyList<string>> jeLand, IReadOnlyList<(string Land, int Anzahl)> laender)
    {
        var eintraege = new List<(string Verein, string Land)>();
        for (int stelle = 0; stelle < laender.Max(l => l.Anzahl); stelle++)
            for (int land = 0; land < laender.Count; land++)
                if (stelle < jeLand[land].Count)
                    eintraege.Add((jeLand[land][stelle], laender[land].Land));
        return eintraege;
    }

    private static async Task<string> AnlegenAsync(
        TestDatenbank datenbank, string art, IReadOnlyList<string> mannschaften, int mannschaftsart)
    {
        string name = $"GuiTest {art} {Guid.NewGuid().ToString("N")[..8]}";
        int id = await datenbank.WertLesenAsync<int>(
            "INSERT INTO Ranglisten (Name, Datum, Quelle) OUTPUT INSERTED.Id VALUES (@name, '2026-01-01', N'GUI-Test')",
            ("@name", name));
        for (int rang = 1; rang <= mannschaften.Count; rang++)
        {
            int eingefuegt = await datenbank.AusfuehrenAsync(
                """
                INSERT INTO RanglistenEintraege (RanglisteId, MannschaftId, Rang, Punkte)
                SELECT @id, Id, @rang, 0 FROM Mannschaften WHERE Name = @mannschaft AND Mannschaftsart = @art
                """,
                ("@id", id), ("@rang", rang), ("@mannschaft", mannschaften[rang - 1]), ("@art", mannschaftsart));
            eingefuegt.Should().Be(1, $"'{mannschaften[rang - 1]}' gibt es im Seed genau einmal");
        }
        return name;
    }
}
