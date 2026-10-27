using FluentAssertions;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Ranglisten, die ein Test an der Oberfläche vorbei anlegt: Die frische Test-Datenbank bringt als Rangliste
/// nur die Ligapyramide Deutschland mit (FIFA- und UEFA-Ranglisten sind im Seed nur Import-Quellen).</summary>
internal static class TestRanglisten
{
    /// <summary>Legt eine Rangliste mit <paramref name="nationen"/> in dieser Reihenfolge unter eindeutigem Namen an
    /// und liefert ihren Namen.</summary>
    public static async Task<string> NationenAnlegenAsync(TestDatenbank datenbank, IReadOnlyList<string> nationen)
    {
        string name = $"GuiTest Nationen {Guid.NewGuid().ToString("N")[..8]}";
        int id = await datenbank.WertLesenAsync<int>(
            "INSERT INTO Ranglisten (Name, Datum, Quelle) OUTPUT INSERTED.Id VALUES (@name, '2026-01-01', N'GUI-Test #533')",
            ("@name", name));
        for (int rang = 1; rang <= nationen.Count; rang++)
        {
            int eingefuegt = await datenbank.AusfuehrenAsync(
                """
                INSERT INTO RanglistenEintraege (RanglisteId, MannschaftId, Rang, Punkte)
                SELECT @id, Id, @rang, 0 FROM Mannschaften WHERE Name = @mannschaft AND Mannschaftsart = 1 -- Nationalmannschaft
                """,
                ("@id", id), ("@rang", rang), ("@mannschaft", nationen[rang - 1]));
            eingefuegt.Should().Be(1, $"'{nationen[rang - 1]}' gibt es im Seed genau einmal als Nationalmannschaft");
        }
        return name;
    }
}
