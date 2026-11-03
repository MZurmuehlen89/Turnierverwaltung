using Fussballturnierverwaltung.Infrastructure.Seed.Daten;

namespace Fussballturnierverwaltung.Infrastructure.Seed;

/// <summary>#583: setzt den Pokalsieger an den Ligapyramiden bestehender Datenbanken — einmalig über die Migration
/// <c>PokalsiegerBestehenderPyramidenSetzen</c>, damit ein später im Editor entfernter Pokalsieger nicht bei jedem Start
/// zurückkommt (neue Pyramiden bekommen ihn beim Anlegen, <see cref="LigapyramideNachruestung"/>). Der Verein wird wie
/// dort im Land der Pyramide und in ihren Gastländern gesucht, das eigene Land zuerst.</summary>
public static class PokalsiegerNachruestung
{
    /// <summary>Je Pyramide mit Pokalsieger ein UPDATE, nur wo die Rangliste noch keinen hat.</summary>
    public static string Sql() => string.Join(Environment.NewLine,
        LigapyramidenSeedDaten.Alle.Where(p => p.Pokalsieger is not null).Select(Update));

    private static string Update(LigapyramidenSeedDaten.Pyramide pyramide)
    {
        var laender = pyramide.VereinsLaender.ToList();
        var inListe = string.Join(", ", laender.Select(Text));
        var vorrang = string.Join(" ", laender.Select((land, i) => $"WHEN {Text(land)} THEN {i}"));
        return $"""
            UPDATE Ranglisten SET PokalsiegerId =
                (SELECT TOP 1 m.Id FROM Mannschaften m JOIN Laender l ON l.Id = m.LandId
                 WHERE m.Name = {Text(pyramide.Pokalsieger!)} AND l.FifaCode IN ({inListe})
                 ORDER BY CASE l.FifaCode {vorrang} END, m.Id)
            WHERE Name = {Text(pyramide.Name)} AND PokalsiegerId IS NULL;
            """;
    }

    private static string Text(string wert) => $"N'{wert.Replace("'", "''")}'";
}
